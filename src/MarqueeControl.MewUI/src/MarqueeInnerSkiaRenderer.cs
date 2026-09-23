using Estable.Marquee.Decoration;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;

namespace Estable.MewUI.Marquee;

/// <summary>選択矩形（穴あけ矩形）の内側に描く装飾（塗り・内枠）を Skia で描画するレンダラー。</summary>
/// <remarks>
/// Avalonia 版の <c>MarqueeInnerDrawer.Draw</c> と同じ引数の形（<see cref="InnerDecoFrame"/> を描くだけ）とする。<br/>
/// 「塗るか」「枠線を描くか」の判断は装飾コアの <see cref="InnerDecoFrame"/> が持つため、<br/>
/// 本クラスはフレームの各項目を <see cref="SKCanvas"/> の描画 API へ写すだけとする（外観値・判断を持たない）。<br/>
/// 座標はすべて DIP で扱い、呼び出し側が DPI スケーリングを適用済みであることを前提とする。<br/>
/// 描画順は「塗り → 内枠」であり、外側の装飾（<see cref="MarqueeOuterSkiaRenderer"/>）の後に呼ぶ。
/// </remarks>
internal static class MarqueeInnerSkiaRenderer
{
    /// <summary>穴あけ矩形の内側の塗りと枠線を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="frame">描画内容（コアが決定した値）。</param>
    /// <remarks>
    /// 1. 塗り矩形（<see cref="InnerDecoFrame.FillRect"/>）を塗り色で塗る（null なら何もしない）。<br/>
    /// 2. 枠線（<see cref="InnerDecoFrame.BorderRects"/>）を描く（空なら何もしない）。<br/>
    /// <br/>
    /// 描画の要否はフレームが持つため、本メソッドに分岐はない（空のコレクションは単に何もしない）。
    /// </remarks>
    public static void Draw(SKCanvas canvas, in InnerDecoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        // ---- 穴あけ矩形の内側の塗り（半透明を許容する） ----
        if (frame.FillRect is { } fill)
        {
            using var fillPaint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill,
                Color = frame.FillColor.ToSKColor(),
            };

            canvas.DrawRect(ToSKRect(fill), fillPaint);
        }

        // ---- 穴あけ矩形の内側の枠線（四辺・矩形の内側） ----
        if (frame.BorderRects.Count > 0)
        {
            using var borderPaint = new SKPaint
            {
                IsAntialias = false,
                Style = SKPaintStyle.Fill,
                Color = frame.BorderColor.ToSKColor(),
            };

            foreach (var rect in frame.BorderRects)
            {
                canvas.DrawRect(ToSKRect(rect), borderPaint);
            }
        }
    }

    /// <summary>コアの矩形を Skia の矩形へ変換する。</summary>
    /// <param name="rect">コアの矩形（DIP）。</param>
    /// <returns>Skia の矩形。</returns>
    private static SKRect ToSKRect(EsRect rect)
        => new((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom);
}
