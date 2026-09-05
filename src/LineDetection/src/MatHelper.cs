using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Estable.AvaloniaUI.Platform.Imaging;
using OpenCvSharp;

namespace Estable.AvaloniaUI.LineDetection;

/// <summary>UnifiedBitmap ↔ Mat 変換のヘルパー。</summary>
/// <remarks>OpenCV 依存を内部に閉じ込める（本クラスだけが <c>OpenCvSharp</c> を参照する）。</remarks>
internal static class MatHelper
{
    /// <summary>UnifiedBitmap（BGRA8888）を Mat（CV_8UC4）へ変換する。</summary>
    /// <param name="bitmap">変換元の UnifiedBitmap。</param>
    /// <returns>変換された Mat（呼び出し元で破棄する）。</returns>
    /// <remarks>
    /// 内部の SKBitmap（BGRA8888・Premul）のピクセルバッファを直接読み取り、<see cref="Mat"/> へコピーする。<br/>
    /// ストライドが幅×4 と異なる場合は行単位でコピーする。
    /// </remarks>
    public static Mat ToMat(UnifiedBitmap bitmap)
    {
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[height * stride];
        var sk = bitmap.ToSKBitmap();
        var srcStride = sk.RowBytes;

        unsafe
        {
            fixed (byte* p = pixels)
            {
                var src = (byte*)sk.GetPixels();
                if (srcStride == stride)
                {
                    Buffer.MemoryCopy(src, p, pixels.Length, pixels.Length);
                }
                else
                {
                    for (var y = 0; y < height; y++)
                    {
                        Buffer.MemoryCopy(src + y * srcStride, p + y * stride, stride, stride);
                    }
                }
            }
        }

        var mat = new Mat(height, width, MatType.CV_8UC4);
        Marshal.Copy(pixels, 0, mat.Data, pixels.Length);
        return mat;
    }
}
