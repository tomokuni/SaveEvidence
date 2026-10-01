namespace EsUtil.SaveEvidence.Parts.ZoomView;

/// <summary>ズームビューの現在状態（実効ズーム率・Fit 状態・ズーム可否）を表す値オブジェクト。</summary>
/// <param name="ZoomPercent">実効ズーム率（%）。 Fit 中はクランプ済みの Fit 実効倍率を返す（0 にはならない）。</param>
/// <param name="IsFit">Fit 表示中かどうか（内部ズーム率が 0 = 自動）。</param>
/// <param name="CanZoomIn">ズームイン可能かどうか（画像未設定時は false）。</param>
/// <param name="CanZoomOut">ズームアウト可能かどうか（画像未設定時は false）。</param>
/// <remarks>
/// 通知（<see cref="ZoomNotice"/>）が運ぶ 4 値を単一の値としてまとめる。<br/>
/// 4 値は同じ判断（<see cref="ZoomViewSession"/>）から同時に確定するため、別々に保持すると「片方だけ古い」矛盾が生じうる。<br/>
/// UI 実装は自 UI のイベント引数へ写すだけにする（判断・計算を持たない）。<br/>
/// 公開 API（UI のイベント引数の元値）であるため <c>public</c>。
/// </remarks>
public readonly record struct ZoomState(double ZoomPercent, bool IsFit, bool CanZoomIn, bool CanZoomOut);
