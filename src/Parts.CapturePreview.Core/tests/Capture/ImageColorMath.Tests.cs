using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.Capture;

/// <summary><see cref="ImageColorMath"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>標本間引きの規則と、空画像のフォールバックを固定する。</remarks>
public class ImageColorMathTests
{
    /// <summary>単色で塗った画像を生成する。</summary>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <param name="color">塗る色（不透明）。</param>
    /// <returns>生成した画像（読み取りの面。 破棄は呼び出し側）。</returns>
    /// <remarks>名前を <c>CreateFilled</c> にしない（コアの充填の入口（確保と充填を 1 回で行う）と紛らわしいため）。</remarks>
    private static EsReadOnlyBitmap CreateSolidBitmap(int width, int height, DrawingColor color)
    {
        using var bitmap = EsBitmap.Create(width, height, color);
        bitmap.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(bitmap);
    }

    [Fact]
    internal void AverageColor_単色画像はその色を返す()
    {
        using var image = CreateSolidBitmap(40, 30, DrawingColor.FromArgb(255, 10, 20, 30));

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
        using var image = EsBitmap.Create(80, 80);
        for (var y = 0; y < 80; y++)
        {
            for (var x = 0; x < 80; x++)
            {
                var value = x < 40 ? (byte)0 : (byte)255;
                image.SetPixel(x, y, value, value, value, 0xFF);
            }
        }
        image.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(image);

        var color = ImageColorMath.AverageColor(read);

        Assert.InRange(color.R, 100, 155);
        Assert.Equal(color.R, color.G);
        Assert.Equal(color.R, color.B);
    }

    [Fact]
    internal void AverageColor_アルファは不透明で返す()
    {
        using var image = CreateSolidBitmap(10, 10, DrawingColor.FromArgb(128, 1, 2, 3));

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

    [Fact]
    internal void Composite_不透明度0なら下地と同じ色()
    {
        var color = ImageColorMath.Composite(
            DrawingColor.FromArgb(255, 100, 150, 200), DrawingColor.FromArgb(0, 255, 0, 0));

        Assert.Equal(255, color.A);
        Assert.Equal(100, color.R);
        Assert.Equal(150, color.G);
        Assert.Equal(200, color.B);
    }

    [Fact]
    internal void Composite_不透明度255なら重ねる色()
    {
        var color = ImageColorMath.Composite(
            DrawingColor.FromArgb(255, 100, 150, 200), DrawingColor.FromArgb(255, 255, 0, 0));

        Assert.Equal(255, color.R);
        Assert.Equal(0, color.G);
        Assert.Equal(0, color.B);
    }

    [Fact]
    internal void Composite_半透明の黒は下地を半分にする()
    {
        var color = ImageColorMath.Composite(
            DrawingColor.FromArgb(255, 200, 100, 20), DrawingColor.FromArgb(128, 0, 0, 0));

        // 200 * 0.498 ≈ 100 / 100 * 0.498 ≈ 50 / 20 * 0.498 ≈ 10
        Assert.Equal(100, color.R);
        Assert.Equal(50, color.G);
        Assert.Equal(10, color.B);
    }
}
