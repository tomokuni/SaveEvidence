using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.SelectionOverlay;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.SelectionOverlay;

/// <summary><see cref="SelectionOutlineRasterizer"/>（選択矩形破線オーバーレイの幾何）の単体テスト。</summary>
/// <remarks>
/// 本クラスは破線の<b>位置・長さ・色の種別</b>だけを列挙する（描画は UI 層が行う）ため、<br/>
/// UI 初期化なしの <c>[Fact]</c> で並列検証できる。 描画結果の画素は各 UI の描画テストが検証する。
/// </remarks>
public class SelectionOutlineRasterizerTests
{
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
}
