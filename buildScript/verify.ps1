<#
.SYNOPSIS
リポジトリの検証をすべて実行し、合否を要約する。

.DESCRIPTION
`.github/検証手順.md` が定める検証を順に実行し、各項目の合否と件数を表示する（不合格があれば終了コード 1）。
Copilot・CI・開発者が同じ手順を 1 コマンドで再現できるようにする（手作業の再実装をなくす）。

.PARAMETER Fix
自動修正できる項目を修正する（markdownlint --fix / dotnet format）。

.PARAMETER SkipTests
テストの実行を省略する（ビルドだけを素早く確かめるとき）。

.PARAMETER SkipFormat
dotnet format による検査を省略する（時間を短縮するとき）。

.EXAMPLE
pwsh -NoProfile -File buildScript/verify.ps1
.EXAMPLE
pwsh -NoProfile -File buildScript/verify.ps1 -Fix
#>
[CmdletBinding()]
param(
    [switch]$Fix,
    [switch]$SkipTests,
    [switch]$SkipFormat
)

$ErrorActionPreference = 'Stop'
$enc = [Text.UTF8Encoding]::new($false)
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

$script:Results = [System.Collections.Generic.List[object]]::new()

function Add-Result([string]$Name, [bool]$Ok, [string]$Detail) {
    $script:Results.Add([pscustomobject]@{ Name = $Name; Ok = $Ok; Detail = $Detail })
    $mark = if ($Ok) { 'OK  ' } else { 'FAIL' }
    Write-Host "$mark  $Name`t$Detail"
}

function Invoke-DotNet([string[]]$Arguments) {
    $out = & dotnet @Arguments 2>&1 | Out-String
    return $out
}

# ============================================================
# 1. ビルド（0 エラー / 0 警告。 IDE0005 とドキュメントコメントを同時に検証する）
# ============================================================
Write-Host '== 1. ビルド =='
$buildText = Invoke-DotNet @('build', 'SaveEvidence.slnx', '-tl:off', '-v:q', '--no-incremental')
$buildWarnings = ([regex]::Matches($buildText, 'warning ')).Count
$buildErrors = ([regex]::Matches($buildText, ': error ')).Count
Add-Result 'ビルド' ($buildWarnings -eq 0 -and $buildErrors -eq 0) "警告 $buildWarnings / エラー $buildErrors"
if ($buildWarnings -gt 0 -or $buildErrors -gt 0) {
    $buildText -split "`r?`n" | Where-Object { $_ -match 'warning |: error ' } | Select-Object -First 20 | ForEach-Object { "      $_.Trim()" }
}

# ============================================================
# 2. テスト（全件合格）
# ============================================================
if (-not $SkipTests) {
    Write-Host '== 2. テスト =='
    $testText = Invoke-DotNet @('test', 'SaveEvidence.slnx', '-tl:off', '--no-build')
    $passed = 0
    foreach ($m in [regex]::Matches($testText, '合格:\s*(\d+)')) { $passed += [int]$m.Groups[1].Value }
    $failed = 0
    foreach ($m in [regex]::Matches($testText, '失敗:\s*(\d+)')) { $failed += [int]$m.Groups[1].Value }
    $projects = [regex]::Matches($testText, '\.dll \(net10\.0[^)]*\)').Count
    Add-Result 'テスト' ($failed -eq 0 -and $passed -gt 0) "合格 $passed / 失敗 $failed / $projects プロジェクト"
}

# ============================================================
# 3. ドキュメント（markdownlint）
# ============================================================
Write-Host '== 3. ドキュメント（markdownlint） =='
$mdArgs = @('--yes', 'markdownlint-cli2', '--config', '.markdownlint.json')
if ($Fix) { $mdArgs += '--fix' }
$mdArgs += @('src/**/*.md', '.github/**/*.md', '*.md', '!**/obj/**', '!**/bin/**')
$mdText = & npx @mdArgs 2>&1 | Out-String
$mdIssues = 0
if ($mdText -match 'Summary:\s*(\d+) issues? in (\d+) files?') { $mdIssues = [int]$Matches[1] }
Add-Result 'markdownlint' ($mdIssues -eq 0) "$mdIssues 件"
if ($mdIssues -gt 0) { $mdText -split "`r?`n" | Where-Object { $_ -match 'error|warning' } | Select-Object -First 10 | ForEach-Object { "      $($_.Trim())" } }

# 以降のドキュメント検査で使う文書一覧
$docs = @(Get-ChildItem -Path (Join-Path $repoRoot 'src'), (Join-Path $repoRoot '.github') -Recurse -File -Filter '*.md' |
        Where-Object { $_.FullName -notmatch '\\(obj|bin|archive)\\' })

# ============================================================
# 4. 相対リンク（存在しないファイルへの参照）
# ============================================================
Write-Host '== 4. 相対リンク =='
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
# 5. 名前空間（フォルダへの追従）
# ============================================================
Write-Host '== 5. 名前空間 =='
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
# 6. 文書が引用する名前空間・ファイル（実在の確認）
# ============================================================
Write-Host '== 6. 文書の引用（実在の確認） =='
$tracked = @{}
foreach ($f in (git ls-files)) { $tracked[$f] = $true }
$trackedNames = @{}
foreach ($f in (git ls-files)) { $trackedNames[[IO.Path]::GetFileName($f)] = $true }
$projectNames = @(Get-ChildItem -Path (Join-Path $repoRoot 'src') -Directory | Select-Object -ExpandProperty Name)
# プロジェクト内のフォルダ（部分的な構成図のルートとして許可する）
$intraFolders = @('src', 'tests', 'sample', 'docs')
# 6a) 文書の裸の相対パス（プロジェクト名から始まっていないもの）
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
            if ($cands.Count -eq 0) { $loosePaths.Add("解決不能 ${rel}:$($i + 1)  $tok") }
            elseif ($cands.Count -eq 1) { $loosePaths.Add("要統一 ${rel}:$($i + 1)  $tok -> $($cands[0])") }
        }
    }
}

# 6b) プロジェクト構成図のルートとファイル名
$treeIssues = [System.Collections.Generic.List[string]]::new()
$rxTreeFile = [regex]'(?<![\w./\\-])([A-Za-z0-9_.\-]+\.(?:csproj|axaml|cs))(?![\w])'
foreach ($f in $docs) {
    $rel = $f.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $ownProj = ($rel -split '/')[1]
    $lines = [IO.File]::ReadAllLines($f.FullName, $enc)
    $inTree = $false
    $treeRootSeen = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line.StartsWith('```')) {
            if ($inTree) { $inTree = $false } elseif ($line.StartsWith('```text')) { $inTree = $true; $treeRootSeen = $false }
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
                    $treeIssues.Add("ルート名 ${rel}:$($i + 1)  $label")
                }
            }
        }
        foreach ($m in $rxTreeFile.Matches($line)) {
            $n = $m.Groups[1].Value
            if (-not $trackedNames.ContainsKey($n)) { $treeIssues.Add("ファイル名 ${rel}:$($i + 1)  $n") }
        }
    }
}

# 6c) 陳腐化した識別子（改名・削除の残骸。 対象を追加して育てる）
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

$quoteIssues = $loosePaths.Count + $treeIssues.Count + $stale.Count
Add-Result '文書の引用' ($quoteIssues -eq 0) "パス $($loosePaths.Count) / 構成図 $($treeIssues.Count) / 陳腐化 $($stale.Count)"
$loosePaths | Select-Object -First 8 | ForEach-Object { "      $_" }
$treeIssues | Select-Object -First 8 | ForEach-Object { "      $_" }
$stale | Select-Object -First 8 | ForEach-Object { "      $_" }

# ============================================================
# 7. 改行コード
# ============================================================
Write-Host '== 7. 改行コード =='
$eolIssues = @(git ls-files --eol | Select-String 'w/mixed|w/lf')
Add-Result '改行コード' (@($eolIssues).Count -eq 0) "$(@($eolIssues).Count) 件"
$eolIssues | Select-Object -First 5 | ForEach-Object { "      $($_.Line)" }

# ============================================================
# 8. フォルダ（空フォルダ / 孤立した bin・obj）
# ============================================================
Write-Host '== 8. フォルダ =='
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
# 9. コードスタイル（dotnet format）
# ============================================================
if (-not $SkipFormat) {
    Write-Host '== 9. コードスタイル（dotnet format） =='
    if ($Fix) {
        & dotnet format whitespace SaveEvidence.slnx --no-restore 2>&1 | Out-Null
        & dotnet format style SaveEvidence.slnx --severity warn --no-restore 2>&1 | Out-Null
        & dotnet format analyzers SaveEvidence.slnx --severity warn --no-restore 2>&1 | Out-Null
    }
    $fmtText = & dotnet format style SaveEvidence.slnx --verify-no-changes --severity warn --no-restore 2>&1 | Out-String
    $fmtIssues = ([regex]::Matches($fmtText, 'warning (IDE|CA)\d+')).Count
    Add-Result 'dotnet format' ($fmtIssues -eq 0) "$fmtIssues 件"
    if ($fmtIssues -gt 0) { $fmtText -split "`r?`n" | Where-Object { $_ -match 'warning ' } | Select-Object -First 10 | ForEach-Object { "      $($_.Trim())" } }
}

# ============================================================
# 要約
# ============================================================
$failed = @($script:Results | Where-Object { -not $_.Ok })
Write-Host ''
Write-Host '===== 検証の要約 ====='
foreach ($r in $script:Results) { Write-Host ("{0}  {1}`t{2}" -f $(if ($r.Ok) { 'OK  ' } else { 'FAIL' }), $r.Name, $r.Detail) }
Write-Host ''
if ($failed.Count -eq 0) {
    Write-Host "すべて合格（$($script:Results.Count) 項目）"
    exit 0
}
Write-Host "不合格 $($failed.Count) 項目: $((($failed | ForEach-Object { $_.Name }) -join ', '))"
exit 1
