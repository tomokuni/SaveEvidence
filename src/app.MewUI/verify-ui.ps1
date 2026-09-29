param(
    [int]$ResizeWidth = 1000,
    [int]$ResizeHeight = 700,
    # 切り出しを開始してルーペ（と中心座標ラベル）まで検証する。
    # 既定は off（通常画面の検証だけを行い、アプリの状態を切り出しモードへ変えない）。
    [switch]$VerifyLoupe
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class W {
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
'@

# 計測側を Per-Monitor V2 にする。未対応だと座標と撮影が 96/dpi に仮想化され、
# ウィンドウの下端・右端が切れた画像になり「表示されていない」と誤判定する（実際に誤判定した）。
[void][W]::SetProcessDpiAwarenessContext([IntPtr](-4))

$repo = 'D:\Dev\Repos\GitHub\SaveEvidence'
$exe = Join-Path $repo 'src\app.MewUI\src\bin\Debug\net10.0-windows\SaveEvidence.MewUI.exe'
# 検証の出力は .work へ置く（リポジトリの作業ツリーを汚さない。.work は .gitignore 済み）
$outDir = Join-Path $repo '.work\mewui-verify'
if (-not (Test-Path $outDir)) { [void](New-Item -ItemType Directory -Path $outDir -Force) }
$stdout = Join-Path $outDir 'mewui-stdout.txt'
$stderr = Join-Path $outDir 'mewui-stderr.txt'

# ユーザーの実設定を壊さないため退避する
$settingsPath = Join-Path $env:LOCALAPPDATA 'SaveEvidence\settings.json'
$settingsBackup = $null
if (Test-Path $settingsPath) { $settingsBackup = Get-Content -LiteralPath $settingsPath -Raw }

# ルーペの枠色は設定（LoupeFrameColor）に依存する（既定 #FFD3D3D3）。
# 中心座標ラベルの文字色も同様（LoupeCoordLabelColor。既定は白）。
# 実設定から解決して判定に使う（色を決め打ちすると設定変更で誤判定する）。
$loupeFrameColor = [System.Drawing.Color]::FromArgb(255, 211, 211, 211)
$loupeTextColor = [System.Drawing.Color]::White
if ($null -ne $settingsBackup) {
    try {
        $settingsJson = $settingsBackup | ConvertFrom-Json
        if ($settingsJson.LoupeFrameColor -match '^#([0-9A-Fa-f]{8})$') {
            $argb = [Convert]::ToUInt32($Matches[1], 16)
            $loupeFrameColor = [System.Drawing.Color]::FromArgb(
                [int](($argb -shr 24) -band 0xFF), [int](($argb -shr 16) -band 0xFF),
                [int](($argb -shr 8) -band 0xFF), [int]($argb -band 0xFF))
        }
        if ($settingsJson.LoupeCoordLabelColor -match '^#([0-9A-Fa-f]{8})$') {
            $argb = [Convert]::ToUInt32($Matches[1], 16)
            $loupeTextColor = [System.Drawing.Color]::FromArgb(
                [int](($argb -shr 24) -band 0xFF), [int](($argb -shr 16) -band 0xFF),
                [int](($argb -shr 8) -band 0xFF), [int]($argb -band 0xFF))
        }
    }
    catch {
        # 解析できない場合は既定色で続行する（設定の破損で検証を止めない）
    }
}

# 計測点（ウィンドウ内の相対位置）。
# pane は狭い幅ではナビがコンパクト表示（アイコンのみ）になるため小さめの位置にする
$points = [ordered]@{
    pane        = @(0.02, 0.50)
    canvasTop   = @(0.90, 0.30)
    canvasMid   = @(0.90, 0.60)
    canvasRight = @(0.97, 0.50)
    statusText  = @(0.30, 0.955)
    statusBar   = @(0.70, 0.965)
}

function Get-Probes([System.Drawing.Bitmap]$bitmap, [int]$w, [int]$h) {
    $result = @()
    foreach ($key in $points.Keys) {
        $px = [int]($w * $points[$key][0]); $py = [int]($h * $points[$key][1])
        if ($px -ge $w) { $px = $w - 1 }
        if ($py -ge $h) { $py = $h - 1 }
        $c = $bitmap.GetPixel($px, $py)
        $result += "$key=$($c.R),$($c.G),$($c.B)"
    }
    return ($result -join ' | ')
}

# 下端 6% の帯で最頻色が占める画素数を数える（ステータスバーの背景が単色で現れることの判定）。
# ステータスバーの背景色はテーマのページ背景色に従うため、色を決め打ちせず最頻色を使う。
function Get-StatusBarPixelCount([System.Drawing.Bitmap]$bitmap) {
    $w = $bitmap.Width; $h = $bitmap.Height
    $y0 = [int]($h * 0.94)
    $counts = @{}
    for ($y = $y0; $y -lt $h; $y++) {
        for ($x = [int]($w * 0.3); $x -lt $w; $x += 2) {
            $c = $bitmap.GetPixel($x, $y)
            $key = "$($c.R),$($c.G),$($c.B)"
            if ($counts.ContainsKey($key)) { $counts[$key]++ } else { $counts[$key] = 1 }
        }
    }
    if ($counts.Count -eq 0) { return 0 }
    return ($counts.Values | Measure-Object -Maximum).Maximum
}

# ルーペ領域の枠（設定の枠色）と白（座標ラベル文字）の画素数を数え、枠の外接矩形を返す。
# ルーペはカーソル追従のため位置が固定でない（カーソルがプレビュー外なら画像中央を右下へ表示する）。
# そのためキャンバス全域を走査し、枠色の画素数で存在を、外接矩形で中心座標ラベルの位置を判定する。
# 枠色は設定（LoupeFrameColor）に依存するため、呼び出し側が実設定から解決した色を渡す。
function Get-LoupeStats([System.Drawing.Bitmap]$bitmap, [System.Drawing.Color]$frameColor) {
    $w = $bitmap.Width; $h = $bitmap.Height
    $frame = 0; $white = 0
    $left = [int]::MaxValue; $top = [int]::MaxValue; $right = -1; $bottom = -1
    for ($y = [int]($h * 0.06); $y -lt [int]($h * 0.93); $y++) {
        for ($x = [int]($w * 0.20); $x -lt [int]($w * 0.99); $x++) {
            $c = $bitmap.GetPixel($x, $y)
            if ($c.R -eq $frameColor.R -and $c.G -eq $frameColor.G -and $c.B -eq $frameColor.B) {
                $frame++
                if ($x -lt $left) { $left = $x }
                if ($x -gt $right) { $right = $x }
                if ($y -lt $top) { $top = $y }
                if ($y -gt $bottom) { $bottom = $y }
            }
            elseif ($c.R -eq 255 -and $c.G -eq 255 -and $c.B -eq 255) { $white++ }
        }
    }
    return [ordered]@{ frame = $frame; white = $white; left = $left; top = $top; right = $right; bottom = $bottom }
}

# ルーペの左上の帯で中心座標ラベルの画素を数える（背景の暗色 + 文字色）。
# ラベルはルーペの左上（余白 2 DIP）に置かれる（コアが配置を単一所有する）ため、
# 枠の外接矩形から上端 25%・左端側 60% の帯を走査する。
# 文字色（既定は白）と背景色（既定は黒 60%。市松模様の上に重なるため純黒にはならない）は
# 設定に依存するため、文字色は呼び出し側が実設定から解決して渡す。
function Get-LoupeLabelStats([System.Drawing.Bitmap]$bitmap, $loupe, [System.Drawing.Color]$textColor) {
    $width = $loupe.right - $loupe.left
    $height = $loupe.bottom - $loupe.top
    if ($width -le 0 -or $height -le 0) { return [ordered]@{ text = 0; dark = 0 } }

    $x0 = [int]($loupe.left + $width * 0.00)
    $x1 = [int]($loupe.left + $width * 0.60)
    $y0 = $loupe.top
    $y1 = [int]($loupe.top + $height * 0.25)
    $text = 0; $dark = 0
    for ($y = $y0; $y -lt $y1; $y++) {
        for ($x = $x0; $x -lt $x1; $x++) {
            $c = $bitmap.GetPixel($x, $y)
            $max = [Math]::Max($c.R, [Math]::Max($c.G, $c.B))
            $min = [Math]::Min($c.R, [Math]::Min($c.G, $c.B))
            # 文字（アンチエイリアスで中間色になるため、文字色より 40 低い値までを文字とみなす）
            if ($c.R -ge [Math]::Max(120, $textColor.R - 40) -and
                $c.G -ge [Math]::Max(120, $textColor.G - 40) -and
                $c.B -ge [Math]::Max(120, $textColor.B - 40)) { $text++ }
            # 背景（暗色かつ無彩色。市松模様の濃色と区別する）
            elseif ($max -le 120 -and ($max - $min) -le 24) { $dark++ }
        }
    }
    return [ordered]@{ text = $text; dark = $dark }
}

# プレビュー領域（ルーペを除く中央）の平均色を求める（ズーム・パンの変化を検出する）
function Get-PreviewAverage([System.Drawing.Bitmap]$bitmap) {
    $w = $bitmap.Width; $h = $bitmap.Height
    $r = 0L; $g = 0L; $b = 0L; $n = 0
    for ($y = [int]($h * 0.25); $y -lt [int]($h * 0.75); $y += 3) {
        for ($x = [int]($w * 0.45); $x -lt [int]($w * 0.75); $x += 3) {
            $c = $bitmap.GetPixel($x, $y)
            $r += $c.R; $g += $c.G; $b += $c.B; $n++
        }
    }
    if ($n -eq 0) { return '0,0,0' }
    return "$([int]($r / $n)),$([int]($g / $n)),$([int]($b / $n))"
}

$script:images = @{}

function Capture-Window([IntPtr]$hwnd, [string]$label) {
    $r = New-Object W+RECT
    [void][W]::GetWindowRect($hwnd, [ref]$r)
    $w = $r.Right - $r.Left
    $h = $r.Bottom - $r.Top

    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    # 2 = PW_RENDERFULLCONTENT（DWM 合成された内容を取得）
    [void][W]::PrintWindow($hwnd, $hdc, 2)
    $g.ReleaseHdc($hdc)
    $g.Dispose()

    $png = Join-Path $outDir "mewui-$label.png"
    $bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
    $probe = Get-Probes $bmp $w $h
    $script:images[$label] = $bmp

    return [ordered]@{ rect = "$w x $h"; probe = $probe; preview = (Get-PreviewAverage $bmp); png = (Split-Path $png -Leaf) }
}

$proc = Start-Process -FilePath $exe -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
$hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 100; $i++) {
    Start-Sleep -Milliseconds 100
    $proc.Refresh()
    if ($proc.HasExited) { break }
    if ($proc.MainWindowHandle -ne [IntPtr]::Zero) { $hwnd = $proc.MainWindowHandle; break }
}

$out = [ordered]@{}
$out['exited'] = $proc.HasExited
$out['hwnd'] = $hwnd.ToInt64()

if ($hwnd -ne [IntPtr]::Zero) {
    Start-Sleep -Milliseconds 2000

    # カーソルをウィンドウ外へ退避する（ルーペはカーソルがプレビュー上にある間のみ表示する）
    [void][W]::SetCursorPos(2, 2)
    Start-Sleep -Milliseconds 500
    $out['initial'] = Capture-Window $hwnd 'initial'

    # ウィンドウを縮めて、プレビューが追随するかを確かめる
    [void][W]::SetWindowPos($hwnd, [IntPtr]::Zero, 100, 100, $ResizeWidth, $ResizeHeight, 0x0004)
    Start-Sleep -Milliseconds 1500
    # ウィンドウが (100,100) へ移動するため、同じ位置（ウィンドウ外）へ退避し直す
    [void][W]::SetCursorPos(2, 2)
    Start-Sleep -Milliseconds 500
    $out['resized'] = Capture-Window $hwnd 'resized'

    # 切り出しを開始してルーペを表示させ、中心座標ラベルの画素を検証する（任意）。
    # 通常画面（CapturePage）はルーペを使わないため、ルーペを見るには切り出し（Ctrl+R）が必要。
    if ($VerifyLoupe) {
        [void][W]::SetForegroundWindow($hwnd)
        Start-Sleep -Milliseconds 300
        $out['foregroundSet'] = ([W]::GetForegroundWindow() -eq $hwnd)

        # Ctrl+R で切り出しを開始する（キーはフォアグラウンドウィンドウへ届く）
        [W]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
        [W]::keybd_event(0x52, 0, 0, [UIntPtr]::Zero)
        [W]::keybd_event(0x52, 0, 2, [UIntPtr]::Zero)
        [W]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 1500

        # カーソルをプレビュー上へ移動する（切り出し中は移動でルーペ中心が更新され、ルーペが表示される）
        $win = New-Object W+RECT
        [void][W]::GetWindowRect($hwnd, [ref]$win)
        for ($i = 1; $i -le 4; $i++) {
            [void][W]::SetCursorPos(
                [int]($win.Left + ($win.Right - $win.Left) * 0.55) + $i,
                [int]($win.Top + ($win.Bottom - $win.Top) * 0.50) + $i)
            Start-Sleep -Milliseconds 150
        }
        Start-Sleep -Milliseconds 800
        $out['loupeShown'] = Capture-Window $hwnd 'loupeShown'

        # 切り出しをキャンセルして元の状態へ戻す（後続の判定に影響させない）
        [W]::keybd_event(0x1B, 0, 0, [UIntPtr]::Zero)
        [W]::keybd_event(0x1B, 0, 2, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 500
    }

    # 判定: ステータスバーが画面内に表示されていること
    $checks = [ordered]@{}
    foreach ($label in @('initial', 'resized')) {
        $count = Get-StatusBarPixelCount $script:images[$label]
        $checks["${label}_statusBarVisible"] = ($count -gt 100)
        $checks["${label}_statusBarPixels"] = $count

        # ルーペ: カーソルがプレビュー上に無い間は表示しない（Avalonia 版と同じ）。
        # 検証時はカーソルを動かさないため、枠色の画素が現れないことを確かめる。
        $loupe = Get-LoupeStats $script:images[$label] $loupeFrameColor
        $checks["${label}_loupeFramePixels"] = $loupe.frame
        $checks["${label}_loupeWhitePixels"] = $loupe.white
        $checks["${label}_loupeHidden"] = ($loupe.frame -lt 200)
    }

    # ルーペ表示時の判定: ルーペが現れ、その左上に中心座標ラベル（暗色背景 + 文字色）が現れること。
    # 通常画面（CapturePage）はルーペを使わないため、本判定は -VerifyLoupe 指定時のみ実行される。
    if ($VerifyLoupe) {
        $loupeShown = Get-LoupeStats $script:images['loupeShown'] $loupeFrameColor
        $checks['loupeShown_loupeFramePixels'] = $loupeShown.frame
        $checks['loupeShown_loupeVisible'] = ($loupeShown.frame -ge 200)
        $checks['loupeShown_labelTextPixels'] = 0
        $checks['loupeShown_labelDarkPixels'] = 0
        $checks['loupeShown_labelVisible'] = $false
        if ($loupeShown.frame -ge 200) {
            $label = Get-LoupeLabelStats $script:images['loupeShown'] $loupeShown $loupeTextColor
            $checks['loupeShown_labelTextPixels'] = $label.text
            $checks['loupeShown_labelDarkPixels'] = $label.dark
            $checks['loupeShown_labelVisible'] = ($label.text -gt 0 -and $label.dark -gt 0)
            $checks['loupeShown_loupeRect'] = "$($loupeShown.left),$($loupeShown.top) - $($loupeShown.right),$($loupeShown.bottom)"
        }
    }
    $out['checks'] = $checks

    foreach ($bmp in @($script:images.Values)) { $bmp.Dispose() }
}

if (-not $proc.HasExited) { $proc.Kill(); $proc.WaitForExit() }

if ($null -ne $settingsBackup) { Set-Content -LiteralPath $settingsPath -Value $settingsBackup -NoNewline }
elseif (Test-Path $settingsPath) { Remove-Item -LiteralPath $settingsPath -Force }

$out | ConvertTo-Json -Depth 5
'--- stderr ---'
if (Test-Path $stderr) { Get-Content -LiteralPath $stderr -Raw }
