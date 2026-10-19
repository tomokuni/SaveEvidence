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
    internal void 帯_は4辺と4隅の画素を1本ずつ覆う()
    {
        // 10 DIP × 100%（scaling 1） → 影の広がりは 10 画素
        var bands = ShadowPlan.BuildDeviceBands(100, 100, 200, 50, scaling: 1);

        // 4 辺 × 10 画素 + 画像の端の画素を覆う 4 本（切り捨てた行・列の白い線を防ぐ）より多い
        Assert.True(bands.Count > 44);

        // どの帯も 1 画素の高さ（横の帯・隅の帯）か 1 画素の幅（縦の帯）である
        Assert.All(bands, b => Assert.True(b.Width == 1 || b.Height == 1));
    }

    [Fact]
    internal void 帯_は影の範囲を隙間なく一度だけ覆う()
    {
        // 画像の外側（影の範囲）を走査し、どの画素もちょうど 1 本の帯で塗られることを確かめる
        // （重なると隅だけ濃くなり、隙間があると背景の白い線が見える）
        var bands = ShadowPlan.BuildDeviceBands(100, 100, 20, 20, scaling: 1);
        var covered = new Dictionary<(int X, int Y), int>();
        foreach (var band in bands)
        {
            for (var y = band.Y; y < band.Y + band.Height; y++)
            {
                for (var x = band.X; x < band.X + band.Width; x++)
                {
                    covered[(x, y)] = covered.GetValueOrDefault((x, y)) + 1;
                }
            }
        }

        // 画像の外側 10 画素のリング（30 × 30 − 20 × 20 = 500 画素）
        for (var y = 90; y < 120; y++)
        {
            for (var x = 90; x < 120; x++)
            {
                if (x >= 100 && x < 120 && y >= 100 && y < 120)
                {
                    continue;
                }

                Assert.Equal(1, covered.GetValueOrDefault((x, y)));
            }
        }

        // 画像の端の行・列も 1 本だけ（画像が端の画素を切り捨てても下地が影になる）
        for (var x = 100; x < 120; x++)
        {
            Assert.Equal(1, covered.GetValueOrDefault((x, 100)));
            Assert.Equal(1, covered.GetValueOrDefault((x, 119)));
        }

        for (var y = 100; y < 120; y++)
        {
            Assert.Equal(1, covered.GetValueOrDefault((100, y)));
            Assert.Equal(1, covered.GetValueOrDefault((119, y)));
        }
    }

    [Fact]
    internal void 帯_は画像に接する画素が最も濃く外側が最も薄い()
    {
        var bands = ShadowPlan.BuildDeviceBands(100, 100, 200, 50, scaling: 1);

        // 上辺の帯（横の帯だけ。 y が 100 未満で高さ 1 画素）
        var top = bands.Where(b => b.Y < 100 && b.Height == 1).OrderByDescending(b => b.Y).ToList();
        Assert.NotEmpty(top);

        // 画像に接する画素（最も下）が最も濃い
        Assert.True(top[0].Argb >> 24 > (top[^1].Argb >> 24));

        // 外側（最も上）はほぼ透明（影の外側に線が見えない）
        Assert.InRange(top[^1].Argb >> 24, 0u, 4u);
    }

    [Fact]
    internal void 帯_は隅の濃さを斜めの距離で決める()
    {
        var bands = ShadowPlan.BuildDeviceBands(100, 100, 200, 50, scaling: 1);

        static uint ColorAt(IReadOnlyList<ShadowBand> bands, int x, int y)
            => bands.First(b => x >= b.X && x < b.X + b.Width && y >= b.Y && y < b.Y + b.Height).Argb;

        // 隅の最初の画素は辺の最初の画素と同じ濃さ（同じ濃さの変化を使う。 隅だけ濃くしない）
        Assert.Equal(ColorAt(bands, 200, 99), ColorAt(bands, 99, 99));

        // 隅も外側へ向けて薄くなる（四角い塊にならず、辺と同じ向きに変化する）
        Assert.True(ColorAt(bands, 99, 95) >> 24 > (ColorAt(bands, 95, 95) >> 24));
        Assert.True(ColorAt(bands, 95, 95) >> 24 > (ColorAt(bands, 91, 95) >> 24));

        // 対角線上でも隅の頂点から離れるほど薄くなる
        Assert.True(ColorAt(bands, 99, 99) >> 24 > (ColorAt(bands, 95, 95) >> 24));
        Assert.True(ColorAt(bands, 95, 95) >> 24 > (ColorAt(bands, 91, 91) >> 24));

        // 隅の各画素は 1 本の帯で塗られる（辺と重ねない）
        Assert.Equal(1, bands.Count(b => 99 >= b.X && 99 < b.X + b.Width && 99 >= b.Y && 99 < b.Y + b.Height));
    }

    [Fact]
    internal void 帯_は画像の端の画素も覆う()
    {
        var bands = ShadowPlan.BuildDeviceBands(100, 100, 200, 50, scaling: 1);

        // 画像の描画側は端の画素を切り捨てることがあり、その行・列が背景（白）のまま残るため
        Assert.Contains(bands, b => b.X == 100 && b.Y == 100 && b.Width == 200 && b.Height == 1);
        Assert.Contains(bands, b => b.X == 100 && b.Y == 149 && b.Width == 200 && b.Height == 1);
        Assert.Contains(bands, b => b.X == 100 && b.Y == 101 && b.Width == 1 && b.Height == 48);
        Assert.Contains(bands, b => b.X == 299 && b.Y == 101 && b.Width == 1 && b.Height == 48);
    }

    [Fact]
    internal void 帯_はDPIの倍率で画素数が変わる()
    {
        var bands = ShadowPlan.BuildDeviceBands(100, 100, 200, 50, scaling: 1.25);

        // 10 DIP × 125% = 12.5 → 12 画素（Math.Round の既定 = 偶数丸め）× 4 辺 + 端の 4 本 + 4 隅
        Assert.True(bands.Count > 52);
    }

    [Fact]
    internal void 帯_は画像が空なら作らない()
        => Assert.Empty(ShadowPlan.BuildDeviceBands(10, 10, 0, 40, scaling: 1));

    /// <summary>一覧用の計画（<c>EsUtil.Feature.Thumbnail</c>）と同じ値であることを検証する。</summary>
    /// <remarks>
    /// キャプチャページの画像と一覧のサムネイルは同じ見た目にする必要がある。 計画が 2 つあるため、<br/>
    /// 値を片方だけ変えると UI 版ごとに影がずれる（本テストが食い違いをビルドで止める）。
    /// </remarks>
    [Fact]
    internal void 一覧用の影の計画と同じ値を使う()
    {
        var other = EsUtil.Feature.Thumbnail.ThumbnailShadowPlan.Default;

        Assert.Equal(ShadowPlan.BlurRadius, other.BlurRadius);
        Assert.Equal(ShadowPlan.ColorArgb, other.ColorArgb);
        Assert.Equal(ShadowPlan.Insets.Left, other.BlurRadius);
        Assert.Equal(other.Edges.Count, ShadowPlan.Edges.Count);
        for (var i = 0; i < other.Edges.Count; i++)
        {
            Assert.Equal(other.Edges[i].Side.ToString(), ShadowPlan.Edges[i].Side.ToString());
            Assert.Equal(other.Edges[i].Extent, ShadowPlan.Edges[i].Extent);
            Assert.Equal(other.Edges[i].Stops.Count, ShadowPlan.Edges[i].Stops.Count);
            for (var j = 0; j < other.Edges[i].Stops.Count; j++)
            {
                Assert.Equal(other.Edges[i].Stops[j].Offset, ShadowPlan.Edges[i].Stops[j].Offset, 6);
                Assert.Equal(other.Edges[i].Stops[j].Argb, ShadowPlan.Edges[i].Stops[j].Argb);
            }
        }

        for (var step = 0; step <= (int)other.BlurRadius; step++)
        {
            Assert.Equal(other.AlphaAtDistance(step), ShadowPlan.AlphaAtDistance(step), 6);
        }
    }
}