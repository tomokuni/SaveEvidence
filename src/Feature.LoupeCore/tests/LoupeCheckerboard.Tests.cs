using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace Estable.Loupe.Tests;

/// <summary><see cref="LoupeCheckerboard"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 市松模様の明暗判定とタイルの列挙のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 本判定と列挙は各 UI（Avalonia / MewUI）の市松模様描画で共用する。
/// </remarks>
public class LoupeCheckerboardTests
{
    /// <summary>列挙子を検証用の一覧へ展開する。</summary>
    /// <param name="tiles">列挙するタイル。</param>
    /// <returns>列挙された矩形（列挙順）。</returns>
    private static List<EsRect> ToList(LoupeCheckerboard.DarkTiles tiles)
    {
        var rects = new List<EsRect>();
        foreach (var tile in tiles)
        {
            rects.Add(tile);
        }

        return rects;
    }

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
    public void ComputeDarkTiles_タイル数は切り上げになる()
    {
        // 64 px / タイル 8 px → 8 タイル × 8 タイル、うち暗色は半数（4×4 + 4×4 = 32）
        var tiles = LoupeCheckerboard.ComputeDarkTiles(pixelSize: 64, tileSize: 8);

        Assert.Equal(32, tiles.Count);
        Assert.Equal(32, ToList(tiles).Count);
    }

    [Fact]
    public void ComputeDarkTiles_割り切れない場合は余りもタイルとして数える()
    {
        // 10 px / タイル 4 px → ceil(2.5) = 3 タイル × 3 タイル、暗色は 4 枚
        Assert.Equal(4, ToList(LoupeCheckerboard.ComputeDarkTiles(pixelSize: 10, tileSize: 4)).Count);
    }

    [Fact]
    public void ComputeDarkTiles_タイルサイズがタイル全体より大きければ暗色は1枚もない()
    {
        // 4 px のルーペに 8 px のタイル → 1 タイル（(0,0) は明色）のみで暗色は 0 枚
        var tiles = LoupeCheckerboard.ComputeDarkTiles(pixelSize: 4, tileSize: 8);

        Assert.Equal(0, tiles.Count);
        Assert.Empty(ToList(tiles));
    }

    [Fact]
    public void ComputeDarkTiles_矩形はタイル座標に一致し走査順に並ぶ()
    {
        var rects = ToList(LoupeCheckerboard.ComputeDarkTiles(pixelSize: 8, tileSize: 4));

        // 8 px / 4 px = 2×2 タイル。走査は行優先のため (1,0) → (0,1) の順
        Assert.Equal([new EsRect(4, 0, 4, 4), new EsRect(0, 4, 4, 4)], rects);
    }

    [Fact]
    public void ComputeDarkTiles_暗色の判定と一致する()
    {
        const int pixelSize = 16;
        const int tileSize = 4;
        var rects = ToList(LoupeCheckerboard.ComputeDarkTiles(pixelSize, tileSize));

        // 列挙された矩形がすべて IsDarkTile の判定と一致する（判定の二重定義が無いこと）
        Assert.All(rects, r =>
        {
            var col = (int)(r.X / tileSize);
            var row = (int)(r.Y / tileSize);
            Assert.True(LoupeCheckerboard.IsDarkTile(col, row));
        });
    }

    [Fact]
    public void ComputeDarkTiles_各矩形はタイルサイズの正方形である()
    {
        var rects = ToList(LoupeCheckerboard.ComputeDarkTiles(pixelSize: 32, tileSize: 6));

        Assert.All(rects, r =>
        {
            Assert.Equal(6.0, r.Width);
            Assert.Equal(6.0, r.Height);
        });
    }

    [Fact]
    public void ComputeDarkTiles_全タイルを重複なく覆う()
    {
        // 明色と暗色を合わせると格子の全タイルになる（列挙の漏れ・重複が無いこと）
        const int tiles = 5;
        const int tileSize = 4;
        var rects = ToList(LoupeCheckerboard.ComputeDarkTiles(tiles * tileSize, tileSize));

        Assert.Equal(tiles * tiles / 2, rects.Count);
        Assert.Equal(rects.Count, rects.Distinct().Count());
    }

    [Fact]
    public void Count_列挙の前後で変わらない()
    {
        var tiles = LoupeCheckerboard.ComputeDarkTiles(pixelSize: 64, tileSize: 8);

        Assert.Equal(32, tiles.Count);
        _ = ToList(tiles);
        Assert.Equal(32, tiles.Count);
    }
}
