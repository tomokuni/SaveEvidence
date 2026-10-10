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
dotnet format による検査（警告以上のコード スタイル。 命名規則を含む）を省略する（時間を短縮するとき）。

.PARAMETER SkipPackage
NuGet の健全性（既知の脆弱性。 推移的依存を含む）の検査を省略する（ネットワークを使いたくないとき）。

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

# ネットワーク照会や MSBuild の評価は待ち時間が長い（実測: NuGet の照会 35 秒 / dotnet format 20 秒）ため、
# 背景へ投入して他の検査と重ねる。 `Start-Job`（PowerShell をもう 1 つ立ち上げる）より軽く、出力は一時ファイルへ書き出す。
function Start-BackgroundDotNet([string[]]$Arguments) {
    $base = Join-Path ([IO.Path]::GetTempPath()) ("se_verify_" + [Guid]::NewGuid().ToString('N'))
    $process = Start-Process -FilePath 'dotnet' -ArgumentList $Arguments -WorkingDirectory $repoRoot -NoNewWindow -PassThru `
        -RedirectStandardOutput ($base + '.out') -RedirectStandardError ($base + '.err')
    return [pscustomobject]@{ Process = $process; OutFile = $base + '.out'; ErrFile = $base + '.err' }
}

# 背景実行の完了を待ち、出力（標準出力 + 標準エラー）と終了コードを返す（一時ファイルは消す）
function Receive-BackgroundDotNet($Background) {
    $Background.Process.WaitForExit()
    $text = ''
    foreach ($file in @($Background.OutFile, $Background.ErrFile)) {
        if (Test-Path -LiteralPath $file) { $text += [IO.File]::ReadAllText($file, $enc) }
        Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue
    }
    return [pscustomobject]@{ Text = $text; ExitCode = $Background.Process.ExitCode }
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

# `dotnet format`（MSBuild の評価。 実測 20 秒）は待ち時間が長いため、ここで背景へ投入し、
# テスト・ドキュメント検査と重ねる（-Fix のときは修正が先に要るため投入しない）。
# `--severity warn` にするのは「警告以上を 0 件にする」合否基準を引数で表すためである
# （情報レベルの提案（IDE0350 等。 実測 63 件）はビルドが強制する範囲ではなく、合否に含めない）。
$backgroundFormat = if ($SkipFormat -or $Fix) { $null } else { Start-BackgroundDotNet @('format', 'style', $Solution, '--verify-no-changes', '--severity', 'warn', '--no-restore') }

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
# 4. NuGet の健全性（既知の脆弱性。 推移的依存も含む）
# ============================================================
if (-not $SkipPackage) {
    Write-Output '== 4. NuGet の健全性 =='
    # 脆弱性は復元の NuGet 監査で検出する（推移的依存も対象。 実測 2.7 秒。 同じ結果を
    # `dotnet package list --vulnerable --include-transitive` で取ると 35 秒かかる）。
    # `--force-evaluate` は「復元は最新」と判定されたときにも監査を走らせるために付ける
    # （付けないと監査が省略され、0 件と誤判定する）。 監査は検査そのものなので、-NoRestore は適用しない。
    # 直接参照の脆弱性はビルドの復元でも検出される（`NuGetAuditMode` の既定は direct であり、
    # 警告は Directory.Build.props の TreatWarningsAsErrors でエラーになる）。 本節は推移的依存まで広げる。
    $auditText = Invoke-DotNet @('restore', $Solution, '-p:NuGetAuditMode=all', '--force-evaluate')
    $auditHits = @($auditText -split "`r?`n" | Where-Object { $_ -match 'NU19\d\d' })

    # 監査自体が失敗した場合（復元の失敗）に 0 件と誤判定しない（終了コードは Invoke-DotNet が取れないため、
    # 警告に加えて「監査の結果が出たか」ではなく NuGet のエラー（NU1xxx）の有無も見る）
    $auditFailed = $auditText -match 'error NU1(?!9)\d\d'
    Add-Result 'NuGet の健全性' ($auditHits.Count -eq 0 -and -not $auditFailed) "脆弱性 $($auditHits.Count) 件（推移的依存を含む）"
    $auditHits | Select-Object -First 10 | ForEach-Object { "      $($_.Trim())" }
    if ($auditFailed) {
        "      監査（復元）が失敗した"
        $auditText -split "`r?`n" | Where-Object { $_ -match 'error NU1' } | Select-Object -First 5 | ForEach-Object { "      $($_.Trim())" }
    }
}

# 以降のドキュメント検査で使う文書一覧（リポジトリ直下の文書も対象にする）
$docs = @(
    Get-ChildItem -Path $repoRoot -File -Filter '*.md'
    Get-ChildItem -Path (Join-Path $repoRoot 'src'), (Join-Path $repoRoot '.github') -Recurse -File -Filter '*.md'
) | Where-Object { $_.FullName -notmatch '\\(obj|bin|archive)\\' }

# パスの照合（8a）はビルドスクリプトのコメントも対象にする（実測: 削除済みの検証スクリプトを
# 指す参照が buildScript/*.bat のコメントに残っていた）
$scriptDocs = @(Get-ChildItem -Path (Join-Path $repoRoot 'buildScript') -Recurse -File -Include '*.bat', '*.ps1')

# ソースの一覧（複数の検査が同じ列挙を繰り返さないよう 1 回だけ作る。
# `-Include` は obj/bin まで全ファイルを走査してから絞るため遅い。 `-Filter` を使う）
$allCs = @(Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -File -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })
$allCsproj = @(Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -File -Filter '*.csproj' |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })

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

# 8f) 文書の体裁（規約 §6・§7）
#     - `docs/SPEC_*.md` は見出しへ番号を付け、冒頭へ `## 目次` を置く（README は番号のみ。 目次は任意）。
#     - 番号は階層を表す（`##` = `<連番>` + ピリオド / `###` = `<親>.<連番>` / `####` = `<親>.<連番>.<連番>`）。
#     - 目次は `##` 以下の見出しだけを、**番号付きの文言**と一致するアンカーで列挙する
#       （見出しを足したのに目次を直していない状態は、markdownlint の MD051 では見つからない）。
#     - 目次見出しは `## 目次 <!-- omit from toc -->` と書く（エディタの目次整形拡張が
#       目次自身を目次へ載せないためのマーカー。 マーカー無しも受け付ける）。
#     - `SPEC_System.md` へ「単一所有」「移行の経緯」等の独立した章を設けない（規約 §6）。
function Get-MdHeadings([string]$Path) {
    $lines = [IO.File]::ReadAllLines($Path, $enc)
    $result = [System.Collections.Generic.List[object]]::new()
    $inFence = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i].StartsWith('```')) { $inFence = -not $inFence; continue }
        if ($inFence) { continue }
        if ($lines[$i] -match '^(#{1,6}) (.+)$') {
            $result.Add([pscustomobject]@{ Line = $i + 1; Level = $Matches[1].Length; Text = $Matches[2] })
        }
    }
    return ,$result
}

function Get-MdSlug([string]$Text) {
    $s = [regex]::Replace($Text, '[^\p{L}\p{M}\p{Nd}\p{Pc}\- ]', '')
    return ($s.ToLowerInvariant() -replace '\s', '-')
}

$rxHeadingNumber = [regex]'^(\d+(?:\.\d+)*)(\.?)\s+(.+)$'
$rxTocHeading = [regex]'^目次( <!-- omit from toc -->)?$'
$rxForbiddenChapter = [regex]'移行|経緯|変更履歴|最適化|単一所有|以前は|旧仕様|旧名'
$rxTocItem = [regex]'^\s*- '
$styleIssues = [System.Collections.Generic.List[string]]::new()
foreach ($f in $docs) {
    $name = [IO.Path]::GetFileName($f.FullName)
    $isSpec = $f.FullName -match '[\\/]docs[\\/]SPEC_[^\\/]+\.md$'
    if (-not $isSpec -and $name -ne 'README.md') { continue }
    $isSystem = $f.FullName -match '[\\/]docs[\\/]SPEC_System\.md$'
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $headings = Get-MdHeadings $f.FullName
    if (@($headings | Where-Object { $_.Level -eq 1 }).Count -ne 1) { $styleIssues.Add(("{0}  H1 が 1 件でない" -f $rel)); continue }

    $expectedToc = [System.Collections.Generic.List[string]]::new()
    $counters = @{}
    foreach ($h in $headings) {
        if ($h.Level -eq 1) { continue }
        if ($h.Level -eq 2 -and $rxTocHeading.IsMatch($h.Text)) { continue }
        if ($isSystem -and $rxForbiddenChapter.IsMatch($h.Text)) {
            $styleIssues.Add(("{0}:{1}  規約 §6 が禁じる章（独立性の高い章を設けない）: {2}" -f $rel, $h.Line, $h.Text))
        }
        if ($h.Level -gt 4) { $styleIssues.Add(("{0}:{1}  見出しの階層が 4 より深い: {2}" -f $rel, $h.Line, $h.Text)); continue }
        $m = $rxHeadingNumber.Match($h.Text)
        if (-not $m.Success) { $styleIssues.Add(("{0}:{1}  見出しに番号が無い: {2}" -f $rel, $h.Line, $h.Text)); continue }
        $numText = $m.Groups[1].Value
        $dot = $m.Groups[2].Value
        $rest = $m.Groups[3].Value
        $parts = $numText.Split('.')
        if ($parts.Count -ne ($h.Level - 1)) {
            $styleIssues.Add(("{0}:{1}  番号の階層が見出しと一致しない: {2}" -f $rel, $h.Line, $h.Text))
            continue
        }
        if ($h.Level -eq 2 -and $dot -ne '.') { $styleIssues.Add(("{0}:{1}  `##` の番号にピリオドが無い: {2}" -f $rel, $h.Line, $h.Text)) }
        if ($h.Level -ne 2 -and $dot -eq '.') { $styleIssues.Add(("{0}:{1}  番号に余分なピリオドがある: {2}" -f $rel, $h.Line, $h.Text)) }

        $sequential = $true
        for ($lv = 0; $lv -lt $parts.Count; $lv++) {
            $key = $lv + 2
            if ($lv -eq ($parts.Count - 1)) { $counters[$key] = [int]$counters[$key] + 1 }
            elseif (-not $counters.ContainsKey($key)) { $counters[$key] = 1 }
            if ($counters[$key] -ne [int]$parts[$lv]) { $sequential = $false }
        }
        for ($lv = ($h.Level + 1); $lv -le 6; $lv++) { [void]$counters.Remove($lv) }
        if (-not $sequential) { $styleIssues.Add(("{0}:{1}  番号が連番でない: {2}" -f $rel, $h.Line, $h.Text)) }

        $display = if ($h.Level -eq 2) { "$numText." } else { $numText }
        $full = "$display $rest"
        $indent = ' ' * (($h.Level - 2) * 2)
        $expectedToc.Add(("{0}- [{1}](#{2})" -f $indent, $full, (Get-MdSlug $full)))
    }

    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    $tocIdx = -1
    for ($i = 0; $i -lt $lines.Count; $i++) { if ($rxTocHeading.IsMatch(($lines[$i] -replace '^## ', ''))) { $tocIdx = $i; break } }
    if ($isSpec) {
        if ($tocIdx -lt 0) { $styleIssues.Add(("{0}  目次が無い（規約 §7）" -f $rel)) }
        else {
            $firstH2 = @($headings | Where-Object { $_.Level -eq 2 } | Select-Object -First 1)
            if ($firstH2 -and -not ($rxTocHeading.IsMatch($firstH2.Text))) { $styleIssues.Add(("{0}  目次が最初の ## 見出しでない（最初は「{1}」）" -f $rel, $firstH2.Text)) }
        }
    }
    if ($tocIdx -ge 0) {
        $actualToc = [System.Collections.Generic.List[string]]::new()
        for ($i = $tocIdx + 1; $i -lt $lines.Count; $i++) {
            if ($rxTocItem.IsMatch($lines[$i])) { $actualToc.Add($lines[$i]); continue }
            if ($lines[$i].Trim() -eq '' -and $actualToc.Count -eq 0) { continue }
            break
        }
        if ($actualToc.Count -ne $expectedToc.Count) {
            $styleIssues.Add(("{0}  目次の件数が一致しない: 実測 {1} / 期待 {2}（見出しを変えたら目次を直す）" -f $rel, $actualToc.Count, $expectedToc.Count))
        }
        else {
            for ($i = 0; $i -lt $actualToc.Count; $i++) {
                if ($actualToc[$i] -ne $expectedToc[$i]) {
                    $styleIssues.Add(("{0}  目次が一致しない: 実測 [{1}] / 期待 [{2}]" -f $rel, $actualToc[$i], $expectedToc[$i]))
                    break
                }
            }
        }
    }
}
Add-Result '文書の体裁' ($styleIssues.Count -eq 0) "番号・目次・禁止章 $($styleIssues.Count) 件"
$styleIssues | Select-Object -First 10 | ForEach-Object { "      $_" }

# 8g) パス記法（規約 §8: プロジェクト内のパスは `src/<プロジェクト名>/…` から書く）
#     プロジェクト内のフォルダ名（`src/<プロジェクト>/src|tests|sample` の直下）で始まる `` `A/B` `` 形式の
#     引用は、どのプロジェクトのものか読めない（実測: `Native/Win` のような記述が残っていた）。
#     リポジトリ ルートのフォルダ（`publish/` / `.work/` 等）、リンクの表示文、コードフェンス内は対象外にする。
$innerFolders = [System.Collections.Generic.HashSet[string]]::new()
foreach ($p in (git ls-files 'src/*/src/*' 'src/*/tests/*' 'src/*/sample/*')) {
    $segments = $p.Split('/')
    if ($segments.Count -ge 5) { [void]$innerFolders.Add($segments[3]) }
}
$rxAllowedRoot = [regex]'^(?:src|tests|sample|docs|buildScript|publish|artifacts|\.github|\.work|\.vscode|obj|bin)/'
$rxPathLike = [regex]'^[A-Za-z][\w.\-]*/[\w.\-]+$'
$rxBacktick = [regex]'`([^`]+)`'
$pathNotation = [System.Collections.Generic.List[string]]::new()
foreach ($f in $docs) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    # 本検査の説明（規則が反例を引用する）は対象外にする（8c / 8e と同じ扱い）
    if ($rel -match '(検証手順|ドキュメント規約)\.md$') { continue }
    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i].StartsWith('```')) { continue }
        foreach ($m in $rxBacktick.Matches($lines[$i])) {
            $token = $m.Groups[1].Value
            if (-not $rxPathLike.IsMatch($token)) { continue }
            if ($rxAllowedRoot.IsMatch($token)) { continue }
            if ($lines[$i].Substring($m.Index + $m.Length) -like '](*') { continue }
            if ($innerFolders.Contains($token.Split('/')[0])) { $pathNotation.Add(("{0}:{1}  {2}" -f $rel, ($i + 1), $token)) }
        }
    }
}
Add-Result '文書のパス記法' ($pathNotation.Count -eq 0) "$($pathNotation.Count) 件"
$pathNotation | Select-Object -First 10 | ForEach-Object { "      $_" }

# 8h) 文書とコメントの記載範囲（依存関係 §7 / ドキュメント規約 §11）
#     可搬な層（EsUtil.Platform.Core / EsUtil.Feature.*）の文書とコメントには、参照先（推移を含む）以外の
#     プロジェクトを書かない（可搬性が損なわれ、読者が「その型を直接使える」と誤解する）。
#     - 到達可能集合は csproj の ProjectReference から求める（§3 の表と DEP0001-0006 が一致を保証する）。
#     - テスト・サンプルは対象プロジェクトと同じ単位として扱う（`X.Tests` への言及は `X` と同じ）。
#     - 相対リンクの URL 部は対象外（リンク先は実在のパスを指す必要がある）。
#     - 2 つ以上のプロジェクトが同じ名前空間を持つ場合は帰属を決められないため、そのトークンは検査しない。
function Get-CanonicalProjectKey([string]$key) {
    $c = $key -replace '\.Tests$', ''
    return ($c -replace '\.Sample$', '')
}

$projTokens = @{}
$projRefs = @{}
$projKeys = [System.Collections.Generic.List[string]]::new()
$projKeySet = [System.Collections.Generic.HashSet[string]]::new()
foreach ($csproj in (Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -File -Filter '*.csproj' |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })) {
    $key = [IO.Path]::GetFileNameWithoutExtension($csproj.Name)
    if ($projKeySet.Add($key)) { $projKeys.Add($key) }
    $csText = [IO.File]::ReadAllText($csproj.FullName, $enc)
    $tokens = [System.Collections.Generic.List[string]]::new()
    $tokens.Add($key)
    $nsMatch = [regex]::Match($csText, '<RootNamespace>([^<]+)</RootNamespace>')
    if ($nsMatch.Success -and $nsMatch.Groups[1].Value -ne $key) { $tokens.Add($nsMatch.Groups[1].Value) }
    $projTokens[$key] = $tokens
    $canonical = Get-CanonicalProjectKey $key
    if (-not $projRefs.ContainsKey($canonical)) { $projRefs[$canonical] = [System.Collections.Generic.HashSet[string]]::new() }
    foreach ($refMatch in [regex]::Matches($csText, '<ProjectReference\s+Include="(?<p>[^"]+)"')) {
        [void]$projRefs[$canonical].Add((Get-CanonicalProjectKey ([IO.Path]::GetFileNameWithoutExtension($refMatch.Groups['p'].Value))))
    }
}
$tokenUseCount = @{}
foreach ($key in $projKeys) { foreach ($t in $projTokens[$key]) { if (-not $tokenUseCount.ContainsKey($t)) { $tokenUseCount[$t] = 0 }; $tokenUseCount[$t]++ } }

$scopeIssues = [System.Collections.Generic.List[string]]::new()
$rxMdLinkUrl = [regex]'\]\([^)]*\)'
foreach ($target in @($projKeys | Where-Object { $_.StartsWith('EsUtil.') -and (Get-CanonicalProjectKey $_) -eq $_ })) {
    $targetDir = Join-Path $repoRoot "src/$target"
    if (-not (Test-Path -LiteralPath $targetDir)) { continue }
    $reachable = [System.Collections.Generic.HashSet[string]]::new()
    $queue = [System.Collections.Generic.Queue[string]]::new()
    foreach ($key in $projKeys) {
        if ((Get-CanonicalProjectKey $key) -eq $target) { [void]$reachable.Add($key); $queue.Enqueue($target) }
    }
    while ($queue.Count -gt 0) {
        $current = $queue.Dequeue()
        if (-not $projRefs.ContainsKey($current)) { continue }
        foreach ($dep in $projRefs[$current]) { if ($reachable.Add($dep)) { $queue.Enqueue($dep) } }
    }
    $patterns = [System.Collections.Generic.List[string]]::new()
    foreach ($key in $projKeys) {
        if ($reachable.Contains((Get-CanonicalProjectKey $key))) { continue }
        foreach ($t in $projTokens[$key]) { if ($tokenUseCount[$t] -eq 1) { $patterns.Add(('(?<![\w.-])' + [regex]::Escape($t) + '(?![\w.-])')) } }
    }
    if ($patterns.Count -eq 0) { continue }
    $rxForbidden = [regex]($patterns -join '|')
    # 列挙は早期に作った一覧から対象プロジェクト配下だけを選ぶ（`-Include` の全走査を繰り返さない）
    # 対象プロジェクト配下だけを選ぶ（パスの区切りまで含めて一致させる。 前方一致だけでは
    # `EsUtil.Platform.Core` が `EsUtil.Platform.Core.Windows` を巻き込む）
    $targetPrefix = $targetDir + [IO.Path]::DirectorySeparatorChar
    $scopeFiles = @(@($allCs | Where-Object { $_.FullName.StartsWith($targetPrefix, [StringComparison]::OrdinalIgnoreCase) }) +
        @($docs | Where-Object { $_.FullName.StartsWith($targetPrefix, [StringComparison]::OrdinalIgnoreCase) }))
    foreach ($f in $scopeFiles) {
        $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
        $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            # リンクの URL 部だけを除外する（`](` を含む行でなければ置換の必要が無い）
            if ($line.IndexOf('](') -ge 0) { $line = $rxMdLinkUrl.Replace($line, ']()') }
            if ($rxForbidden.IsMatch($line)) { $scopeIssues.Add(("{0}:{1}  [{2}]" -f $rel, ($i + 1), $rxForbidden.Match($line).Value)) }
        }
    }
}
Add-Result '文書の記載範囲' ($scopeIssues.Count -eq 0) "可搬な層の参照外のプロジェクト $($scopeIssues.Count) 件"
$scopeIssues | Select-Object -First 10 | ForEach-Object { "      $_" }

# 8i) 状態を表す真偽値の命名（命名規約: 述語の接頭辞）
#     状態・判定の真偽値は Is / Has / Can / Should / Show で始める（`Redraw` では true の意味が読めない）。
#     過去形（`Was`）は使わない（真偽値の種類を増やさない）。
#     設定・外観（Settings / Options / Appearance / Config）の真偽値は動作を表す動詞句でもよい（.NET の慣習）。
$rxStateBool = [regex]'(?<![\w.])(?:public|internal|private|protected)\s+(?:static\s+)?(?:virtual\s+|override\s+|sealed\s+|new\s+|readonly\s+)*bool\s+(?<n>[A-Za-z_]\w*)\s*\{'
$rxBoolPredicate = [regex]'^(Is|Has|Can|Should|Show|Use|Supports|Contains|Needs)'
$rxSettingsTypeName = [regex]'(Settings|Options|Appearance|Config)$'
$boolIssues = [System.Collections.Generic.List[string]]::new()
foreach ($f in (Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -File -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    # ファイル先頭の型をそのファイルの型とみなす（1 ファイル 1 型の規約）
    $declaredType = ''
    foreach ($line in $lines) {
        $typeMatch = [regex]::Match($line, '(?:class|struct|record(?:\s+(?:class|struct))?)\s+([A-Za-z_]\w*)')
        if ($typeMatch.Success) { $declaredType = $typeMatch.Groups[1].Value; break }
    }
    if ($declaredType -match $rxSettingsTypeName) { continue }
    for ($i = 0; $i -lt $lines.Count; $i++) {
        foreach ($m in $rxStateBool.Matches($lines[$i])) {
            $name = $m.Groups['n'].Value
            if (-not $rxBoolPredicate.IsMatch($name)) { $boolIssues.Add(("{0}:{1}  [{2}] {3}" -f $rel, ($i + 1), $declaredType, $name)) }
        }
    }
}
Add-Result '状態の真偽値の命名' ($boolIssues.Count -eq 0) "$($boolIssues.Count) 件"
$boolIssues | Select-Object -First 10 | ForEach-Object { "      $_" }

# 8j) 命名と構造（規約が手作業に委ねていた項目を機械検査へ移したもの）
#     - テスト メソッドは internal（テスト規約。 インターフェースを実装するクラスは除外する）
#     - テスト クラス名はファイル名から `.Tests` を除き `.` を除去した名前（命名規約）
#     - 可搬な層のカテゴリ フォルダは単数形（命名規約）
#     - `async void` はイベント ハンドラーだけ（コーディング規約 §7）
$codeIssues = [System.Collections.Generic.List[string]]::new()
foreach ($f in @($allCs | Where-Object { $_.FullName -match '\\tests\\' })) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    # インターフェースを実装するクラスの行範囲（モック・スパイの public 実装を許すため）
    $ifaceClassStarts = [System.Collections.Generic.List[int]]::new()
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $classMatch = [regex]::Match($lines[$i], '(?<![\w.])class\s+[A-Za-z_]\w*\s*(?<rest>.*)$')
        if (-not $classMatch.Success) { continue }
        $decl = $classMatch.Groups['rest'].Value
        $j = $i
        while ($decl -notmatch '\{' -and $j + 1 -lt $lines.Count) { $j++; $decl += ' ' + $lines[$j] }
        if ($decl -match ':\s*[^[{]*\bI[A-Z]\w*') { $ifaceClassStarts.Add($i) }
    }
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -notmatch '^\s{4,}public\s+(?:async\s+)?(?:void|Task|ValueTask)\s+(?<n>[A-Za-z_]\w*)\s*\(') { continue }
        $inIfaceClass = $false
        foreach ($start in $ifaceClassStarts) { if ($i -gt $start) { $inIfaceClass = $true } }
        if (-not $inIfaceClass) { $codeIssues.Add(("{0}:{1}  テスト メソッドが public（internal にする）: {2}" -f $rel, ($i + 1), $Lines[$i].Trim())) }
    }
    $base = [IO.Path]::GetFileNameWithoutExtension($f.Name)
    if ($base -match '\.Tests$') {
        $expectedClass = (($base -replace '\.Tests$', '') -replace '\.', '') + 'Tests'
        $classNames = @([regex]::Matches([IO.File]::ReadAllText($f.FullName, $enc), '(?m)^\s*(?:public|internal)\s+(?:sealed\s+)?class\s+([A-Za-z_]\w*)') | ForEach-Object { $_.Groups[1].Value })
        if ($classNames.Count -gt 0 -and $classNames -notcontains $expectedClass) {
            $codeIssues.Add(("{0}  テスト クラス名 [{1}] がファイル名から決まる [{2}] と一致しない" -f $rel, ($classNames -join ', '), $expectedClass))
        }
    }
}
foreach ($f in @($allCs | Where-Object { $_.FullName -notmatch '\\(tests|sample)\\' })) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $m = [regex]::Match($lines[$i], 'async\s+void\s+[A-Za-z_]\w*\s*\((?<p>[^)]*)\)')
        if (-not $m.Success) { continue }
        # イベント ハンドラーは `(object?, EventArgs 派生)` の 2 引数
        if ($m.Groups['p'].Value -match '^\s*object\??\s+\w+\s*,\s*\w+EventArgs\??\s+\w+\s*$') { continue }
        $codeIssues.Add(("{0}:{1}  イベント ハンドラー以外の `async void`（`async Task` にして失敗をログへ記録する）: {2}" -f $rel, ($i + 1), $lines[$i].Trim()))
    }
}
# 可搬な層のカテゴリ フォルダは単数形（型の集合を表すカテゴリは複数形でよい）
$allowedPluralFolders = @('Models', 'Services', 'Views', 'Enums', 'Controls', 'Extensions', 'Properties', 'Interop')
foreach ($projDir in (Get-ChildItem -Path (Join-Path $repoRoot 'src') -Directory -Filter 'EsUtil.*')) {
    foreach ($d in (Get-ChildItem -Path (Join-Path $projDir.FullName 'src') -Recurse -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })) {
        if ($d.Name -cin $allowedPluralFolders) { continue }
        if ($d.Name -cmatch 's$' -and $d.Name -notmatch '(ss|us|is|as|ics|ous)$') {
            $codeIssues.Add(("{0}  カテゴリ フォルダが複数形（単数形にする。 型の集合を表すカテゴリは除く）" -f $d.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')))
        }
    }
}
Add-Result '命名と構造' ($codeIssues.Count -eq 0) "テストの可視性・クラス名・フォルダ・async void $($codeIssues.Count) 件"
$codeIssues | Select-Object -First 10 | ForEach-Object { "      $_" }

# 8k) イベントの発火は Raise ヘルパーへ集約する（`event` へ `?.Invoke` を書かない。 コーディング規約 §7）
$rxEventDecl = [regex]'(?<![\w.])(?:public|internal|private|protected)\s+(?:static\s+)?event\s+(?:EventHandler(?:<[^>]+>)?|\w+(?:<\w+>)?)\s+(?<n>[A-Za-z_]\w*)'
$invokeIssues = [System.Collections.Generic.List[string]]::new()
foreach ($f in @($allCs | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $text = [IO.File]::ReadAllText($f.FullName, $enc)
    $eventNames = @($rxEventDecl.Matches($text) | ForEach-Object { $_.Groups['n'].Value })
    if ($eventNames.Count -eq 0) { continue }
    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        foreach ($n in $eventNames) {
            if ($lines[$i] -match ("(?<![\w.])" + [regex]::Escape($n) + "\?\.Invoke\(")) { $invokeIssues.Add(("{0}:{1}  {2}" -f $rel, ($i + 1), $lines[$i].Trim())) }
        }
    }
}
Add-Result 'イベントの発火' ($invokeIssues.Count -eq 0) "event への ?.Invoke $($invokeIssues.Count) 件（Raise ヘルパーへ集約する）"
$invokeIssues | Select-Object -First 10 | ForEach-Object { "      $_" }

# 8l) ログ定義のドキュメントコメントへレベルと出力書式を書く（ログ規約 §5）
$logDocIssues = [System.Collections.Generic.List[string]]::new()
foreach ($f in @($allCs | Where-Object { $_.FullName -notmatch '\\(obj|bin|tests|sample)\\' })) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -notmatch '\[LoggerMessage\(') { continue }
        $doc = [System.Collections.Generic.List[string]]::new()
        for ($j = $i - 1; $j -ge 0 -and $lines[$j].TrimStart().StartsWith('///'); $j--) { $doc.Insert(0, $lines[$j]) }
        $text = $doc -join "`n"
        if ($text -notmatch '(LogLevel|ログレベル):') { $logDocIssues.Add(("{0}:{1}  ログレベルが doc にない" -f $rel, ($i + 1))) }
        if ($text -notmatch '出力書式:') { $logDocIssues.Add(("{0}:{1}  出力書式が doc にない" -f $rel, ($i + 1))) }
    }
}
Add-Result 'ログ定義の doc' ($logDocIssues.Count -eq 0) "レベル・出力書式の記載漏れ $($logDocIssues.Count) 件"
$logDocIssues | Select-Object -First 10 | ForEach-Object { "      $_" }

# 8m) コードの抑止は ビルド設定規約.md の「付録 A 抑止の一覧」にあるものだけ（規約が例外の所有者）
$buildRulesPath = Join-Path $repoRoot '.github/ビルド設定規約.md'
$buildRules = [IO.File]::ReadAllText($buildRulesPath, $enc)
$suppressSites = [System.Collections.Generic.List[object]]::new()
# 列挙は 8j で作った一覧を再利用する（`-Include` は obj/bin まで全ファイルを走査してから絞るため遅い。 実測 6 秒 → 0.5 秒）
foreach ($f in @($allCs + $allCsproj)) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        # 抑止の記述が無い行は正規表現を走らせない（実測: 2.5 秒 → 1 秒未満）
        if ($lines[$i].IndexOf('pragma') -lt 0 -and $lines[$i].IndexOf('SuppressMessage') -lt 0 -and $lines[$i].IndexOf('NoWarn') -lt 0) { continue }
        $m = [regex]::Match($lines[$i], '#pragma\s+warning\s+disable\s+(?<c>[A-Z]+\d+)')
        if ($m.Success) { $suppressSites.Add([pscustomobject]@{ File = $rel; Line = $i + 1; Code = $m.Groups['c'].Value }) }
        $m = [regex]::Match($lines[$i], 'SuppressMessage\([^)]*"(?<c>[A-Z]+\d+)"')
        if ($m.Success) { $suppressSites.Add([pscustomobject]@{ File = $rel; Line = $i + 1; Code = $m.Groups['c'].Value }) }
        $m = [regex]::Match($lines[$i], '<NoWarn>(?<c>[^<]+)</NoWarn>')
        if ($m.Success) { foreach ($c in ($m.Groups['c'].Value -split '[;,]')) { if ($c.Trim() -match '^[A-Z]+\d+$') { $suppressSites.Add([pscustomobject]@{ File = $rel; Line = $i + 1; Code = $c.Trim() }) } } }
    }
}
$unlisted = @($suppressSites | Where-Object { $buildRules -notmatch [regex]::Escape($_.Code) -or $buildRules -notmatch [regex]::Escape($_.File) })
Add-Result '抑止の一覧' ($unlisted.Count -eq 0) "付録 A にない抑止 $($unlisted.Count) 件 / 抑止 $($suppressSites.Count) 件"
$unlisted | Select-Object -First 10 | ForEach-Object { "      {0}:{1}  {2}（付録 A へ理由付きで追加する）" -f $_.File, $_.Line, $_.Code }

# 8n) 文書の H1（ドキュメント規約 §2。 対象はリポジトリ直下と src/ 配下の文書）
$h1Issues = [System.Collections.Generic.List[string]]::new()
$docFiles = @(@(Get-ChildItem -Path $repoRoot -File -Filter '*.md') + @(Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -File -Filter '*.md'))
foreach ($f in @($docFiles | Where-Object { $_.FullName -notmatch '\\(obj|bin|archive)\\' })) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    if ($rel -eq 'src/README.md') { continue }
    $h1 = $null
    foreach ($l in [IO.File]::ReadAllLines($f.FullName, $enc)) { if ($l.StartsWith('# ')) { $h1 = $l.Substring(2).Trim(); break } }
    $expected = $null
    if ($rel -eq 'README.md') { $expected = 'SaveEvidence' }
    elseif ($rel -match '^src/(?<p>[^/]+)/docs/SPEC_(?<k>[A-Za-z]+)\.md$') {
        $kind = @{ 'API' = 'API リファレンス'; 'System' = 'システム仕様書'; 'ExtFunc' = '外部機能仕様書' }[$Matches['k']]
        if ($kind) { $expected = "$($Matches['p']) $kind" }
        elseif ($h1 -notlike "$($Matches['p']) *") { $h1Issues.Add(("{0}  H1 [{1}] が「{2} <用途名>」の形でない" -f $rel, $h1, $Matches['p'])) }
        continue
    }
    elseif ($rel -match '^src/(?<p>[^/]+)/sample/README\.md$') { $expected = "$($Matches['p']) サンプルアプリケーション" }
    elseif ($rel -match '^src/(?<p>[^/]+)/README\.md$') { $expected = $Matches['p'] }
    if ($null -eq $expected) { continue }
    if ($null -eq $h1) { $h1Issues.Add(("{0}  H1 がない" -f $rel)) }
    elseif ($h1 -ne $expected) { $h1Issues.Add(("{0}  H1 [{1}] / 期待 [{2}]" -f $rel, $h1, $expected)) }
}
Add-Result '文書の H1' ($h1Issues.Count -eq 0) "$($h1Issues.Count) 件"
$h1Issues | Select-Object -First 10 | ForEach-Object { "      $_" }

# 8o) 禁止した識別子（命名規約: 略語・短縮語 / 座標系の禁止語）
#     宣言された名前（型・メンバー）のセグメントが禁止語と一致しないことを見る。
#     フレームワークが定める名前（MewUI の OnPaintSurface 等）は許容する。
$forbiddenSegments = @('Msg', 'Btn', 'Ctx', 'Svc', 'Impl', 'Mgr', 'Tmp', 'Legacy', 'Deprecated', 'Domain', 'Surface')
$frameworkNames = @('OnPaintSurface')
$rxSplit = [regex]'(?<=[a-z0-9])(?=[A-Z])|_'
$rxDeclPatterns = @(
    [regex]'(?<![\w.])(?:class|struct|interface|enum)\s+(?<n>[A-Za-z_]\w*)',
    [regex]'(?<![\w.])(?:record(?:\s+(?:class|struct))?)\s+(?<n>[A-Za-z_]\w*)',
    [regex]'(?m)^\s{4}(?:public|internal|private|protected)\s+(?:static\s+|readonly\s+|sealed\s+|override\s+|virtual\s+|async\s+|partial\s+|unsafe\s+|new\s+|required\s+|volatile\s+)*[\w<>,\[\]\?\.]+\s+(?<n>[A-Za-z_]\w*)\s*(?:\(|\{|=>|;|=)'
)
$identIssues = [System.Collections.Generic.List[string]]::new()
foreach ($f in $allCs) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*(//|///|\*)') { continue }
        foreach ($rx in $rxDeclPatterns) {
            foreach ($m in $rx.Matches($lines[$i])) {
                $name = $m.Groups['n'].Value
                if ($frameworkNames -contains $name) { continue }
                foreach ($seg in ($rxSplit.Split($name))) {
                    if ($forbiddenSegments -contains $seg) {
                        $identIssues.Add(("{0}:{1}  [{2} in {3}]（略語・座標系の禁止語。 命名規約）" -f $rel, ($i + 1), $seg, $name))
                    }
                }
            }
        }
    }
}
Add-Result '禁止した識別子' ($identIssues.Count -eq 0) "略語・禁止語 $($identIssues.Count) 件"
$identIssues | Select-Object -First 10 | ForEach-Object { "      $_" }

# 8p) 例外の扱い（例外処理規約 §4: 捕捉したら記録する / 空の catch を作らない）
#     記録とは「ログ・Debug/Trace 出力・理由コメント」のいずれかである。
$catchIssues = [System.Collections.Generic.List[string]]::new()
foreach ($f in $allCs) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*(//|///|\*)') { continue }
        if ($lines[$i] -notmatch '\bcatch\b') { continue }
        # catch のブロックを集める
        $depth = 0
        $start = $i
        $j = $i
        while ($j -lt $lines.Count -and $lines[$j] -notmatch '\{') { $j++ }
        $block = [System.Collections.Generic.List[string]]::new()
        for ($k = $j; $k -lt $lines.Count; $k++) {
            $block.Add($lines[$k])
            $depth += ([regex]::Matches($lines[$k], '\{')).Count - ([regex]::Matches($lines[$k], '\}')).Count
            if ($depth -le 0) { break }
        }
        $body = $block -join "`n"
        # 波かっこの中身（`{}` の中が空かどうか）
        $inner = [regex]::Match($body, '\{(?<inner>.*)\}', [Text.RegularExpressions.RegexOptions]::Singleline).Groups['inner'].Value
        $inner = [regex]::Replace($inner, '(?m)^\s*//.*$', '')   # コメント行を除く
        if ($inner.Trim().Length -eq 0 -and $body -notmatch '//') {
            $catchIssues.Add(("{0}:{1}  空の catch（理由コメントを書くか、記録する）" -f $rel, ($start + 1)))
            continue
        }
        if ($body -notmatch '\.Log[A-Z]|Log[A-Z]\w*\(|Debug\.WriteLine|Trace\.WriteLine|Console\.' -and $body -notmatch '//') {
            $catchIssues.Add(("{0}:{1}  捕捉した例外を記録していない（ログ・Debug 出力・理由コメントのいずれかを書く）" -f $rel, ($start + 1)))
        }
    }
}
Add-Result '例外の記録' ($catchIssues.Count -eq 0) "記録のない catch $($catchIssues.Count) 件"
$catchIssues | Select-Object -First 10 | ForEach-Object { "      $_" }

# 8q) イベント ハンドラーの登録は XAML とコードのどちらか一方（コーディング規約 §7）
$wiringIssues = [System.Collections.Generic.List[string]]::new()
foreach ($projDir in (Get-ChildItem -Path (Join-Path $repoRoot 'src') -Directory)) {
    $axamlFiles = @(Get-ChildItem -Path $projDir.FullName -Recurse -File -Filter '*.axaml' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })
    if ($axamlFiles.Count -eq 0) { continue }
    $csFiles = @(Get-ChildItem -Path $projDir.FullName -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })
    $csText = ($csFiles | ForEach-Object { [IO.File]::ReadAllText($_.FullName, $enc) }) -join "`n"
    $handlerNames = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($x in $axamlFiles) {
        foreach ($m in [regex]::Matches([IO.File]::ReadAllText($x.FullName, $enc), '="(?<n>On[A-Z]\w+)"')) { [void]$handlerNames.Add($m.Groups['n'].Value) }
    }
    foreach ($n in $handlerNames) {
        if ($csText -match ('\+=\s*' + [regex]::Escape($n) + '\b')) {
            $wiringIssues.Add(("{0}  {1} を XAML とコードの両方で登録している（どちらか一方に統一する）" -f $projDir.Name, $n))
        }
    }
}
Add-Result 'ハンドラーの配線' ($wiringIssues.Count -eq 0) "$($wiringIssues.Count) 件"
$wiringIssues | Select-Object -First 10 | ForEach-Object { "      $_" }

# 8r) README の必須節（ドキュメント規約 §3。 番号は spec の体裁検査が担保する）
#     必須: 概要（または目的）/ 使い方（クイックスタート・実行・インストール）/ テスト（または検証）/
#           ドキュメント（または関連ドキュメント）/ ライセンス（末尾）
$readmeRequired = @(
    [pscustomobject]@{ Name = '概要または目的'; Pattern = '^(概要|目的)$' },
    [pscustomobject]@{ Name = '使い方'; Pattern = '(クイックスタート|実行|インストール|使い方|主な API|使用例)' },
    [pscustomobject]@{ Name = 'テストまたは検証'; Pattern = '(テスト|検証)' },
    [pscustomobject]@{ Name = 'ドキュメント'; Pattern = '(ドキュメント|SPEC_)' },
    [pscustomobject]@{ Name = 'ライセンス'; Pattern = 'ライセンス' }
)
$readmeIssues = [System.Collections.Generic.List[string]]::new()
foreach ($f in @(Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -File -Filter 'README.md' |
        Where-Object { $_.FullName -notmatch '\\(obj|bin|sample)\\' })) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    # `src/README.md` は層構成の文書（ドキュメント規約 §2 の例外）
    if ($rel -eq 'src/README.md') { continue }
    $headings = @([IO.File]::ReadAllLines($f.FullName, $enc) |
        Where-Object { $_ -match '^#{2,3} ' } |
        ForEach-Object { ($_ -replace '^#{2,3}\s*', '') -replace '^\d+(\.\d+)*\.?\s*', '' })
    foreach ($req in $readmeRequired) {
        if (@($headings | Where-Object { $_ -match $req.Pattern }).Count -eq 0) {
            $readmeIssues.Add(("{0}  「{1}」の節がない（ドキュメント規約 §3）" -f $rel, $req.Name))
        }
    }
    # ライセンスは末尾に置く
    if ($headings.Count -gt 0 -and $headings[$headings.Count - 1] -notmatch 'ライセンス') {
        $readmeIssues.Add(("{0}  ライセンスの節が末尾でない（末尾は「{1}」）" -f $rel, $headings[$headings.Count - 1]))
    }
}
Add-Result 'README の必須節' ($readmeIssues.Count -eq 0) "$($readmeIssues.Count) 件"
$readmeIssues | Select-Object -First 10 | ForEach-Object { "      $_" }

# 8s) SPEC_System の必須節（ドキュメント規約 §6。 節の名前は同義語を許す）
$sysRequired = @(
    [pscustomobject]@{ Name = '概要'; Pattern = '(概要|目的)' },
    [pscustomobject]@{ Name = '技術スタック'; Pattern = '(技術スタック|依存パッケージ)' },
    [pscustomobject]@{ Name = 'アーキテクチャ'; Pattern = '(アーキテクチャ|構成図)' },
    [pscustomobject]@{ Name = 'コンポーネント'; Pattern = '(コンポーネント|クラス図|モジュール)' },
    [pscustomobject]@{ Name = '依存関係'; Pattern = '(依存|参照)' },
    [pscustomobject]@{ Name = '例外処理ポリシー'; Pattern = '(例外|エラー)' }
)
$sysIssues = [System.Collections.Generic.List[string]]::new()
foreach ($f in @(Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -File -Filter 'SPEC_System.md' |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $headings = @([IO.File]::ReadAllLines($f.FullName, $enc) |
        Where-Object { $_ -match '^#{2,3} ' } |
        ForEach-Object { ($_ -replace '^#{2,3}\s*', '') -replace '^\d+(\.\d+)*\.?\s*', '' })
    foreach ($req in $sysRequired) {
        if (@($headings | Where-Object { $_ -match $req.Pattern }).Count -eq 0) {
            $sysIssues.Add(("{0}  「{1}」の節がない（ドキュメント規約 §6）" -f $rel, $req.Name))
        }
    }
}
Add-Result 'SPEC_System の必須節' ($sysIssues.Count -eq 0) "$($sysIssues.Count) 件"
$sysIssues | Select-Object -First 10 | ForEach-Object { "      $_" }

# 8t) テストの `{対象}` の実在（テスト規約 §2。 削除・改名した対象のテストを残さない）
#     `{対象}` は実在のソース（ファイル名または型名）と一致するか、その名前で始まる（`<対象><観点>` を許す）。
$rxTestFileName = [regex]'(?<target>.+)\.Tests\.cs$'
$testTargetIssues = [System.Collections.Generic.List[string]]::new()
foreach ($projDir in (Get-ChildItem -Path (Join-Path $repoRoot 'src') -Directory)) {
    $srcDir = Join-Path $projDir.FullName 'src'
    if (-not (Test-Path -LiteralPath $srcDir)) { continue }
    # ソースの型名・ファイル名の集合
    $srcNames = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($cs in (Get-ChildItem -Path $srcDir -Recurse -File -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })) {
        $name = [IO.Path]::GetFileNameWithoutExtension($cs.Name)
        foreach ($part in $name.Split('.')) { [void]$srcNames.Add($part) }
        foreach ($m in [regex]::Matches([IO.File]::ReadAllText($cs.FullName, $enc), '(?m)^\s*(?:(?:public|internal|private|protected|file)\s+)*(?:static\s+|sealed\s+|abstract\s+|partial\s+|readonly\s+|ref\s+)*(?:class|struct|interface|enum|record(?:\s+(?:class|struct))?)\s+([A-Za-z_]\w*)')) {
            [void]$srcNames.Add($m.Groups[1].Value)
        }
    }
    foreach ($t in (Get-ChildItem -Path (Join-Path $projDir.FullName 'tests') -Recurse -File -Filter '*.Tests.cs' -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })) {
        $rel = $t.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
        $m = $rxTestFileName.Match($t.Name)
        if (-not $m.Success) { continue }
        $target = $m.Groups['target'].Value
        # 対象の先頭の語（`CaptureTargetContract` → `Capture`）。 `<対象><観点>` と、複数の型をまたぐ検証を許す。
        $first = ([regex]::Match($target, '^([A-Z][a-z0-9]*|[A-Z]+)')).Value
        $matched = $false
        foreach ($n in $srcNames) {
            if ($target -eq $n -or
                $target.StartsWith($n, [StringComparison]::Ordinal) -or
                $n.StartsWith($target, [StringComparison]::Ordinal) -or
                ($first.Length -gt 0 -and $n.StartsWith($first, [StringComparison]::Ordinal))) { $matched = $true; break }
        }
        if (-not $matched) { $testTargetIssues.Add(("{0}  `{{対象}}` [{1}] に対応するソースがない" -f $rel, $target)) }
    }
}
Add-Result 'テストの対象の実在' ($testTargetIssues.Count -eq 0) "$($testTargetIssues.Count) 件"
$testTargetIssues | Select-Object -First 10 | ForEach-Object { "      $_" }

# 8u) SPEC_API の internal 型には印を付ける（ドキュメント規約 §4）
#     型の節（`## N. `Type`` / `### N.M `Type``）の型がソースで `internal` なら、見出しに「（内部」が要る。
$rxTypeHeading = [regex]'^#{2,3} \d+(?:\.\d+)*\.?\s*(?:`(?<t>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)`|(?<t2>[A-Za-z_]\w*))'
$internalIssues = [System.Collections.Generic.List[string]]::new()
foreach ($f in @(Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -File -Filter 'SPEC_API.md' |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $projectDir = (Get-Item $f.FullName).Directory.Parent.FullName
    $srcDir = Join-Path $projectDir 'src'
    if (-not (Test-Path -LiteralPath $srcDir)) { continue }
    # そのプロジェクトの internal 型の集合
    $internalTypes = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($cs in (Get-ChildItem -Path $srcDir -Recurse -File -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })) {
        foreach ($m in [regex]::Matches([IO.File]::ReadAllText($cs.FullName, $enc), '(?m)^\s*internal\s+(?:static\s+|sealed\s+|abstract\s+|partial\s+|readonly\s+|ref\s+)*(?:class|struct|interface|enum|record(?:\s+(?:class|struct))?)\s+([A-Za-z_]\w*)')) {
            [void]$internalTypes.Add($m.Groups[1].Value)
        }
    }
    foreach ($line in [IO.File]::ReadAllLines($f.FullName, $enc)) {
        $m = $rxTypeHeading.Match($line)
        if (-not $m.Success) { continue }
        $name = if ($m.Groups['t'].Success) { $m.Groups['t'].Value } else { $m.Groups['t2'].Value }
        $last = ($name -split '\.')[-1]
        if (-not $internalTypes.Contains($last)) { continue }
        # 印は「（内部」または「`internal`」のいずれか（公開面と取り違えないための明示）
        if ($line -notmatch '（内部' -and $line -notmatch '`internal`') { $internalIssues.Add(("{0}  internal 型 `{1}` の節に「（内部）」の印がない" -f $rel, $last)) }
    }
}
Add-Result 'SPEC_API の internal 印' ($internalIssues.Count -eq 0) "$($internalIssues.Count) 件"
$internalIssues | Select-Object -First 10 | ForEach-Object { "      $_" }

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
# 孤立した bin/obj は「親に csproj が無い」ことで判定する（親を毎回列挙せず、csproj を持つフォルダーの集合と照合する）
$csprojDirs = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($csproj in $allCsproj) { [void]$csprojDirs.Add([IO.Path]::GetDirectoryName($csproj.FullName)) }
$orphanBins = @(Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -Directory |
        Where-Object { $_.Name -in @('bin', 'obj') } |
        Where-Object { -not $csprojDirs.Contains([IO.Path]::GetDirectoryName($_.FullName)) })
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

# 10d) XAML の名前空間宣言（宣言した `xmlns:接頭辞` が使われているか）
#      XAML の名前空間宣言は、その接頭辞を書かなくなった時点で不要になる（エディタは
#      「名前空間宣言が不要」と指摘するが、ビルドは通るため機械検査が要る）。
#      宣言そのもの（`xmlns:接頭辞="..."`）を取り除いた残りで `接頭辞:` が使われているかを見る。
$xamlNamespaceIssues = [System.Collections.Generic.List[string]]::new()
foreach ($f in (Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -File -Filter '*.axaml' |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' })) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    $prefixes = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $lines) {
        foreach ($m in [regex]::Matches($line, 'xmlns:([A-Za-z_]\w*)\s*=')) {
            if (-not $prefixes.Contains($m.Groups[1].Value)) { $prefixes.Add($m.Groups[1].Value) }
        }
    }
    foreach ($prefix in $prefixes) {
        $rxDeclaration = [regex]("xmlns:$([regex]::Escape($prefix))\s*=\s*""[^""]*""")
        $rxUse = [regex]("(?<![\w:])$([regex]::Escape($prefix)):")
        $used = $false
        foreach ($line in $lines) {
            if ($rxUse.IsMatch($rxDeclaration.Replace($line, ''))) { $used = $true; break }
        }
        if (-not $used) { $xamlNamespaceIssues.Add(("{0}  未使用の名前空間宣言: {1}" -f $rel, $prefix)) }
    }
}
Add-Result 'XAML の名前空間' ($xamlNamespaceIssues.Count -eq 0) "未使用の宣言 $($xamlNamespaceIssues.Count) 件"
$xamlNamespaceIssues | Select-Object -First 10 | ForEach-Object { "      $_" }

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
# 11. コードスタイル（dotnet format。 エラー・警告を 0 件にする）
# ============================================================
if (-not $SkipFormat) {
    Write-Output '== 11. コードスタイル（dotnet format） =='
    if ($Fix) {
        & dotnet format whitespace $Solution --no-restore 2>&1 | Out-Null
        & dotnet format style $Solution --severity warn --no-restore 2>&1 | Out-Null
        & dotnet format analyzers $Solution --severity warn --no-restore 2>&1 | Out-Null
    }

    # 検査はビルドの直後に背景へ投入済み（-Fix のときは修正の後で実行する）
    if ($backgroundFormat) {
        $received = Receive-BackgroundDotNet $backgroundFormat
        $fmtText = $received.Text
        $fmtExit = $received.ExitCode
    }
    else {
        $fmtText = & dotnet format style $Solution --verify-no-changes --severity warn --no-restore 2>&1 | Out-String
        $fmtExit = $LASTEXITCODE
    }
    $fmtIssues = ([regex]::Matches($fmtText, ': (error|warning) (IDE|CA)\d+')).Count
    # 合否は「警告以上」の件数で判定する（引数の `--severity warn` と同じ基準。 情報レベルの提案は
    # ビルドが強制する範囲ではなく、改善の必要がない状態でも返るため合否に含めない）。
    # error も数えるのは**命名規則（IDE1006）がビルドでは報告されない**ためである
    # （実測: `private static readonly` を PascalCase で書いた 12 件が error IDE1006 で出ており、
    #   ビルドは 0 警告だった。 命名規則の実効的な強制はこの検査だけが担う）。
    # 実行自体の失敗（引数の誤り・復元の失敗等）は 2 以外の終了コードになるため、それだけを不合格にする。
    $fmtRunFailed = $fmtExit -gt 2
    Add-Result 'dotnet format' ($fmtIssues -eq 0 -and -not $fmtRunFailed) "警告以上 $fmtIssues 件"
    if ($fmtIssues -gt 0) { $fmtText -split "`r?`n" | Where-Object { $_ -match ': (error|warning) (IDE|CA)\d+' } | Select-Object -First 10 | ForEach-Object { "      $($_.Trim())" } }
    if ($fmtRunFailed) {
        "      実行に失敗した（終了コード $fmtExit）"
        $fmtText -split "`r?`n" | Where-Object { $_.Trim().Length -gt 0 } | Select-Object -First 5 | ForEach-Object { "      $($_.Trim())" }
    }
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
