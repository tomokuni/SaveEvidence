using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="ShadowPlan"/> の仕様テスト（画像の 4 辺へ落とす影の描画計画）。</summary>
/// <remarks>
/// 4 つの UI 版が同じ計画から描くため、幅・濃さ・矩形の位置そのものを固定する<br/>
/// （片方だけ変えると UI 版ごとに影の見た目がずれる）。
/// </remarks>
public class ShadowPlanTests
{
    [Fact]
    internal void 影_は下方向へずらす()
        => Assert.True(ShadowPlan.OffsetY > 0);

    [Fact]
    internal void 影_は半透明の黒を使う()
    {
        Assert.Equal(0x00u, (ShadowPlan.ColorArgb >> 16) & 0xFF);
        Assert.Equal(0x00u, (ShadowPlan.ColorArgb >> 8) & 0xFF);
        Assert.Equal(0x00u, ShadowPlan.ColorArgb & 0xFF);
        Assert.InRange(ShadowPlan.ColorArgb >> 24, 0x40u, 0xBFu);
    }

    [Fact]
    internal void 影の濃さ_は端で最も濃く外側へ向けて薄くなる()
    {
        var near = ShadowPlan.AlphaAtDistance(1);
        var middle = ShadowPlan.AlphaAtDistance(4);
        var far = ShadowPlan.AlphaAtDistance(ShadowPlan.BlurRadius);

        Assert.True(near > middle);
        Assert.True(middle > far);
    }

    [Fact]
    internal void 影の濃さ_は端で2割程度()
        => Assert.InRange(ShadowPlan.AlphaAtDistance(1), 0.15, 0.30);

    [Fact]
    internal void 影の濃さ_はぼかしの外側で0になる()
    {
        Assert.InRange(ShadowPlan.AlphaAtDistance(ShadowPlan.BlurRadius), 0.0, 0.05);
        Assert.Equal(0.0, ShadowPlan.AlphaAtDistance(ShadowPlan.BlurRadius + 1));
    }

    [Fact]
    internal void 影の色_は濃さを透過度として持つ()
    {
        var color = ShadowPlan.ColorAtDistance(1);

        Assert.Equal(0x000000u, color & 0x00FFFFFF);
        Assert.InRange(color >> 24, 0x26u, 0x4Du);
    }

    [Fact]
    internal void 辺の幅_は下辺が最も広く上辺が最も狭い()
    {
        Assert.Equal(ShadowPlan.BlurRadius + ShadowPlan.OffsetY, ShadowPlan.Insets.Bottom);
        Assert.Equal(ShadowPlan.BlurRadius - ShadowPlan.OffsetY, ShadowPlan.Insets.Top);
        Assert.Equal(ShadowPlan.BlurRadius, ShadowPlan.Insets.Left);
        Assert.Equal(ShadowPlan.Insets.Left, ShadowPlan.Insets.Right);
    }

    [Fact]
    internal void 辺の停止点_は外側が0で画像の端が1になる()
    {
        foreach (var edge in ShadowPlan.Edges)
        {
            Assert.Equal(0.0, edge.Stops[0].Offset, 3);
            Assert.Equal(1.0, edge.Stops[^1].Offset, 3);
            Assert.True(edge.Stops[^1].Argb >> 24 > edge.Stops[0].Argb >> 24);
        }
    }

    [Fact]
    internal void 辺の停止点_は外側から内側へ濃くなる()
    {
        foreach (var edge in ShadowPlan.Edges)
        {
            for (var i = 1; i < edge.Stops.Count; i++)
            {
                Assert.True(edge.Stops[i].Offset > edge.Stops[i - 1].Offset);
                Assert.True(edge.Stops[i].Argb >> 24 >= edge.Stops[i - 1].Argb >> 24);
            }
        }
    }

    [Fact]
    internal void 上辺の濃さ_は下辺より薄い()
    {
        var top = ShadowPlan.Edge(ShadowSide.Top);
        var bottom = ShadowPlan.Edge(ShadowSide.Bottom);

        Assert.True(top.Stops[^1].Argb >> 24 < bottom.Stops[^1].Argb >> 24);
    }

    [Fact]
    internal void 矩形_は4辺を作り画像へ掛からない()
    {
        var fills = ShadowPlan.BuildEdgeRects(100, 100, 200, 50);

        Assert.Equal(4, fills.Count);

        var top = fills.Single(f => f.Edge.Side == ShadowSide.Top);
        var bottom = fills.Single(f => f.Edge.Side == ShadowSide.Bottom);
        var left = fills.Single(f => f.Edge.Side == ShadowSide.Left);
        var right = fills.Single(f => f.Edge.Side == ShadowSide.Right);

        Assert.Equal(100 - ShadowPlan.Insets.Top, top.Y);
        Assert.Equal(100, top.Y + top.Height);
        Assert.Equal(100 + 50, bottom.Y);
        Assert.Equal(100 - ShadowPlan.Insets.Left, left.X);
        Assert.Equal(100, left.X + left.Width);
        Assert.Equal(300, right.X);
        Assert.Equal(200 + ShadowPlan.Insets.Left + ShadowPlan.Insets.Right, top.Width);
        Assert.Equal(50, left.Height);
    }

    [Fact]
    internal void 矩形_は画像が空なら作らない()
        => Assert.Empty(ShadowPlan.BuildEdgeRects(10, 10, 0, 40));
}
