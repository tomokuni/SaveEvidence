using EsUtil.Platform.Core.Imaging;
using Estable.Marquee.Decoration;
using SkiaSharp;

namespace Estable.MewUI.Marquee;

/// <summary>選択矩形の外側に描く装飾（暗転マスク・穴あけ枠）を Skia で描画するレンダラー。</summary>
/// <remarks>
/// Avalonia 版の <c>MarqueeOuterDrawer.Draw</c> と同じ引数の形（<see cref="OuterDecoFrame"/> を描くだけ）とする。<br/>
/// 「どの矩形を暗転するか」「枠線を描くか」の判断は装飾コアの <see cref="OuterDecoFrame"/> が持つため、<br/>
/// 本クラスはフレームの各項目を <see cref="SKCanvas"/> の描画 API へ写すだけとする（外観値・判断を持たない）。<br/>
/// <b>文字（ラベル）は本クラスが描かない</b>: テキストの実寸測定が必要なため、描画完了時にホストへ通知し<br/>
/// <see cref="MarqueeLabelRenderer"/> が描く（文字の描画をホストの責務とする）。<br/>
/// 座標はすべて DIP で扱い、呼び出し側が DPI スケーリングを適用済みであることを前提とする。<br/>
/// 選択矩形そのもの（ハンドル・始点マーカー）は <see cref="MarqueeSkiaRenderer"/> が担う。
/// </remarks>
internal static class MarqueeOuterSkiaRenderer
{
    /// <summary>暗転マスク・枠線を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標で描画できる状態であること）。</param>
    /// <param name="frame">描画内容（コアが決定した値）。</param>
    /// <remarks>
    /// 1. 暗転領域（<see cref="OuterDecoFrame.OverlayRegions"/>）を暗転色で塗る（空なら何もしない）。<br/>
    /// 2. 枠線（<see cref="OuterDecoFrame.BorderRects"/>）を描く（空なら何もしない）。<br/>
    /// <br/>
    /// 描画の要否はフレームが持つため、本メソッドに分岐はない（空のコレクションは単に何もしない）。<br/>
    /// ラベル（<see cref="OuterDecoFrame.Labels"/>）は文字の描画をホストが担うため、本メソッドは描かない。
    /// </remarks>
    public static void Draw(SKCanvas canvas, in OuterDecoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        // ---- 暗転マスク（4辺の穴あけ。矩形計算はコアの純粋関数に委譲） ----
        SkiaFill.Rects(canvas, frame.OverlayRegions, frame.OverlayColor);

        // ---- 選択枠線（四辺・矩形の内側） ----
        SkiaFill.Rects(canvas, frame.BorderRects, frame.BorderColor);
    }
}
