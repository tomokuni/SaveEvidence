using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.ZoomView;

/// <summary><see cref="ZoomViewAppearance"/> の仕様テスト（外観の単一所有と市松模様のタイル規則）。</summary>
/// <remarks>
/// タイルサイズの意味（0 以下 = 描かない / 1 は下限へ切り上げ）はコアが単一所有するため、両 UI のホストが<br/>
/// 同じ規則を共有することを本テストで固定する（UI ごとに「0 の意味」がずれる事故の防止）。<br/>
/// UI 初期化を必要としないため、Avalonia ヘッドレスに依存せず並列実行できる。
/// </remarks>
public class ZoomViewAppearanceTests
{
    [Fact]
    internal void Default_は市松模様を描かない()
    {
        var appearance = ZoomViewAppearance.Default;

        Assert.Equal(0, appearance.CheckerSize);
        Assert.False(appearance.HasCheckerboard);
        Assert.Equal(ZoomViewAppearance.DefaultScrollBarThickness, appearance.ScrollBarThickness);
        Assert.Equal(ZoomViewAppearance.DefaultScrollBarMargin, appearance.ScrollBarMargin);
        Assert.True(appearance.ShowScrollBars);
    }

    [Fact]
    internal void MinimumCheckerTileSize_は2である()
    {
        Assert.Equal(2, ZoomViewAppearance.MinimumCheckerTileSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    internal void ResolveCheckerTileSize_0以下は描かない0を返す(int checkerSize)
    {
        Assert.Equal(0, ZoomViewAppearance.ResolveCheckerTileSize(checkerSize));
    }

    [Fact]
    internal void ResolveCheckerTileSize_1は下限の2へ切り上げる()
    {
        Assert.Equal(ZoomViewAppearance.MinimumCheckerTileSize, ZoomViewAppearance.ResolveCheckerTileSize(1));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    internal void ResolveCheckerTileSize_下限以上はそのまま返す(int checkerSize)
    {
        Assert.Equal(checkerSize, ZoomViewAppearance.ResolveCheckerTileSize(checkerSize));
    }

    [Theory]
    [InlineData(-3)]
    [InlineData(0)]
    internal void HasCheckerboard_実効タイルサイズが0なら描かない(int checkerSize)
    {
        var appearance = ZoomViewAppearance.Default with { CheckerSize = checkerSize };

        Assert.False(appearance.HasCheckerboard);
        Assert.Empty(appearance.ComputeLightTileRects(new EsRect(0, 0, 50, 50)));
    }

    [Fact]
    internal void HasCheckerboard_1は実効値2として描く()
    {
        var appearance = ZoomViewAppearance.Default with { CheckerSize = 1 };

        // 「1 = 描かない」ではない（1 ピクセル幅の市松は縞に見えるため下限へ切り上げる）
        Assert.True(appearance.HasCheckerboard);

        var tiles = appearance.ComputeLightTileRects(new EsRect(0, 0, 4, 4));

        Assert.All(tiles, tile => Assert.Equal(ZoomViewAppearance.MinimumCheckerTileSize, tile.Width));
    }

    [Fact]
    internal void ComputeLightTileRects_明暗の配置規則を単一所有する()
    {
        var appearance = ZoomViewAppearance.Default with { CheckerSize = 10 };

        var tiles = appearance.ComputeLightTileRects(new EsRect(0, 0, 25, 25));

        // 3x3 のタイルのうち (列 + 行) が偶数の 5 枚が明色
        Assert.Equal(5, tiles.Count);
        Assert.Contains(tiles, tile => tile.X == 0 && tile.Y == 0);
        Assert.Contains(tiles, tile => tile.X == 20 && tile.Y == 0);
        Assert.Contains(tiles, tile => tile.X == 10 && tile.Y == 10);
        Assert.Contains(tiles, tile => tile.X == 0 && tile.Y == 20);
        Assert.Contains(tiles, tile => tile.X == 20 && tile.Y == 20);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(50, 0)]
    [InlineData(0, 0)]
    [InlineData(-1, 50)]
    internal void ComputeLightTileRects_領域が空なら空になる(double width, double height)
    {
        var appearance = ZoomViewAppearance.Default with { CheckerSize = 8 };

        Assert.Empty(appearance.ComputeLightTileRects(new EsRect(0, 0, width, height)));
    }

    /// <summary>タイルが領域で切り取られ、領域の外へはみ出さないことを検証する。</summary>
    [Fact]
    internal void ComputeLightTileRects_領域の外へはみ出さない()
    {
        var appearance = ZoomViewAppearance.Default with { CheckerSize = 10 };

        // 格子の途中で切れた領域（画像の表示矩形の一部が画面外等）
        var tiles = appearance.ComputeLightTileRects(new EsRect(5, 5, 10, 10));

        Assert.All(tiles, tile =>
        {
            Assert.True(tile.X >= 5 && tile.Right <= 15, $"X が領域外: {tile}");
            Assert.True(tile.Y >= 5 && tile.Bottom <= 15, $"Y が領域外: {tile}");
        });
        // 格子はビューポート原点基準のため、(0,0) と (1,1) の明色タイルが領域で切り取られる
        Assert.Equal(2, tiles.Count);
        Assert.Contains(new EsRect(5, 5, 5, 5), tiles);
        Assert.Contains(new EsRect(10, 10, 5, 5), tiles);
    }

    /// <summary>格子の原点がビューポートの原点であることを検証する（領域が動いても模様が這わない）。</summary>
    [Fact]
    internal void ComputeLightTileRects_格子はビューポート原点を基準にする()
    {
        var appearance = ZoomViewAppearance.Default with { CheckerSize = 10 };

        // 同じ大きさの領域を右へずらすと、明色の位置（格子の偶奇）は絶対座標に従う
        var atOrigin = appearance.ComputeLightTileRects(new EsRect(0, 0, 10, 10));
        var shifted = appearance.ComputeLightTileRects(new EsRect(10, 0, 10, 10));

        Assert.Equal(new EsRect(0, 0, 10, 10), Assert.Single(atOrigin));
        Assert.Empty(shifted);
    }

    /// <summary>市松模様を描く範囲（画像の表示矩形の内側）を検証する。</summary>
    /// <param name="x">画像の表示矩形の X。</param>
    /// <param name="y">画像の表示矩形の Y。</param>
    /// <param name="width">画像の表示矩形の幅。</param>
    /// <param name="height">画像の表示矩形の高さ。</param>
    /// <param name="expectedX">期待する領域の X。</param>
    /// <param name="expectedY">期待する領域の Y。</param>
    /// <param name="expectedWidth">期待する領域の幅。</param>
    /// <param name="expectedHeight">期待する領域の高さ。</param>
    [Theory]
    [InlineData(0, 0, 100, 100, 0, 0, 100, 100)]
    [InlineData(-20, -10, 100, 100, 0, 0, 80, 90)]
    [InlineData(80, 80, 100, 100, 80, 80, 20, 20)]
    [InlineData(20, 30, 100, 100, 20, 30, 80, 70)]
    internal void ComputeCheckerArea_画像の表示矩形とビューポートの交差を返す(
        double x, double y, double width, double height,
        double expectedX, double expectedY, double expectedWidth, double expectedHeight)
    {
        var appearance = ZoomViewAppearance.Default with { CheckerSize = 8 };

        var area = appearance.ComputeCheckerArea(new EsRect(x, y, width, height), new EsSize(100, 100));

        Assert.Equal(new EsRect(expectedX, expectedY, expectedWidth, expectedHeight), area);
    }

    /// <summary>画像がビューポートの外にある場合は範囲が空になることを検証する。</summary>
    /// <param name="x">画像の表示矩形の X。</param>
    /// <param name="y">画像の表示矩形の Y。</param>
    [Theory]
    [InlineData(200, 0)]
    [InlineData(0, 200)]
    [InlineData(-200, -200)]
    internal void ComputeCheckerArea_可視部分が無ければ空になる(double x, double y)
    {
        var appearance = ZoomViewAppearance.Default with { CheckerSize = 8 };

        var area = appearance.ComputeCheckerArea(new EsRect(x, y, 100, 100), new EsSize(100, 100));

        Assert.True(area.IsEmpty, $"空ではない: {area}");
    }

    /// <summary>市松模様を描かない設定では範囲が空になることを検証する。</summary>
    [Fact]
    internal void ComputeCheckerArea_市松模様を描かない設定なら空()
    {
        var appearance = ZoomViewAppearance.Default with { CheckerSize = 0 };

        Assert.Equal(default, appearance.ComputeCheckerArea(new EsRect(0, 0, 100, 100), new EsSize(100, 100)));
    }

    [Fact]
    internal void HasScrollBarTrack_アルファ0なら描かない()
    {
        var appearance = ZoomViewAppearance.Default with
        {
            ScrollBarTrackColor = System.Drawing.Color.FromArgb(0, 255, 255, 255),
        };

        Assert.False(appearance.HasScrollBarTrack);
    }

    [Fact]
    internal void HasScrollBarTrack_アルファが0より大きければ描く()
    {
        var appearance = ZoomViewAppearance.Default with
        {
            ScrollBarTrackColor = System.Drawing.Color.FromArgb(40, 255, 255, 255),
        };

        Assert.True(appearance.HasScrollBarTrack);
    }
}
