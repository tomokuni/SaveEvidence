using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.LoupeCore.Drawing;
using EsUtil.Feature.LoupeCore.Setting;

namespace EsUtil.Feature.LoupeCore.Tests.Checkerboard;

/// <summary><see cref="LoupeCheckerTiles"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 市松模様のタイルの列挙のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 列挙子はコアの描画内容（<see cref="Loupe.ResolveContent"/>）から得る（利用者と同じ経路で検証する）。<br/>
/// 明暗の判定（<c>LoupeCheckerboard.IsDarkTile</c>）は本列挙子の内部手順のため、列挙結果を通して検証する。
/// </remarks>
public class LoupeCheckerTilesTests
{
    /// <summary>描画内容を解決する（タイルサイズは物理ピクセルで指定する）。</summary>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="tileSize">1 タイルの一辺（物理ピクセル）。</param>
    /// <returns>解決した描画内容。</returns>
    private static LoupeDrawContent Content(int pixelSize, int tileSize)
        => new Loupe().ResolveContent(
            new LoupeRenderRequest(null, pixelSize, pixelSize, ZoomLevel: 4, new LoupeMetrics(), 1.0, tileSize, 1));

    /// <summary>列挙子を検証用の一覧へ展開する。</summary>
    /// <param name="tiles">列挙するタイル。</param>
    /// <returns>列挙された矩形（列挙順）。</returns>
    private static List<EsRect> ToList(LoupeCheckerTiles tiles)
    {
        var rects = new List<EsRect>();
        foreach (var tile in tiles)
        {
            rects.Add(tile);
        }

        return rects;
    }

    [Fact]
    public void タイル数は切り上げになる()
    {
        // 64 px / タイル 8 px → 8 タイル × 8 タイル、うち暗色は半数（4×4 + 4×4 = 32）
        var tiles = Content(pixelSize: 64, tileSize: 8).CheckerTiles;

        Assert.Equal(32, tiles.Count);
        Assert.Equal(32, ToList(tiles).Count);
    }

    [Fact]
    public void 割り切れない場合は余りもタイルとして数える()
    {
        // 10 px / タイル 4 px → ceil(2.5) = 3 タイル × 3 タイル、暗色は 4 枚
        Assert.Equal(4, ToList(Content(pixelSize: 10, tileSize: 4).CheckerTiles).Count);
    }

    [Fact]
    public void タイルサイズがタイル全体より大きければ暗色は1枚もない()
    {
        // 4 px のルーペに 8 px のタイル → 1 タイル（(0,0) は明色）のみで暗色は 0 枚
        var tiles = Content(pixelSize: 4, tileSize: 8).CheckerTiles;

        Assert.Equal(0, tiles.Count);
        Assert.Empty(ToList(tiles));
    }

    [Fact]
    public void タイルサイズが0なら列挙する矩形がない()
    {
        // 0 は市松模様を描かない指定（0 除算を避けて空になる）
        var tiles = Content(pixelSize: 64, tileSize: 0).CheckerTiles;

        Assert.Equal(0, tiles.Count);
        Assert.Empty(ToList(tiles));
    }

    [Fact]
    public void 矩形はタイル座標に一致し走査順に並ぶ()
    {
        var rects = ToList(Content(pixelSize: 8, tileSize: 4).CheckerTiles);

        // 8 px / 4 px = 2×2 タイル。走査は行優先のため (1,0) → (0,1) の順
        Assert.Equal([new EsRect(4, 0, 4, 4), new EsRect(0, 4, 4, 4)], rects);
    }

    [Fact]
    public void 各矩形はタイルサイズの正方形である()
    {
        var rects = ToList(Content(pixelSize: 32, tileSize: 6).CheckerTiles);

        Assert.All(rects, r =>
        {
            Assert.Equal(6.0, r.Width);
            Assert.Equal(6.0, r.Height);
        });
    }

    [Fact]
    public void 明暗の判定は列と行の和の偶奇で決まる()
    {
        // 8 px / 4 px = 2×2 タイルのうち暗色は (1,0) と (0,1) の 2 枚（交互パターン）
        var rects = ToList(Content(pixelSize: 8, tileSize: 4).CheckerTiles);

        Assert.Equal([new EsRect(4, 0, 4, 4), new EsRect(0, 4, 4, 4)], rects);
    }

    [Fact]
    public void 全タイルを重複なく覆う()
    {
        // 明色と暗色を合わせると格子の全タイルになる（列挙の漏れ・重複が無いこと）
        const int tiles = 5;
        const int tileSize = 4;
        var rects = ToList(Content(tiles * tileSize, tileSize).CheckerTiles);

        Assert.Equal(tiles * tiles / 2, rects.Count);
        Assert.Equal(rects.Count, rects.Distinct().Count());
    }

    [Fact]
    public void Count_列挙の前後で変わらない()
    {
        var tiles = Content(pixelSize: 64, tileSize: 8).CheckerTiles;

        Assert.Equal(32, tiles.Count);
        _ = ToList(tiles);
        Assert.Equal(32, tiles.Count);
    }

    [Fact]
    public void 既定値は空を列挙する()
    {
        // 描画内容に含まれない場合（既定値）は列挙する矩形がない
        Assert.Equal(0, default(LoupeCheckerTiles).Count);
        Assert.Empty(ToList(default(LoupeCheckerTiles)));
    }
}
