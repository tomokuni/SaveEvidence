using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;

namespace Estable.MewUI.ZoomView.Tests;

/// <summary><see cref="ZoomSkiaRenderer"/> の描画テスト（Skia のビットマップ上で画素を検証する）。</summary>
/// <remarks>
/// Skia のビットマップはウィンドウを必要としないため、UI 初期化なしで並列実行できる。<br/>
/// 表示矩形の計算はコア（<c>ZoomMath</c>）が単一所有するため、本テストは描画結果（どの位置に何色が出るか）を検証する。
/// </remarks>
public class ZoomSkiaRendererTests
{
    /// <summary>画像がビューポートより小さい場合は中央寄せで描画されることを検証する。</summary>
    [Fact]
    public void ComputeImageRect_画像が小さい場合は中央寄せになる()
    {
        // 画像 400x300、ビューポート 200x200 → Fit スケール = min(0.5, 0.667) = 0.5
        // 表示サイズ 200x150、中央寄せ原点 = (0, (200-150)/2) = (0, 25)
        var rect = ZoomSkiaRenderer.ComputeImageRect(
            new EsSize(400, 300), new EsSize(200, 200), 0, default, centerAlign: true);

        Assert.Equal(0, rect.X);
        Assert.Equal(25, rect.Y);
        Assert.Equal(200, rect.Width);
        Assert.Equal(150, rect.Height);
    }

    /// <summary>中央寄せを無効にすると原点が左上になることを検証する。</summary>
    [Fact]
    public void ComputeImageRect_中央寄せ無効なら左上に置く()
    {
        var rect = ZoomSkiaRenderer.ComputeImageRect(
            new EsSize(400, 300), new EsSize(200, 200), 0, default, centerAlign: false);

        Assert.Equal(0, rect.X);
        Assert.Equal(0, rect.Y);
        Assert.Equal(200, rect.Width);
        Assert.Equal(150, rect.Height);
    }

    /// <summary>固定倍率では倍率がそのまま表示サイズへ反映されることを検証する。</summary>
    [Fact]
    public void ComputeImageRect_固定倍率は倍率どおりの表示サイズになる()
    {
        // 画像 100x100、ビューポート 400x400、200% → 表示サイズ 200x200、中央寄せ (100, 100)
        var rect = ZoomSkiaRenderer.ComputeImageRect(
            new EsSize(100, 100), new EsSize(400, 400), 200, default, centerAlign: true);

        Assert.Equal(100, rect.X);
        Assert.Equal(100, rect.Y);
        Assert.Equal(200, rect.Width);
        Assert.Equal(200, rect.Height);
    }

    /// <summary>画像がビューポートより大きい場合はオフセットでパンすることを検証する。</summary>
    [Fact]
    public void ComputeImageRect_画像が大きい場合はオフセットでパンする()
    {
        // 画像 400x400、ビューポート 200x200、100% → 表示サイズ 400x400
        // オフセット (100, 50) → 原点 = (-100, -50)
        var rect = ZoomSkiaRenderer.ComputeImageRect(
            new EsSize(400, 400), new EsSize(200, 200), 100, new EsVector(100, 50), centerAlign: true);

        Assert.Equal(-100, rect.X);
        Assert.Equal(-50, rect.Y);
        Assert.Equal(400, rect.Width);
        Assert.Equal(400, rect.Height);
    }

    /// <summary>画像未設定（サイズ 0）の場合は空の矩形になることを検証する。</summary>
    [Fact]
    public void ComputeImageRect_画像サイズが0なら空の矩形になる()
    {
        var rect = ZoomSkiaRenderer.ComputeImageRect(default, new EsSize(200, 200), 0, default, centerAlign: true);

        Assert.Equal(default, rect);
    }

    /// <summary>Fit 表示で画像がビューポートの中央に描画されることを検証する。</summary>
    [Fact]
    public void Render_Fit表示では画像が中央に描画される()
    {
        // 50x50 の赤画像を 100x200 のビューポートへ Fit（= 200%）
        // → 表示サイズ 100x100、中央寄せ原点 = (0, (200-100)/2) = (0, 50)
        using var source = TestHelper.CreateBitmap(50, 50, SKColors.Red);
        using var target = TestHelper.CreateTarget(100, 200);

        ZoomSkiaRenderer.Render(
            target.Canvas, source, new EsSize(50, 50), new EsSize(100, 200), 0, default, centerAlign: true);

        // 画像は縦方向の中央に描画される
        Assert.Equal(SKColors.Red, target.GetPixel(50, 100));
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(50, 10));
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(50, 190));
    }

    /// <summary>固定倍率で画像が中央寄せで描画されることを検証する。</summary>
    [Fact]
    public void Render_固定倍率で画像を中央寄せで描画する()
    {
        // 10x10 の赤画像を 100%、ビューポート 40x40 → 表示 10x10、中央寄せ原点 (15, 15)
        using var source = TestHelper.CreateBitmap(10, 10, SKColors.Red);
        using var target = TestHelper.CreateTarget(40, 40);

        ZoomSkiaRenderer.Render(
            target.Canvas, source, new EsSize(10, 10), new EsSize(40, 40), 100, default, centerAlign: true);

        Assert.Equal(SKColors.Red, target.GetPixel(20, 20));
        // 画像の外は描画されない
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(5, 5));
    }

    /// <summary>オフセットでパンした位置へ描画されることを検証する。</summary>
    [Fact]
    public void Render_オフセットでパンした位置へ描画する()
    {
        // 100x100 の赤画像を 100%、ビューポート 60x60、オフセット (50, 0) → 原点 (-50, 0)
        using var source = TestHelper.CreateBitmap(100, 100, SKColors.Red);
        using var target = TestHelper.CreateTarget(60, 60);

        ZoomSkiaRenderer.Render(
            target.Canvas, source, new EsSize(100, 100), new EsSize(60, 60), 100, new EsVector(50, 0), centerAlign: true);

        // 画像の左半分（パンで可視化された部分）が描画される
        Assert.Equal(SKColors.Red, target.GetPixel(5, 5));
        Assert.Equal(SKColors.Red, target.GetPixel(49, 59));
    }

    /// <summary>ソースが null の場合は何も描画しないことを検証する。</summary>
    [Fact]
    public void Render_ソースがnullなら何も描画しない()
    {
        using var target = TestHelper.CreateTarget(40, 40);

        ZoomSkiaRenderer.Render(target.Canvas, null, new EsSize(10, 10), new EsSize(40, 40), 100, default, centerAlign: true);

        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(5, 5));
    }

    /// <summary>キャンバスが null の場合は例外になることを検証する。</summary>
    [Fact]
    public void Render_キャンバスがnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(
            () => ZoomSkiaRenderer.Render(null!, null, new EsSize(10, 10), new EsSize(40, 40), 100, default, true));
    }
}
