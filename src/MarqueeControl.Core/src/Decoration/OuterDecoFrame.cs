using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace Estable.Marquee.Decoration;

/// <summary>暗転マスクの 1 フレーム分の描画内容（描画に必要な値のみをコアが決定した結果）。</summary>
/// <remarks>
/// UI 層は本値の各項目を自身の描画 API へ写すだけにする（判断・計算・既定値の解決は含まない）。<br/>
/// 描画するものが無い場合（暗転なし・枠線なし）は各項目が空になるため、UI 層の分岐は不要。<br/>
/// どの項目を描くか（穴あけ矩形・全面暗転・透明色）は <see cref="Marquee.ResolveContent"/> の側が単一所有する。<br/>
/// 色は UI 非依存の <see cref="DrawingColor"/> を使用し、UI 境界で自 UI の色型へ変換する。
/// </remarks>
/// <param name="OverlayRegions">暗転して塗る領域（DIP）。空の場合は塗らない。</param>
/// <param name="OverlayColor">暗転の色。</param>
/// <param name="BorderRects">枠線を描く矩形（DIP）。空の場合は描かない。</param>
/// <param name="BorderColor">枠線の色。</param>
public readonly record struct OuterDecoFrame(
    IReadOnlyList<EsRect> OverlayRegions,
    DrawingColor OverlayColor,
    IReadOnlyList<EsRect> BorderRects,
    DrawingColor BorderColor)
{
}
