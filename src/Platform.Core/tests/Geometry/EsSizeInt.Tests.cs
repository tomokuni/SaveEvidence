using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Geometry;

/// <summary><see cref="EsSizeInt"/> の単体テスト（整数のサイズと空の判定・DIP サイズへの変換）。</summary>
/// <remarks>UI 初期化が不要な純粋な値型のため、<c>[Fact]</c> で並列実行される。</remarks>
public class EsSizeIntTests
{
    [Fact]
    public void 成分をそのまま保持する()
    {
        var size = new EsSizeInt(1920, 1080);

        Assert.Equal(1920, size.Width);
        Assert.Equal(1080, size.Height);
    }

    [Fact]
    public void 等価性は値で判定される()
    {
        Assert.Equal(new EsSizeInt(4, 8), new EsSizeInt(4, 8));
        Assert.NotEqual(new EsSizeInt(4, 8), new EsSizeInt(8, 4));
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(4, 0)]
    [InlineData(-1, 8)]
    [InlineData(4, -1)]
    public void IsEmpty_幅または高さが0以下なら真(int width, int height)
    {
        Assert.True(new EsSizeInt(width, height).IsEmpty);
    }

    [Fact]
    public void IsEmpty_幅と高さがともに正なら偽()
    {
        Assert.False(new EsSizeInt(1, 1).IsEmpty);
    }

    [Fact]
    public void ToEsSize_成分をそのまま引き継ぐ()
    {
        var size = new EsSizeInt(1920, 1080).ToEsSize();

        Assert.Equal(1920.0, size.Width);
        Assert.Equal(1080.0, size.Height);
    }

    [Fact]
    public void ToEsSize_負の寸法も引き継ぐ()
    {
        var size = new EsSizeInt(-3, -4).ToEsSize();

        Assert.Equal(-3.0, size.Width);
        Assert.Equal(-4.0, size.Height);
    }
}
