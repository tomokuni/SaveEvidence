using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CaptureSession.Tests;

/// <summary><see cref="FadeMaskDragRectOverlay"/>（選択矩形破線オーバーレイ描画）の単体テスト。</summary>
/// <remarks>
/// 描画は純粋な静的メソッド（<see cref="EsBitmap"/> のみを使用）のため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。<br/>
/// 期待値は実測（画素の読み出し）で確認する。 画素は BGRA のタプルで返る。
/// </remarks>
public class FadeMaskDragRectOverlayTests
{
    /// <summary>白（描画されていない状態）の画素値。</summary>
    private static readonly (byte B, byte G, byte R, byte A) s_white = (255, 255, 255, 255);

    /// <summary>テスト用の単色画像（白）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した画像。</returns>
    private static EsBitmap CreateSolidBitmap(int width, int height)
        => EsBitmap.Create(width, height, DrawingColor.White);

    /// <summary>指定座標の画素を BGRA のタプルで取得する。</summary>
    /// <param name="bitmap">対象の画像。</param>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>画素（青・緑・赤・アルファ）。</returns>
    private static (byte B, byte G, byte R, byte A) PixelAt(EsBitmap bitmap, int x, int y) => bitmap.GetPixel(x, y);

    /// <summary>選択矩形の辺に破線が描画されることを検証する。</summary>
    [Fact]
    public void DrawSelectionRect_選択矩形の辺に破線が描画される()
    {
        // 白の 40x40 画像へ選択矩形 (10,10,20,20) を倍率 4 で描画する
        using var bitmap = CreateSolidBitmap(40, 40);

        FadeMaskDragRectOverlay.DrawSelectionRect(
            bitmap, new EsRect(10, 10, 20, 20), new EsPoint(5, 5), 4.0, DrawingColor.Black, DrawingColor.Transparent);

        // 上辺（ドメイン y=10 → 画像 (10-5)*4 = 20）のピクセルが描画されている
        var pixel = PixelAt(bitmap, 20, 20);
        Assert.True(pixel.B < 255, $"選択矩形の辺に線が描画されていない: B={pixel.B}");

        // 選択矩形の内側は描画されない
        Assert.Equal(s_white, PixelAt(bitmap, 30, 30));
    }

    /// <summary>2 色の破線が交互に描画されることを検証する。</summary>
    [Fact]
    public void DrawSelectionRect_2色の破線が交互に描画される()
    {
        using var bitmap = CreateSolidBitmap(64, 64);

        // 上辺の y は 0。 破線は 1 ピクセル描画 + 1 ピクセル空白の 2 ピクセル周期で、色は A → B → A … と切り替わる
        FadeMaskDragRectOverlay.DrawSelectionRect(
            bitmap, new EsRect(0, 0, 10, 10), new EsPoint(0, 0), 4.0, DrawingColor.Red, DrawingColor.Blue);

        // 破線の先頭 2 ピクセルは色 A（赤）、次の 2 ピクセルは色 B（青）
        Assert.Equal((byte)0, PixelAt(bitmap, 0, 0).B);
        Assert.Equal((byte)255, PixelAt(bitmap, 0, 0).R);
        Assert.Equal((byte)0, PixelAt(bitmap, 3, 0).R);
        Assert.Equal((byte)255, PixelAt(bitmap, 3, 0).B);
    }

    /// <summary>倍率が 2 未満の場合は描画しないことを検証する。</summary>
    /// <param name="scale">描画倍率。</param>
    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.99)]
    public void DrawSelectionRect_倍率2未満では描画しない(double scale)
    {
        using var bitmap = CreateSolidBitmap(40, 40);

        FadeMaskDragRectOverlay.DrawSelectionRect(
            bitmap, new EsRect(10, 10, 20, 20), new EsPoint(5, 5), scale, DrawingColor.Black, DrawingColor.Transparent);

        // 白のまま（1 ピクセルも描画されない）
        Assert.Equal(s_white, PixelAt(bitmap, 20, 20));
    }

    /// <summary>切り出し領域と交差しない場合は描画しないことを検証する。</summary>
    [Fact]
    public void DrawSelectionRect_切り出し領域と交差しない場合は描画しない()
    {
        using var bitmap = CreateSolidBitmap(16, 16);

        // 画像（切り出し領域 16x16）の外側に完全に出る矩形を指定する
        FadeMaskDragRectOverlay.DrawSelectionRect(
            bitmap, new EsRect(-100, -100, 10, 10), new EsPoint(0, 0), 4.0, DrawingColor.Black, DrawingColor.Transparent);

        Assert.Equal(s_white, PixelAt(bitmap, 8, 8));
    }

    /// <summary>画像の範囲外へはみ出す矩形でも例外にならず、画像内の辺のみ描画されることを検証する。</summary>
    [Fact]
    public void DrawSelectionRect_画像外へはみ出しても例外にならない()
    {
        using var bitmap = CreateSolidBitmap(16, 16);

        // 画像より大きい矩形（倍率 4 で 84x84）。 画像内に入る上辺・左辺のみが描画される
        FadeMaskDragRectOverlay.DrawSelectionRect(
            bitmap, new EsRect(0, 0, 20, 20), new EsPoint(0, 0), 4.0, DrawingColor.Black, DrawingColor.Transparent);

        Assert.Equal((byte)0, PixelAt(bitmap, 0, 0).B);
    }

    /// <summary>画像が null の場合は例外になることを検証する。</summary>
    [Fact]
    public void DrawSelectionRect_画像がnullの場合は例外になる()
    {
        Assert.Throws<ArgumentNullException>(() => FadeMaskDragRectOverlay.DrawSelectionRect(
            null!, new EsRect(0, 0, 10, 10), new EsPoint(0, 0), 4.0, DrawingColor.Black, DrawingColor.White));
    }

    // ====================================================================
    // 破線の列挙（EnumerateDashes）
    // ====================================================================

    /// <summary>4 辺の破線が上辺 → 下辺 → 左辺 → 右辺の順に列挙されることを検証する。</summary>
    [Fact]
    public void EnumerateDashes_4辺の破線を上から順に列挙する()
    {
        // 切り出し左上 (0,0)・倍率 4・選択矩形 (10,10,20,20) → 画像座標は (40,40)-(124,124)
        var dashes = FadeMaskDragRectOverlay.EnumerateDashes(
            new EsRect(10, 10, 20, 20), new EsPoint(0, 0), 4.0, pixelSize: 200);

        Assert.NotEmpty(dashes);

        // 最初のグループは上辺（Y = 40 で高さ 1）、次のグループは下辺（Y = 124 で高さ 1）
        Assert.All(dashes.TakeWhile(d => d.Y == 40), d => Assert.Equal(1, d.Height));
        Assert.Contains(dashes, d => d.Y == 124 && d.Height == 1);
        // 左辺・右辺は幅 1（X = 40 / 124）
        Assert.Contains(dashes, d => d.X == 40 && d.Width == 1);
        Assert.Contains(dashes, d => d.X == 124 && d.Width == 1);
        // 上辺は 40 から始まる
        Assert.Equal(40, dashes[0].X);
    }

    /// <summary>色の種別が辺ごとに色 A から始まり交互に切り替わることを検証する。</summary>
    [Fact]
    public void EnumerateDashes_色の種別が辺ごとに色Aから交互になる()
    {
        var dashes = FadeMaskDragRectOverlay.EnumerateDashes(
            new EsRect(10, 10, 20, 20), new EsPoint(0, 0), 4.0, pixelSize: 200);

        // 上辺（Y = 40）の色は A → B → A … の順
        var top = dashes.Where(d => d.Y == 40).ToList();
        Assert.True(top.Count >= 2);
        Assert.True(top[0].IsColorA);
        Assert.False(top[1].IsColorA);

        // 辺が変わると色 A から数え直す（下辺の最初も色 A）
        var bottom = dashes.Where(d => d.Y == 124).ToList();
        Assert.True(bottom[0].IsColorA);
    }

    /// <summary>倍率が 2 未満の場合は列挙しないことを検証する。</summary>
    /// <param name="scale">描画倍率。</param>
    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.99)]
    public void EnumerateDashes_倍率2未満では列挙しない(double scale)
    {
        var dashes = FadeMaskDragRectOverlay.EnumerateDashes(
            new EsRect(10, 10, 20, 20), new EsPoint(0, 0), scale, pixelSize: 200);

        Assert.Empty(dashes);
    }

    /// <summary>切り出し領域と交差しない場合は列挙しないことを検証する。</summary>
    [Fact]
    public void EnumerateDashes_切り出し領域と交差しない場合は列挙しない()
    {
        var dashes = FadeMaskDragRectOverlay.EnumerateDashes(
            new EsRect(-100, -100, 10, 10), new EsPoint(0, 0), 4.0, pixelSize: 16);

        Assert.Empty(dashes);
    }

    /// <summary>列挙した破線が <see cref="EsBitmap"/> への描画結果と一致することを検証する。</summary>
    /// <remarks>列挙（MewUI の描画経路）と <c>DrawSelectionRect</c>（Avalonia の描画経路）が同じ幾何を使うことを担保する。</remarks>
    [Fact]
    public void EnumerateDashes_列挙と画像への描画が一致する()
    {
        using var bitmap = CreateSolidBitmap(64, 64);
        var selection = new EsRect(2, 3, 8, 9);
        var topLeft = new EsPoint(1, 2);

        FadeMaskDragRectOverlay.DrawSelectionRect(bitmap, selection, topLeft, 4.0, DrawingColor.Red, DrawingColor.Blue);

        // 列挙した破線が覆う画素は、画像へ描画した画素と 1 画素も違わない
        var drawn = new HashSet<(int X, int Y)>();
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                if (PixelAt(bitmap, x, y) != s_white)
                {
                    drawn.Add((x, y));
                }
            }
        }

        var enumerated = new HashSet<(int X, int Y)>();
        foreach (var dash in FadeMaskDragRectOverlay.EnumerateDashes(selection, topLeft, 4.0, 64))
        {
            for (var offset = 0; offset < dash.Width; offset++)
            {
                for (var row = 0; row < dash.Height; row++)
                {
                    if (dash.X + offset is >= 0 and < 64 && dash.Y + row is >= 0 and < 64)
                    {
                        enumerated.Add((dash.X + offset, dash.Y + row));
                    }
                }
            }
        }

        Assert.NotEmpty(drawn);
        Assert.Equal(drawn, enumerated);
    }
}
