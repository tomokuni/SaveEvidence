using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="ShadowPlan"/> の仕様テスト（画像の 4 辺・4 隅へ落とす影の描画計画）。</summary>
/// <remarks>
/// 4 つの UI 版が同じ計画から描くため、幅・濃さ・矩形の位置そのものを固定する<br/>
/// （片方だけ変えると UI 版ごとに影の見た目がずれる）。
/// </remarks>
public class ShadowPlanTests
{
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
    internal void 余白_は4辺とも同じ()
    {
        Assert.Equal(ShadowPlan.BlurRadius, ShadowPlan.Insets.Left);
        Assert.Equal(ShadowPlan.Insets.Left, ShadowPlan.Insets.Top);
        Assert.Equal(ShadowPlan.Insets.Left, ShadowPlan.Insets.Right);
        Assert.Equal(ShadowPlan.Insets.Left, ShadowPlan.Insets.Bottom);
    }

    [Fact]
    internal void 辺_は4つあり幅も濃さも同じ()
    {
        Assert.Equal(4, ShadowPlan.Edges.Count);

        foreach (var edge in ShadowPlan.Edges)
        {
            Assert.Equal(ShadowPlan.BlurRadius, edge.Extent);

            // 画像の端（位置 1）が最も濃い
            Assert.Equal(ShadowPlan.ColorAtDistance(1), edge.Stops[^1].Argb);
            Assert.Equal(1.0, edge.Stops[^1].Offset, 3);
        }

        // 4 辺の濃さは一致する（上辺だけ薄くすると画像との間に白っぽい隙間が見える）
        Assert.Equal(
            ShadowPlan.Edge(ShadowSide.Top).Stops[^1].Argb,
            ShadowPlan.Edge(ShadowSide.Bottom).Stops[^1].Argb);
    }

    [Fact]
    internal void 辺の停止点_は外側が0で外側ほど薄い()
    {
        foreach (var edge in ShadowPlan.Edges)
        {
            Assert.Equal(0.0, edge.Stops[0].Offset, 3);
            for (var i = 1; i < edge.Stops.Count; i++)
            {
                Assert.True(edge.Stops[i].Offset > edge.Stops[i - 1].Offset);
                Assert.True(edge.Stops[i].Argb >> 24 >= edge.Stops[i - 1].Argb >> 24);
            }
        }
    }

    [Fact]
    internal void 隅の停止点_は中心が最も濃く半径で0に近づく()
    {
        var stops = ShadowPlan.CornerStops;

        Assert.Equal(0.0, stops[0].Offset, 3);
        Assert.Equal(ShadowPlan.ColorAtDistance(1), stops[0].Argb);
        Assert.Equal(1.0, stops[^1].Offset, 3);
        Assert.True(stops[^1].Argb >> 24 < 0x10);
    }

    [Fact]
    internal void 辺の矩形_は画像の幅と高さいっぱい()
    {
        var fills = ShadowPlan.BuildEdgeRects(100, 100, 200, 50);

        Assert.Equal(4, fills.Count);

        var top = fills.Single(f => f.Edge.Side == ShadowSide.Top);
        var bottom = fills.Single(f => f.Edge.Side == ShadowSide.Bottom);
        var left = fills.Single(f => f.Edge.Side == ShadowSide.Left);
        var right = fills.Single(f => f.Edge.Side == ShadowSide.Right);

        Assert.Equal(100, top.X);
        Assert.Equal(100 - ShadowPlan.BlurRadius, top.Y);
        Assert.Equal(200, top.Width);
        Assert.Equal(100, top.Y + top.Height);

        Assert.Equal(100, bottom.X);
        Assert.Equal(150, bottom.Y);
        Assert.Equal(200, bottom.Width);

        Assert.Equal(100 - ShadowPlan.BlurRadius, left.X);
        Assert.Equal(100, left.Y);
        Assert.Equal(50, left.Height);

        Assert.Equal(300, right.X);
        Assert.Equal(50, right.Height);
    }

    [Fact]
    internal void 隅の矩形_は中心が画像の隅の頂点になる()
    {
        var fills = ShadowPlan.BuildCornerFills(100, 100, 200, 50);

        Assert.Equal(4, fills.Count);
        Assert.All(fills, f => Assert.Equal(ShadowPlan.BlurRadius, f.Size));

        // 中心（隅の矩形に対する相対位置）が画像の隅の頂点を指す
        Assert.All(fills, f =>
            Assert.InRange((f.X + (f.CenterX * f.Size)) - 100, -0.001, 200.001));
        Assert.All(fills, f =>
            Assert.InRange((f.Y + (f.CenterY * f.Size)) - 100, -0.001, 50.001));

        Assert.Contains(fills, f => f.X < 100 && f.Y < 100);   // 左上
        Assert.Contains(fills, f => f.X >= 300 && f.Y < 100);  // 右上
        Assert.Contains(fills, f => f.X >= 300 && f.Y >= 150); // 右下
        Assert.Contains(fills, f => f.X < 100 && f.Y >= 150);  // 左下
    }

    [Fact]
    internal void 矩形_は画像が空なら作らない()
    {
        Assert.Empty(ShadowPlan.BuildEdgeRects(10, 10, 0, 40));
        Assert.Empty(ShadowPlan.BuildCornerFills(10, 10, 40, 0));
    }
}
