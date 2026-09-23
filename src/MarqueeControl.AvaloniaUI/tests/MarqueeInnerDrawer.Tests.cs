using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Platform.Core.Geometry;
using Estable.AvaloniaUI.Marquee.Drawing;
using Estable.Marquee.Decoration;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.AvaloniaUI.Marquee.Tests;

/// <summary><see cref="MarqueeInnerDrawer"/> の描画テスト（例外なく描画を完遂することを検証する）。</summary>
/// <remarks>
/// 対象: <c>MarqueeInnerDrawer.cs</c>（フレームの描画のみ）。<br/>
/// 描画内容を決める判断はコアの <c>InnerDecoFrame</c> が単一所有し、画素の実測は MewUI 版<br/>
/// （<c>MarqueeInnerSkiaRendererTests</c>）が担う。Avalonia の <see cref="RenderTargetBitmap"/> 経由は<br/>
/// <see cref="OperatingSystem.IsWindows"/> 以外や未初期化時にピクセルを読めないため、<br/>
/// 本テストは「描画が例外なく完了すること」を検証する。<br/>
/// <see cref="MarqueeOuterDrawerTests"/> と同じ方式である。
/// </remarks>
public class MarqueeInnerDrawerTests
{
    /// <summary>描画に使う塗り色（赤 16%。半透明）。</summary>
    private static readonly DrawingColor s_fill = DrawingColor.FromArgb(40, 255, 59, 48);

    /// <summary>描画に使う枠線色（赤・不透明）。</summary>
    private static readonly DrawingColor s_border = DrawingColor.FromArgb(255, 255, 59, 48);

    /// <summary>テスト用のフレームを生成する。</summary>
    /// <param name="fillRect">塗る矩形。null の場合は塗らない。</param>
    /// <param name="borderRects">枠線の矩形。null の場合は空。</param>
    /// <returns>生成したフレーム。</returns>
    private static InnerDecoFrame CreateFrame(EsRect? fillRect = null, IReadOnlyList<EsRect>? borderRects = null)
        => new(fillRect, s_fill, borderRects ?? [], s_border);

    /// <summary>描画コンテキストを生成する。</summary>
    /// <returns>描画先のコンテキスト。破棄は呼び出し側。</returns>
    private static DrawingContext CreateContext()
    {
        var rtb = new RenderTargetBitmap(new PixelSize(300, 300), new Vector(96, 96));
        return rtb.CreateDrawingContext();
    }

    /// <summary>塗り矩形と枠線を描画することを検証する。</summary>
    [AvaloniaFact]
    public void Draw_塗り矩形と枠線を描画する()
    {
        using var ctx = CreateContext();

        MarqueeInnerDrawer.Draw(ctx, CreateFrame(
            fillRect: new EsRect(50, 50, 40, 30),
            borderRects:
            [
                new EsRect(50, 50, 40, 2),
                new EsRect(50, 78, 40, 2),
                new EsRect(50, 52, 2, 26),
                new EsRect(88, 52, 2, 26),
            ]));
    }

    /// <summary>塗り矩形が無い場合は枠線のみを描画することを検証する。</summary>
    [AvaloniaFact]
    public void Draw_塗りなしでも枠線を描画する()
    {
        using var ctx = CreateContext();

        MarqueeInnerDrawer.Draw(ctx, CreateFrame(borderRects: [new EsRect(10, 10, 100, 2)]));
    }

    /// <summary>何も持たないフレームでは何も描画しない（例外も出ない）ことを検証する。</summary>
    [AvaloniaFact]
    public void Draw_空のフレームでは何も描画しない()
    {
        using var ctx = CreateContext();

        MarqueeInnerDrawer.Draw(ctx, CreateFrame());
    }

    /// <summary>描画コンテキストが null の場合は例外になることを検証する。</summary>
    [AvaloniaFact]
    public void Draw_コンテキストがnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() => MarqueeInnerDrawer.Draw(null!, CreateFrame()));
    }
}
