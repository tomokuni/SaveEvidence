using EsUtil.Platform.Core.Geometry;
using Estable.Marquee.Decoration;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.Marquee.Tests;

/// <summary><see cref="MarqueeInnerSkiaRenderer"/> の描画テスト（Skia のビットマップ上で画素を検証する）。</summary>
/// <remarks>
/// 対象: <c>MarqueeInnerSkiaRenderer.cs</c>（フレームの描画のみ）。<br/>
/// Skia のビットマップはウィンドウを必要としないため、UI 初期化なしで並列実行できる。<br/>
/// 「塗るか」「枠線を描くか」の判断はコアの <c>InnerDecoFrame</c> が単一所有するため、<br/>
/// 本テストは「与えられたフレームを描く」ことのみを検証する。
/// </remarks>
public class MarqueeInnerSkiaRendererTests
{
    /// <summary>描画に使う塗り色（赤 16%）。</summary>
    private static readonly DrawingColor s_fill = DrawingColor.FromArgb(40, 255, 59, 48);

    /// <summary>描画に使う枠線色（赤・不透明）。</summary>
    private static readonly DrawingColor s_border = DrawingColor.FromArgb(255, 255, 59, 48);

    /// <summary>塗りを半透明で描いた結果の予期値（乗算済みアルファの丸めで成分が厳密一致しないため、透明度と色相で検証する）。</summary>
    private static readonly SKColor s_fillPixel = new(255, 59, 48, 40);

    /// <summary>枠線を描いた結果の色（不透明）。</summary>
    private static readonly SKColor s_borderPixel = new(255, 59, 48, 255);

    /// <summary>半透明の画素が指定のアルファと赤系の色相で描かれていることを検証する。</summary>
    /// <param name="pixel">検証する画素。</param>
    /// <remarks>Skia は乗算済みアルファで保持するため、半透明色の読み戻しは成分が丸められる（例: G=59 → 57）。</remarks>
    private static void AssertTranslucentRed(SKColor pixel)
    {
        Assert.Equal(s_fillPixel.Alpha, pixel.Alpha);
        Assert.True(pixel.Red > pixel.Green, "赤系の色相になっていない。");
        Assert.True(pixel.Red > pixel.Blue, "赤系の色相になっていない。");
    }

    /// <summary>テスト用のフレームを生成する。</summary>
    /// <param name="fillRect">塗る矩形。null の場合は塗らない。</param>
    /// <param name="borderRects">枠線の矩形。null の場合は空。</param>
    /// <returns>生成したフレーム。</returns>
    private static InnerDecoFrame CreateFrame(EsRect? fillRect = null, IReadOnlyList<EsRect>? borderRects = null)
        => new(fillRect, s_fill, borderRects ?? [], s_border);

    /// <summary>塗り矩形を指定色（半透明）で塗ることを検証する。</summary>
    [Fact]
    public void Draw_塗り矩形を塗る()
    {
        using var target = TestHelper.CreateTarget(100, 100);

        MarqueeInnerSkiaRenderer.Draw(target.Canvas, CreateFrame(fillRect: new EsRect(10, 10, 40, 30)));

        AssertTranslucentRed(target.GetPixel(30, 25));
        // 指定していない領域は透明のまま
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(5, 5));
    }

    /// <summary>塗り矩形が null の場合は何も塗らないことを検証する。</summary>
    [Fact]
    public void Draw_塗り矩形が無ければ何も塗らない()
    {
        using var target = TestHelper.CreateTarget(100, 100);

        MarqueeInnerSkiaRenderer.Draw(target.Canvas, CreateFrame());

        Assert.Equal(0, target.CountColor(0, 0, 100, 100, s_borderPixel));
    }

    /// <summary>枠線の矩形を指定色で描くことを検証する。</summary>
    [Fact]
    public void Draw_枠線の矩形を描く()
    {
        using var target = TestHelper.CreateTarget(100, 100);

        // 実線の枠線は内側 4 辺（太さ 2）で構成される
        MarqueeInnerSkiaRenderer.Draw(target.Canvas, CreateFrame(
            fillRect: new EsRect(50, 50, 40, 30),
            borderRects:
            [
                new EsRect(50, 50, 40, 2),
                new EsRect(50, 78, 40, 2),
                new EsRect(50, 52, 2, 26),
                new EsRect(88, 52, 2, 26),
            ]));

        // 塗り（半透明）の上へ枠線が重なるため、辺の位置は不透明の枠線色になる
        Assert.Equal(s_borderPixel, target.GetPixel(70, 50));
        Assert.Equal(s_borderPixel, target.GetPixel(70, 79));
        Assert.Equal(s_borderPixel, target.GetPixel(50, 65));
        Assert.Equal(s_borderPixel, target.GetPixel(89, 65));
        // 辺以外は塗りの色
        AssertTranslucentRed(target.GetPixel(70, 65));
    }

    /// <summary>何も持たないフレームでは何も描画しない（例外も出ない）ことを検証する。</summary>
    [Fact]
    public void Draw_空のフレームでは何も描画しない()
    {
        using var target = TestHelper.CreateTarget(50, 50);

        MarqueeInnerSkiaRenderer.Draw(target.Canvas, CreateFrame());

        Assert.Equal(0, target.CountColor(0, 0, 50, 50, s_borderPixel));
    }

    /// <summary>キャンバスが null の場合は例外になることを検証する。</summary>
    [Fact]
    public void Draw_キャンバスがnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() => MarqueeInnerSkiaRenderer.Draw(null!, CreateFrame()));
    }
}
