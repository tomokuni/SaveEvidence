using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.SelectionOverlay;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Tests.SelectionOverlay;

/// <summary><see cref="SelectionOverlayGraphicsRenderer"/>（選択矩形の破線オーバーレイ）のテスト。</summary>
/// <remarks>
/// 破線の幾何（位置・長さ・色の種別）はコアの <c>SelectionOutlineRasterizer</c> が単一所有するため、<br/>
/// 本テストは「列挙された破線が 2 色で描かれること」「描かない条件で空になること」を実画素で検証する。<br/>
/// 描画面の座標系はルーペ画像の物理ピクセルである（ウィンドウを必要としない）。
/// </remarks>
public sealed class SelectionOverlayGraphicsRendererTests
{
    /// <summary>選択矩形の 4 辺が 2 色の破線で描かれることを検証する。</summary>
    [Fact]
    internal void DrawSelectionRect_2色の破線を描く()
    {
        var colorA = DrawingColor.FromArgb(255, 255, 255, 255);
        var colorB = DrawingColor.FromArgb(255, 0, 0, 0);
        using var target = TestHelper.CreateTarget(64, 64);

        SelectionOverlayGraphicsRenderer.DrawSelectionRect(
            target.Graphics,
            new EsRect(100, 100, 4, 4),
            captureTopLeft: new EsPoint(100, 100),
            scale: 8,
            pixelSize: 64,
            colorA,
            colorB);

        Assert.True(target.CountColor(0, 0, 64, 64, colorA) > 0);
        Assert.True(target.CountColor(0, 0, 64, 64, colorB) > 0);
    }

    /// <summary>倍率が小さい場合は破線を描かないことを検証する。</summary>
    /// <remarks>1 倍では破線が潰れるため、描くかどうかの判断はコアが単一所有する。</remarks>
    [Fact]
    internal void DrawSelectionRect_低倍率では描かない()
    {
        var colorA = DrawingColor.FromArgb(255, 255, 255, 255);
        using var target = TestHelper.CreateTarget(64, 64);

        SelectionOverlayGraphicsRenderer.DrawSelectionRect(
            target.Graphics,
            new EsRect(10, 10, 4, 4),
            captureTopLeft: new EsPoint(10, 10),
            scale: 1,
            pixelSize: 64,
            colorA,
            DrawingColor.Black);

        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(32, 32));
    }

    /// <summary>切り出し領域と交差しない場合は破線を描かないことを検証する。</summary>
    [Fact]
    internal void DrawSelectionRect_領域外では描かない()
    {
        var colorA = DrawingColor.FromArgb(255, 255, 255, 255);
        using var target = TestHelper.CreateTarget(64, 64);

        // 切り出し領域の左上（0,0）から十分に離れた選択矩形（交差判定はコアが単一所有する）
        SelectionOverlayGraphicsRenderer.DrawSelectionRect(
            target.Graphics,
            new EsRect(1000, 1000, 4, 4),
            captureTopLeft: new EsPoint(0, 0),
            scale: 8,
            pixelSize: 64,
            colorA,
            DrawingColor.Black);

        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(32, 32));
    }

    /// <summary>描画面が null の場合は例外になることを検証する。</summary>
    [Fact]
    internal void DrawSelectionRect_描画面がnullなら例外になる()
        => Assert.Throws<ArgumentNullException>(() => SelectionOverlayGraphicsRenderer.DrawSelectionRect(
            null!,
            new EsRect(0, 0, 4, 4),
            new EsPoint(0, 0),
            8,
            64,
            DrawingColor.White,
            DrawingColor.Black));
}
