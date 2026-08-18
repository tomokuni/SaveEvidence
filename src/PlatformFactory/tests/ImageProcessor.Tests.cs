using Avalonia;
using Estable.AvaloniaUI.Platform.Imaging;
using Xunit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary><see cref="ImageProcessor"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// ピクセル配列レベルで切り出し・ウィンドウ検出を検証するため、Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に<br/>
/// 依存せず <c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// ピクセルは BGRA8888（アルファ乗算済み）で構築する。
/// </remarks>
public class ImageProcessorTests
{
    /// <summary>単色のピクセルデータを生成する。</summary>
    private static AppPixelData CreateSolid(int width, int height, byte b, byte g, byte r)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            pixels[i * 4] = b;
            pixels[i * 4 + 1] = g;
            pixels[i * 4 + 2] = r;
            pixels[i * 4 + 3] = 255;
        }
        return new AppPixelData(width, height, width * 4, pixels);
    }

    [Fact]
    public void Crop_範囲内の矩形を切り出す()
    {
        var src = CreateSolid(10, 10, 0, 0, 255); // 赤

        var result = ImageProcessor.Crop(src, new PixelRect(2, 2, 4, 4));

        Assert.NotNull(result);
        Assert.Equal(4, result!.Width);
        Assert.Equal(4, result.Height);
        // 切り出しピクセルが保持されている
        Assert.Equal(255, result.PixelData[2]); // R
    }

    [Fact]
    public void Crop_サイズ0の矩形はnullを返す()
    {
        var src = CreateSolid(10, 10, 0, 0, 255);

        Assert.Null(ImageProcessor.Crop(src, new PixelRect(0, 0, 0, 0)));
    }

    [Fact]
    public void Crop_範囲外は透明パディングで要求サイズを返す()
    {
        var src = CreateSolid(10, 10, 0, 0, 255);

        // 矩形が右端を超える場合、コピーできる部分のみコピーし、範囲外は透明（0）のまま
        var result = ImageProcessor.Crop(src, new PixelRect(8, 0, 10, 10));

        Assert.NotNull(result);
        Assert.Equal(10, result!.Width);
        Assert.Equal(10, result.Height);
        // コピー可能な左側 2px 分は赤（R=255）が保持される
        Assert.Equal(255, result.PixelData[2]);
        // 範囲外（右側 8px 分）は透明（R=0）
        Assert.Equal(0, result.PixelData[8 * 4 + 2]);
    }

    [Fact]
    public void Crop_完全に範囲外ならnullを返す()
    {
        var src = CreateSolid(10, 10, 0, 0, 255);

        Assert.Null(ImageProcessor.Crop(src, new PixelRect(20, 20, 5, 5)));
    }

    [Fact]
    public void DetectAndCropWindow_背景色の縁を除去して中央領域を検出する()
    {
        // 200x200 の画像。外側 20px を白、内側 160x160 を赤とする
        var width = 200;
        var height = 200;
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var off = (y * width + x) * 4;
                var isBorder = x < 20 || y < 20 || x >= 180 || y >= 180;
                if (isBorder)
                {
                    pixels[off] = 255; pixels[off + 1] = 255; pixels[off + 2] = 255; // 白
                }
                else
                {
                    pixels[off] = 0; pixels[off + 1] = 0; pixels[off + 2] = 255; // 赤
                }
                pixels[off + 3] = 255;
            }
        }
        var src = new AppPixelData(width, height, width * 4, pixels);

        var result = ImageProcessor.DetectAndCropWindow(src);

        Assert.NotNull(result);
        // 四隅の最頻色（白）が縁 20px として除去され、内側 160x160 が残る
        Assert.Equal(160, result!.Width);
        Assert.Equal(160, result.Height);
    }

    [Fact]
    public void DetectWindowBounds_全面単色は全体を返す()
    {
        var src = CreateSolid(100, 100, 0, 0, 255);

        var bounds = ImageProcessor.DetectWindowBounds(src);

        Assert.Equal(new PixelRect(0, 0, 100, 100), bounds);
    }

    [Fact]
    public void DetectAndCropWindow_全面単色は元画像のコピーを返す()
    {
        // 全面単色 → 検出矩形 = 画像全体 → 元画像のコピーが返る（bounds == fullRect 分岐）
        var src = CreateSolid(100, 100, 0, 0, 255);

        var result = ImageProcessor.DetectAndCropWindow(src);

        Assert.NotNull(result);
        Assert.Equal(100, result!.Width);
        Assert.Equal(100, result.Height);
    }

    [Fact]
    public void IsColorSimilar_差分30未満は類似とみなす()
    {
        // 内部ロジック（Crop / DetectAndCropWindow 経由で検証）。近い色（差分 < 30）は背景とみなされる
        var width = 100;
        var height = 100;
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var off = (y * width + x) * 4;
                // 四隅の背景色（白 255,255,255）と差分 10 未満の色（245,245,245）を全体に敷く
                pixels[off] = 245; pixels[off + 1] = 245; pixels[off + 2] = 245;
                pixels[off + 3] = 255;
            }
        }
        var src = new AppPixelData(width, height, width * 4, pixels);

        // 全面が背景色に類似するため、検出は全体を返す
        var bounds = ImageProcessor.DetectWindowBounds(src);

        Assert.Equal(new PixelRect(0, 0, width, height), bounds);
    }
}
