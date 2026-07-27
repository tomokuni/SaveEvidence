using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace Estable.AvaloniaUI.Platform.Imaging;

/// <summary>Avalonia 非依存のピクセルバッファ（BGRA8888・アルファ乗算済み）。</summary>
/// <remarks>
/// 幅・高さ・ストライドと生ピクセルデータ（<see cref="byte[]"/>）を保持する。<br/>
/// Avalonia の <see cref="Bitmap"/> とは <see cref="ToBitmap"/> / <see cref="ToPixelData"/> で相互変換し、<br/>
/// 変換は <c>Bitmap.CopyPixels</c>（ネイティブ memcpy 相当）と <c>WriteableBitmap.Lock</c>（直接コピー）を使用するため<br/>
/// PNG エンコード/デコードや System.Drawing を経由しない（高速・可逆）。<br/>
/// <br/>
/// <b>ハイブリッド方針</b>: 表示・保存・イベント境界（Image.Source / SaveImage）は <see cref="Bitmap"/>（WriteableBitmap）を<br/>
/// そのまま使用し（ゼロコピー）、ピクセル操作（切り出し・ウィンドウ検出・色解析）は本型（byte[]）で行う。<br/>
/// 変換は「Bitmap → 配列」の 1 往復のみ（編集時）に限定することで、変換コピーの回数を最小化する。<br/>
/// <br/>
/// ピクセル形式は BGRA8888（アルファ乗算済み、Format32bppArgb 相当）を想定する。
/// </remarks>
public sealed class AppPixelData
{
    /// <summary>画像の幅（ピクセル）。</summary>
    public int Width { get; }

    /// <summary>画像の高さ（ピクセル）。</summary>
    public int Height { get; }

    /// <summary>1ラインあたりのバイト数（Width × 4、パディングを含む）。</summary>
    public int Stride { get; }

    /// <summary>生ピクセルデータ（BGRA8888）。</summary>
    public byte[] PixelData { get; }

    /// <summary>ピクセルバッファを初期化する。</summary>
    /// <param name="width">画像の幅（ピクセル）。</param>
    /// <param name="height">画像の高さ（ピクセル）。</param>
    /// <param name="stride">1ラインあたりのバイト数。</param>
    /// <param name="pixelData">生ピクセルデータ（呼び出し元から所有権が移る）。</param>
    public AppPixelData(int width, int height, int stride, byte[] pixelData)
    {
        Width = width;
        Height = height;
        Stride = stride;
        PixelData = pixelData;
    }

    /// <summary><see cref="Bitmap"/>（WriteableBitmap）に変換する。</summary>
    /// <returns>変換された Bitmap。サイズが 0 の場合は null。</returns>
    /// <remarks>
    /// WriteableBitmap にピクセルを直接コピーして生成する。<br/>
    /// コピーは行単位で行い、ストライド差（パディング）を吸収する。<br/>
    /// </remarks>
    public Bitmap? ToBitmap()
    {
        var w = Width;
        var h = Height;
        if (w < 1 || h < 1) return null;

        var wb = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var locked = wb.Lock();
        var dstStride = locked.RowBytes;
        var rowBytes = Math.Min(Stride, dstStride);
        for (var y = 0; y < h; y++)
        {
            Marshal.Copy(PixelData, y * Stride, locked.Address + y * dstStride, rowBytes);
        }
        return wb;
    }

    /// <summary><see cref="Bitmap"/> を <see cref="AppPixelData"/> に変換する。</summary>
    /// <param name="bitmap">変換対象の Bitmap。</param>
    /// <returns>変換されたピクセルデータ。</returns>
    /// <remarks>
    /// <c>Bitmap.CopyPixels</c>（ネイティブ memcpy 相当）で直接読み取るため、<br/>
    /// PNG エンコード/デコードや System.Drawing を経由しない（高速・可逆）。<br/>
    /// サイズが 0 の場合は空のデータ（0x0）を返す。<br/>
    /// </remarks>
    public static unsafe AppPixelData ToPixelData(Bitmap bitmap)
    {
        var w = bitmap.PixelSize.Width;
        var h = bitmap.PixelSize.Height;
        if (w < 1 || h < 1)
            return new AppPixelData(0, 0, 0, []);

        var stride = w * 4;
        var pixels = new byte[stride * h];
        fixed (byte* p = pixels)
        {
            bitmap.CopyPixels(new PixelRect(0, 0, w, h), (nint)p, pixels.Length, stride);
        }
        return new AppPixelData(w, h, stride, pixels);
    }
}
