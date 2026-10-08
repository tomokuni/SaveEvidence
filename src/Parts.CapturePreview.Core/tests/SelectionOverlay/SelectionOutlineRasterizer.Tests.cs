using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.SelectionOverlay;

/// <summary><see cref="SelectionOutlineRasterizer"/>（選択矩形破線オーバーレイ描画）の単体テスト。</summary>
/// <remarks>
/// 描画は純粋な静的メソッド（<see cref="EsWritableBitmap"/> のみを使用）のため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。<br/>
/// 期待値は実測（画素の読み出し）で確認する。 画素は BGRA のタプルで返る。
/// </remarks>
public class SelectionOutlineRasterizerTests
{
    /// <summary>白（描画されていない状態）の画素値。</summary>
    private static readonly (byte B, byte G, byte R, byte A) s_white = (255, 255, 255, 255);

    /// <summary>テスト用の単色画像（白）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した画像。</returns>
    private static EsWritableBitmap CreateSolidBitmap(int width, int height)
        => EsWritableBitmap.Create(width, height, DrawingColor.White);

    /// <summary>指定座標の画素を BGRA のタプルで取得する。</summary>
    /// <param name="bitmap">対象の画像。</param>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>画素（青・緑・赤・アルファ）。</returns>
    private static (byte B, byte G, byte R, byte A) PixelAt(EsWritableBitmap bitmap, int x, int y) => bitmap.GetPixel(x, y);

    /// <summary>選択矩形の辺に破線が描画されることを検証する。</summary>
    [Fact]
    internal void DrawSelectionRect_選択矩形の辺に破線が描画される()
    {
        // 白の 40x40 画像へ選択矩形 (10,10,20,20) を倍率 4 で描画する
        using var bitmap = CreateSolidBitmap(40, 40);

        SelectionOutlineRasterizer.DrawSelectionRect(
            bitmap, new EsRect(10, 10, 20, 20), new EsPoint(5, 5), 4.0, DrawingColor.Black, DrawingColor.Transparent);

        // 上辺・左辺は覆う画素の 1 画素外側（対象 (9,9) → 画像 19）に描画される
        var pixel = PixelAt(bitmap, 19, 19);
        Assert.True(pixel.B < 255, $"選択矩形の辺に線が描画されていない: B={pixel.B}");

        // 選択矩形の内側は描画されない
        Assert.Equal(s_white, PixelAt(bitmap, 30, 30));
    }

    /// <summary>2 色の破線が交互に描画されることを検証する。</summary>
    [Fact]
    internal void DrawSelectionRect_2色の破線が交互に描画される()
    {
        using var bitmap = CreateSolidBitmap(64, 64);

        // 選択矩形 (1,1,10,10)・倍率 4 → 覆う画素は 4 から。 枠はその 1 画素外側（y = 3）
        // 破線は 2 ピクセル描画 + 2 ピクセル空白の周期で、色は A → B → A … と切り替わる
        SelectionOutlineRasterizer.DrawSelectionRect(
            bitmap, new EsRect(1, 1, 10, 10), new EsPoint(0, 0), 4.0, DrawingColor.Red, DrawingColor.Blue);

        // 破線の先頭 2 ピクセルは色 A（赤）、次の 2 ピクセルは色 B（青）
        Assert.Equal((byte)0, PixelAt(bitmap, 3, 3).B);
        Assert.Equal((byte)255, PixelAt(bitmap, 3, 3).R);
        Assert.Equal((byte)0, PixelAt(bitmap, 5, 3).R);
        Assert.Equal((byte)255, PixelAt(bitmap, 5, 3).B);
    }

    /// <summary>倍率が 2 未満の場合は描画しないことを検証する。</summary>
    /// <param name="scale">描画倍率。</param>
    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.99)]
    internal void DrawSelectionRect_倍率2未満では描画しない(double scale)
    {
        using var bitmap = CreateSolidBitmap(40, 40);

        SelectionOutlineRasterizer.DrawSelectionRect(
            bitmap, new EsRect(10, 10, 20, 20), new EsPoint(5, 5), scale, DrawingColor.Black, DrawingColor.Transparent);

        // 白のまま（1 ピクセルも描画されない）
        Assert.Equal(s_white, PixelAt(bitmap, 20, 20));
    }

    /// <summary>切り出し領域と交差しない場合は描画しないことを検証する。</summary>
    [Fact]
    internal void DrawSelectionRect_切り出し領域と交差しない場合は描画しない()
    {
        using var bitmap = CreateSolidBitmap(16, 16);

        // 画像（切り出し領域 16x16）の外側に完全に出る矩形を指定する
        SelectionOutlineRasterizer.DrawSelectionRect(
            bitmap, new EsRect(-100, -100, 10, 10), new EsPoint(0, 0), 4.0, DrawingColor.Black, DrawingColor.Transparent);

        Assert.Equal(s_white, PixelAt(bitmap, 8, 8));
    }

    /// <summary>画像の範囲外へはみ出す矩形でも例外にならず、画像内の辺のみ描画されることを検証する。</summary>
    [Fact]
    internal void DrawSelectionRect_画像外へはみ出しても例外にならない()
    {
        using var bitmap = CreateSolidBitmap(16, 16);

        // 左上が画像外、右下が画像内に入る矩形（倍率 4） → 右辺・下辺（画像 8）だけが描画される
        SelectionOutlineRasterizer.DrawSelectionRect(
            bitmap, new EsRect(-2, -2, 4, 4), new EsPoint(0, 0), 4.0, DrawingColor.Black, DrawingColor.Transparent);

        Assert.Equal((byte)0, PixelAt(bitmap, 8, 8).B);
    }

    /// <summary>画像が null の場合は例外になることを検証する。</summary>
    [Fact]
    internal void DrawSelectionRect_画像がnullの場合は例外になる()
    {
        Assert.Throws<ArgumentNullException>(() => SelectionOutlineRasterizer.DrawSelectionRect(
            null!, new EsRect(0, 0, 10, 10), new EsPoint(0, 0), 4.0, DrawingColor.Black, DrawingColor.White));
    }

    // ====================================================================
    // 破線の列挙（EnumerateDashes）
    // ====================================================================

    /// <summary>4 辺の破線が上辺 → 下辺 → 左辺 → 右辺の順に列挙されることを検証する。</summary>
    [Fact]
    internal void EnumerateDashes_4辺の破線を上から順に列挙する()
    {
        // 切り出し左上 (0,0)・倍率 4・選択矩形 (10,10,20,20)（半開区間） → 覆う画素は (40,40)-(119,119)、枠はその 1 画素外側
        var dashes = SelectionOutlineRasterizer.EnumerateDashes(
            new EsRect(10, 10, 20, 20), new EsPoint(0, 0), 4.0, pixelSize: 200);

        Assert.NotEmpty(dashes);

        // 上辺（Y = 39 = 覆う画素の 1 画素上）は 39 から始まり、右辺（X = 120 = 覆う画素の終端）で終わる
        var top = dashes.TakeWhile(d => d.Y == 39).ToList();
        Assert.All(top, d => Assert.Equal(1, d.Height));
        Assert.Equal(39, top[0].X);
        Assert.Equal(120, top[^1].X + top[^1].Width - 1);
        // 下辺は覆う画素の終端（Y = 120）
        Assert.Contains(dashes, d => d.Y == 120 && d.Height == 1);
        // 左辺・右辺は幅 1（X = 39 / 120）
        Assert.Contains(dashes, d => d.X == 39 && d.Width == 1);
        Assert.Contains(dashes, d => d.X == 120 && d.Width == 1);
    }

    /// <summary>色の種別が辺ごとに色 A から始まり交互に切り替わることを検証する。</summary>
    [Fact]
    internal void EnumerateDashes_色の種別が辺ごとに色Aから交互になる()
    {
        var dashes = SelectionOutlineRasterizer.EnumerateDashes(
            new EsRect(10, 10, 20, 20), new EsPoint(0, 0), 4.0, pixelSize: 200);

        // 上辺（Y = 39）の色は A → B → A … の順
        var top = dashes.Where(d => d.Y == 39).ToList();
        Assert.True(top.Count >= 2);
        Assert.True(top[0].IsColorA);
        Assert.False(top[1].IsColorA);

        // 辺が変わると色 A から数え直す（下辺の最初も色 A）
        var bottom = dashes.Where(d => d.Y == 120).ToList();
        Assert.True(bottom[0].IsColorA);
    }

    /// <summary>覆う画素数が区間の流儀（閉区間は終点を含む）で決まることを検証する。</summary>
    /// <param name="interval">区間の流儀。</param>
    /// <param name="width">矩形の幅（対象座標）。</param>
    /// <param name="expectedRight">期待する右辺の X 座標（画像ピクセル）。</param>
    /// <remarks>倍率 4・左上 (0,0) のため、覆う画素は 0 から始まり、右辺は 覆う画素数 × 4 になる（半開区間は 幅、閉区間は 幅 + 1 画素）。</remarks>
    [Theory]
    [InlineData(EsRectInterval.HalfOpen, 3, 12)]
    [InlineData(EsRectInterval.Closed, 3, 16)]
    internal void EnumerateDashes_区間の流儀で覆う画素数が変わる(EsRectInterval interval, int width, int expectedRight)
    {
        var dashes = SelectionOutlineRasterizer.EnumerateDashes(
            new EsRect(0, 0, width, 1, interval), new EsPoint(0, 0), 4.0, pixelSize: 64);

        // 左辺は覆う画素の先頭（0）の 1 画素外側、右辺は覆う画素の終端
        Assert.Contains(dashes, d => d.X == -1 && d.Width == 1);
        Assert.Contains(dashes, d => d.X == expectedRight && d.Width == 1);
    }

    /// <summary>破線が十字線の中心点（ドラッグ中の画素の帯）と重ならないことを検証する。</summary>
    /// <param name="endX">ドラッグの終点 X（対象座標。 ルーペの中心になる）。</param>
    /// <param name="endY">ドラッグの終点 Y（対象座標）。</param>
    /// <remarks>
    /// ルーペの十字線はドラッグ中の点（＝選択の角）の画素から拡大率ぶんの幅を持つ。<br/>
    /// 破線を選択が覆う画素の内側へ引くと、その角の辺が十字線の中心点に重なる（終点が左上のときに「少しかぶって」見える）。<br/>
    /// 外側へ引けば、<b>ドラッグの向き（終点が右下 / 左上）に関係なく中心点の外側に接する</b>。<br/>
    /// 本テストは 4 辺のどのダッシュも十字線の中心点（中心の画素の正方形）と交差しないことを固定する。
    /// </remarks>
    [Theory]
    [InlineData(30, 20)]   // 終点が右下（始点 (10,10) → 終点 (30,20)）
    [InlineData(10, 10)]   // 終点が左上（始点 (30,20) → 終点 (10,10)）
    internal void EnumerateDashes_破線が十字線の中心点と重ならない(int endX, int endY)
    {
        const double scale = 4.0;
        var selection = new EsRect(10, 10, 20, 10, EsRectInterval.Closed);
        var dashes = SelectionOutlineRasterizer.EnumerateDashes(selection, new EsPoint(0, 0), scale, pixelSize: 200);

        // 十字線の中心点（ドラッグ中の画素。 拡大率ぶんの正方形）
        var centerX = CropBoundsMath.RoundToPixel(endX * scale);
        var centerY = CropBoundsMath.RoundToPixel(endY * scale);
        var zoomLevel = (int)scale;

        Assert.NotEmpty(dashes);
        foreach (var dash in dashes)
        {
            var overlaps = dash.X < centerX + zoomLevel && dash.X + dash.Width > centerX
                && dash.Y < centerY + zoomLevel && dash.Y + dash.Height > centerY;
            Assert.False(
                overlaps,
                $"破線が十字線の中心点と重なる: dash=({dash.X},{dash.Y},{dash.Width},{dash.Height}) / 中心点=({centerX},{centerY})");
        }
    }

    /// <summary>倍率が 2 未満の場合は列挙しないことを検証する。</summary>
    /// <param name="scale">描画倍率。</param>
    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.99)]
    internal void EnumerateDashes_倍率2未満では列挙しない(double scale)
    {
        var dashes = SelectionOutlineRasterizer.EnumerateDashes(
            new EsRect(10, 10, 20, 20), new EsPoint(0, 0), scale, pixelSize: 200);

        Assert.Empty(dashes);
    }

    /// <summary>切り出し領域と交差しない場合は列挙しないことを検証する。</summary>
    [Fact]
    internal void EnumerateDashes_切り出し領域と交差しない場合は列挙しない()
    {
        var dashes = SelectionOutlineRasterizer.EnumerateDashes(
            new EsRect(-100, -100, 10, 10), new EsPoint(0, 0), 4.0, pixelSize: 16);

        Assert.Empty(dashes);
    }

    /// <summary>列挙した破線が <see cref="EsWritableBitmap"/> への描画結果と一致することを検証する。</summary>
    /// <remarks>列挙（MewUI の描画経路）と <c>DrawSelectionRect</c>（Avalonia の描画経路）が同じ幾何を使うことを担保する。</remarks>
    [Fact]
    internal void EnumerateDashes_列挙と画像への描画が一致する()
    {
        using var bitmap = CreateSolidBitmap(64, 64);
        var selection = new EsRect(2, 3, 8, 9);
        var topLeft = new EsPoint(1, 2);

        SelectionOutlineRasterizer.DrawSelectionRect(bitmap, selection, topLeft, 4.0, DrawingColor.Red, DrawingColor.Blue);

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
        foreach (var dash in SelectionOutlineRasterizer.EnumerateDashes(selection, topLeft, 4.0, 64))
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
