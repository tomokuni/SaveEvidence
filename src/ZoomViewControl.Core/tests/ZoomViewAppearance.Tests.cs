using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace Estable.ZoomView.Tests;

/// <summary><see cref="ZoomViewAppearance"/> の仕様テスト（外観の単一所有と市松模様のタイル規則）。</summary>
/// <remarks>
/// タイルサイズの意味（0 以下 = 描かない / 1 は下限へ切り上げ）はコアが単一所有するため、両 UI のホストが<br/>
/// 同じ規則を共有することを本テストで固定する（UI ごとに「0 の意味」がずれる事故の防止）。<br/>
/// UI 初期化を必要としないため、Avalonia ヘッドレスに依存せず並列実行できる。
/// </remarks>
public class ZoomViewAppearanceTests
{
    [Fact]
    public void Default_は市松模様を描かない()
    {
        var appearance = ZoomViewAppearance.Default;

        Assert.Equal(0, appearance.CheckerSize);
        Assert.False(appearance.HasCheckerboard);
        Assert.Equal(ZoomViewAppearance.DefaultScrollBarThickness, appearance.ScrollBarThickness);
        Assert.Equal(ZoomViewAppearance.DefaultScrollBarMargin, appearance.ScrollBarMargin);
        Assert.True(appearance.ShowScrollBars);
    }

    [Fact]
    public void MinimumCheckerTileSize_は2である()
    {
        Assert.Equal(2, ZoomViewAppearance.MinimumCheckerTileSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void ResolveCheckerTileSize_0以下は描かない0を返す(int checkerSize)
    {
        Assert.Equal(0, ZoomViewAppearance.ResolveCheckerTileSize(checkerSize));
    }

    [Fact]
    public void ResolveCheckerTileSize_1は下限の2へ切り上げる()
    {
        Assert.Equal(ZoomViewAppearance.MinimumCheckerTileSize, ZoomViewAppearance.ResolveCheckerTileSize(1));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public void ResolveCheckerTileSize_下限以上はそのまま返す(int checkerSize)
    {
        Assert.Equal(checkerSize, ZoomViewAppearance.ResolveCheckerTileSize(checkerSize));
    }

    [Theory]
    [InlineData(-3)]
    [InlineData(0)]
    public void HasCheckerboard_実効タイルサイズが0なら描かない(int checkerSize)
    {
        var appearance = ZoomViewAppearance.Default with { CheckerSize = checkerSize };

        Assert.False(appearance.HasCheckerboard);
        Assert.Empty(appearance.ComputeLightTileRects(new EsSize(50, 50)));
    }

    [Fact]
    public void HasCheckerboard_1は実効値2として描く()
    {
        var appearance = ZoomViewAppearance.Default with { CheckerSize = 1 };

        // 「1 = 描かない」ではない（1 ピクセル幅の市松は縞に見えるため下限へ切り上げる）
        Assert.True(appearance.HasCheckerboard);

        var tiles = appearance.ComputeLightTileRects(new EsSize(4, 4));

        Assert.All(tiles, tile => Assert.Equal(ZoomViewAppearance.MinimumCheckerTileSize, tile.Width));
    }

    [Fact]
    public void ComputeLightTileRects_明暗の配置規則を単一所有する()
    {
        var appearance = ZoomViewAppearance.Default with { CheckerSize = 10 };

        var tiles = appearance.ComputeLightTileRects(new EsSize(25, 25));

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
    public void ComputeLightTileRects_ビューポートが空なら空になる(double width, double height)
    {
        var appearance = ZoomViewAppearance.Default with { CheckerSize = 8 };

        Assert.Empty(appearance.ComputeLightTileRects(new EsSize(width, height)));
    }

    [Fact]
    public void HasScrollBarTrack_アルファ0なら描かない()
    {
        var appearance = ZoomViewAppearance.Default with
        {
            ScrollBarTrackColor = System.Drawing.Color.FromArgb(0, 255, 255, 255),
        };

        Assert.False(appearance.HasScrollBarTrack);
    }

    [Fact]
    public void HasScrollBarTrack_アルファが0より大きければ描く()
    {
        var appearance = ZoomViewAppearance.Default with
        {
            ScrollBarTrackColor = System.Drawing.Color.FromArgb(40, 255, 255, 255),
        };

        Assert.True(appearance.HasScrollBarTrack);
    }
}
