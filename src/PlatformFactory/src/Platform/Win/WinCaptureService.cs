using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Platform;
using Estable.AvaloniaUI.Platform.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DrawingBitmap = System.Drawing.Bitmap;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace Estable.AvaloniaUI.Platform.Win;

/// <summary>画面/ウィンドウのキャプチャ機能を提供するクラス。</summary>
/// <remarks>
/// CopyFromScreen（<c>System.Drawing.Graphics</c>）および PrintWindow（Win32 API）を<br/>
/// 使用して画面/ウィンドウのキャプチャを行う。<br/>
/// P/Invoke 呼び出しは <c>WinNative</c> に委譲する。<br/>
/// <see cref="ICaptureService"/> の実装であり、テスト時のモックに備える。<br/>
/// Windows 固有の型（System.Drawing.Bitmap / Rectangle / IntPtr）は本クラス（Win 実装）の内部に閉じ、<br/>
/// 公開 API（<see cref="ICaptureService"/>）はプラットフォーム非依存の型（<see cref="UnifiedBitmap"/> / <see cref="IPlatformHandle"/>）のみを使用する。<br/>
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

    /// <summary>指定された画面領域をキャプチャし、UnifiedBitmap として返す。</summary>
    /// <param name="bounds">キャプチャする画面領域（画面物理座標）。</param>
    /// <returns>キャプチャした UnifiedBitmap。失敗時は null。破棄は呼び出し側。</returns>
    /// <remarks>
    /// 内部で System.Drawing（CopyFromScreen）を使用してキャプチャし、<see cref="UnifiedBitmap"/> へ変換する。<br/>
    /// <see cref="UnifiedBitmap"/>（SKBitmap 内部）の生成はスレッド非依存のため、<br/>
    /// CopyFromScreen（GDI 同期コール）と同一のバックグラウンドスレッドで実行可能である。<br/>
    /// 公開 API はプラットフォーム非依存の型（<see cref="UnifiedBitmap"/> / <see cref="IPlatformHandle"/>）のみを使用する。<br/>
    /// </remarks>
    public UnifiedBitmap? CaptureArea(PixelRect bounds)
    {
        var sdRect = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        using var sdBmp = CaptureSdBitmap(sdRect);
        return ConvertToUnifiedBitmap(sdBmp);
    }

    /// <summary>仮想スクリーン（全モニタを包含する領域）全体を 1 回のキャプチャで取得する。</summary>
    /// <param name="origin">仮想スクリーンの原点（全モニタのバウンディングボックス左上、画面物理座標）。</param>
    /// <returns>仮想スクリーン全体の UnifiedBitmap。キャプチャ失敗時は null。破棄は呼び出し側。</returns>
    /// <remarks>
    /// 仮想スクリーン境界は Win32 の <c>GetSystemMetrics</c>（SM_XVIRTUALSCREEN 等）で取得し、<br/>
    /// 全モニタを包含する矩形を 1 回の CopyFromScreen でキャプチャする。<br/>
    /// 境界取得とキャプチャはスレッド非依存のため、バックグラウンドスレッドから安全に実行できる。<br/>
    /// </remarks>
    public UnifiedBitmap? CaptureVirtualScreen(out PixelPoint origin)
    {
        // Win32 の仮想スクリーン境界（全モニタのバウンディングボックス）を取得
        var (vx, vy, vw, vh) = WinNative.GetVirtualScreenBounds();
        origin = new PixelPoint(vx, vy);
        if (vw <= 0 || vh <= 0) return null;

        _log.LogVirtualScreenBounds(vx, vy, vw, vh);
        return CaptureArea(new PixelRect(vx, vy, vw, vh));
    }

    /// <summary>System.Drawing.Bitmap → UnifiedBitmap 変換。</summary>
    /// <param name="source">変換元の System.Drawing.Bitmap。</param>
    /// <returns>変換結果の UnifiedBitmap。</returns>
    /// <remarks>
    /// <see cref="UnifiedBitmap.FromSystemDrawingBitmap"/>（LockBits ベース）で BGRA8888・Premul へ変換する。<br/>
    /// PNG エンコード/デコードを経由しない（高速・可逆）。アルファは常に 255（不透明）に設定される。<br/>
    /// SKBitmap の生成はスレッド非依存のため、バックグラウンドスレッドから呼び出し可能。<br/>
    /// </remarks>
    private static UnifiedBitmap ConvertToUnifiedBitmap(DrawingBitmap source)
    {
        // アルファを常に 255（不透明）に設定するため、Format32bppArgb で読み取り、Premul へ設定する。
        var rect = new Rectangle(0, 0, source.Width, source.Height);
        var bmpData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var w = source.Width;
            var h = source.Height;
            var stride = bmpData.Stride;
            var result = UnifiedBitmap.Create(w, h);
            var dstStride = result.Stride;
            var rowBytes = Math.Min(stride, dstStride);
            unsafe
            {
                var pDst = (byte*)result.Bitmap.GetPixels();
                for (var y = 0; y < h; y++)
                {
                    var pSrc = (byte*)(bmpData.Scan0 + y * stride);
                    // 行全体を一括コピーし、アルファバイトのみ 255（不透明）へ上書きする
                    Buffer.MemoryCopy(pSrc, pDst + y * dstStride, rowBytes, rowBytes);
                    for (var x = 0; x < w; x++)
                    {
                        pDst[y * dstStride + x * 4 + 3] = 255;
                    }
                }
            }
            return result;
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

}
