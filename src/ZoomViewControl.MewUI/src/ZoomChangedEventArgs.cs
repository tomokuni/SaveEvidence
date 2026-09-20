namespace Estable.MewUI.ZoomView;

/// <summary>ズーム率変更イベントの引数（MewUI 版）。</summary>
/// <param name="ZoomPercent">現在の実効ズーム率（%）。Fit 中は実効倍率（ビューポートと元画像サイズから算出）、固定倍率中はズーム率。0 にはならない。</param>
/// <param name="IsFit">現在 Fit 表示中かどうか（内部ズーム率が 0）。</param>
/// <param name="CanZoomIn">ズームイン可能かどうか。</param>
/// <param name="CanZoomOut">ズームアウト可能かどうか。</param>
/// <remarks>
/// Avalonia 版と同じ情報を通知する（UI 間でホストの結線を揃えるため）。<br/>
/// 可否はコアの段階遷移仕様で判定し、ホストは本イベントを受けて任意の UI（ボタン等）の状態を更新する。<br/>
/// <see cref="ZoomPercent"/> は実効値のため Fit 中でも 0 にならない。Fit 判定は <see cref="IsFit"/> を使用する。
/// </remarks>
public sealed record ZoomChangedEventArgs(double ZoomPercent, bool IsFit, bool CanZoomIn, bool CanZoomOut);
