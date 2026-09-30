using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.FadeMaskDragRect;

/// <summary>選択矩形の可否判定を提供する静的クラス。</summary>
/// <remarks>
/// 「選択として成立する矩形かどうか」の条件を単一所有する。<br/>
/// 幅または高さが 0 以下の矩形（退化した矩形）は選択なしとして扱い、暗転マスクの注目・破線オーバーレイ・<br/>
/// 切り出しのいずれからも選択として扱わない（UI ごとに判定がずれると、0 サイズの選択が片方の UI だけに残る）。<br/>
/// 両 UI の矩形型（<c>Avalonia.Rect</c> / コアの <see cref="EsRect"/>）はいずれも幅・高さを持つため、<br/>
/// 型変換を発生させないよう寸法で判定する。
/// </remarks>
internal static class FadeMaskDragRectSelection
{
    /// <summary>選択矩形として成立するサイズかどうかを判定する。</summary>
    /// <param name="width">矩形の幅。</param>
    /// <param name="height">矩形の高さ。</param>
    /// <returns>幅と高さがともに正の場合は true（選択として成立する）。</returns>
    public static bool IsUsable(double width, double height) => width > 0 && height > 0;
}
