using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee.Core.Decoration;

/// <summary>穴あけ矩形の内側の装飾の 1 フレーム分の描画内容（描画に必要な値のみをコアが決定した結果）。</summary>
/// <remarks>
/// UI 層は本値の各項目を自身の描画 API へ写すだけにする（判断・計算・既定値の解決は含まない）。<br/>
/// 描画するものが無い場合（塗りなし・枠線なし）は各項目が空になる（<see cref="HasContent"/> で判定できる）。<br/>
/// 描画の可否は <c>MarqueeDecoOptions</c> の明示フラグが単一所有する。<br/>
/// <b>色は含まない</b>: 色は描画にだけ必要な設定であり、描画の設定（<c>MarqueePaint</c>）が単一所有する。
/// </remarks>
/// <param name="FillRect">塗る矩形（DIP。穴あけ矩形そのもの）。null の場合は塗らない。</param>
/// <param name="BorderRects">枠線を描く矩形（DIP。穴あけ矩形の内側 4 辺）。空の場合は描かない。</param>
public readonly record struct MarqueeInnerDecoFrame(
    EsRect? FillRect,
    IReadOnlyList<EsRect> BorderRects)
{
    /// <summary>描く値があるかどうかを取得する。</summary>
    /// <value>塗り矩形または枠線矩形を持つ場合は true。</value>
    /// <remarks>
    /// 層（<c>MarqueeDrawLayer.Decoration</c>）に含めるかどうかの判断は <c>Marquee.ResolveContent</c> が本値を使って行う<br/>
    /// （層にある ⇔ 描く値がある。判断を UI 層へ置かない）。
    /// </remarks>
    public bool HasContent => FillRect is not null || BorderRects.Count > 0;
}
