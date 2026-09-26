using EsUtil.Feature.MarqueeCore.Decoration;
using EsUtil.SaveEvidence.Common.Imaging;


namespace EsUtil.SaveEvidence.Marquee.UIBase;

/// <summary>選択矩形（穴あけ矩形）の内側に描く装飾（塗り・内枠）を描画ポートへ写すレンダラ。</summary>
/// <remarks>
/// <b>位置付け</b>: 「どう描くか」だけを持つ層である。「塗るか」「枠線を描くか」の判断は<br/>
/// 装飾コア（<see cref="MarqueeInnerDecoFrame"/>）が持つため、本クラスはフレームの各項目を描画ポートへ写すだけとする。<br/>
/// 描画順は「塗り → 内枠」であり、外側の装飾（<see cref="MarqueeOuterRenderer"/>）の後に呼ぶ（コアの層順が指示する）。
/// </remarks>
public static class MarqueeInnerRenderer
{
    /// <summary>穴あけ矩形の内側の塗りと枠線を描く。</summary>
    /// <param name="canvas">描画先の描画ポート。null 不可。</param>
    /// <param name="frame">描画内容（コアが決定した値）。</param>

    /// <param name="paint">描画に使う色（描画にだけ必要な設定。本層が単一所有する）。null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> が null の場合。</exception>
    /// <remarks>
    /// 1. 塗り矩形（<see cref="MarqueeInnerDecoFrame.FillRect"/>）を塗り色で塗る（null なら何もしない）。<br/>
    /// 2. 枠線（<see cref="MarqueeInnerDecoFrame.BorderRects"/>）を描く（空なら何もしない）。<br/>
    /// <br/>
    /// 描画の要否はフレームが持つため、本メソッドに分岐はない（空のコレクションは単に何もしない）。
    /// </remarks>
    public static void Draw(IEsCanvas canvas, in MarqueeInnerDecoFrame frame, MarqueePaint paint)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(paint);

        // ---- 穴あけ矩形の内側の塗り（半透明を許容する） ----
        if (frame.FillRect is { } fill)
        {
            canvas.FillRect(fill, paint.InnerFillColor);
        }

        // ---- 穴あけ矩形の内側の枠線（内側 4 辺） ----
        canvas.FillRects(frame.BorderRects, paint.InnerBorderColor);
    }
}
