using Avalonia.Media;
using EsUtil.Platform.AvaloniaUI;
using Estable.Marquee.Decoration;

namespace Estable.AvaloniaUI.Marquee.Drawing;

/// <summary>選択矩形（穴あけ矩形）の内側に描く装飾（塗り・内枠）を描画する静的クラス。</summary>
/// <remarks>
/// MewUI 版の <c>MarqueeInnerSkiaRenderer.Draw</c> と同じ引数の形（<see cref="InnerDecoFrame"/> を描くだけ）とする。<br/>
/// 「塗るか」「枠線を描くか」の判断は装飾コアの <see cref="InnerDecoFrame"/> が持つため、<br/>
/// 本クラスはフレームの各項目を Avalonia の描画 API へ写すだけとする（外観値・判断を持たない）。<br/>
/// 描画順は「塗り → 内枠」であり、外側の装飾（<see cref="MarqueeOuterDrawer"/>）の後に呼ぶ<br/>
/// （暗転マスクの上へ内側の塗りを重ねる）。
/// </remarks>
internal static class MarqueeInnerDrawer
{
    /// <summary>穴あけ矩形の内側の塗りと枠線を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="frame">描画内容（コアが決定した値）。</param>
    /// <remarks>
    /// 1. 塗り矩形（<see cref="InnerDecoFrame.FillRect"/>）を塗り色で塗る（null なら何もしない）。<br/>
    /// 2. 枠線（<see cref="InnerDecoFrame.BorderRects"/>）を描く（空なら何もしない）。<br/>
    /// <br/>
    /// 描画の要否はフレームが持つため、本メソッドに分岐はない（空のコレクションは単に何もしない）。
    /// </remarks>
    public static void Draw(DrawingContext context, in InnerDecoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(context);

        // ---- 穴あけ矩形の内側の塗り（半透明を許容する） ----
        if (frame.FillRect is { } fill)
        {
            context.FillRectangle(MarqueeBrushCache.GetBrush(frame.FillColor.ToAvaloniaColor()), fill.ToAvaloniaRect());
        }

        // ---- 穴あけ矩形の内側の枠線（四辺・矩形の内側） ----
        if (frame.BorderRects.Count > 0)
        {
            var borderBrush = MarqueeBrushCache.GetBrush(frame.BorderColor.ToAvaloniaColor());
            foreach (var rect in frame.BorderRects)
            {
                context.DrawRectangle(borderBrush, null, rect.ToAvaloniaRect());
            }
        }
    }
}
