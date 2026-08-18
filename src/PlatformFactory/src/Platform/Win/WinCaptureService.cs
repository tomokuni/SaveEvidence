using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DrawingBitmap = System.Drawing.Bitmap;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;
using DrawingPoint = System.Drawing.Point;
// Bitmap は System.Drawing.Bitmap（DrawingBitmap 別名）と衝突するため、Avalonia 側を優先する別名で解決する
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace Estable.AvaloniaUI.Platform.Win;

/// <summary>画面/ウィンドウのキャプチャ機能を提供するクラス。</summary>
/// <remarks>
/// CopyFromScreen（<c>System.Drawing.Graphics</c>）および PrintWindow（Win32 API）を<br/>
/// 使用して画面/ウィンドウのキャプチャを行う。<br/>
/// P/Invoke 呼び出しは <c>WinNative</c> に委譲する。<br/>
/// <see cref="ICaptureService"/> の実装であり、テスト時のモックに備える。<br/>
/// Windows 固有の型（System.Drawing.Bitmap / Rectangle / IntPtr）は本クラス（Win 実装）の内部に閉じ、<br/>
/// 公開 API（<see cref="ICaptureService"/>）はプラットフォーム非依存の型（<see cref="Bitmap"/> / <see cref="IPlatformHandle"/>）のみを使用する。<br/>
/// 生成は <see cref="PlatformFactory.CreateCaptureService"/> が唯一の経路（Windows のみ）。<br/>
/// System.Drawing は Windows 専用のため、本クラスは <see cref="SupportedOSPlatformAttribute"/> で Windows に限定する。<br/>
/// 継承不可。<br/>
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed partial class WinCaptureService(ILogger? logger = null) : ICaptureService
{
    /// <summary>内部ロガー（null の場合は出力なしに正規化）。</summary>
    private readonly ILogger _log = logger ?? NullLogger.Instance;

    // ====================================================================
    // 公開メソッド（プラットフォーム非依存の型のみ）
    // ====================================================================

    /// <summary>指定された画面領域をキャプチャし、Avalonia Bitmap として返す。</summary>
    /// <param name="bounds">キャプチャする画面領域（画面物理座標）。</param>
    /// <returns>キャプチャした Bitmap。失敗時は null。</returns>
    /// <remarks>
    /// 内部で System.Drawing（CopyFromScreen）を使用してキャプチャし、<see cref="ConvertToAvaloniaBitmap"/> で<br/>
    /// Avalonia Bitmap へ直接変換する。WriteableBitmap の生成・ピクセル書き込みはスレッド非依存のため、<br/>
    /// CopyFromScreen（GDI 同期コール）と同一のバックグラウンドスレッドで実行可能である。<br/>
    /// 公開 API はプラットフォーム非依存の型（<see cref="Bitmap"/> / <see cref="IPlatformHandle"/>）のみを使用する。<br/>
    /// </remarks>
    public Bitmap? CaptureArea(PixelRect bounds)
    {
        var sdRect = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        using var sdBmp = CaptureSdBitmap(sdRect);
        return ConvertToAvaloniaBitmap(sdBmp);
    }

    /// <summary>仮想スクリーン（全モニタを包含する領域）全体を 1 回のキャプチャで取得する。</summary>
    /// <param name="origin">仮想スクリーンの原点（全モニタのバウンディングボックス左上、画面物理座標）。</param>
    /// <returns>仮想スクリーン全体の Bitmap。キャプチャ失敗時は null。</returns>
    /// <remarks>
    /// 仮想スクリーン境界は Win32 の <c>GetSystemMetrics</c>（SM_XVIRTUALSCREEN 等）で取得し、<br/>
    /// 全モニタを包含する矩形を 1 回の CopyFromScreen でキャプチャする。<br/>
    /// 境界取得とキャプチャはスレッド非依存のため、バックグラウンドスレッドから安全に実行できる。<br/>
    /// </remarks>
    public Bitmap? CaptureVirtualScreen(out PixelPoint origin)
    {
        // Win32 の仮想スクリーン境界（全モニタのバウンディングボックス）を取得
        var (vx, vy, vw, vh) = WinNative.GetVirtualScreenBounds();
        origin = new PixelPoint(vx, vy);
        if (vw <= 0 || vh <= 0) return null;

        _log.LogVirtualScreenBounds(vx, vy, vw, vh);
        return CaptureArea(new PixelRect(vx, vy, vw, vh));
    }

    /// <summary>指定されたウィンドウの領域をキャプチャする（System.Drawing ベースの内部実装）。</summary>
    /// <param name="hWnd">キャプチャ対象のウィンドウハンドル。</param>
    /// <param name="mode">キャプチャ方式（RedrawDirect は PrintWindow、ScreenCopy は CopyFromScreen）。</param>
    /// <returns>キャプチャした画像。失敗時（PrintWindow 全フラグ失敗等）は null。</returns>
    /// <remarks>
    /// 仕様 2.4.3: PrintWindow 方式で失敗した場合は null を返し、CopyFromScreen へのフォールバックは行わない。<br/>
    /// CopyFromScreen 方式の場合は背景キャプチャ画像から切り出す。<br/>
    /// </remarks>
    public DrawingBitmap? CaptureWindowCore(IntPtr hWnd, WindowCaptureMode mode = WindowCaptureMode.RedrawDirect)
    {       
        _log.LogCaptureWindow(hWnd.ToString("X"), mode);
        var visibleRect = GetWindowVisibleRect(hWnd);
        if (visibleRect.IsEmpty) return null;

        if (mode == WindowCaptureMode.RedrawDirect)
        {
            var fullRect = GetWindowRect(hWnd);
            if (fullRect.IsEmpty) return null;
            var fw = fullRect.Width;
            var fh = fullRect.Height;

            var fullBitmap = TryPrintWindow(hWnd, fw, fh, 0)
                          ?? TryPrintWindow(hWnd, fw, fh, WinNative.PW_RENDERFULLCONTENT)
                          ?? TryPrintWindow(hWnd, fw, fh, WinNative.PW_CLIENTONLY);

            if (fullBitmap is not null)
            {
                _log.LogPrintWindowSuccess(fullBitmap.Width, fullBitmap.Height);
                var cropped = CropToVisibleRect(fullBitmap, fullRect, visibleRect);
                if (cropped is not null)
                {
                    fullBitmap.Dispose();
                    _log.LogCroppedToVisibleRect(cropped.Width, cropped.Height);
                    return cropped;
                }
                return fullBitmap;
            }

            // 仕様 2.4.3: PrintWindow 全フラグ失敗時は null を返す（CopyFromScreen へのフォールバックは行わない）
            _log.LogPrintWindowAllFlagsFailed();
            return null;
        }

        _log.LogCopyFromScreen(visibleRect);
        return CaptureSdBitmap(visibleRect);
    }

    /// <summary>ウィンドウの可視矩形領域を取得する（最大化はワークエリア・通常は DWM 影除外）。</summary>
    /// <param name="hWnd">対象ウィンドウハンドル。</param>
    /// <returns>可視矩形。取得失敗時は <see cref="Rectangle.Empty"/>。</returns>
    /// <remarks>
    /// 可視矩形の取得（最大化のワークエリア / DWM 影除外 / GetWindowRect フォールバック）は
    /// <see cref="WinNative.TryGetVisibleRect"/> に委譲する（<see cref="WinElementFinder"/> と共用・単一所有）。
    /// </remarks>
    public Rectangle GetWindowVisibleRect(IntPtr hWnd)
    {
        if (!WinNative.TryGetVisibleRect(hWnd, out var rect)) return Rectangle.Empty;
        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    /// <summary>ウィンドウの矩形領域を取得する（GetWindowRect ラッパー）。</summary>
    /// <param name="hWnd">対象ウィンドウハンドル。</param>
    /// <returns>ウィンドウ矩形。取得失敗時は <see cref="Rectangle.Empty"/>。</returns>
    public Rectangle GetWindowRect(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return Rectangle.Empty;
        if (!WinNative.GetWindowRect(hWnd, out var rect)) return Rectangle.Empty;
        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    /// <summary>ウィンドウ領域をキャプチャし Avalonia Bitmap を返す（インターフェイス実装）。</summary>
    /// <param name="handle">キャプチャ対象のウィンドウ識別子。</param>
    /// <param name="mode">キャプチャ方式。</param>
    /// <returns>キャプチャした画像。失敗時は null（仕様 2.4.3）。</returns>
    public Bitmap? CaptureWindow(IPlatformHandle handle, WindowCaptureMode mode = WindowCaptureMode.RedrawDirect)
    {
        var hWnd = handle.Handle;
        using var sdBmp = CaptureWindowCore(hWnd, mode);
        if (sdBmp is null) return null;
        return ConvertToAvaloniaBitmap(sdBmp);
    }

    /// <summary>System.Drawing.Bitmap → Avalonia Bitmap 変換。</summary>
    /// <param name="source">変換元の System.Drawing.Bitmap。</param>
    /// <returns>変換結果の Avalonia Bitmap。</returns>
    /// <remarks>
    /// LockBits でピクセルデータ（Format32bppArgb）を直接取得し、WriteableBitmap のロック領域へコピーする。<br/>
    /// PNG エンコード/デコードを経由しない（高速・可逆）。アルファは常に 255（不透明）に設定する。<br/>
    /// WriteableBitmap の生成・Lock はスレッド非依存のため、バックグラウンドスレッドから呼び出し可能。<br/>
    /// </remarks>
    public Bitmap ConvertToAvaloniaBitmap(DrawingBitmap source)
    {
        var rect = new Rectangle(0, 0, source.Width, source.Height);
        var bmpData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var w = source.Width;
            var h = source.Height;
            var stride = bmpData.Stride;
            var wb = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96),
                AvaloniaPixelFormat.Bgra8888, AlphaFormat.Premul);
            using var locked = wb.Lock();
            var dstStride = locked.RowBytes;
            var rowBytes = Math.Min(stride, dstStride);

            unsafe
            {
                for (var y = 0; y < h; y++)
                {
                    var pSrc = (byte*)(bmpData.Scan0 + y * stride);
                    var pDst = (byte*)(locked.Address + y * dstStride);
                    // 行全体を一括コピーし、アルファバイトのみ 255（不透明）へ上書きする
                    Buffer.MemoryCopy(pSrc, pDst, rowBytes, rowBytes);
                    for (var x = 0; x < w; x++)
                    {
                        pDst[x * 4 + 3] = 255;
                    }
                }
            }

            return wb;
        }
        finally
        {
            source.UnlockBits(bmpData);
        }
    }

    // ====================================================================
    // 内部ヘルパー（System.Drawing は本セクションに閉じ込める）
    // ====================================================================

    /// <summary>指定された画面領域を System.Drawing.Bitmap としてキャプチャする（内部ヘルパー）。</summary>
    /// <param name="bounds">キャプチャする画面領域（画面物理座標）。</param>
    /// <returns>キャプチャした System.Drawing.Bitmap。</returns>
    /// <remarks>
    /// CopyFromScreen は GDI の同期コールであり、DWM 応答不能時にはブロックする可能性がある。<br/>
    /// 呼び出し側（SelectionSession）はバックグラウンド実行とタイムアウトで保護する。<br/>
    /// </remarks>
    private DrawingBitmap CaptureSdBitmap(Rectangle bounds)
    {
        _log.LogCaptureSdBitmap(bounds);
        var bitmap = new DrawingBitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppRgb);
        using var g = Graphics.FromImage(bitmap);
        g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
        _log.LogCaptureSdBitmapDone(bitmap.Width, bitmap.Height);
        return bitmap;
    }

    private DrawingBitmap? TryPrintWindow(IntPtr hWnd, int width, int height, uint flags)
    {
        try
        {
            var bitmap = new DrawingBitmap(width, height);
            using var g = Graphics.FromImage(bitmap);
            var hdc = g.GetHdc();
            bool success;
            try
            {
                success = WinNative.PrintWindow(hWnd, hdc, flags);
            }
            finally
            {
                // GetHdc 後の HDC は例外経路でも必ず解放する（デバイスコンテキストのリーク防止）
                g.ReleaseHdc(hdc);
            }

            if (success && IsBitmapValid(bitmap))
            {
                _log.LogTryPrintWindowSuccess(flags, bitmap.Width, bitmap.Height);
                return bitmap;
            }
            _log.LogTryPrintWindowFailed(flags, success);
            bitmap.Dispose();
        }
        catch (Exception ex)
        {
            _log.LogTryPrintWindowError(ex, flags);
        }
        return null;
    }

    private bool IsBitmapValid(DrawingBitmap bitmap)
    {
        var w = bitmap.Width;
        var h = bitmap.Height;
        DrawingPoint[] samples =
        [
            new DrawingPoint(w / 2, h / 2),
            new DrawingPoint(w / 4, h / 4),
            new DrawingPoint(w * 3 / 4, h / 4),
            new DrawingPoint(w / 4, h * 3 / 4),
            new DrawingPoint(w * 3 / 4, h * 3 / 4),
        ];
        foreach (var pt in samples)
        {
            if (pt.X >= 0 && pt.X < w && pt.Y >= 0 && pt.Y < h)
            {
                var pixel = bitmap.GetPixel(pt.X, pt.Y);
                if (pixel.R != 0 || pixel.G != 0 || pixel.B != 0) return true;
            }
        }
        return false;
    }

    private DrawingBitmap? CropToVisibleRect(DrawingBitmap fullBitmap, Rectangle fullRect, Rectangle visibleRect)
    {
        if (visibleRect.IsEmpty || visibleRect == fullRect) return null;
        var cropX = visibleRect.X - fullRect.X;
        var cropY = visibleRect.Y - fullRect.Y;
        var cropW = visibleRect.Width;
        var cropH = visibleRect.Height;
        if (cropX < 0) { cropW += cropX; cropX = 0; }
        if (cropY < 0) { cropH += cropY; cropY = 0; }
        cropW = Math.Min(cropW, fullBitmap.Width - cropX);
        cropH = Math.Min(cropH, fullBitmap.Height - cropY);
        if (cropW <= 0 || cropH <= 0) return null;
        if (cropX == 0 && cropY == 0 && cropW == fullBitmap.Width && cropH == fullBitmap.Height) return null;
        try
        {
            return fullBitmap.Clone(new Rectangle(cropX, cropY, cropW, cropH), fullBitmap.PixelFormat);
        }
        catch
        {
            // Clone 失敗（ピクセル形式非対応等）は null を返し、呼び出し側（CaptureWindow）は
            // PrintWindow 成功分の fullBitmap をそのまま使用する（切り出しはベストエフォート）。
            return null;
        }
    }
}
