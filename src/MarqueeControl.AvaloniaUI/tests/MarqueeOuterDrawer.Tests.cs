using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core.Geometry;
using Estable.AvaloniaUI.Marquee.Drawing;
using Estable.Marquee.Decoration;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.AvaloniaUI.Marquee.Tests;

/// <summary><see cref="MarqueeOuterDrawer"/> の描画テスト（DrawingContext 描画の例外なし検証）。</summary>
/// <remarks>
/// 対象: <c>MarqueeOuterDrawer.cs</c>（フレームの非文字の描画のみ）。<br/>
/// 「どの矩形を暗転するか」「枠線を描くか」の判断はコアの <c>OuterDecoFrame</c> が単一所有するため、<br/>
/// 本テストは「与えられたフレームを描く」こと（例外なし・ブラシキャッシュの容量管理）のみを検証する。<br/>
/// フレームの解決（判断）はコアの <c>OuterDecoFrameTests</c> が、文字の描画は<br/>
/// <see cref="MarqueeLabelRendererTests"/> が検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class MarqueeOuterDrawerTests
{
    /// <summary>描画に使う暗転色（赤 50%）。</summary>
    internal static readonly DrawingColor Overlay = DrawingColor.FromArgb(128, 255, 0, 0);

    /// <summary>描画に使う枠線色（青・不透明）。</summary>
    internal static readonly DrawingColor Border = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>描画コンテキストを生成する。</summary>
    /// <returns>描画先のコンテキスト。破棄は呼び出し側。</returns>
    internal static DrawingContext CreateContext()
    {
        var rtb = new RenderTargetBitmap(new PixelSize(300, 300), new Vector(96, 96));
        return rtb.CreateDrawingContext();
    }

    /// <summary>テスト用のフレームを生成する。</summary>
    /// <param name="overlayRegions">暗転領域。null の場合は空。</param>
    /// <param name="borderRects">枠線の矩形。null の場合は空。</param>
    /// <returns>生成したフレーム。</returns>
    internal static OuterDecoFrame CreateFrame(
        IReadOnlyList<EsRect>? overlayRegions = null,
        IReadOnlyList<EsRect>? borderRects = null)
        => new(overlayRegions ?? [], Overlay, borderRects ?? [], Border);

    /// <summary>暗転領域と枠線を描画することを検証する。</summary>
    [AvaloniaFact]
    public void Draw_暗転領域と枠線を描画する()
    {
        using var ctx = CreateContext();

        MarqueeOuterDrawer.Draw(ctx, CreateFrame(
            overlayRegions: [new EsRect(0, 0, 300, 50), new EsRect(0, 50, 50, 250)],
            borderRects: [new EsRect(50, 50, 100, 2), new EsRect(50, 128, 100, 2)]));
    }

    /// <summary>何も持たないフレームでは何も描画しない（例外も出ない）ことを検証する。</summary>
    [AvaloniaFact]
    public void Draw_空のフレームでは何も描画しない()
    {
        using var ctx = CreateContext();

        MarqueeOuterDrawer.Draw(ctx, CreateFrame());
    }

    /// <summary>ブラシキャッシュ容量超過時も例外なく描画することを検証する。</summary>
    [AvaloniaFact]
    public void Draw_ブラシキャッシュ容量超過時も例外なく描画する()
    {
        // キャッシュ容量（CapacityLimitedCache の既定容量）を超える色を順次描画し、容量超過時のキャッシュクリア経路を実行する
        for (int i = 0; i <= CapacityLimitedCache<Color, SolidColorBrush>.DefaultCapacity; i++)
        {
            using var ctx = CreateContext();
            var argb = Color.FromArgb((byte)(100 + i % 100), (byte)(i * 2), (byte)(255 - i), (byte)(i * 3));
            var color = argb.ToDrawingColor();
            var frame = CreateFrame(
                overlayRegions: [new EsRect(0, 0, 300, 50)],
                borderRects: [new EsRect(0, 0, 300, 2)]) with { OverlayColor = color, BorderColor = color };

            MarqueeOuterDrawer.Draw(ctx, frame);
        }
    }
}
