using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using app.Helpers;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingRectangle = System.Drawing.Rectangle;

namespace app.Converters;

/// <summary>
/// System.Drawing.Bitmap と Avalonia.Media.Imaging.Bitmap 間の相互変換ユーティリティ。
/// </summary>
/// <remarks>
/// System.Drawing.Common を併用し、画面キャプチャ処理（CaptureManager）で生成された
/// System.Drawing.Bitmap を Avalonia UI で表示可能な形式に変換するために使用する。<br/>
/// 変換には 32bpp ARGB のピクセルバッファ経由方式を採用し、品質とパフォーマンスを両立する。<br/>
/// </remarks>
public static class BitmapConverters
{
    /// <summary>System.Drawing.Bitmap を Avalonia Bitmap に変換する。</summary>
    /// <param name="source">変換元の System.Drawing.Bitmap</param>
    /// <returns>変換結果の Avalonia Bitmap</returns>
    /// <exception cref="ArgumentNullException">source が null の場合にスローされる。</exception>
    public static Avalonia.Media.Imaging.Bitmap ToAvaloniaBitmap(this DrawingBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);
        CaptureLogger.Log("[BitmapConverters] ToAvaloniaBitmap: {0}x{1} start", source.Width, source.Height);

        // ピクセルデータを 32bpp ARGB で取得し、Avalonia Bitmap に変換する
        var rect = new DrawingRectangle(0, 0, source.Width, source.Height);
        var bmpData = source.LockBits(rect, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        try
        {
            var stride = bmpData.Stride;
            var height = source.Height;
            var rowSize = stride;

            // WriteableBitmap 経由で Avalonia Bitmap を生成
            var wb = new WriteableBitmap(
                new Avalonia.PixelSize(source.Width, source.Height),
                new Avalonia.Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using var locked = wb.Lock();
            var destStride = locked.RowBytes;
            var pixels = new byte[rowSize];

            for (var y = 0; y < height; y++)
            {
                // 1行分のピクセルデータをソースから読み込む
                Marshal.Copy(bmpData.Scan0 + y * stride, pixels, 0, rowSize);

                // 画面キャプチャ画像には透過が無いため、全ピクセルのアルファを 255 に強制する
                // （PrintWindow は alpha=255、CopyFromScreen は alpha=0 とバラつくため統一）
                for (var x = 3; x < rowSize; x += 4)
                {
                    pixels[x] = 255;
                }

                // 行ごとに出力先にコピー（stride が異なる場合に対応）
                Marshal.Copy(pixels, 0,
                    locked.Address + y * destStride,
                    Math.Min(rowSize, destStride));
            }

            return wb;
        }
        finally
        {
            source.UnlockBits(bmpData);
        }
    }

    /// <summary>Avalonia Bitmap を System.Drawing.Bitmap に変換する。</summary>
    /// <param name="source">変換元の Avalonia Bitmap（Opaque または Premul の PixelFormat に対応）</param>
    /// <returns>変換結果の System.Drawing.Bitmap（Format32bppArgb）</returns>
    /// <exception cref="ArgumentNullException">source が null の場合にスローされる。</exception>
    public static DrawingBitmap ToDrawingBitmap(this Avalonia.Media.Imaging.Bitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // MemoryStream 経由で System.Drawing.Bitmap に変換
        using var ms = new MemoryStream();
        source.Save(ms);
        ms.Seek(0, SeekOrigin.Begin);
        return new System.Drawing.Bitmap(ms);
    }
}
