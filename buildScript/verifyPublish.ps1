<#
.SYNOPSIS
配布物（Native AOT）を作成し、起動と設定の往復を検証する。

.DESCRIPTION
`.github/検証手順.md` §18 が定める「配布物の検証」を実行する。
対象 UI の定義（publish スクリプト・出力フォルダ・配布名）は `.github/release-config.json` が単一所有し、
本スクリプトはそれを読む（UI を追加しても本スクリプトの変更は不要）。

検証の内容:
1. publish（`buildScript/*_publish_aot.bat`）を実行し（`-SkipPublish` で省略）、出力フォルダの exe が配布名と一致することを確認する。
2. 実行ファイルが起動中でないことを確認する（起動中は設定ファイルが競合するため中止する）。
3. ユーザーの設定ファイル（%LOCALAPPDATA% 配下の SaveEvidence フォルダーの settings.json）を退避する。
4. exe を起動し、メイン ウィンドウが現れるのを待って `WM_CLOSE` で正常終了させる。
5. 終了後の設定ファイルが空（`{}`）でなく、起動前と同じキーを保っていることを確認する（Native AOT のトリミングで
   設定が全損する不具合の回帰検出。 検証は [`検証手順.md`](../.github/検証手順.md) §18）。

**設定ファイルは必ず元へ戻す**（`try` / `finally`）。 アプリは終了時にウィンドウ配置などを保存するため、
戻さないとユーザーの実設定が書き換わる。

.PARAMETER Ui
対象の UI（既定は `All`）。 `.github/release-config.json` の `name` と一致させる。

.PARAMETER SkipPublish
publish を省略し、既存の出力フォルダを検証する（CI は publish 済みの成果物を検証する）。

.PARAMETER WindowTimeoutSeconds
メイン ウィンドウが現れるまでの待ち時間（既定 60 秒）。

.PARAMETER ExitTimeoutSeconds
`WM_CLOSE` を送ってからプロセスが終了するまでの待ち時間（既定 60 秒）。

.EXAMPLE
pwsh -NoProfile -File buildScript/verifyPublish.ps1

.EXAMPLE
pwsh -NoProfile -File buildScript/verifyPublish.ps1 -Ui MewUI -SkipPublish
#>
[CmdletBinding()]
param(
    [ValidateSet('All', 'AvaloniaUI', 'MewUI')][string]$Ui = 'All',
    [switch]$SkipPublish,
    [int]$WindowTimeoutSeconds = 60,
    [int]$ExitTimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
$enc = [Text.UTF8Encoding]::new($false)
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

$script:Results = [System.Collections.Generic.List[object]]::new()

function Add-Result([string]$Name, [bool]$Ok, [string]$Detail) {
    $script:Results.Add([pscustomobject]@{ Name = $Name; Ok = $Ok; Detail = $Detail })
    $mark = if ($Ok) { 'OK  ' } else { 'FAIL' }
    Write-Output "$mark  $Name`t$Detail"
}

# メイン ウィンドウへ WM_CLOSE を送るためだけの Win32 呼び出し（強制終了させない）
Add-Type -Namespace SeVerifyPublish -Name Native -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
public static extern bool PostMessage(System.IntPtr hWnd, uint msg, System.IntPtr wParam, System.IntPtr lParam);
'@

# アプリが共有する設定ファイル（両 UI で同じ場所。 App.Core の SettingsService が単一所有する）
# 場所は %LOCALAPPDATA% 配下の SaveEvidence フォルダーの settings.json
$settingsPath = Join-Path (Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'SaveEvidence') 'settings.json'

# 設定ファイルの最上位キー（無い場合は $null、空の JSON は空配列）
function Get-SettingKeys {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseSingularNouns', '', Justification = 'コレクションを返す関数のため複数形にする')]
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    $text = [IO.File]::ReadAllText($Path, $enc)
    if ([string]::IsNullOrWhiteSpace($text)) { return @() }

    $json = $text | ConvertFrom-Json -AsHashtable
    if ($json -isnot [System.Collections.IDictionary]) { return @() }
    return @($json.Keys | Sort-Object)
}

# exe を起動し、ウィンドウが現れてから正常終了させる
function Invoke-AppRoundTrip([string]$ExePath, [int]$WindowTimeout, [int]$ExitTimeout) {
    $process = Start-Process -FilePath $ExePath -PassThru

    $handle = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds($WindowTimeout)
    while ((Get-Date) -lt $deadline) {
        if ($process.HasExited) { break }
        $process.Refresh()
        $handle = $process.MainWindowHandle
        if ($handle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 250
    }

    if ($handle -eq [IntPtr]::Zero) {
        if (-not $process.HasExited) { $process.Kill() }
        return [pscustomobject]@{ Closed = $false; Reason = "メイン ウィンドウが $WindowTimeout 秒以内に現れませんでした" }
    }

    [void][SeVerifyPublish.Native]::PostMessage($handle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)   # WM_CLOSE
    if (-not $process.WaitForExit($ExitTimeout * 1000)) {
        $process.Kill()
        return [pscustomobject]@{ Closed = $false; Reason = "WM_CLOSE 後 $ExitTimeout 秒以内に終了しませんでした" }
    }

    return [pscustomobject]@{ Closed = $true; Reason = '' }
}

# リリース設定（UI の定義）を読む
$config = [IO.File]::ReadAllText((Join-Path $repoRoot '.github/release-config.json'), $enc) | ConvertFrom-Json
$targets = @($config.uis | Where-Object { $Ui -eq 'All' -or $_.name -eq $Ui })
if ($targets.Count -eq 0) {
    Write-Output "FAIL  対象 UI が見つかりません: $Ui"
    exit 1
}

$settingsBackedUp = $false
$settingsBackupPath = Join-Path ([IO.Path]::GetTempPath()) ("se_settings_" + [Guid]::NewGuid().ToString('N') + '.json')

try {
    foreach ($target in $targets) {
        Write-Output "== $($target.name) =="
        $outputDir = Join-Path $repoRoot (($target.output) -replace '/', '\')

        # 1. publish（-SkipPublish のときは既存の出力を使う）
        if (-not $SkipPublish) {
            $scriptPath = ($target.script) -replace '/', '\'
            Write-Output "      publish: $scriptPath"
            $publishText = (& cmd.exe /c "`"$scriptPath`"" 2>&1 | Out-String)
            if ($LASTEXITCODE -ne 0) {
                Add-Result "$($target.name) publish" $false "終了コード $LASTEXITCODE"
                $publishText -split "`r?`n" | Where-Object { $_ -match 'error|ERROR' } | Select-Object -First 5 | ForEach-Object { "      $($_.Trim())" }
                continue
            }
        }

        if (-not (Test-Path -LiteralPath $outputDir -PathType Container)) {
            Add-Result "$($target.name) 配布物" $false "出力フォルダがありません: $($target.output)"
            continue
        }

        # exe が 1 つだけあり、名前が配布名（release-config.json の asset）と一致することを確認する
        $exes = @(Get-ChildItem -LiteralPath $outputDir -File -Filter '*.exe')
        $expectedExeName = [IO.Path]::GetFileNameWithoutExtension($target.asset) + '.exe'
        Add-Result "$($target.name) 配布物" ($exes.Count -eq 1 -and $exes[0].Name -eq $expectedExeName) "$($target.output) の exe $($exes.Count) 個 / 期待 $expectedExeName"
        if ($exes.Count -ne 1) {
            $exes | ForEach-Object { "      $($_.Name)" }
            continue
        }

        $exe = $exes[0]

        # 2. 起動中のプロセスがあると設定ファイルが競合する（検証結果が不定になる）
        $running = @(Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($exe.Name)) -ErrorAction SilentlyContinue)
        if ($running.Count -gt 0) {
            Add-Result "$($target.name) 起動と終了" $false "$($exe.Name) が起動中です。終了してから実行してください"
            continue
        }

        # 3. 設定ファイルを退避する
        $hadSettings = Test-Path -LiteralPath $settingsPath
        if ($hadSettings) {
            Copy-Item -LiteralPath $settingsPath -Destination $settingsBackupPath -Force
            $settingsBackedUp = $true
        }
        $before = Get-SettingKeys $settingsPath

        # 4. 起動と終了（正常終了させる。 強制終了では終了時の保存が走らない）
        $roundTrip = Invoke-AppRoundTrip -ExePath $exe.FullName -WindowTimeout $WindowTimeoutSeconds -ExitTimeout $ExitTimeoutSeconds
        Add-Result "$($target.name) 起動と終了" $roundTrip.Closed $(if ($roundTrip.Closed) { $exe.Name } else { $roundTrip.Reason })

        # 5. 設定の往復（空になっていないこと・キーが保たれていること）
        $after = Get-SettingKeys $settingsPath
        $beforeCount = if ($null -eq $before) { 0 } else { $before.Count }
        $afterCount = if ($null -eq $after) { 0 } else { $after.Count }
        $keysKept = $null -ne $after -and $afterCount -gt 0 -and ($null -eq $before -or (Compare-Object $before $after).Count -eq 0)
        Add-Result "$($target.name) 設定の往復" $keysKept "起動前 $beforeCount キー / 終了後 $afterCount キー"
        if (-not $keysKept) {
            Write-Output '      設定ファイルが空になったか、キーが失われています（Native AOT のトリミングで設定が全損する不具合の疑い）'
        }

        # 6. 設定ファイルを元へ戻す（次の UI の検証もユーザーの実設定から始める）
        if ($settingsBackedUp) {
            Copy-Item -LiteralPath $settingsBackupPath -Destination $settingsPath -Force
            $settingsBackedUp = $false
        }
    }
}
finally {
    # 途中で失敗してもユーザーの設定ファイルを必ず戻す
    if ($settingsBackedUp -and (Test-Path -LiteralPath $settingsBackupPath)) {
        Copy-Item -LiteralPath $settingsBackupPath -Destination $settingsPath -Force
    }
    if (Test-Path -LiteralPath $settingsBackupPath) { Remove-Item -LiteralPath $settingsBackupPath -Force }
}

Write-Output ''
Write-Output '===== 配布物の検証の要約 ====='
foreach ($result in $script:Results) {
    $mark = if ($result.Ok) { 'OK  ' } else { 'FAIL' }
    Write-Output "$mark  $($result.Name)`t$($result.Detail)"
}

$failed = @($script:Results | Where-Object { -not $_.Ok })
if ($failed.Count -gt 0) {
    Write-Output ''
    Write-Output "不合格 $($failed.Count) 項目: $(($failed | ForEach-Object { $_.Name }) -join ', ')"
    exit 1
}

Write-Output ''
Write-Output "すべて合格（合否 $($script:Results.Count) 項目）"
