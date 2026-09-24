using EsUtil.Platform.Core.Geometry;
using Estable.Marquee.Decoration;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.Marquee.Tests;

/// <summary><see cref="MarqueeOuterSkiaRenderer"/> の描画テスト（Skia のビットマップ上で画素を検証する）。</summary>
/// <remarks>
/// 対象: <c>MarqueeOuterSkiaRenderer.cs</c>（フレームの非文字の描画のみ）。<br/>
/// Skia のビットマップはウィンドウを必要としないため、UI 初期化なしで並列実行できる。<br/>
/// 「どの矩形を暗転するか」「枠線を描くか」の判断はコアの <c>OuterDecoFrame</c> が単一所有するため、<br/>
/// 本テストは「与えられたフレームを描く」ことのみを検証する。<br/>
/// 文字の描画は <c>MarqueeLabelRendererTests</c>、フォント解決は Platform.Core の <c>SkiaText</c> のテストが検証する。
/// </remarks>
public class MarqueeOuterSkiaRendererTests
{
    /// <summary>描画に使う暗転色（赤 50%）。</summary>
    private static readonly DrawingColor s_overlay = DrawingColor.FromArgb(128, 255, 0, 0);

    /// <summary>描画に使う枠線色（青・不透明）。</summary>
    private static readonly DrawingColor s_border = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>暗転を半透明で塗った結果の色（赤 50% を透明背景へ重ねた色。SKColor の引数順は R,G,B,A）。</summary>
    private static readonly SKColor s_overlayPixel = new(255, 0, 0, 128);

    /// <summary>描画されていない画素（透明）。</summary>
    private static readonly SKColor s_transparent = new(0, 0, 0, 0);

    /// <summary>テスト用のフレームを生成する。</summary>
    /// <param name="overlayRegions">暗転領域。null の場合は空。</param>
    /// <param name="borderRects">枠線の矩形。null の場合は空。</param>
    /// <returns>生成したフレーム。</returns>
    internal static OuterDecoFrame CreateFrame(
        IReadOnlyList<EsRect>? overlayRegions = null,
        IReadOnlyList<EsRect>? borderRects = null)
        => new(overlayRegions ?? [], s_overlay, borderRects ?? [], s_border);

    /// <summary>暗転領域を指定色で塗ることを検証する。</summary>
    [Fact]
    public void Draw_暗転領域を塗る()
    {
        using var target = TestHelper.CreateTarget(100, 100);

        MarqueeOuterSkiaRenderer.Draw(target.Canvas, CreateFrame(
            overlayRegions: [new EsRect(0, 0, 100, 30), new EsRect(0, 30, 20, 70)]));

        Assert.Equal(s_overlayPixel, target.GetPixel(50, 10));
        Assert.Equal(s_overlayPixel, target.GetPixel(10, 50));
        // 指定していない領域は透明のまま
        Assert.Equal(s_transparent, target.GetPixel(50, 50));
    }

    /// <summary>暗転領域が空の場合は何も塗らないことを検証する。</summary>
    [Fact]
    public void Draw_暗転領域が空なら何も塗らない()
    {
        using var target = TestHelper.CreateTarget(50, 50);

        MarqueeOuterSkiaRenderer.Draw(target.Canvas, CreateFrame());

        Assert.Equal(s_transparent, target.GetPixel(25, 25));
    }

    /// <summary>枠線の矩形を指定色で描くことを検証する。</summary>
    [Fact]
    public void Draw_枠線の矩形を描く()
    {
        using var target = TestHelper.CreateTarget(100, 100);

        MarqueeOuterSkiaRenderer.Draw(target.Canvas, CreateFrame(
            borderRects: [new EsRect(20, 30, 40, 2)]));

        Assert.Equal(SKColors.Blue, target.GetPixel(40, 30));
        Assert.Equal(s_transparent, target.GetPixel(40, 50));
    }

    /// <summary>枠線も暗転も持たないフレームでは何も描かないことを検証する。</summary>
    [Fact]
    public void Draw_空のフレームでは何も描かない()
    {
        using var target = TestHelper.CreateTarget(200, 100);

        MarqueeOuterSkiaRenderer.Draw(target.Canvas, CreateFrame());

        // ラベルは装飾の概念ではなくなった（サイズラベルはコントロールが要素として配置する）ため、
        // 本レンダラーが描くものは無い
        Assert.Equal(s_transparent, target.GetPixel(12, 16));
    }

    /// <summary>キャンバスが null の場合は例外になることを検証する。</summary>
    [Fact]
    public void Draw_キャンバスがnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() => MarqueeOuterSkiaRenderer.Draw(null!, CreateFrame()));
    }
}
