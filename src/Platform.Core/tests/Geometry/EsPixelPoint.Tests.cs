using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary><see cref="EsPixelPoint"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 小数を扱う座標型への変換は本型が単一所有するため、成分が欠落・入れ替わりなく引き継がれることを検証する。
/// </remarks>
public class EsPixelPointTests
{
    /// <summary>整数の成分がそのまま小数の座標型へ引き継がれることを検証する。</summary>
    [Fact]
    public void ToEsPoint_成分をそのまま引き継ぐ()
    {
        var result = new EsPixelPoint(120, 340).ToEsPoint();

        Assert.Equal(120, result.X);
        Assert.Equal(340, result.Y);
    }

    /// <summary>負の座標（セカンダリモニタの原点）も引き継ぐことを検証する。</summary>
    [Fact]
    public void ToEsPoint_負の座標も引き継ぐ()
    {
        var result = new EsPixelPoint(-1920, -200).ToEsPoint();

        Assert.Equal(-1920, result.X);
        Assert.Equal(-200, result.Y);
    }

    /// <summary>原点は原点のまま変換されることを検証する。</summary>
    [Fact]
    public void ToEsPoint_原点は原点のまま()
    {
        Assert.Equal(default(EsPoint), new EsPixelPoint(0, 0).ToEsPoint());
    }
}
