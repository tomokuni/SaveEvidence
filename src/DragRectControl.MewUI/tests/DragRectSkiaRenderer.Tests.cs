using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.DragRect.Tests;

/// <summary><see cref="DragRectSkiaRenderer"/> の描画テスト（Skia のビットマップ上で画素を検証する）。</summary>
/// <remarks>
/// Skia のビットマップはウィンドウを必要としないため、UI 初期化なしで並列実行できる。<br/>
/// ハンドル位置の計算はコアの <c>SelectionGeometry</c> が単一所有するため、本テストは描画結果を検証する。
/// </remarks>
public class DragRectSkiaRendererTests
{
    /// <summary>検証に使う枠線・ハンドルの塗り色（白・不透明）。</summary>
    private static readonly DrawingColor s_white = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>検証に使う枠線色（赤・不透明）。</summary>
    private static readonly DrawingColor s_red = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>描画されていない画素（透明）。</summary>
    private static readonly SKColor s_transparent = new(0, 0, 0, 0);

    // ====================================================================
    // ComputeCoordLabelPosition（純粋計算）
    // ====================================================================

    [Fact]
    public void 配置_カーソル右側のオフセット位置に配置する()
    {
        var pos = DragRectSkiaRenderer.ComputeCoordLabelPosition(
            new EsPoint(100, 50), new EsSize(800, 600), 120, 24);

        // カーソル (100,50) + オフセット (8,8)
        Assert.Equal(new EsPoint(108, 58), pos);
    }

    [Fact]
    public void 配置_右端をはみ出す場合は領域内へクランプする()
    {
        var pos = DragRectSkiaRenderer.ComputeCoordLabelPosition(
            new EsPoint(790, 50), new EsSize(800, 600), 120, 24);

        // x = 798 → クランプ: 800 - 120 = 680
        Assert.Equal(680, pos.X);
        Assert.Equal(58, pos.Y);
    }

    [Fact]
    public void 配置_下端をはみ出す場合は領域内へクランプする()
    {
        var pos = DragRectSkiaRenderer.ComputeCoordLabelPosition(
            new EsPoint(100, 590), new EsSize(800, 600), 120, 24);

        // y = 598 → クランプ: 600 - 24 = 576
        Assert.Equal(108, pos.X);
        Assert.Equal(576, pos.Y);
    }

    [Fact]
    public void 配置_負座標でも0未満にならない()
    {
        var pos = DragRectSkiaRenderer.ComputeCoordLabelPosition(
            new EsPoint(0, 0), new EsSize(800, 600), 120, 24);

        // x = 8, y = 8（負にならない）
        Assert.Equal(8, pos.X);
        Assert.Equal(8, pos.Y);
    }

    [Fact]
    public void 配置_クライアントサイズ0ではクランプしない()
    {
        var pos = DragRectSkiaRenderer.ComputeCoordLabelPosition(
            new EsPoint(100, 50), new EsSize(0, 0), 120, 24);

        // サイズ 0 の軸はクランプ条件をスキップし、オフセットのみ
        Assert.Equal(new EsPoint(108, 58), pos);
    }

    [Fact]
    public void 配置_ラベルが領域より大きい場合は0にクランプする()
    {
        var pos = DragRectSkiaRenderer.ComputeCoordLabelPosition(
            new EsPoint(100, 50), new EsSize(50, 50), 120, 24);

        // ラベル幅 120 > 領域幅 50 → x = 0
        Assert.Equal(0, pos.X);
    }

    // ====================================================================
    // DrawBorder
    // ====================================================================

    [Fact]
    public void DrawBorder_矩形の辺のみを太さ分描画する()
    {
        using var target = TestHelper.CreateTarget(120, 120);

        // 矩形 (10,10)-(60,50)・太さ 2 → 辺は [9,11) を覆う（ストロークは経路の中心に描かれる）
        DragRectSkiaRenderer.DrawBorder(target.Canvas, new EsRect(10, 10, 50, 40), s_white, borderThickness: 2);

        // 内側は描画されない
        Assert.Equal(s_transparent, target.GetPixel(30, 25));
        // 左辺・右辺・上辺・下辺が白で描かれる
        Assert.Equal(SKColors.White, target.GetPixel(9, 25));
        Assert.Equal(SKColors.White, target.GetPixel(60, 25));
        Assert.Equal(SKColors.White, target.GetPixel(30, 9));
        Assert.Equal(SKColors.White, target.GetPixel(30, 49));
    }

    // ====================================================================
    // DrawHandles
    // ====================================================================

    [Fact]
    public void DrawHandles_8方向のハンドル中心を塗りつぶす()
    {
        using var target = TestHelper.CreateTarget(120, 120);
        var rect = new EsRect(20, 20, 60, 40); // 中心 (50,40)・Right=80 Bottom=60

        DragRectSkiaRenderer.DrawHandles(target.Canvas, rect, handleSize: 10, s_white, s_red);

        // 各ハンドルの中心（塗りの内側）が白になる
        Assert.Equal(SKColors.White, target.GetPixel(20, 20)); // TopLeft
        Assert.Equal(SKColors.White, target.GetPixel(80, 20)); // TopRight
        Assert.Equal(SKColors.White, target.GetPixel(20, 60)); // BottomLeft
        Assert.Equal(SKColors.White, target.GetPixel(80, 60)); // BottomRight
        Assert.Equal(SKColors.White, target.GetPixel(50, 20)); // TopCenter
        Assert.Equal(SKColors.White, target.GetPixel(20, 40)); // MiddleLeft
        Assert.Equal(SKColors.White, target.GetPixel(80, 40)); // MiddleRight
        Assert.Equal(SKColors.White, target.GetPixel(50, 60)); // BottomCenter

        // 矩形の中心（ハンドルなし）は描画されない
        Assert.Equal(s_transparent, target.GetPixel(50, 40));
    }

    [Fact]
    public void DrawHandles_枠線色でハンドルの縁を描画する()
    {
        using var target = TestHelper.CreateTarget(120, 120);
        var rect = new EsRect(50, 50, 40, 40); // TopLeft ハンドルの外縁は (45,45)-(55,55)

        DragRectSkiaRenderer.DrawHandles(target.Canvas, rect, handleSize: 10, s_white, s_red);

        // ハンドルの外縁に赤（枠線）が現れる
        Assert.True(target.CountColor(43, 43, 58, 48, new SKColor(255, 0, 0, 255)) > 0);
    }

    // ====================================================================
    // DrawStartMarker
    // ====================================================================

    [Fact]
    public void DrawStartMarker_中心から上下左右へ十字線を描画する()
    {
        using var target = TestHelper.CreateTarget(80, 80);

        // 中心 (40,40)・半幅 8・太さ 2
        DragRectSkiaRenderer.DrawStartMarker(target.Canvas, new EsPoint(40, 40), s_white, thickness: 2, size: 8);

        // 水平線: x ∈ [32,48] / y ∈ [39,41)
        Assert.Equal(SKColors.White, target.GetPixel(35, 39));
        // 垂直線: y ∈ [32,48] / x ∈ [39,41)
        Assert.Equal(SKColors.White, target.GetPixel(39, 35));
        // 十字の外側は描画されない
        Assert.Equal(s_transparent, target.GetPixel(30, 30));
        Assert.Equal(s_transparent, target.GetPixel(40, 60));
    }

    // ====================================================================
    // DrawCoordLabel
    // ====================================================================

    [Fact]
    public void DrawCoordLabel_背景を塗り文字を描画する()
    {
        using var target = TestHelper.CreateTarget(300, 100);
        var background = DrawingColor.FromArgb(255, 0, 0, 0);

        DragRectSkiaRenderer.DrawCoordLabel(
            target.Canvas,
            cursorDip: new EsPoint(10, 10),
            domainPos: new EsPoint(120, 340),
            clientSize: new EsSize(300, 100),
            textColor: s_white,
            backgroundColor: background,
            fontSize: 12);

        // ラベル左上はカーソル + (8,8) = (18,18)。パディング 4 の内側は背景色になる
        Assert.Equal(SKColors.Black, target.GetPixel(19, 19));
        // 文字（白系）が描画されている（アンチエイリアスを含むため完全一致ではなく存在のみを確認する）
        Assert.True(target.CountColor(18, 18, 120, 40, new SKColor(255, 255, 255, 255)) > 0);
        // ラベルの外側は描画されない
        Assert.Equal(s_transparent, target.GetPixel(5, 5));
    }

    [Fact]
    public void DrawCoordLabel_ラベルが領域より広い場合は左端0へクランプする()
    {
        using var target = TestHelper.CreateTarget(60, 60);
        var background = DrawingColor.FromArgb(255, 0, 0, 0);

        DragRectSkiaRenderer.DrawCoordLabel(
            target.Canvas,
            cursorDip: new EsPoint(15, 5),
            domainPos: new EsPoint(0, 0),
            clientSize: new EsSize(20, 60), // ラベルの幅（20 超）より狭い領域
            textColor: s_white,
            backgroundColor: background,
            fontSize: 12);

        // x は 0 へクランプされ、y はカーソル + 8 = 13 のまま（ラベル高 16 < 60）
        Assert.Equal(SKColors.Black, target.GetPixel(0, 13));
        Assert.Equal(s_transparent, target.GetPixel(0, 12));
    }
}
