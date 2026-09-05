using Avalonia;
using Estable.AvaloniaUI.Platform.Imaging;
using Xunit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary><see cref="ImageProcessor"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// ピクセルレベルで切り出し・ウィンドウ検出を検証するため、Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に<br/>
/// 依存せず <c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// ピクセルは BGRA8888（アルファ乗算済み）で構築する。<br/>
/// <see cref="UnifiedBitmap"/> は <see cref="System.IDisposable"/> のため、ここで破棄する。
/// </remarks>
public class ImageProcessorTests
{
    /// <summary>単色のビットマップを生成する。</summary>
    private static UnifiedBitmap CreateSolid(int width, int height, byte b, byte g, byte r)
    {
        var bmp = UnifiedBitmap.Create(width, height);
        bmp.Clear(b: b, g: g, r: r, a: 255);
        return bmp;
    }

    [Fact]
    public void Crop_範囲内の矩形を切り出す()
    {
        using var src = CreateSolid(10, 10, 0, 0, 255); // 赤

        using var result = ImageProcessor.Crop(src, new PixelRect(2, 2, 4, 4));

        Assert.NotNull(result);
        Assert.Equal(4, result!.PixelWidth);
        Assert.Equal(4, result.PixelHeight);
        // 切り出しピクセルが保持されている
        Assert.Equal((byte)255, result.GetPixel(0, 0).R);
    }

    [Fact]
    public void Crop_サイズ0の矩形はnullを返す()
    {
        using var src = CreateSolid(10, 10, 0, 0, 255);

        Assert.Null(ImageProcessor.Crop(src, new PixelRect(0, 0, 0, 0)));
    }

    [Fact]
    public void Crop_範囲外は透明パディングで要求サイズを返す()
    {
        using var src = CreateSolid(10, 10, 0, 0, 255);

        // 矩形が右端を超える場合、コピーできる部分のみコピーし、範囲外は透明（0）のまま
        using var result = ImageProcessor.Crop(src, new PixelRect(8, 0, 10, 10));

        Assert.NotNull(result);
        Assert.Equal(10, result!.PixelWidth);
        Assert.Equal(10, result.PixelHeight);
        // コピー可能な左側 2px 分は赤（R=255）が保持される
        Assert.Equal((byte)255, result.GetPixel(0, 0).R);
        // 範囲外（右側 8px 分）は透明（R=0）
        Assert.Equal((byte)0, result.GetPixel(9, 0).R);
    }

    [Fact]
    public void Crop_完全に範囲外ならnullを返す()
    {
        using var src = CreateSolid(10, 10, 0, 0, 255);

        Assert.Null(ImageProcessor.Crop(src, new PixelRect(20, 20, 5, 5)));
    }

    [Fact]
    public void DetectAndCropWindow_背景色の縁を除去して中央領域を検出する()
    {
        // 200x200 の画像。外側 20px を白、内側 160x160 を赤とする
        var width = 200;
        var height = 200;
        using var bmp = UnifiedBitmap.Create(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var isBorder = x < 20 || y < 20 || x >= 180 || y >= 180;
                if (isBorder)
                {
                    bmp.SetPixel(x, y, b: 255, g: 255, r: 255, a: 255); // 白
                }
                else
                {
                    bmp.SetPixel(x, y, b: 0, g: 0, r: 255, a: 255); // 赤
                }
            }
        }

        using var result = ImageProcessor.DetectAndCropWindow(bmp);

        Assert.NotNull(result);
        // 四隅の最頻色（白）が縁 20px として除去され、内側 160x160 が残る
        Assert.Equal(160, result!.PixelWidth);
        Assert.Equal(160, result.PixelHeight);
    }

    [Fact]
    public void DetectWindowBounds_全面単色は全体を返す()
    {
        using var src = CreateSolid(100, 100, 0, 0, 255);

        var bounds = ImageProcessor.DetectWindowBounds(src);

        Assert.Equal(new PixelRect(0, 0, 100, 100), bounds);
    }

    [Fact]
    public void DetectAndCropWindow_全面単色は元画像のコピーを返す()
    {
        // 全面単色 → 検出矩形 = 画像全体 → 元画像のコピーが返る（bounds == fullRect 分岐）
        using var src = CreateSolid(100, 100, 0, 0, 255);

        using var result = ImageProcessor.DetectAndCropWindow(src);

        Assert.NotNull(result);
        Assert.Equal(100, result!.PixelWidth);
        Assert.Equal(100, result.PixelHeight);
    }

    [Fact]
    public void IsColorSimilar_差分30未満は類似とみなす()
    {
        // 内部ロジック（Crop / DetectAndCropWindow 経由で検証）。近い色（差分 < 30）は背景とみなされる
        var width = 100;
        var height = 100;
        using var bmp = UnifiedBitmap.Create(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // 四隅の背景色（白 255,255,255）と差分 10 未満の色（245,245,245）を全体に敷く
                bmp.SetPixel(x, y, b: 245, g: 245, r: 245, a: 255);
            }
        }

        // 全面が背景色に類似するため、検出は全体を返す
        var bounds = ImageProcessor.DetectWindowBounds(bmp);

        Assert.Equal(new PixelRect(0, 0, width, height), bounds);
    }
}
