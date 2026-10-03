using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using Xunit;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Tests.ZoomView;

/// <summary><see cref="ZoomSkiaRenderer"/>（MewUI の描画層）のテスト。</summary>
/// <remarks>
/// 描画層は「セッションが確定した表示矩形へ画像を 1 回で描く」だけであり、判断・計算を持たない。<br/>
/// 本テストは実画素を読み、表示矩形（Fit の収まり・パン後の位置）どおりに描かれることを検証する。
/// </remarks>
public class ZoomSkiaRendererTests
{
    /// <summary>表示条件を確定させたセッションを生成する。</summary>
    /// <param name="image">ズーム対象の画像。</param>
    /// <param name="viewportSize">ビューポートの一辺（DIP）。</param>
    /// <returns>セッション。</returns>
    private static ZoomViewSession CreateSession(EsBitmap image, double viewportSize)
    {
        var session = new ZoomViewSession();
        session.SetViewport(new EsSize(viewportSize, viewportSize));
        session.SetSource(hasSource: true, new EsSize(image.Width, image.Height));
        return session;
    }

    /// <summary>Fit 表示で画像がビューポート全体へ描画されることを検証する。</summary>
    [Fact]
    public void Render_Fitで画像が描画される()
    {
        using var image = TestHelper.CreateBitmap(100, 100, SKColors.Red);
        var session = CreateSession(image, 200);
        using var target = TestHelper.CreateTarget(200, 200);

        ZoomSkiaRenderer.Render(target.Canvas, image, session);

        Assert.Equal(SKColors.Red, target.GetPixel(100, 100));
    }

    /// <summary>画像が未設定の場合は何も描画しないことを検証する。</summary>
    [Fact]
    public void Render_画像なしは何も描画しない()
    {
        var session = new ZoomViewSession();
        session.SetViewport(new EsSize(100, 100));
        using var target = TestHelper.CreateTarget(100, 100);

        ZoomSkiaRenderer.Render(target.Canvas, source: null, session);

        Assert.Equal(0, target.GetPixel(50, 50).Alpha);
    }

    /// <summary>パン後の表示矩形へ画像が描画されることを検証する。</summary>
    /// <remarks>表示矩形はコアが確定するため、描画層は矩形どおりに描くだけでよい。</remarks>
    [Fact]
    public void Render_パン後の位置へ描画される()
    {
        using var image = TestHelper.CreateBitmap(400, 400, SKColors.Red);
        var session = CreateSession(image, 200);
        session.SetZoom(100);
        // 1 ノッチ（48 DIP）を実効倍率 1.0 で除した 48 コンテンツ座標ぶんパンする
        session.PanByWheel(new EsVector(0, 1), shift: false);
        using var target = TestHelper.CreateTarget(200, 200);

        ZoomSkiaRenderer.Render(target.Canvas, image, session);

        // オフセット 48 に対して画像は上へ 48 移動するため、y=0 の画素は画像の y=48 になる（全面赤）
        Assert.Equal(SKColors.Red, target.GetPixel(100, 0));
        Assert.Equal(SKColors.Red, target.GetPixel(100, 199));
    }

    /// <summary>表示矩形が空（画像サイズ 0）の場合は描画しないことを検証する。</summary>
    [Fact]
    public void Render_退化した画像は描画しない()
    {
        using var image = TestHelper.CreateBitmap(1, 1, SKColors.Red);
        var session = new ZoomViewSession();
        session.SetViewport(new EsSize(100, 100));
        session.SetSource(hasSource: true, new EsSize(0, 0));
        using var target = TestHelper.CreateTarget(100, 100);

        ZoomSkiaRenderer.Render(target.Canvas, image, session);

        Assert.Equal(0, target.GetPixel(50, 50).Alpha);
    }
}
