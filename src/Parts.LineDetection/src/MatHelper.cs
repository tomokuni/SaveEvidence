using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Imaging;
using OpenCvSharp;

namespace EsUtil.SaveEvidence.Parts.LineDetection;

/// <summary>コアの画像 ↔ Mat 変換のヘルパー。</summary>
/// <remarks>OpenCV 依存を内部に閉じ込める（本クラスだけが <c>OpenCvSharp</c> を参照する）。</remarks>
internal static class MatHelper
{
    /// <summary>画像（BGRA8888）を Mat（CV_8UC4）へ変換する。</summary>
    /// <param name="bitmap">変換元の画像。 借用であり破棄しない（読み取るだけのため <see cref="EsReadOnlyBitmap"/> を受ける）。</param>
    /// <returns>変換された Mat（呼び出し元で破棄する）。</returns>
    /// <remarks>
    /// ピクセル バッファ（<see cref="EsReadOnlyBitmap.ReadOnlyPixels"/>）を直接読み取り、<see cref="Mat"/> へコピーする。<br/>
    /// ストライドが幅×4 と異なる場合は行単位でコピーする。
    /// </remarks>
    public static Mat ToMat(EsReadOnlyBitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var stride = width * EsReadOnlyBitmap.BytesPerPixel;
        var pixels = new byte[height * stride];
        var srcStride = bitmap.Stride;

        unsafe
        {
            fixed (byte* src = bitmap.ReadOnlyPixels)
            fixed (byte* dst = pixels)
            {
                if (srcStride == stride)
                {
                    Buffer.MemoryCopy(src, dst, pixels.Length, pixels.Length);
                }
                else
                {
                    for (var y = 0; y < height; y++)
                    {
                        Buffer.MemoryCopy(src + y * srcStride, dst + y * stride, stride, stride);
                    }
                }
            }
        }

        var mat = new Mat(height, width, MatType.CV_8UC4);
        Marshal.Copy(pixels, 0, mat.Data, pixels.Length);
        return mat;
    }
}
