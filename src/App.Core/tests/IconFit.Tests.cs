using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="IconFit"/> の単体テスト（外接矩形を枠いっぱいへ広げる変換）。</summary>
public class IconFitTests
{
    /// <summary>横長の図形は横幅が枠いっぱいになり、上下が均等に余ることを検証する。</summary>
    [Fact]
    internal void Resolve_横長の図形は横幅いっぱいになる()
    {
        var fit = IconFit.Resolve(new EsRect(0, 0, 20, 10), 40);

        Assert.NotNull(fit);
        Assert.Equal(2.0, fit.Value.Scale, 6);
        Assert.Equal(0.0, fit.Value.OffsetX, 6);
        // 高さは 20 ピクセルになり、残りの 20 ピクセルが上下へ 10 ずつ割り付く
        Assert.Equal(10.0, fit.Value.OffsetY, 6);
    }

    /// <summary>図形が原点から離れている場合は、平行移動で枠の中央へ寄せることを検証する。</summary>
    [Fact]
    internal void Resolve_原点から離れた図形も枠の中央へ寄せる()
    {
        // 外接矩形が (4, 4) から始まる 16x16（viewBox 24x24 の中の余白付きのアイコン）
        var fit = IconFit.Resolve(new EsRect(4, 4, 16, 16), 32);

        Assert.NotNull(fit);
        Assert.Equal(2.0, fit.Value.Scale, 6);
        // 拡大後の左上は (8, 8)。 中央へ寄せるには -8 だけ動かす
        Assert.Equal(-8.0, fit.Value.OffsetX, 6);
        Assert.Equal(-8.0, fit.Value.OffsetY, 6);
    }

    /// <summary>退化した外接矩形（幅または高さが 0）は null になることを検証する。</summary>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 10)]
    internal void Resolve_退化した矩形はnullになる(int width, int height)
        => Assert.Null(IconFit.Resolve(new EsRect(0, 0, width, height), 32));
}
