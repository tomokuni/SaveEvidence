using System.Runtime.InteropServices;
using OpenCvSharp;

namespace Estable.AvaloniaUI.LineDetection;

/// <summary>PixelImage ↔ Mat 変換のヘルパー。</summary>
/// <remarks>OpenCV 依存を内部に閉じ込める（本クラスだけが <c>OpenCvSharp</c> を参照する）。</remarks>
internal static class MatHelper
{
    /// <summary>PixelImage（BGRA8888）を Mat（CV_8UC4）へ変換する。</summary>
    /// <param name="image">変換元の PixelImage。</param>
    /// <returns>変換された Mat（呼び出し元で破棄する）。</returns>
    public static Mat ToMat(PixelImage image)
    {
        var width = image.Width;
        var height = image.Height;
        var mat = new Mat(height, width, MatType.CV_8UC4);
        var pixels = image.Pixels.ToArray();

        if (image.Stride == width * 4)
        {
            // 通常の場合（連続したバッファ）は一括コピーする
            Marshal.Copy(pixels, 0, mat.Data, pixels.Length);
        }
        else
        {
            // Stride が width*4 と異なる場合（パディングあり）は行ごとにコピーする
            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(pixels, y * image.Stride, mat.Data + y * width * 4, width * 4);
            }
        }

        return mat;
    }
}
