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

            // 画像の端（位置 1）が最も濃い（画像のすぐ外側が最も濃い影になる）
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
    internal void 帯_は4辺それぞれ画素の数だけ作られる()
    {
        // 10 DIP × 100%（scaling 1） → 1 辺 10 画素 × 4 辺
        var bands = ShadowPlan.BuildDeviceBands(100, 100, 200, 50, scaling: 1);

        Assert.Equal(40, bands.Count);
        Assert.All(bands, b => Assert.True(b.Width == 1 || b.Height == 1));
    }

    [Fact]
    internal void 帯_は画像に接する画素が最も濃く外側が最も薄い()
    {
        var bands = ShadowPlan.BuildDeviceBands(100, 100, 200, 50, scaling: 1);

        // 上辺の帯（y が 100 未満）
        var top = bands.Where(b => b.Y < 100).OrderByDescending(b => b.Y).ToList();
        Assert.NotEmpty(top);

        // 画像に接する画素（最も下）が最も濃い
        Assert.True(top[0].Argb >> 24 > (top[^1].Argb >> 24));

        // 外側（最も上）はほぼ透明（影の外側に線が見えない）
        Assert.InRange(top[^1].Argb >> 24, 0u, 4u);
    }

    [Fact]
    internal void 帯_は画像の外側にだけ置かれる()
    {
        var bands = ShadowPlan.BuildDeviceBands(100, 100, 200, 50, scaling: 1);

        // 上辺・下辺は画像の幅ちょうど、左右の辺は画像の外側
        Assert.All(bands.Where(b => b.Y < 100), b => Assert.Equal(100, b.X));
        Assert.All(bands.Where(b => b.Y < 100), b => Assert.Equal(200, b.Width));
        Assert.All(bands.Where(b => b.Y >= 150), b => Assert.Equal(200, b.Width));
        Assert.All(bands.Where(b => b.X < 100), b => Assert.Equal(1, b.Width));
        Assert.All(bands.Where(b => b.X >= 300), b => Assert.Equal(1, b.Width));
    }

    [Fact]
    internal void 帯_はDPIの倍率で画素数が変わる()
    {
        var bands = ShadowPlan.BuildDeviceBands(100, 100, 200, 50, scaling: 1.25);

        // 10 DIP × 125% = 12.5 → 12 画素（Math.Round の既定 = 偶数丸め）× 4 辺
        Assert.Equal(48, bands.Count);
    }

    [Fact]
    internal void 帯_は画像が空なら作らない()
        => Assert.Empty(ShadowPlan.BuildDeviceBands(10, 10, 0, 40, scaling: 1));
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
    internal void 隅の矩形_は画像が空なら作らない()
        => Assert.Empty(ShadowPlan.BuildCornerFills(10, 10, 40, 0));
}
