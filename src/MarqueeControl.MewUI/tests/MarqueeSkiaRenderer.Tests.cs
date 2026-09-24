using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.Marquee.Tests;

/// <summary><see cref="MarqueeSkiaRenderer"/> の描画テスト（Skia のビットマップ上で画素を検証する）。</summary>
/// <remarks>
/// Skia のビットマップはウィンドウを必要としないため、UI 初期化なしで並列実行できる。<br/>
/// ハンドル位置の計算はコアの <c>SelectionGeometry</c> が単一所有するため、本テストは描画結果を検証する。
/// </remarks>
public class MarqueeSkiaRendererTests
{
    /// <summary>検証に使う枠線・ハンドルの塗り色（白・不透明）。</summary>
    private static readonly DrawingColor s_white = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>検証に使う枠線色（赤・不透明）。</summary>
    private static readonly DrawingColor s_red = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>描画されていない画素（透明）。</summary>
    private static readonly SKColor s_transparent = new(0, 0, 0, 0);

    // ====================================================================
    // DrawHandles
    // ====================================================================

    [Fact]
    public void DrawHandles_8方向のハンドル中心を塗りつぶす()
    {
        using var target = TestHelper.CreateTarget(120, 120);
        var rect = new EsRect(20, 20, 60, 40); // 中心 (50,40)・Right=80 Bottom=60

        MarqueeSkiaRenderer.DrawHandles(target.Canvas, rect, handleSize: 10, s_white, s_red);

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

        MarqueeSkiaRenderer.DrawHandles(target.Canvas, rect, handleSize: 10, s_white, s_red);

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
        MarqueeSkiaRenderer.DrawStartMarker(target.Canvas, new EsPoint(40, 40), s_white, thickness: 2, size: 8);

        // 水平線: x ∈ [32,48] / y ∈ [39,41)
        Assert.Equal(SKColors.White, target.GetPixel(35, 39));
        // 垂直線: y ∈ [32,48] / x ∈ [39,41)
        Assert.Equal(SKColors.White, target.GetPixel(39, 35));
        // 十字の外側は描画されない
        Assert.Equal(s_transparent, target.GetPixel(30, 30));
        Assert.Equal(s_transparent, target.GetPixel(40, 60));
    }
}
