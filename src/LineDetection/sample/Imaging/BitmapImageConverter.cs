using Avalonia;
using Avalonia.Media.Imaging;
using Estable.AvaloniaUI.LineDetection;
using System.Runtime.InteropServices;

namespace LineDetectionSample.Imaging;

/// <summary><see cref="Bitmap"/> を検出器の入力（<see cref="PixelImage"/>）へ変換するユーティリティ。</summary>
/// <remarks>
/// 検出器（<see cref="ILineDetector"/>）は OpenCV・Avalonia に依存しない純粋なピクセルバッファ（<see cref="PixelImage"/>）を<br/>
/// 受け取る。<see cref="Bitmap"/>（BGRA8888）からピクセルを読み出し、<see cref="PixelImage"/> を構築する。<br/>
/// <see cref="Bitmap.CopyPixels"/> は実機の Skia 環境で動作するため、本変換はサンプルアプリ（実機）で使用する。
/// </remarks>
internal static class BitmapImageConverter
{
    /// <summary>Bitmap（BGRA8888）を PixelImage（BGRA ピクセルバッファ）へ変換する。</summary>
    /// <param name="bitmap">変換元の Bitmap。</param>
    /// <returns>変換された PixelImage。</returns>
    /// <remarks>
    /// 1. Bitmap のピクセルデータを BGRA バイト列として読み出す<br/>
    /// 2. 幅・高さ・ストライド（幅 × 4）とバイト列から <see cref="PixelImage"/> を構築する
    /// </remarks>
    public static PixelImage ToPixelImage(Bitmap bitmap)
    {
        var width = bitmap.PixelSize.Width;
        var height = bitmap.PixelSize.Height;
        var stride = width * 4;
        var pixels = new byte[height * stride];

        unsafe
        {
            fixed (byte* p = pixels)
            {
                bitmap.CopyPixels(new PixelRect(0, 0, width, height), (nint)p, pixels.Length, stride);
            }
        }

        return new PixelImage(width, height, stride, pixels);
    }
}
