using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace Estable.Loupe.Tests;

/// <summary><see cref="LoupeCheckerboard"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 市松模様の明暗判定のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 本判定は各 UI（Avalonia / MewUI）の市松模様描画で共用する。
/// </remarks>
public class LoupeCheckerboardTests
{
    /// <summary>交互パターン（列と行の和が奇数）で暗色を判定することを検証する。</summary>
    [Fact]
    public void IsDarkTile_交互パターンで暗色を判定する()
    {
        Assert.True(LoupeCheckerboard.IsDarkTile(0, 1));
        Assert.True(LoupeCheckerboard.IsDarkTile(1, 0));
        Assert.False(LoupeCheckerboard.IsDarkTile(0, 0));
        Assert.False(LoupeCheckerboard.IsDarkTile(1, 1));
    }

    /// <summary>列と行の和の偶奇で明暗が決まることを検証する。</summary>
    [Fact]
    public void IsDarkTile_列と行の和が奇数なら暗色になる()
    {
        // (2 + 3) = 5（奇数）→ 暗色
        Assert.True(LoupeCheckerboard.IsDarkTile(2, 3));
        // (4 + 2) = 6（偶数）→ 明色
        Assert.False(LoupeCheckerboard.IsDarkTile(4, 2));
    }

    [Fact]
    public void ComputeDarkTileRects_タイル数は切り上げになる()
    {
        // 64 px / タイル 8 px → 8 タイル × 8 タイル、うち暗色は半数（4×4 + 4×4 = 32）
        var rects = LoupeCheckerboard.ComputeDarkTileRects(pixelSize: 64, tileSize: 8);

        Assert.Equal(32, rects.Count);
    }

    [Fact]
    public void ComputeDarkTileRects_割り切れない場合は余りもタイルとして数える()
    {
        // 10 px / タイル 4 px → ceil(2.5) = 3 タイル × 3 タイル、暗色は 4 枚
        var rects = LoupeCheckerboard.ComputeDarkTileRects(pixelSize: 10, tileSize: 4);

        Assert.Equal(4, rects.Count);
    }

    [Fact]
    public void ComputeDarkTileRects_タイルサイズがタイル全体より大きければ暗色は1枚もない()
    {
        // 4 px のルーペに 8 px のタイル → 1 タイル（(0,0) は明色）のみで暗色は 0 枚
        var rects = LoupeCheckerboard.ComputeDarkTileRects(pixelSize: 4, tileSize: 8);

        Assert.Empty(rects);
    }

    [Fact]
    public void ComputeDarkTileRects_矩形はタイル座標に一致する()
    {
        var rects = LoupeCheckerboard.ComputeDarkTileRects(pixelSize: 8, tileSize: 4);

        // 8 px / 4 px = 2×2 タイル。暗色は (1,0) と (0,1)
        Assert.Equal(2, rects.Count);
        Assert.Contains(new EsRect(4, 0, 4, 4), rects); // (col=1, row=0)
        Assert.Contains(new EsRect(0, 4, 4, 4), rects); // (col=0, row=1)
    }

    [Fact]
    public void ComputeDarkTileRects_暗色の判定と一致する()
    {
        const int pixelSize = 16;
        const int tileSize = 4;
        var rects = LoupeCheckerboard.ComputeDarkTileRects(pixelSize, tileSize);

        // 列挙された矩形がすべて IsDarkTile の判定と一致する（判定の二重定義が無いこと）
        Assert.All(rects, r =>
        {
            var col = (int)(r.X / tileSize);
            var row = (int)(r.Y / tileSize);
            Assert.True(LoupeCheckerboard.IsDarkTile(col, row));
        });
    }

    [Fact]
    public void ComputeDarkTileRects_各矩形はタイルサイズの正方形である()
    {
        var rects = LoupeCheckerboard.ComputeDarkTileRects(pixelSize: 32, tileSize: 6);

        Assert.All(rects, r =>
        {
            Assert.Equal(6.0, r.Width);
            Assert.Equal(6.0, r.Height);
        });
    }
}
