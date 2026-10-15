using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Crop;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Tests.Crop;

/// <summary><see cref="RectOverlayGraphicsRenderer"/>（オーバーレイ矩形の描画層）のテスト。</summary>
/// <remarks>
/// コア（<see cref="OverlayRectFrame"/>）が確定した描画入力を GDI+ へ写すだけの層であり、<br/>
/// 本テストは「枠が矩形の内側に描かれること」「破線が 2 色で描かれること」を実画素で検証する。<br/>
/// ウィンドウを必要としないため、オフスクリーンのビットマップ上で実行できる。
/// </remarks>
public sealed class RectOverlayGraphicsRendererTests
{
    /// <summary>実線の枠が前景色で矩形の内側へ描かれることを検証する。</summary>
    [Fact]
    internal void Render_実線は枠だけを描く()
    {
        var foreground = DrawingColor.FromArgb(255, 255, 0, 0);
        var frame = new OverlayRectFrame(
            new EsRect(10, 10, 40, 40),
            foreground,
            DrawingColor.FromArgb(255, 0, 0, 255),
            Thickness: 2,
            IsDashed: false,
            DashLength: 0,
            GapLength: 0);
        using var target = TestHelper.CreateTarget(80, 80);

        RectOverlayGraphicsRenderer.Render(target.Graphics, frame);

        // 枠は矩形の内側（半幅ぶん内側へ寄せた位置）に描かれる
        Assert.Equal(foreground, target.GetPixel(11, 20));
        Assert.Equal(foreground, target.GetPixel(30, 11));
        // 矩形の内部は塗らない
        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(30, 30));
        // 矩形の外側は塗らない
        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(5, 5));
    }

    /// <summary>破線の枠が背景色と前景色の 2 色で描かれることを検証する。</summary>
    [Fact]
    internal void Render_破線は2色で描く()
    {
        var foreground = DrawingColor.FromArgb(255, 255, 0, 0);
        var background = DrawingColor.FromArgb(255, 0, 0, 255);
        var frame = new OverlayRectFrame(
            new EsRect(0, 0, 80, 80),
            foreground,
            background,
            Thickness: 1,
            IsDashed: true,
            DashLength: 4,
            GapLength: 4);
        using var target = TestHelper.CreateTarget(80, 80);

        RectOverlayGraphicsRenderer.Render(target.Graphics, frame);

        // 破線は「背景色の実線 → 前景色の破線」の 2 回描画である（どちらの色も残る）
        Assert.True(target.CountColor(0, 0, 80, 80, foreground) > 0);
        Assert.True(target.CountColor(0, 0, 80, 80, background) > 0);
    }

    /// <summary>描画できる大きさでない場合は何も描画しないことを検証する。</summary>
    /// <param name="width">矩形の幅。</param>
    /// <param name="height">矩形の高さ。</param>
    [Theory]
    [InlineData(0, 40)]
    [InlineData(40, 0)]
    [InlineData(-1, 40)]
    internal void Render_空の矩形は描かない(double width, double height)
    {
        var frame = new OverlayRectFrame(
            new EsRect(10, 10, width, height),
            DrawingColor.Red,
            DrawingColor.Blue,
            Thickness: 2,
            IsDashed: false,
            DashLength: 0,
            GapLength: 0);
        using var target = TestHelper.CreateTarget(80, 80);

        RectOverlayGraphicsRenderer.Render(target.Graphics, frame);

        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(11, 11));
    }

    /// <summary>描画面が null の場合は例外になることを検証する。</summary>
    [Fact]
    internal void Render_描画面がnullなら例外になる()
        => Assert.Throws<ArgumentNullException>(() => RectOverlayGraphicsRenderer.Render(
            null!,
            new OverlayRectFrame(
                new EsRect(0, 0, 10, 10),
                DrawingColor.Red,
                DrawingColor.Blue,
                Thickness: 1,
                IsDashed: false,
                DashLength: 0,
                GapLength: 0)));
}
