using Avalonia.Media;
using EsUtil.Platform.AvaloniaUI;
using Estable.Marquee.Decoration;

namespace Estable.AvaloniaUI.Marquee.Drawing;

/// <summary>選択矩形の外側に描く装飾（暗転マスク・穴あけ枠）を描画する静的クラス。</summary>
/// <remarks>
/// MewUI 版の <c>MarqueeOuterSkiaRenderer.Draw</c> と同じ引数の形（<see cref="OuterDecoFrame"/> を描くだけ）とする。<br/>
/// 「どの矩形を暗転するか」「枠線を描くか」の判断は装飾コアの <see cref="OuterDecoFrame"/> が持つため、<br/>
/// 本クラスはフレームの各項目を Avalonia の描画 API へ写すだけとする（外観値・判断を持たない）。<br/>
/// <b>文字（ラベル）は本クラスが描かない</b>: テキストの実寸測定が必要なため、描画完了時にホストへ通知し<br/>
/// <see cref="MarqueeLabelRenderer"/> が描く（文字の描画をホストの責務とする）。<br/>
/// 選択矩形そのもの（枠線・ドラッグハンドル・始点マーカー・座標ラベル）は <see cref="MarqueeDrawer"/> が担う。
/// </remarks>
internal static class MarqueeOuterDrawer
{
    /// <summary>色に対応するブラシを取得する（キャッシュ優先・未登録なら生成して登録）。</summary>
    /// <param name="color">ブラシの色。</param>
    /// <returns>色に対応する <see cref="SolidColorBrush"/>。</returns>
    private static SolidColorBrush GetBrush(Color color)
        => MarqueeBrushCache.GetBrush(color);

    /// <summary>暗転マスク・枠線を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="frame">描画内容（コアが決定した値）。</param>
    /// <remarks>
    /// 1. 暗転領域（<see cref="OuterDecoFrame.OverlayRegions"/>）を暗転色で塗る（空なら何もしない）。<br/>
    /// 2. 枠線（<see cref="OuterDecoFrame.BorderRects"/>）を描く（空なら何もしない）。<br/>
    /// <br/>
    /// 描画の要否はフレームが持つため、本メソッドに分岐はない（空のコレクションは単に何もしない）。<br/>
    /// ラベル（<see cref="OuterDecoFrame.Labels"/>）は文字の描画をホストが担うため、本メソッドは描かない。
    /// </remarks>
    public static void Draw(DrawingContext context, in OuterDecoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(context);

        // ---- 暗転マスク（4辺の穴あけ。矩形計算はコアの純粋関数に委譲） ----
        if (frame.OverlayRegions.Count > 0)
        {
            var overlayBrush = GetBrush(frame.OverlayColor.ToAvaloniaColor());
            foreach (var region in frame.OverlayRegions)
            {
                context.FillRectangle(overlayBrush, region.ToAvaloniaRect());
            }
        }

        // ---- 選択枠線（四辺・矩形の内側） ----
        if (frame.BorderRects.Count > 0)
        {
            var borderBrush = GetBrush(frame.BorderColor.ToAvaloniaColor());
            foreach (var rect in frame.BorderRects)
            {
                context.DrawRectangle(borderBrush, null, rect.ToAvaloniaRect());
            }
        }
    }
}
