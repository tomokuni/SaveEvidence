using EsUtil.SaveEvidence.App.WinForms.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinForms.Tests;

/// <summary>GDI+ のビットマップとコアの画像の相互変換を検証するテスト。</summary>
/// <remarks>描画は <see cref="Bitmap"/> と <c>Graphics.FromImage</c> だけで完結するため、ウィンドウを必要としない。</remarks>
public class GdiBitmapInteropTests
{
    /// <summary>ビットマップの画素がコアの画像へ同じ並びで複製されることを検証する。</summary>
    [Fact]
    public void ビットマップの画素が同じ並びで複製される()
    {
        using var bitmap = TestHelper.CreateBitmap(3, 2, Color.FromArgb(255, 10, 20, 30));

        using var image = GdiBitmapInterop.ToEsReadOnlyBitmap(bitmap);

        Assert.Equal(3, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal((byte)10, image.GetPixel(1, 1).R);
        Assert.Equal((byte)20, image.GetPixel(1, 1).G);
        Assert.Equal((byte)30, image.GetPixel(1, 1).B);
        Assert.Equal((byte)255, image.GetPixel(1, 1).A);
    }

    /// <summary>コアの画像が GDI+ のビットマップへ同じ画素で複製されることを検証する。</summary>
    [Fact]
    public void コアの画像が同じ画素で複製される()
    {
        using var source = TestHelper.CreateEsBitmap(4, 3);

        using var bitmap = GdiBitmapInterop.ToDrawingBitmap(source);

        Assert.Equal(4, bitmap.Width);
        Assert.Equal(3, bitmap.Height);
        Assert.Equal(source.GetPixel(2, 1).R, bitmap.GetPixel(2, 1).R);
        Assert.Equal(source.GetPixel(2, 1).G, bitmap.GetPixel(2, 1).G);
        Assert.Equal(source.GetPixel(2, 1).B, bitmap.GetPixel(2, 1).B);
    }

    /// <summary>書き換え可能な画像としても同じ画素で複製されることを検証する。</summary>
    [Fact]
    public void 書き換え可能な画像として複製される()
    {
        using var bitmap = TestHelper.CreateBitmap(2, 2, Color.FromArgb(255, 1, 2, 3));

        using var image = GdiBitmapInterop.ToEsBitmap(bitmap);

        Assert.False(image.IsReadOnly);
        Assert.Equal(2, image.Width);
        Assert.Equal(2, image.Height);
    }

    /// <summary>往復しても画素が変わらないことを検証する。</summary>
    [Fact]
    public void 往復しても画素が変わらない()
    {
        using var original = TestHelper.CreateBitmap(5, 5, Color.FromArgb(255, 200, 100, 50));
        using var image = GdiBitmapInterop.ToEsReadOnlyBitmap(original);

        using var roundTrip = GdiBitmapInterop.ToDrawingBitmap(image);

        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                Assert.Equal(original.GetPixel(x, y).ToArgb(), roundTrip.GetPixel(x, y).ToArgb());
            }
        }
    }

    /// <summary>引数が null の場合は例外になることを検証する。</summary>
    [Fact]
    public void 引数がnullの場合は例外になる()
        => Assert.Throws<ArgumentNullException>(() => GdiBitmapInterop.ToDrawingBitmap(null!));
}
