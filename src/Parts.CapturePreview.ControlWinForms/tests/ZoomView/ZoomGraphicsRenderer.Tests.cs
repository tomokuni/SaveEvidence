using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.ZoomView;
using EsUtil.Ui.WinForms.Interop.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Tests.ZoomView;

/// <summary><see cref="ZoomGraphicsRenderer"/>（WinForms の描画層）のテスト。</summary>
/// <remarks>
/// 描画層は「セッションが確定した表示矩形へ画像を 1 回で描く」だけであり、判断・計算を持たない。<br/>
/// 本テストは実画素を読み、表示矩形（Fit の収まり・パン後の位置）どおりに描かれることを検証する。<br/>
/// ウィンドウ（ハンドル）を必要としないため、オフスクリーンのビットマップ上で実行できる。
/// </remarks>
public sealed class ZoomGraphicsRendererTests
{
    /// <summary>Fit 表示で画像がビューポート全体へ描画されることを検証する。</summary>
    [Fact]
    internal void Render_Fitで画像が描画される()
    {
        using var image = TestHelper.CreateBitmap(100, 100, DrawingColor.FromArgb(255, 255, 0, 0));
        using var bitmap = EsBitmapDrawingInterop.ToDrawingBitmap(image);
        var session = CreateSession(image, 200);
        using var target = TestHelper.CreateTarget(200, 200);

        ZoomGraphicsRenderer.Render(target.Graphics, bitmap, session);

        Assert.Equal(DrawingColor.FromArgb(255, 255, 0, 0), target.GetPixel(100, 100));
    }

    /// <summary>画像が未設定の場合は何も描画しないことを検証する。</summary>
    [Fact]
    internal void Render_画像なしは何も描画しない()
    {
        var session = new ZoomViewSession();
        session.SetViewport(new EsSize(100, 100));
        using var target = TestHelper.CreateTarget(100, 100);

        ZoomGraphicsRenderer.Render(target.Graphics, source: null, session);

        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(50, 50));
    }

    /// <summary>パン後の表示矩形へ画像が描画されることを検証する。</summary>
    /// <remarks>表示矩形はコアが確定するため、描画層は矩形どおりに描くだけでよい。</remarks>
    [Fact]
    internal void Render_パン後の位置へ描画される()
    {
        using var image = TestHelper.CreateBitmap(400, 400, DrawingColor.FromArgb(255, 255, 0, 0));
        using var bitmap = EsBitmapDrawingInterop.ToDrawingBitmap(image);
        var session = CreateSession(image, 200);
        session.SetZoom(100);
        // 1 ノッチ（48 DIP）を実効倍率 1.0 で除した 48 コンテンツ座標ぶんパンする
        session.PanByWheel(new EsVector(0, 1), shift: false);
        using var target = TestHelper.CreateTarget(200, 200);

        ZoomGraphicsRenderer.Render(target.Graphics, bitmap, session);

        // オフセット 48 に対して画像は上へ 48 移動するため、y=0 の画素は画像の y=48 になる（全面赤）
        Assert.Equal(DrawingColor.FromArgb(255, 255, 0, 0), target.GetPixel(100, 0));
        Assert.Equal(DrawingColor.FromArgb(255, 255, 0, 0), target.GetPixel(100, 199));
    }

    /// <summary>表示矩形が空（画像サイズ 0）の場合は描画しないことを検証する。</summary>
    [Fact]
    internal void Render_退化した画像は描画しない()
    {
        using var image = TestHelper.CreateBitmap(1, 1, DrawingColor.FromArgb(255, 255, 0, 0));
        using var bitmap = EsBitmapDrawingInterop.ToDrawingBitmap(image);
        var session = new ZoomViewSession();
        session.SetViewport(new EsSize(100, 100));
        session.SetSource(hasSource: true, new EsSize(0, 0));
        using var target = TestHelper.CreateTarget(100, 100);

        ZoomGraphicsRenderer.Render(target.Graphics, bitmap, session);

        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(50, 50));
    }

    /// <summary>スクロールバーのトラックとサムが指定の矩形へ描かれることを検証する。</summary>
    [Fact]
    internal void RenderScrollBar_トラックとサムを描く()
    {
        var track = DrawingColor.FromArgb(255, 0, 0, 255);
        var thumb = DrawingColor.FromArgb(255, 0, 255, 0);
        var bar = new ZoomScrollBar(new EsRect(0, 0, 100, 8), new EsRect(10, 0, 30, 8));
        using var target = TestHelper.CreateTarget(120, 20);

        ZoomGraphicsRenderer.RenderScrollBar(
            target.Graphics, bar, track, thumb, drawTrack: true, DrawingColor.Transparent, 0);

        Assert.Equal(track, target.GetPixel(2, 4));
        Assert.Equal(thumb, target.GetPixel(20, 4));
    }

    /// <summary>トラックを描かない指定ではサムだけを描くことを検証する。</summary>
    [Fact]
    internal void RenderScrollBar_トラックを描かない指定()
    {
        var track = DrawingColor.FromArgb(255, 0, 0, 255);
        var thumb = DrawingColor.FromArgb(255, 0, 255, 0);
        var bar = new ZoomScrollBar(new EsRect(0, 0, 100, 8), new EsRect(10, 0, 30, 8));
        using var target = TestHelper.CreateTarget(120, 20);

        ZoomGraphicsRenderer.RenderScrollBar(
            target.Graphics, bar, track, thumb, drawTrack: false, DrawingColor.Transparent, 0);

        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(2, 4));
        Assert.Equal(thumb, target.GetPixel(20, 4));
    }

    /// <summary>スクロールバーが null の場合は何も描画しないことを検証する。</summary>
    [Fact]
    internal void RenderScrollBar_バーなしは何も描画しない()
    {
        using var target = TestHelper.CreateTarget(20, 20);

        ZoomGraphicsRenderer.RenderScrollBar(
            target.Graphics, null, DrawingColor.Red, DrawingColor.Blue, drawTrack: true, DrawingColor.Transparent, 0);

        Assert.Equal(TestHelper.EmptyPixel, target.GetPixel(5, 5));
    }

    /// <summary>スクロールバーの枠がトラックの縁へ描かれることを検証する。</summary>
    [Fact]
    internal void RenderScrollBar_枠を描く()
    {
        var track = DrawingColor.FromArgb(255, 255, 255, 255);
        var thumb = DrawingColor.FromArgb(255, 0, 255, 0);
        var border = DrawingColor.FromArgb(255, 255, 0, 0);
        var bar = new ZoomScrollBar(new EsRect(10, 10, 100, 20), new EsRect(30, 10, 30, 20));
        using var target = TestHelper.CreateTarget(140, 40);

        ZoomGraphicsRenderer.RenderScrollBar(
            target.Graphics, bar, track, thumb, drawTrack: true, border, borderThickness: 2);

        // 枠はトラックの縁（上辺の内側）へ描かれ、内側はトラックの色のままになる
        Assert.Equal(border, target.GetPixel(60, 10));
        Assert.Equal(track, target.GetPixel(60, 20));
    }

    /// <summary>表示条件を確定させたセッションを生成する。</summary>
    /// <param name="image">ズーム対象の画像。</param>
    /// <param name="viewportSize">ビューポートの一辺（DIP）。</param>
    /// <returns>セッション。</returns>
    private static ZoomViewSession CreateSession(EsReadOnlyBitmap image, double viewportSize)
    {
        var session = new ZoomViewSession();
        session.SetViewport(new EsSize(viewportSize, viewportSize));
        session.SetSource(hasSource: true, new EsSize(image.Width, image.Height));
        return session;
    }
}
