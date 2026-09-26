using EsUtil.Feature.MarqueeCore.Decoration;
using EsUtil.SaveEvidence.Common.Imaging;


namespace EsUtil.SaveEvidence.Marquee.UIBase;

/// <summary>選択矩形の外側に描く装飾（暗転マスク・穴あけ枠）を描画ポートへ写すレンダラ。</summary>
/// <remarks>
/// <b>位置付け</b>: 「どう描くか」だけを持つ層である。「どの矩形を暗転するか」「枠線を描くか」の判断は<br/>
/// 装飾コア（<see cref="MarqueeOuterDecoFrame"/>）が持つため、本クラスはフレームの各項目を描画ポートへ写すだけとする<br/>
/// （外観値・判断を持たない）。<br/>
/// 選択矩形そのもの（ハンドル・始点マーカー）は <see cref="MarqueeRenderer"/> が担う。
/// </remarks>
public static class MarqueeOuterRenderer
{
    /// <summary>暗転マスク・枠線を描く。</summary>
    /// <param name="canvas">描画先の描画ポート。null 不可。</param>
    /// <param name="frame">描画内容（コアが決定した値）。</param>

    /// <param name="paint">描画に使う色（描画にだけ必要な設定。本層が単一所有する）。null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> が null の場合。</exception>
    /// <remarks>
    /// 1. 暗転領域（<see cref="MarqueeOuterDecoFrame.OverlayRegions"/>）を暗転色で塗る（空なら何もしない）。<br/>
    /// 2. 枠線（<see cref="MarqueeOuterDecoFrame.BorderRects"/>）を描く（空なら何もしない）。<br/>
    /// <br/>
    /// 描画の要否はフレームが持つため、本メソッドに分岐はない（空のコレクションは単に何もしない）。
    /// </remarks>
    public static void Draw(IEsCanvas canvas, in MarqueeOuterDecoFrame frame, MarqueePaint paint)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(paint);

        // ---- 暗転マスク（描画領域から穴あけ矩形を除いた帯。矩形の計算はコアの純粋関数が単一所有する） ----
        canvas.FillRects(frame.OverlayRegions, paint.OverlayColor);

        // ---- 装飾の枠線（穴あけ矩形の内側 4 辺） ----
        canvas.FillRects(frame.BorderRects, paint.OuterBorderColor);
    }
}
