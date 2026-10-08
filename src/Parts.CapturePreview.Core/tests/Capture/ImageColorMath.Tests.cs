using EsUtil.Platform.Core.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.Capture;

/// <summary><see cref="ImageColorMath"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>標本間引きの規則と、空画像のフォールバックを固定する。</remarks>
public class ImageColorMathTests
{
    /// <summary>単色で塗った画像を生成する。</summary>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <param name="color">塗る色（不透明）。</param>
    /// <returns>生成した画像（破棄は呼び出し側）。</returns>
    private static EsWritableBitmap CreateFilled(int width, int height, DrawingColor color)
        => EsWritableBitmap.Create(width, height, color);

    [Fact]
    internal void AverageColor_単色画像はその色を返す()
    {
        using var image = CreateFilled(40, 30, DrawingColor.FromArgb(255, 10, 20, 30));

        var color = ImageColorMath.AverageColor(image);

        Assert.Equal(255, color.A);
        Assert.Equal(10, color.R);
        Assert.Equal(20, color.G);
        Assert.Equal(30, color.B);
    }

    [Fact]
    internal void AverageColor_2色の画像は平均を返す()
    {
        // 左半分を黒・右半分を白にする（標本間引きで概ね中間になる）
        using var image = EsWritableBitmap.Create(80, 80);
        for (var y = 0; y < 80; y++)
        {
            for (var x = 0; x < 80; x++)
            {
                var value = x < 40 ? (byte)0 : (byte)255;
                image.SetPixel(x, y, value, value, value, 0xFF);
            }
        }

        var color = ImageColorMath.AverageColor(image);

        Assert.InRange(color.R, 100, 155);
        Assert.Equal(color.R, color.G);
        Assert.Equal(color.R, color.B);
    }

    [Fact]
    internal void AverageColor_アルファは不透明で返す()
    {
        using var image = CreateFilled(10, 10, DrawingColor.FromArgb(128, 1, 2, 3));

        var color = ImageColorMath.AverageColor(image);

        Assert.Equal(255, color.A);
    }

    [Fact]
    internal void AverageColor_画像がnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() => ImageColorMath.AverageColor(null!));
    }

    [Fact]
    internal void SampleDivisor_は40である()
    {
        Assert.Equal(40, ImageColorMath.SampleDivisor);
    }
}
