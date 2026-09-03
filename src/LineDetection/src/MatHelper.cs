using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using OpenCvSharp;

namespace Estable.AvaloniaUI.LineDetection;

/// <summary>Bitmap ↔ Mat 変換のヘルパー。</summary>
internal static class MatHelper
{
    /// <summary>Bitmap（BGRA8888）を Mat（CV_8UC4）へ変換する。</summary>
    /// <param name="bitmap">変換元の Bitmap。</param>
    /// <returns>変換された Mat（呼び出し元で破棄する）。</returns>
    public static Mat ToMat(Bitmap bitmap)
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

        var mat = new Mat(height, width, MatType.CV_8UC4);
        Marshal.Copy(pixels, 0, mat.Data, pixels.Length);
        return mat;
    }
}
