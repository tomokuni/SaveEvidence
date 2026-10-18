using Avalonia.Media;
using EsUtil.SaveEvidence.App.Core.Services;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Controls;

/// <summary>影の計画（<see cref="ShadowPlan"/>）から Avalonia の <see cref="BoxShadows"/> を作る。</summary>
/// <remarks>
/// <b>AvaloniaUI は影を自前で描かない</b>: <c>BoxShadow</c>（ぼかし付きの矩形）が 4 辺・4 隅を<br/>
/// そのまま正しく描くため、計画の値（幅・色）だけを写せば他の UI 版と同じ見た目になる。<br/>
/// そのため<b>この UI 版だけが「計画の濃さのプロファイル」を使わない</b>（<c>BoxShadow</c> のぼかしが<br/>
/// プロファイルの元になっている）。
/// </remarks>
internal static class ShadowFactory
{
    /// <summary>影の定義を生成する。</summary>
    /// <returns><c>BoxShadow</c> の集合（1 つ分）。</returns>
    public static BoxShadows Create() => new(new BoxShadow
    {
        OffsetX = 0,
        // 上下左右へ同じだけ落とす（ずらすと上辺だけ薄くなり、画像との間に白っぽい隙間が見える）
        OffsetY = 0,
        Blur = ShadowPlan.BlurRadius,
        Spread = 0,
        Color = Color.FromUInt32(ShadowPlan.ColorArgb),
    });
}
