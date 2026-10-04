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
Write-Output '== 1. ビルド =='
$buildArgs = @('build', $Solution, '-c', $Configuration, '-tl:off', '-v:q', '--no-incremental') + $restoreArgs
$buildText = Invoke-DotNet $buildArgs
$buildWarnings = ([regex]::Matches($buildText, 'warning ')).Count
$buildErrors = ([regex]::Matches($buildText, ': error ')).Count
Add-Result 'ビルド' ($buildWarnings -eq 0 -and $buildErrors -eq 0) "警告 $buildWarnings / エラー $buildErrors"
if ($buildWarnings -gt 0 -or $buildErrors -gt 0) {
    $buildText -split "`r?`n" | Where-Object { $_ -match 'warning |: error ' } | Select-Object -First 20 | ForEach-Object { "      $($_.Trim())" }
}

# ============================================================
# 2. テスト（全件合格。 -Coverage でカバレッジも集計する）
# ============================================================
if (-not $SkipTests) {
    Write-Output '== 2. テスト =='
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
# 3. NuGet の健全性（脆弱性・非推奨。 推移的依存も含む）
# ============================================================
if (-not $SkipPackage) {
    Write-Output '== 3. NuGet の健全性 =='
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
}

# 以降のドキュメント検査で使う文書一覧
$docs = @(Get-ChildItem -Path (Join-Path $repoRoot 'src'), (Join-Path $repoRoot '.github') -Recurse -File -Filter '*.md' |
        Where-Object { $_.FullName -notmatch '\\(obj|bin|archive)\\' })

# ============================================================
# 4. ドキュメント（markdownlint）
# ============================================================
Write-Output '== 4. ドキュメント（markdownlint） =='
$mdArgs = @('--yes', 'markdownlint-cli2', '--config', '.markdownlint.json')
if ($Fix) { $mdArgs += '--fix' }
$mdArgs += @('src/**/*.md', '.github/**/*.md', '*.md', '!**/obj/**', '!**/bin/**')
$mdText = & npx @mdArgs 2>&1 | Out-String
$mdIssues = 0
if ($mdText -match 'Summary:\s*(\d+) issues? in (\d+) files?') { $mdIssues = [int]$Matches[1] }
Add-Result 'markdownlint' ($mdIssues -eq 0) "$mdIssues 件"
if ($mdIssues -gt 0) { $mdText -split "`r?`n" | Where-Object { $_ -match 'error|warning' } | Select-Object -First 10 | ForEach-Object { "      $($_.Trim())" } }

# ============================================================
# 5. 相対リンク（存在しないファイルへの参照）
# ============================================================
Write-Output '== 5. 相対リンク =='
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
# 6. 名前空間（フォルダへの追従）
# ============================================================
Write-Output '== 6. 名前空間 =='
$nsMismatches = [System.Collections.Generic.List[string]]::new()
$checkedFiles = 0
Get-ChildItem -Path (Join-Path $repoRoot 'src') -Directory | ForEach-Object {
    $srcDir = Join-Path $_.FullName 'src'
    $csproj = Join-Path $srcDir "$($_.Name).csproj"
    if (-not (Test-Path -LiteralPath $csproj)) { return }
    $t = [IO.File]::ReadAllText($csproj, $enc)
    $rootNs = if ($t -match '<RootNamespace>([^<]+)</RootNamespace>') { $Matches[1] } else { $_.Name }
    Get-ChildItem -Path $srcDir -Recurse -File -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } | ForEach-Object {
            $script:checkedFiles++
            $rel = $_.FullName.Substring($srcDir.Length + 1)
            $dir = [IO.Path]::GetDirectoryName($rel)
            $expected = if ([string]::IsNullOrEmpty($dir)) { $rootNs } else { $rootNs + '.' + ($dir -replace '\\', '.') }
            $m = [regex]::Match([IO.File]::ReadAllText($_.FullName, $enc), '(?m)^namespace\s+([^;{]+)')
            $actual = if ($m.Success) { $m.Groups[1].Value.Trim() } else { '(宣言なし)' }
            if ($actual -ne $expected) { $nsMismatches.Add("$rel  期待=$expected  実際=$actual") }
        }
}
Add-Result '名前空間' ($nsMismatches.Count -eq 0) "$checkedFiles ファイル / 不一致 $($nsMismatches.Count)"
$nsMismatches | Select-Object -First 10 | ForEach-Object { "      $_" }

# ============================================================
# 7. 文書が引用する名前空間・ファイル（実在の確認）
# ============================================================
Write-Output '== 7. 文書の引用（実在の確認） =='
$tracked = @{}
$trackedNames = @{}
foreach ($f in (git ls-files)) {
    $tracked[$f] = $true
    $trackedNames[[IO.Path]::GetFileName($f)] = $true
}
$projectNames = @(Get-ChildItem -Path (Join-Path $repoRoot 'src') -Directory | Select-Object -ExpandProperty Name)
# プロジェクト内のフォルダ（部分的な構成図のルートとして許可する）
$intraFolders = @('src', 'tests', 'sample', 'docs')

# 7a) 文書の裸の相対パス（プロジェクト名から始まっていないもの）
$rxQuotedPath = [regex]'(?<![\w./\\-])((?:[A-Za-z0-9_.\-]+[\\/])+[A-Za-z0-9_.\-]+\.(?:csproj|axaml|props|slnx|json|yaml|yml|ps1|bat|cs))(?![\w])'
$loosePaths = [System.Collections.Generic.List[string]]::new()
foreach ($f in $docs) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $docDir = ([IO.Path]::GetDirectoryName($rel) + '/').Replace('\', '/')
    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $rxLink.Replace($lines[$i], ']()')
        foreach ($m in $rxQuotedPath.Matches($line)) {
            $tok = $m.Groups[1].Value
            if ($tok -match '^https?') { continue }
            if ($tok.StartsWith('..')) { continue }
            $norm = $tok.Replace('\', '/')
            if ($tracked.ContainsKey($norm) -or $tracked.ContainsKey($docDir + $norm)) { continue }
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

# 7b) プロジェクト構成図のルートとファイル名
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

# 7c) 陳腐化した識別子（改名・削除の残骸。 対象を追加して育てる）
$stalePatterns = @(
    'App\.LineDetection',
    'EsUtil\.SaveEvidence\.LineDetection',
    'verify-ui',
    'MarqueeCore\.ResolveContent',
    'MarqueePresenter\.ResolveContent'
)
$stale = [System.Collections.Generic.List[string]]::new()
foreach ($p in $stalePatterns) {
    foreach ($hit in @(git -c core.quotepath=false grep -n -E -- $p 'src' '.github' 2>$null)) {
        if ($hit -match 'archive/') { continue }
        # 検証手順は本検査の説明にこれらの名前を引用するため対象外にする
        if ($hit -match '検証手順\.md') { continue }
        $stale.Add("$p : $hit")
    }
}

# 7d) 節参照の破損（文書.md の直後に「§」が無い数字が続く形。 一括置換で § が消えた残骸）
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

# ============================================================
# 8. 改行コード
# ============================================================
Write-Output '== 8. 改行コード =='
$eolIssues = @(git ls-files --eol | Select-String 'w/mixed|w/lf')
Add-Result '改行コード' (@($eolIssues).Count -eq 0) "$(@($eolIssues).Count) 件"
$eolIssues | Select-Object -First 5 | ForEach-Object { "      $($_.Line)" }

# ============================================================
# 9. フォルダ（空フォルダ / 孤立した bin・obj）
# ============================================================
Write-Output '== 9. フォルダ =='
$emptyDirs = @(Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -Directory |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
        Where-Object { @(Get-ChildItem -LiteralPath $_.FullName -Force).Count -eq 0 })
$orphanBins = @(Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -Directory |
        Where-Object { $_.Name -in @('bin', 'obj') } |
        Where-Object { @(Get-ChildItem -LiteralPath (Split-Path -Parent $_.FullName) -Filter '*.csproj').Count -eq 0 })
Add-Result 'フォルダ' ($emptyDirs.Count -eq 0 -and $orphanBins.Count -eq 0) "空 $($emptyDirs.Count) / 孤立 bin・obj $($orphanBins.Count)"
$emptyDirs | Select-Object -First 5 | ForEach-Object { "      $($_.FullName.Substring($repoRoot.Length + 1))" }
$orphanBins | Select-Object -First 5 | ForEach-Object { "      $($_.FullName.Substring($repoRoot.Length + 1))" }

# ============================================================
# 10. コードスタイル（dotnet format。 情報レベルまで含めて 0 件にする）
# ============================================================
if (-not $SkipFormat) {
    Write-Output '== 10. コードスタイル（dotnet format） =='
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
