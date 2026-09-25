using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.MarqueeCore.Decoration;

/// <summary>暗転マスクの 1 フレーム分の描画内容（描画に必要な値のみをコアが決定した結果）。</summary>
/// <remarks>
/// UI 層は本値の各項目を自身の描画 API へ写すだけにする（判断・計算・既定値の解決は含まない）。<br/>
/// 描画するものが無い場合（暗転なし・枠線なし）は各項目が空になるため、UI 層の分岐は不要。<br/>
/// どの項目を描くか（穴あけ矩形・全面暗転）は <c>MarqueeDecoOptions</c> の明示フラグが単一所有する。<br/>
/// <b>色は含まない</b>: 色は描画にだけ必要な設定であり、描画の設定（<c>MarqueePaint</c>）が単一所有する。
/// </remarks>
/// <param name="OverlayRegions">暗転して塗る領域（DIP）。空の場合は塗らない。</param>
/// <param name="BorderRects">枠線を描く矩形（DIP）。空の場合は描かない。</param>
public readonly record struct OuterDecoFrame(
    IReadOnlyList<EsRect> OverlayRegions,
    IReadOnlyList<EsRect> BorderRects)
{
}
