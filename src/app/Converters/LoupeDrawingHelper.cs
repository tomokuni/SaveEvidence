using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;

namespace app.Converters;

/// <summary>
/// ルーペ描画ヘルパー：拡大画像（ピクセルブロック＋市松模様背景）＋十字線＋外枠を1枚のBitmapに合成する。
/// </summary>
/// <remarks>
/// 100%画像のピクセルを <c>zoomLevel</c>倍に拡大し、1pixel を <c>zoomLevel x zoomLevel</c> のブロックで表現する。<br/>
/// 十字線の幅は <c>zoomLevel</c> と等しく、ピクセルブロックのグリッドに合わせて描画する。<br/>
/// </remarks>
public static class LoupeDrawingHelper
{
    /// <summary>
    /// ルーペ画像を描画する。
    /// </summary>
    /// <param name="sourceImage">拡大元の100%画像（カーソル中心に切り出された実ピクセル）</param>
    /// <param name="loupeSize">ルーペの一辺のサイズ（ピクセル）</param>
    /// <param name="zoomLevel">拡大率（4〜32、偶数推奨）</param>
    /// <param name="crossColor">十字線の色文字列（例: "Red"）</param>
    /// <param name="crossOpacity">十字線の不透明度（0.0〜1.0）</param>
    /// <param name="checkerSize">市松模様の1タイルサイズ（表示ピクセル、Settings.PreviewCheckerSize から渡す）</param>
    /// <returns>描画結果の Avalonia Bitmap</returns>
    public static Bitmap DrawLoupe(
        System.Drawing.Bitmap sourceImage,
        int loupeSize,
        int zoomLevel,
        string crossColor,
        double crossOpacity,
        int checkerSize)
    {
        var srcWidth = sourceImage.Width;
        var srcHeight = sourceImage.Height;

        // ソース画像のピクセルデータを読み込む
        var srcData = sourceImage.LockBits(
            new System.Drawing.Rectangle(0, 0, srcWidth, srcHeight),
            ImageLockMode.ReadOnly,
            DrawingPixelFormat.Format32bppArgb);
        var srcStride = srcData.Stride;
        var srcPixels = new byte[srcStride * srcHeight];
        Marshal.Copy(srcData.Scan0, srcPixels, 0, srcPixels.Length);
        sourceImage.UnlockBits(srcData);

        // WriteableBitmap にピクセルブロックを直接書き込む
        var dpi = new Vector(96, 96);
        var pixelSize = new PixelSize(loupeSize, loupeSize);
        var wb = new WriteableBitmap(pixelSize, dpi, AvaloniaPixelFormat.Bgra8888, AlphaFormat.Premul);

        using var locked = wb.Lock();
        var dstStride = locked.RowBytes;
        var dstPixels = new byte[dstStride * loupeSize];

        // 市松模様の背景を描画
        DrawCheckerBackground(dstPixels, loupeSize, dstStride, checkerSize);

        // ソース画像の各ピクセルを zoomLevel x zoomLevel ブロックに拡大して描画
        for (var sy = 0; sy < srcHeight; sy++)
        {
            for (var sx = 0; sx < srcWidth; sx++)
            {
                var srcOff = sy * srcStride + sx * 4;
                var b = srcPixels[srcOff];
                var g = srcPixels[srcOff + 1];
                var r = srcPixels[srcOff + 2];
                var a = srcPixels[srcOff + 3];

                // 透明ピクセル（α=0）は画像範囲外 → スキップして市松模様をそのまま残す
                if (a == 0) continue;

                var blockX = sx * zoomLevel;
                var blockY = sy * zoomLevel;
                var maxDx = Math.Min(zoomLevel, loupeSize - blockX);
                var maxDy = Math.Min(zoomLevel, loupeSize - blockY);

                for (var dy = 0; dy < maxDy; dy++)
                {
                    for (var dx = 0; dx < maxDx; dx++)
                    {
                        var dstOff = (blockY + dy) * dstStride + (blockX + dx) * 4;
                        dstPixels[dstOff] = b;
                        dstPixels[dstOff + 1] = g;
                        dstPixels[dstOff + 2] = r;
                        dstPixels[dstOff + 3] = a;
                    }
                }
            }
        }

        // 十字線を描画（幅 = zoomLevel、ピクセルブロックのグリッドに合わせる）
        DrawCrosshair(dstPixels, loupeSize, zoomLevel, dstStride, Color.TryParse(crossColor, out var cc) ? cc : Colors.Red, crossOpacity);

        // バッファを WriteableBitmap に書き込み
        Marshal.Copy(dstPixels, 0, locked.Address, dstPixels.Length);

        return wb;
    }

    /// <summary>
    /// 画像なし時の市松模様のみのルーペを描画する。
    /// </summary>
    /// <param name="loupeSize">ルーペの一辺のサイズ（ピクセル）</param>
    /// <param name="crossColor">十字線の色文字列</param>
    /// <param name="crossOpacity">十字線の不透明度</param>
    /// <param name="checkerSize">市松模様の1タイルサイズ</param>
    /// <returns>市松模様のみの Avalonia Bitmap</returns>
    public static Bitmap DrawLoupeCheckerboardOnly(
        int loupeSize,
        string crossColor,
        double crossOpacity,
        int checkerSize)
    {
        var dpi = new Vector(96, 96);
        var pixelSize = new PixelSize(loupeSize, loupeSize);
        var wb = new WriteableBitmap(pixelSize, dpi, AvaloniaPixelFormat.Bgra8888, AlphaFormat.Premul);

        using var locked = wb.Lock();
        var dstStride = locked.RowBytes;
        var dstPixels = new byte[dstStride * loupeSize];

        // 市松模様の背景
        DrawCheckerBackground(dstPixels, loupeSize, dstStride, checkerSize);

        Marshal.Copy(dstPixels, 0, locked.Address, dstPixels.Length);
        return wb;
    }

    /// <summary>市松模様の背景をピクセルバッファに描画する。</summary>
    /// <param name="checkerSize">市松模様の1タイルサイズ（表示ピクセル）</param>
    private static void DrawCheckerBackground(byte[] pixels, int size, int stride, int checkerSize)
    {
        // タイルサイズが1未満の場合はデフォルト値2を使用
        var cs = checkerSize < 1 ? 2 : checkerSize;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var isLight = (x / cs + y / cs) % 2 == 0;
                // 薄いグレー（#CC）とより薄いグレー（#E8）
                var gray = isLight ? (byte)0xCC : (byte)0xE8;
                var off = y * stride + x * 4;
                pixels[off] = gray;
                pixels[off + 1] = gray;
                pixels[off + 2] = gray;
                pixels[off + 3] = 255;
            }
        }
    }

    /// <summary>十字線を描画する（幅 = zoomLevel、ピクセルブロックのグリッドに合わせる）。</summary>
    /// <remarks>十字線色を不透明度でアルファブレンドし、下のピクセルブロックが透けて見えるようにする。</remarks>
    private static void DrawCrosshair(byte[] pixels, int loupeSize, int zoomLevel, int stride, Color color, double opacity)
    {
        // 中心に最も近いブロック境界を十字線の開始位置とする
        // 実際のソースピクセル数は ceil(loupeSize/zoomLevel) であり、
        // 整数除算 loupeSize/zoomLevel と一致しない場合があるため
        // Math.Ceiling で正しいブロック数を計算する
        var blocksAcross = (int)Math.Ceiling((double)loupeSize / zoomLevel);
        var centerBlock = blocksAcross / 2;
        var crosshairStart = centerBlock * zoomLevel;

        var cr = color.R;
        var cg = color.G;
        var cb = color.B;

        // 垂直線（x = crosshairStart 〜 crosshairStart + zoomLevel - 1）
        for (var y = 0; y < loupeSize; y++)
        {
            for (var dx = 0; dx < zoomLevel; dx++)
            {
                var px = crosshairStart + dx;
                if (px >= loupeSize) break;
                var off = y * stride + px * 4;
                // 既存ピクセル（背景）を読み取ってアルファブレンド
                var bgB = pixels[off];
                var bgG = pixels[off + 1];
                var bgR = pixels[off + 2];
                pixels[off] = (byte)(cb * opacity + bgB * (1 - opacity));
                pixels[off + 1] = (byte)(cg * opacity + bgG * (1 - opacity));
                pixels[off + 2] = (byte)(cr * opacity + bgR * (1 - opacity));
                pixels[off + 3] = 255;
            }
        }

        // 水平線（y = crosshairStart 〜 crosshairStart + zoomLevel - 1）
        for (var x = 0; x < loupeSize; x++)
        {
            for (var dy = 0; dy < zoomLevel; dy++)
            {
                var py = crosshairStart + dy;
                if (py >= loupeSize) break;
                var off = py * stride + x * 4;
                // 既存ピクセル（背景）を読み取ってアルファブレンド
                var bgB = pixels[off];
                var bgG = pixels[off + 1];
                var bgR = pixels[off + 2];
                pixels[off] = (byte)(cb * opacity + bgB * (1 - opacity));
                pixels[off + 1] = (byte)(cg * opacity + bgG * (1 - opacity));
                pixels[off + 2] = (byte)(cr * opacity + bgR * (1 - opacity));
                pixels[off + 3] = 255;
            }
        }
    }

}
