namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;

/// <summary>スクロールバー 1 軸分の同期値（最大オフセット・表示範囲・スクロール可否）を表す値オブジェクト。</summary>
/// <param name="MaxOffset">スクロール範囲の最大オフセット（コンテンツ座標）。</param>
/// <param name="ViewportSize">表示範囲（コンテンツ座標。 ビューポート ÷ 実効スケール）。</param>
/// <param name="IsScrollable">スクロール可能かどうか（最大オフセットが 0 より大きい）。</param>
/// <remarks>
/// 標準のスクロールバー部品を持つ UI（Avalonia の <c>ScrollBar</c>）が、<c>Maximum</c> / <c>ViewportSize</c> /<br/>
/// <c>IsVisible</c> へ写すための値。<br/>
/// 算出式（最大オフセット・表示範囲）は <c>ScrollMath</c> が単一所有し、本値は UI へ渡すための入れ物である<br/>
/// （UI 層に式を書かない）。 自前でスクロールバーを描画する UI（MewUI）は <see cref="ZoomViewSession.CurrentScrollBars"/> を使う。
/// </remarks>
public readonly record struct ZoomScrollBarMetrics(double MaxOffset, double ViewportSize, bool IsScrollable)
{
    /// <summary>オフセットをスクロール範囲（0〜<see cref="MaxOffset"/>）へクランプする。</summary>
    /// <param name="offset">クランプするオフセット（コンテンツ座標）。</param>
    /// <returns>範囲内のオフセット。</returns>
    /// <remarks>
    /// 標準のスクロールバー部品（Avalonia の <c>ScrollBar</c>）は値が <c>Maximum</c> の範囲外だと例外になるため、<br/>
    /// 代入前に本メソッドでクランプする（範囲の規則を UI ごとに書かない）。
    /// </remarks>
    public double ClampOffset(double offset) => Math.Clamp(offset, 0, MaxOffset);
}
