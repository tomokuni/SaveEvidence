<#
.SYNOPSIS
リポジトリの検証をすべて実行し、合否を要約する。

.DESCRIPTION
`.github/検証手順.md` が定める検証を順に実行し、各項目の合否と件数を表示する（不合格があれば終了コード 1）。
Copilot・CI・開発者が同じ手順を 1 コマンドで再現できるようにする（手作業の再実装をなくす）。

.PARAMETER Solution
対象のソリューション ファイル（既定: SaveEvidence.slnx）。 CI は release-config.json が持つ値を渡す。

.PARAMETER Configuration
ビルド構成（既定: Debug）。 CI は Release を渡す。

.PARAMETER Fix
自動修正できる項目を修正する（markdownlint --fix / dotnet format）。

.PARAMETER SkipTests
テストの実行を省略する（ビルドだけを素早く確かめるとき）。

.PARAMETER SkipBuild
ビルドを省略し、直前にビルドした出力を使う（文書だけを直したときの往復を短くする）。
テストは `--no-build` で実行するため、**一度もビルドしていないと失敗する**（先にビルドを 1 回実行する）。

.PARAMETER SkipFormat
dotnet format による検査を省略する（時間を短縮するとき）。

.PARAMETER SkipPackage
NuGet の健全性（脆弱性・非推奨）の検査を省略する（ネットワークを使いたくないとき）。

.PARAMETER Coverage
テスト時にカバレッジを取得し、対象ファイルごとの被覆率を集計する（テスト規約の「カバレッジ 100% を目指す」の数値確認用）。

.PARAMETER NoRestore
`--no-restore` を付ける（呼び出し側が復元済みのとき。 CI は復元を別ステップで行う）。

.EXAMPLE
pwsh -NoProfile -File buildScript/verify.ps1
.EXAMPLE
pwsh -NoProfile -File buildScript/verify.ps1 -Fix
.EXAMPLE
pwsh -NoProfile -File buildScript/verify.ps1 -Configuration Release -Solution SaveEvidence.slnx -NoRestore
#>
[CmdletBinding()]
param(
    [string]$Solution = 'SaveEvidence.slnx',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$Fix,
    [switch]$SkipBuild,
    [switch]$SkipTests,
    [switch]$SkipFormat,
    [switch]$SkipPackage,
    [switch]$Coverage,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$enc = [Text.UTF8Encoding]::new($false)
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

$script:Results = [System.Collections.Generic.List[object]]::new()

function Add-Result([string]$Name, [bool]$Ok, [string]$Detail) {
    $script:Results.Add([pscustomobject]@{ Name = $Name; Ok = $Ok; Kind = 'Check'; Detail = $Detail })
    $mark = if ($Ok) { 'OK  ' } else { 'FAIL' }
    Write-Output "$mark  $Name`t$Detail"
}

# 合否に含めない参考値（数値を見るためだけの項目）
function Add-Info([string]$Name, [string]$Detail) {
    $script:Results.Add([pscustomobject]@{ Name = $Name; Ok = $true; Kind = 'Info'; Detail = $Detail })
    Write-Output "INFO  $Name`t$Detail"
}

function Invoke-DotNet([string[]]$Arguments) {
    return (& dotnet @Arguments 2>&1 | Out-String)
}

# 設定に応じた共通引数（復元の有無は呼び出し側が決める）
$restoreArgs = if ($NoRestore) { @('--no-restore') } else { @() }

# ============================================================
# 1. ビルド（0 エラー / 0 警告。 IDE0005・書式・アナライザー・ドキュメントコメントを同時に検証する）
# ============================================================
if (-not $SkipBuild) {
    Write-Output '== 1. ビルド =='
    # 警告のエラー化は Directory.Build.props の TreatWarningsAsErrors が単一所有する（-warnaserror は付けない）。
    # これで素の `dotnet build` でも規約違反が終了コードに出る（スクリプトの引数に依存しない）。
    $buildArgs = @('build', $Solution, '-c', $Configuration, '-tl:off', '-v:q', '--no-incremental') + $restoreArgs
    $buildText = Invoke-DotNet $buildArgs
    $buildWarnings = ([regex]::Matches($buildText, 'warning ')).Count
    $buildErrors = ([regex]::Matches($buildText, ': error ')).Count
    Add-Result 'ビルド' ($buildWarnings -eq 0 -and $buildErrors -eq 0) "警告 $buildWarnings / エラー $buildErrors"
    if ($buildWarnings -gt 0 -or $buildErrors -gt 0) {
        $buildText -split "`r?`n" | Where-Object { $_ -match 'warning |: error ' } | Select-Object -First 20 | ForEach-Object { "      $($_.Trim())" }
    }
}
else {
    # 省略した検査は合否に含めない（-SkipBuild は文書だけを直したときの往復を短くする用途）
    Add-Info 'ビルド' '省略（-SkipBuild。 直前にビルドした出力を使う）'
}

# ============================================================
# 2. ビルド設定（単一所有と配布物へ XML を出さないこと）
# ============================================================
Write-Output '== 2. ビルド設定 =='
$propsPath = Join-Path $repoRoot 'Directory.Build.props'
$propsText = if (Test-Path -LiteralPath $propsPath) { [IO.File]::ReadAllText($propsPath, $enc) } else { '' }
$missingProps = [System.Collections.Generic.List[string]]::new()
foreach ($p in 'EnforceCodeStyleInBuild', 'GenerateDocumentationFile', 'CopyDocumentationFileToOutputDirectory', 'PublishDocumentationFiles',
    'AnalysisLevel', 'AnalysisMode', 'TreatWarningsAsErrors') {
    if ($propsText -notmatch "<$p>") { $missingProps.Add($p) }
}
# 各 .csproj が重複して指定していないか（単一所有の確認）
$duplicateProps = @(Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -File -Filter '*.csproj' |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
        Where-Object { [IO.File]::ReadAllText($_.FullName) -match '<EnforceCodeStyleInBuild>|<GenerateDocumentationFile>|PublishDocumentationFiles|<AnalysisLevel>|<AnalysisMode>|<TreatWarningsAsErrors>' })
Add-Result 'ビルド設定' ($missingProps.Count -eq 0 -and $duplicateProps.Count -eq 0) "不足 $($missingProps.Count) / 重複 $($duplicateProps.Count)"
$missingProps | ForEach-Object { "      Directory.Build.props に無い: $_" }
$duplicateProps | ForEach-Object { "      重複指定: $($_.FullName.Substring($repoRoot.Length + 1))" }

# 2b) アナライザー設定が .cs へ適用されているか
#     `.editorconfig` の `dotnet_diagnostic.*` は「直前のセクション」に属する。 セクションを分け忘れると
#     設定が `.cs` 以外（例: `[*.csproj]`）に属し、**設定が無効のまま「0 警告」になる**（実測で発生した）。
$editorConfigPath = Join-Path $repoRoot '.editorconfig'
$analyzerSectionIssues = [System.Collections.Generic.List[string]]::new()
if (Test-Path -LiteralPath $editorConfigPath) {
    $section = ''
    $line = 0
    foreach ($text in [IO.File]::ReadAllLines($editorConfigPath, $enc)) {
        $line++
        if ($text -match '^\s*\[(?<pattern>.+)\]\s*$') { $section = $Matches['pattern']; continue }
        if ($text -match '^\s*dotnet_diagnostic\.[A-Za-z0-9]+\.[a-z_]+\s*=') {
            # 対象に .cs を含むセクションか。 次のいずれかなら `.cs` へ適用される:
            #   `*.cs`（ファイル パターン） / `src/X/src/**`（ディレクトリ配下） / `*` や `**`（全ファイル）
            # `*.csproj` や `*.cs-broken` のような取り違えは、いずれにも当たらず検出される（実測で確認）。
            $sectionOk = $true
            foreach ($part in $section.Replace('{', '').Replace('}', '').Split(',')) {
                $p = $part.Trim()
                if ($p -eq '*' -or $p -eq '**') { continue }
                if ($p.EndsWith('.cs') -or $p.EndsWith('/**') -or $p.EndsWith('**')) { continue }
                $sectionOk = $false
            }
            if (-not $sectionOk) {
                $analyzerSectionIssues.Add(("{0}:{1}  {2}（セクション: [{3}]）" -f '.editorconfig', $line, $text.Trim(), $section))
            }
        }
    }
}
Add-Result 'アナライザー設定' ($analyzerSectionIssues.Count -eq 0) "無効なセクション $($analyzerSectionIssues.Count) 件"
$analyzerSectionIssues | Select-Object -First 8 | ForEach-Object { "      $_" }

# 2c) ログの有効判定を定義側で行っているか（呼び出し側で IsEnabled を書かない）
#     判定はログ定義（<対象>LogMessage / AppLog）が単一所有する。 呼び出し側に書くと、レベルを変えるたびに
#     全呼び出し元を直すことになり、雑音にもなる（実測: 7 箇所のゲートを定義側へ移した）。
#     --untracked: 新規ファイル（未コミット）も対象にする（無視対象の obj / bin は走査されない）。
$callSiteGates = [System.Collections.Generic.List[string]]::new()
foreach ($hit in @(git -c core.quotepath=false grep --untracked -n -E '\bIsEnabled\s*\(\s*LogLevel\.' -- 'src' 2>$null)) {
    if ($hit -match '\\tests\\' -or $hit -match '/(tests)/') { continue }
    # ログ定義（*LogMessage.cs / AppLog.cs）と、記録用ロガーの実装は対象外
    if ($hit -match '(LogMessage|AppLog)\.cs:') { continue }
    $callSiteGates.Add($hit)
}
Add-Result 'ログの有効判定' ($callSiteGates.Count -eq 0) "呼び出し側の IsEnabled $($callSiteGates.Count) 件"
$callSiteGates | Select-Object -First 8 | ForEach-Object { "      $_" }

# ============================================================
# 3. テスト（全件合格。 -Coverage でカバレッジも集計する）
# ============================================================
if (-not $SkipTests) {
    Write-Output '== 3. テスト =='
    $testArgs = @('test', $Solution, '-c', $Configuration, '-tl:off', '--no-build')
    $coverageDir = Join-Path ([IO.Path]::GetTempPath()) ("se_cov_" + [Guid]::NewGuid().ToString('N'))
    if ($Coverage) { $testArgs += @('--collect:XPlat Code Coverage', "--results-directory:$coverageDir") }
    $testText = Invoke-DotNet $testArgs
    $passed = 0
    foreach ($m in [regex]::Matches($testText, '合格:\s*(\d+)')) { $passed += [int]$m.Groups[1].Value }
    $failed = 0
    foreach ($m in [regex]::Matches($testText, '失敗:\s*(\d+)')) { $failed += [int]$m.Groups[1].Value }
    $projects = [regex]::Matches($testText, '\.dll \(net10\.0[^)]*\)').Count
    Add-Result 'テスト' ($failed -eq 0 -and $passed -gt 0) "合格 $passed / 失敗 $failed / $projects プロジェクト"
    if ($failed -gt 0) {
        # 失敗したテスト名を出す（合計だけでは原因を特定できない）
        $testText -split "`r?`n" | Where-Object { $_ -match '^\s*(失敗|Failed)\s+\S' } | Select-Object -First 10 | ForEach-Object { "      $($_.Trim())" }
    }

    if ($Coverage) {
        $files = @(Get-ChildItem -Path $coverageDir -Recurse -File -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue)
        if ($files.Count -eq 0) {
            Add-Result 'カバレッジ' $false 'レポートが生成されませんでした'
        }
        else {
            # 同じファイルが複数のテスト プロジェクトから出るため、行ごとに「被覆した実行があれば被覆」として集計する。
            # 生成コード（ソース ジェネレーターの出力・アセンブリ属性）は対象外にする
            $byFile = @{}
            foreach ($f in $files) {
                [xml]$doc = [IO.File]::ReadAllText($f.FullName, $enc)
                foreach ($cls in $doc.coverage.packages.package.classes.class) {
                    $name = ([string]$cls.filename).Replace('\', '/')
                    if ($name -notmatch '/src/' -or $name -match '/obj/' -or $name -match '\.g\.cs$' -or $name -match '\.Designer\.cs$' -or $name -match 'AssemblyInfo\.cs$' -or $name -match 'AssemblyAttributes\.cs$') { continue }
                    if (-not $byFile.ContainsKey($name)) { $byFile[$name] = @{} }
                    foreach ($line in $cls.lines.line) {
                        $n = [string]$line.number
                        $hit = [int]$line.hits
                        if (-not $byFile[$name].ContainsKey($n) -or $byFile[$name][$n] -lt $hit) { $byFile[$name][$n] = $hit }
                    }
                }
            }
            $totalLines = 0
            $coveredLines = 0
            $uncovered = [System.Collections.Generic.List[string]]::new()
            foreach ($name in ($byFile.Keys | Sort-Object)) {
                $lines = $byFile[$name]
                $total = $lines.Count
                $covered = @($lines.Values | Where-Object { $_ -gt 0 }).Count
                $totalLines += $total
                $coveredLines += $covered
                if ($covered -lt $total) { $uncovered.Add(("{0}  {1}/{2}" -f $name, $covered, $total)) }
            }
            $rate = if ($totalLines -gt 0) { 100.0 * $coveredLines / $totalLines } else { 0 }
            # カバレッジは参考値（合否に含めない。 目標は テスト規約が定める）
            Add-Info 'カバレッジ' ("行被覆 {0:N2}%（{1}/{2}） 未達 {3} ファイル（生成コードを除く）" -f $rate, $coveredLines, $totalLines, $uncovered.Count)
            $uncovered | Select-Object -First 15 | ForEach-Object { "      $_" }
            if ($uncovered.Count -gt 15) { "      … 他 $($uncovered.Count - 15) ファイル" }
        }
        Remove-Item -Recurse -Force $coverageDir -ErrorAction SilentlyContinue
    }
}

# ============================================================
# 4. NuGet の健全性（脆弱性・非推奨。 推移的依存も含む）
# ============================================================
if (-not $SkipPackage) {
    Write-Output '== 4. NuGet の健全性 =='
    $pkgIssues = 0
    $pkgDetails = [System.Collections.Generic.List[string]]::new()
    foreach ($kind in @('--vulnerable', '--deprecated')) {
        $pkgArgs = @('package', 'list', $kind, '--include-transitive') + $restoreArgs
        $pkgText = Invoke-DotNet $pkgArgs
        # 指摘がある場合だけ表（`> ` で始まる行）が出る
        $rows = @($pkgText -split "`r?`n" | Where-Object { $_ -match '^\s*>\s+\S' })
        if ($rows.Count -gt 0) {
            $pkgIssues += $rows.Count
            $pkgDetails.Add(("{0}:" -f $kind))
            $rows | Select-Object -First 10 | ForEach-Object { $pkgDetails.Add("      $($_.Trim())") }
        }
    }
    Add-Result 'NuGet の健全性' ($pkgIssues -eq 0) "脆弱性・非推奨 $pkgIssues 件"
    $pkgDetails | ForEach-Object { $_ }

    # 更新候補（参考値。 合否に含めない）。 ネットワークを使うため -SkipPackage で省略できる
    $outdatedText = Invoke-DotNet @('package', 'list', '--outdated') + $restoreArgs
    $outdatedRows = @($outdatedText -split "`r?`n" | Where-Object { $_ -match '^\s*>\s+\S' })
    Add-Info 'NuGet の更新候補' "$($outdatedRows.Count) 件（参考値。 合否に含めない）"
    $outdatedRows | Select-Object -First 8 | ForEach-Object { "      $($_.Trim())" }
}

# 以降のドキュメント検査で使う文書一覧（リポジトリ直下の文書も対象にする）
$docs = @(
    Get-ChildItem -Path $repoRoot -File -Filter '*.md'
    Get-ChildItem -Path (Join-Path $repoRoot 'src'), (Join-Path $repoRoot '.github') -Recurse -File -Filter '*.md'
) | Where-Object { $_.FullName -notmatch '\\(obj|bin|archive)\\' }

# パスの照合（8a）はビルドスクリプトのコメントも対象にする（実測: 削除済みの検証スクリプトを
# 指す参照が buildScript/*.bat のコメントに残っていた）
$scriptDocs = @(Get-ChildItem -Path (Join-Path $repoRoot 'buildScript') -Recurse -File -Include '*.bat', '*.ps1')

# ============================================================
# 5. ドキュメント（markdownlint）
# ============================================================
Write-Output '== 5. ドキュメント（markdownlint） =='
$mdArgs = @('--yes', 'markdownlint-cli2', '--config', '.markdownlint.json')
if ($Fix) { $mdArgs += '--fix' }
$mdArgs += @('src/**/*.md', '.github/**/*.md', '*.md', '!**/obj/**', '!**/bin/**')
$mdText = & npx @mdArgs 2>&1 | Out-String
$mdIssues = 0
if ($mdText -match 'Summary:\s*(\d+) issues? in (\d+) files?') { $mdIssues = [int]$Matches[1] }
Add-Result 'markdownlint' ($mdIssues -eq 0) "$mdIssues 件"
if ($mdIssues -gt 0) { $mdText -split "`r?`n" | Where-Object { $_ -match 'error|warning' } | Select-Object -First 10 | ForEach-Object { "      $($_.Trim())" } }

# ============================================================
# 6. 相対リンク（存在しないファイルへの参照）
# ============================================================
Write-Output '== 6. 相対リンク =='
$brokenLinks = [System.Collections.Generic.List[string]]::new()
$rxLink = [regex]'\]\((?<t>[^)\s]+)\)'
$rxCode = [regex]'`[^`]*`'
foreach ($f in $docs) {
    # コード片（`...`）の中のリンク風表記は文書の例示なので対象外にする
    $text = $rxCode.Replace([IO.File]::ReadAllText($f.FullName, $enc), '``')
    foreach ($m in $rxLink.Matches($text)) {
        $target = $m.Groups['t'].Value
        if ($target -match '^(https?:|mailto:|\w+:|#)') { continue }
        $path = [Uri]::UnescapeDataString($target.Split('#')[0])
        if ($path.Length -eq 0) { continue }
        # ファイルへの参照だけを対象にする（`.` も `/` も含まない語は例示・用語）
        if ($path -notmatch '[./]') { continue }
        $full = [IO.Path]::GetFullPath((Join-Path $f.DirectoryName $path))
        if (-not (Test-Path -LiteralPath $full)) { $brokenLinks.Add("$($f.FullName.Substring($repoRoot.Length + 1)) -> $target") }
    }
}
Add-Result '相対リンク' ($brokenLinks.Count -eq 0) "$($brokenLinks.Count) 件"
$brokenLinks | Select-Object -First 10 | ForEach-Object { "      $_" }

# ============================================================
# 7. 名前空間（フォルダへの追従）
# ============================================================
# 検査はビルドが担う（IDE0130: 名前空間がフォルダー構造と一致しない）。
# 実測で IDE0130 が期待どおりに検出することを確かめてあり、自作の照合は持たない。
Write-Output '== 7. 名前空間（ビルドの IDE0130 が強制する） =='
Add-Info '名前空間' 'ビルドの IDE0130 が強制する（不一致はビルドが失敗する）'

# ============================================================
# 8. 文書が引用する名前空間・ファイル（実在の確認）
# ============================================================
Write-Output '== 8. 文書の引用（実在の確認） =='
$tracked = @{}
$trackedNames = @{}
foreach ($f in (git ls-files)) {
    $tracked[$f] = $true
    $trackedNames[[IO.Path]::GetFileName($f)] = $true
}
$projectNames = @(Get-ChildItem -Path (Join-Path $repoRoot 'src') -Directory | Select-Object -ExpandProperty Name)
# プロジェクト内のフォルダ（部分的な構成図のルートとして許可する）
$intraFolders = @('src', 'tests', 'sample', 'docs')

# 8a) 文書の裸の相対パス（プロジェクト名から始まっていないもの）
$rxQuotedPath = [regex]'(?<![\w./\\-])((?:[A-Za-z0-9_.\-]+[\\/])+[A-Za-z0-9_.\-]+\.(?:csproj|axaml|props|slnx|json|yaml|yml|ps1|bat|cs))(?![\w])'
$loosePaths = [System.Collections.Generic.List[string]]::new()
foreach ($f in ($docs + $scriptDocs)) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $docDir = ([IO.Path]::GetDirectoryName($rel) + '/').Replace('\', '/')
    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        # 環境変数を含む行のパスはリポジトリ外（%LOCALAPPDATA%\~ 等）を指すため対象外にする
        if ($lines[$i] -match '%[A-Za-z_]+%|\$env:') { continue }
        $line = $rxLink.Replace($lines[$i], ']()')
        foreach ($m in $rxQuotedPath.Matches($line)) {
            $tok = $m.Groups[1].Value
            if ($tok -match '^https?') { continue }
            if ($tok.StartsWith('..')) { continue }
            $norm = $tok.Replace('\', '/')
            # 実在の判定は追跡ファイルと作業ツリーの両方で行う（未追跡の新規ファイルを「解決不能」としない）
            if ($tracked.ContainsKey($norm) -or $tracked.ContainsKey($docDir + $norm)) { continue }
            if (Test-Path -LiteralPath (Join-Path $repoRoot $norm)) { continue }
            if (Test-Path -LiteralPath (Join-Path $f.DirectoryName $norm)) { continue }
            $cands = @()
            foreach ($p in $projectNames) {
                foreach ($pre in @('', 'src/', 'tests/', 'sample/')) {
                    if ($tracked.ContainsKey("src/$p/$pre$norm")) { $cands += "src/$p/$pre$norm" }
                }
            }
            if ($cands.Count -eq 0) { $loosePaths.Add(("解決不能 {0}:{1}  {2}" -f $rel, ($i + 1), $tok)) }
            elseif ($cands.Count -eq 1) { $loosePaths.Add(("要統一 {0}:{1}  {2} -> {3}" -f $rel, ($i + 1), $tok, $cands[0])) }
        }
    }
}

# 8b) プロジェクト構成図のルートとファイル名
$treeIssues = [System.Collections.Generic.List[string]]::new()
$rxTreeFile = [regex]'(?<![\w./\\-])([A-Za-z0-9_.\-]+\.(?:csproj|axaml|cs))(?![\w])'
foreach ($f in $docs) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    $inTree = $false
    $treeRootSeen = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line.StartsWith('```')) {
            if ($inTree) { $inTree = $false; } elseif ($line.StartsWith('```text')) { $inTree = $true; $treeRootSeen = $false }
            continue
        }
        if (-not $inTree) { continue }
        # ツリーのルートになり得るのはブロックの先頭行だけ（以降の行はプロジェクト内の相対）
        if (-not $treeRootSeen) {
            if ($line.Trim().Length -eq 0) { continue }
            $treeRootSeen = $true
            if ($line -match '^(?:src/)?([A-Za-z0-9_.\-]+)/\s*$') {
                $label = $Matches[1]
                # ルートはプロジェクト フォルダ名、またはプロジェクト内のフォルダ（部分的な構成図）
                if ($projectNames -notcontains $label -and $intraFolders -notcontains $label) {
                    $treeIssues.Add(("ルート名 {0}:{1}  {2}" -f $rel, ($i + 1), $label))
                }
            }
        }
        foreach ($m in $rxTreeFile.Matches($line)) {
            $n = $m.Groups[1].Value
            if (-not $trackedNames.ContainsKey($n)) { $treeIssues.Add(("ファイル名 {0}:{1}  {2}" -f $rel, ($i + 1), $n)) }
        }
    }
}

# 8c) 陳腐化した識別子（改名・削除の残骸。 対象を追加して育てる）
#     全追跡ファイルを対象にする（文書だけでなくビルドスクリプト・設定ファイルのコメントも含める）
$stalePatterns = @(
    'App\.LineDetection',
    'EsUtil\.SaveEvidence\.LineDetection',
    'verify-ui',
    'MarqueeCore\.ResolveContent',
    'MarqueePresenter\.ResolveContent',
    'App\.(AvaloniaUI|MewUI)\.exe',
    'SaveEvidence\.exe',
    'app\.(Core|AvaloniaUI|MewUI)'
)
$stale = [System.Collections.Generic.List[string]]::new()
foreach ($p in $stalePatterns) {
    foreach ($hit in @(git -c core.quotepath=false grep --untracked -n -E -- $p 2>$null)) {
        if ($hit -match 'archive/') { continue }
        # 検証手順と本スクリプトは、この検査の説明としてこれらの名前を引用するため対象外にする
        if ($hit -match '検証手順\.md') { continue }
        if ($hit -match 'buildScript/verify\.ps1') { continue }
        $stale.Add("$p : $hit")
    }
}

# 8d) 節参照の破損（文書.md の直後に「§」が無い数字が続く形。 一括置換で § が消えた残骸）
$rxSectionRef = [regex]'\]\([^)]*\.md\)\s*\d'
foreach ($f in $docs) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($rxSectionRef.IsMatch($lines[$i])) { $stale.Add(("節参照 {0}:{1}  {2}" -f $rel, ($i + 1), $lines[$i].Trim())) }
    }
}

$quoteIssues = $loosePaths.Count + $treeIssues.Count + $stale.Count
Add-Result '文書の引用' ($quoteIssues -eq 0) "パス $($loosePaths.Count) / 構成図 $($treeIssues.Count) / 陳腐化 $($stale.Count)"
$loosePaths | Select-Object -First 8 | ForEach-Object { "      $_" }
$treeIssues | Select-Object -First 8 | ForEach-Object { "      $_" }
$stale | Select-Object -First 8 | ForEach-Object { "      $_" }

# 8d) 文書へ陳腐化する統計（テスト件数など）を書いていないか
#     「（41 件）」「テストは **577 件**」のような実測値はコードを変えるたびに古くなる。
#     .github/ドキュメント規約.md §10 が「書かない」と定めており、実測は verify.ps1 が出力する。
#     検査を説明する本手順だけは例として引用するため対象外にする（8c と同じ扱い）。
$rxStatistic = [regex]'（[1-9]\d* 件）|テストは \*\*[1-9]\d* 件'
$statistic = [System.Collections.Generic.List[string]]::new()
foreach ($f in $docs) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    if ($rel -match '検証手順\.md$') { continue }
    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($rxStatistic.IsMatch($lines[$i])) { $statistic.Add(("{0}:{1}  {2}" -f $rel, ($i + 1), $lines[$i].Trim())) }
    }
}
Add-Result '文書の統計値' ($statistic.Count -eq 0) "陳腐化する件数の記載 $($statistic.Count) 件"
$statistic | Select-Object -First 10 | ForEach-Object { "      $_" }

# 8e) 文書が引用する型名の接頭辞（実在しない名前空間を書かない）
#     文書は型を「ルート名前空間の型 = 接頭辞なし」「下位名前空間の型 = 名前空間の末尾セグメントを接頭辞」で書く
#     （`Logging.MarqueeLogMessage`）。 フォルダ名を名前空間として書いた `Drawing.MarqueeGeometry` のような
#     陳腐化は、名前の実在検査（8a / 8b）では見つからない（型名は実在するため。 実測: 4 文書 24 箇所が該当した）。
#     - 判定は「点で修飾された名前の**末尾が実在の型名**」のものだけを対象にする（`MarqueeNotice.Kind` 等は対象外）。
#     - 修飾子が「名前空間の接尾辞（セグメント単位）」または「外側の型名（入れ子型）」なら適合とする。
#     - 修飾子が小文字始まり（`e.Rect` のようなローカル変数・引数のメンバー参照）は対象外にする。
#     - 外部フレームワーク（`System.` / `Aprillz.` 等）はリポジトリの型名と同名の型を持つため対象外にする。
$rxTypeDecl = [regex]'(?m)^\s*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|ref|file|unsafe|new|required)\s+)*(?:class|struct|interface|enum|delegate|record(?:\s+(?:class|struct))?)\s+([A-Za-z_]\w*)'
$typeQualifiers = @{}
foreach ($f in (Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -File -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })) {
    $text = [IO.File]::ReadAllText($f.FullName, $enc)
    $nsMatch = [regex]::Match($text, '(?m)^namespace\s+([\w.]+)\s*;')
    $suffixes = @()
    if ($nsMatch.Success) {
        $segments = $nsMatch.Groups[1].Value.Split('.')
        for ($k = 0; $k -lt $segments.Count; $k++) { $suffixes += ($segments[$k..($segments.Count - 1)] -join '.') }
    }
    foreach ($tm in $rxTypeDecl.Matches($text)) {
        $name = $tm.Groups[1].Value
        if (-not $typeQualifiers.ContainsKey($name)) { $typeQualifiers[$name] = [System.Collections.Generic.HashSet[string]]::new() }
        foreach ($s in $suffixes) { [void]$typeQualifiers[$name].Add($s) }
    }
}
$rxTypeRef = [regex]'(?<![\w.])((?:[A-Za-z_]\w*\.)+[A-Za-z_]\w*)(?![\w./\\-])'
$rxExternalRoot = [regex]'^(System|Microsoft|Avalonia|SkiaSharp|Serilog|Xunit|Newtonsoft|Windows|Moq|NSubstitute|Aprillz|ShimSkiaSharp|HarfBuzz|CommunityToolkit)\b'
$typeRefIssues = [System.Collections.Generic.List[string]]::new()
foreach ($f in $docs) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    # 本検査の説明（不合格の例を引用する）は対象外にする（8c と同じ扱い）
    if ($rel -match '検証手順\.md$') { continue }
    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        foreach ($m in $rxTypeRef.Matches($lines[$i])) {
            $token = $m.Groups[1].Value
            $segments = $token.Split('.')
            $last = $segments[-1]
            if (-not $typeQualifiers.ContainsKey($last)) { continue }
            if ($rxExternalRoot.IsMatch($token)) { continue }
            $isVariable = $false
            for ($k = 0; $k -lt $segments.Count - 1; $k++) { if ($segments[$k] -cmatch '^[a-z]') { $isVariable = $true } }
            if ($isVariable) { continue }
            $qualifier = ($segments[0..($segments.Count - 2)] -join '.')
            $ok = $typeQualifiers[$last].Contains($qualifier)
            if (-not $ok -and $typeQualifiers.ContainsKey($segments[-2])) { $ok = $true }
            if (-not $ok) {
                # 修飾子の途中に型名がある場合（`X.Y.Type.Member` の型部分）は判定しない
                $middleIsType = $false
                if ($segments.Count -ge 3) {
                    for ($k = 0; $k -lt $segments.Count - 2; $k++) { if ($typeQualifiers.ContainsKey($segments[$k])) { $middleIsType = $true } }
                }
                if (-not $middleIsType) { $typeRefIssues.Add(("{0}:{1}  {2}" -f $rel, ($i + 1), $token)) }
            }
        }
    }
}
Add-Result '文書の型参照' ($typeRefIssues.Count -eq 0) "$($typeRefIssues.Count) 件"
$typeRefIssues | Select-Object -First 10 | ForEach-Object { "      $_" }

# ============================================================
# 9. 改行コード
# ============================================================
Write-Output '== 9. 改行コード =='
$eolIssues = @(git ls-files --eol | Select-String 'w/mixed|w/lf')
Add-Result '改行コード' (@($eolIssues).Count -eq 0) "$(@($eolIssues).Count) 件"
$eolIssues | Select-Object -First 5 | ForEach-Object { "      $($_.Line)" }

# ============================================================
# 10. フォルダ（空フォルダ / 孤立した bin・obj）
# ============================================================
Write-Output '== 10. フォルダ =='
$emptyDirs = @(Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -Directory |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
        Where-Object { @(Get-ChildItem -LiteralPath $_.FullName -Force).Count -eq 0 })
$orphanBins = @(Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -Directory |
        Where-Object { $_.Name -in @('bin', 'obj') } |
        Where-Object { @(Get-ChildItem -LiteralPath (Split-Path -Parent $_.FullName) -Filter '*.csproj').Count -eq 0 })
Add-Result 'フォルダ' ($emptyDirs.Count -eq 0 -and $orphanBins.Count -eq 0) "空 $($emptyDirs.Count) / 孤立 bin・obj $($orphanBins.Count)"
$emptyDirs | Select-Object -First 5 | ForEach-Object { "      $($_.FullName.Substring($repoRoot.Length + 1))" }
$orphanBins | Select-Object -First 5 | ForEach-Object { "      $($_.FullName.Substring($repoRoot.Length + 1))" }

# 10b) テスト ファイル名（`.` を挟まない `{対象}Tests.cs` を使わない）
#      ファイル名は `{対象}.Tests.cs`、クラス名は `{対象}Tests` とする（命名規約）。 クラス名だけから
#      作った `{対象}Tests.cs` は、ファイル名とクラス名の対応が崩れる（実測: 17 ファイルを統一した）
$testNameIssues = [System.Collections.Generic.List[string]]::new()
foreach ($f in @(git -c core.quotepath=false ls-files 2>$null)) {
    if ($f -notmatch '/tests/' -or $f -notmatch '\.cs$') { continue }
    $name = [IO.Path]::GetFileName($f)
    if (-not $name.EndsWith('Tests.cs')) { continue }
    if ($name.EndsWith('.Tests.cs')) { continue }
    $testNameIssues.Add($f)
}
Add-Result 'テストの命名' ($testNameIssues.Count -eq 0) "`{対象}Tests.cs $($testNameIssues.Count) 件"
$testNameIssues | Select-Object -First 10 | ForEach-Object { "      $_" }

# 10c) ソースとテストの対応（参考値。 合否に含めない）
#      テストはソースのフォルダを鏡映し、専用ファイルの名前を `{ソース名}.Tests.cs` とする。 ただし
#      専用ファイルを持つかは検証の単位で決めるため（公開 API 経由で網羅する型は持たなくてよい）、
#      合否にはせず件数だけを表示する
$testPairs = 0
$testTotal = 0
foreach ($proj in (Get-ChildItem -Path (Join-Path $repoRoot 'src') -Directory)) {
    $srcDir = Join-Path $proj.FullName 'src'
    $testDir = Join-Path $proj.FullName 'tests'
    if (-not (Test-Path -LiteralPath $srcDir) -or -not (Test-Path -LiteralPath $testDir)) { continue }
    foreach ($f in (Get-ChildItem -Path $srcDir -Recurse -File -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })) {
        $testTotal++
        $rel = $f.FullName.Substring($srcDir.Length + 1)
        $fileName = [IO.Path]::GetFileNameWithoutExtension($rel) + '.Tests.cs'
        $dir = [IO.Path]::GetDirectoryName($rel)
        $expected = if ([string]::IsNullOrEmpty($dir)) { Join-Path $testDir $fileName } else { Join-Path (Join-Path $testDir $dir) $fileName }
        if (Test-Path -LiteralPath $expected) { $testPairs++ }
    }
}
Add-Info 'ソースとテストの対応' "専用ファイルあり $testPairs / ソース $testTotal（参考値。 公開 API 経由で検証する型は専用ファイルを持たない）"

# ============================================================
# 11. コードスタイル（dotnet format。 情報レベルまで含めて 0 件にする）
# ============================================================
if (-not $SkipFormat) {
    Write-Output '== 11. コードスタイル（dotnet format） =='
    if ($Fix) {
        & dotnet format whitespace $Solution --no-restore 2>&1 | Out-Null
        & dotnet format style $Solution --severity warn --no-restore 2>&1 | Out-Null
        & dotnet format analyzers $Solution --severity warn --no-restore 2>&1 | Out-Null
    }
    $fmtText = & dotnet format style $Solution --verify-no-changes --severity info --no-restore 2>&1 | Out-String
    $fmtIssues = ([regex]::Matches($fmtText, 'warning (IDE|CA)\d+')).Count
    Add-Result 'dotnet format' ($fmtIssues -eq 0) "$fmtIssues 件"
    if ($fmtIssues -gt 0) { $fmtText -split "`r?`n" | Where-Object { $_ -match 'warning ' } | Select-Object -First 10 | ForEach-Object { "      $($_.Trim())" } }
}

# ============================================================
# 要約
# ============================================================
$failed = @($script:Results | Where-Object { $_.Kind -eq 'Check' -and -not $_.Ok })
Write-Output ''
Write-Output '===== 検証の要約 ====='
foreach ($r in $script:Results) {
    $mark = if ($r.Kind -eq 'Info') { 'INFO' } elseif ($r.Ok) { 'OK  ' } else { 'FAIL' }
    Write-Output ("{0}  {1}`t{2}" -f $mark, $r.Name, $r.Detail)
}
Write-Output ''
if ($failed.Count -eq 0) {
    $checks = @($script:Results | Where-Object { $_.Kind -eq 'Check' }).Count
    Write-Output "すべて合格（合否 $checks 項目）"
    exit 0
}
$names = ($failed | ForEach-Object { $_.Name }) -join ', '
Write-Output "不合格 $($failed.Count) 項目: $names"
exit 1
