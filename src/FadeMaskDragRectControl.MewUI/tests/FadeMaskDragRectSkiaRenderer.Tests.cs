using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.FadeMaskDragRect.Tests;

/// <summary><see cref="FadeMaskDragRectSkiaRenderer"/>（選択矩形破線オーバーレイ）の描画テスト。</summary>
/// <remarks>
/// 破線の幾何はコアの <c>FadeMaskDragRectOverlay.EnumerateDashes</c> が単一所有するため、<br/>
/// 本テストは「列挙された破線がキャンバスへ 2 色で描かれること」を画素で実測する。<br/>
/// Skia のビットマップはウィンドウを必要としないため、UI 初期化なしで並列実行できる。
/// </remarks>
public class FadeMaskDragRectSkiaRendererTests
{
    /// <summary>選択矩形の辺に破線が描画されることを検証する。</summary>
    [Fact]
    public void DrawSelectionRect_選択矩形の辺に破線を描画する()
    {
        using var target = TestHelper.CreateTarget(64, 64);

        // 切り出し左上 (0,0)・倍率 4・選択矩形 (10,10,20,20) → 画像座標は (40,40)-(124,124)
        FadeMaskDragRectSkiaRenderer.DrawSelectionRect(
            target.Canvas, new EsRect(10, 10, 20, 20), new EsPoint(0, 0), 4.0, 200,
            DrawingColor.Red, DrawingColor.Blue);

        // 上辺（y = 40）と左辺（x = 40）が描画される
        Assert.Equal(new SKColor(255, 0, 0), target.GetPixel(40, 40));
        Assert.NotEqual(new SKColor(0, 0, 0, 0), target.GetPixel(40, 41));
        // 選択矩形の内側は描画されない
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(50, 50));
    }

    /// <summary>2 色の破線が交互に描画されることを検証する。</summary>
    [Fact]
    public void DrawSelectionRect_2色の破線を交互に描画する()
    {
        using var target = TestHelper.CreateTarget(64, 64);

        // 上辺の y は 0。破線は 1 ピクセル描画 + 1 ピクセル空白の 2 ピクセル周期で、色は A → B → A … と切り替わる
        FadeMaskDragRectSkiaRenderer.DrawSelectionRect(
            target.Canvas, new EsRect(0, 0, 10, 10), new EsPoint(0, 0), 4.0, 64,
            DrawingColor.Red, DrawingColor.Blue);

        // 破線の先頭 2 ピクセルは色 A（赤）、次の 2 ピクセルは色 B（青）
        Assert.Equal(new SKColor(255, 0, 0), target.GetPixel(0, 0));
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(3, 0));
    }

    /// <summary>倍率が 2 未満の場合は描画しないことを検証する。</summary>
    /// <param name="scale">描画倍率。</param>
    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.99)]
    public void DrawSelectionRect_倍率2未満では描画しない(double scale)
    {
        using var target = TestHelper.CreateTarget(64, 64);

        FadeMaskDragRectSkiaRenderer.DrawSelectionRect(
            target.Canvas, new EsRect(10, 10, 20, 20), new EsPoint(0, 0), scale, 64,
            DrawingColor.Red, DrawingColor.Blue);

        Assert.Equal(0, target.CountColor(new SKColor(255, 0, 0)));
    }

    /// <summary>切り出し領域と交差しない場合は描画しないことを検証する。</summary>
    [Fact]
    public void DrawSelectionRect_切り出し領域と交差しない場合は描画しない()
    {
        using var target = TestHelper.CreateTarget(16, 16);

        FadeMaskDragRectSkiaRenderer.DrawSelectionRect(
            target.Canvas, new EsRect(-100, -100, 10, 10), new EsPoint(0, 0), 4.0, 16,
            DrawingColor.Red, DrawingColor.Blue);

        Assert.Equal(0, target.CountColor(new SKColor(255, 0, 0)));
    }

    /// <summary>画像の範囲外へはみ出す矩形でも例外にならず、画像内の辺のみ描画されることを検証する。</summary>
    [Fact]
    public void DrawSelectionRect_画像外へはみ出しても例外にならない()
    {
        using var target = TestHelper.CreateTarget(16, 16);

        // 画像より大きい矩形（倍率 4 で 84x84）。画像内に入る上辺（y = 0）が描画される
        FadeMaskDragRectSkiaRenderer.DrawSelectionRect(
            target.Canvas, new EsRect(0, 0, 20, 20), new EsPoint(0, 0), 4.0, 16,
            DrawingColor.Red, DrawingColor.Blue);

        Assert.Equal(new SKColor(255, 0, 0), target.GetPixel(0, 0));
    }

    /// <summary>キャンバスが null の場合は例外になることを検証する。</summary>
    [Fact]
    public void DrawSelectionRect_キャンバスがnullの場合は例外になる()
    {
        Assert.Throws<ArgumentNullException>(() => FadeMaskDragRectSkiaRenderer.DrawSelectionRect(
            null!, new EsRect(0, 0, 10, 10), new EsPoint(0, 0), 4.0, 64, DrawingColor.Red, DrawingColor.Blue));
    }
}
