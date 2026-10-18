namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>拡大率メニュー（ステータス バーの拡大率の押下で出す選択肢）の値（4 つの UI 版で共通）。</summary>
/// <remarks>
/// 値は「拡大率（%）」であり、メニューの先頭には Fit（画像をウィンドウへ合わせる）を別途置く。<br/>
/// 選択肢と並びを 1 か所で決める（UI 版ごとに選択肢が変わらないようにする）。<br/>
/// ⊖⊕ ボタンの段階（<c>ZoomTransition.DefaultButtonZoomLevels</c>）とは目的が異なるため別に持つ。
/// </remarks>
public static class ZoomMenuLevels
{
    /// <summary>拡大率の選択肢（%・昇順）。</summary>
    public static IReadOnlyList<double> All { get; } = [10, 15, 25, 35, 50, 75, 100, 150, 200, 250, 350, 500];
}
