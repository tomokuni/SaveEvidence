using Avalonia;

namespace Estable.AvaloniaUI.ZoomView;

/// <summary>ズーム率変更イベントの引数。</summary>
/// <param name="ZoomPercent">現在の実効ズーム率（%）。Fit 中は実効倍率（ビューポートと元画像サイズから算出）、固定倍率中はズーム率。0 にはならない（Fit 中でも実効倍率を返す）。</param>
/// <param name="IsFit">現在 Fit 表示中かどうか（内部ズーム率が 0）。</param>
/// <param name="CanZoomIn">ズームイン可能かどうか。</param>
/// <param name="CanZoomOut">ズームアウト可能かどうか。</param>
/// <remarks>
/// 実効ズーム率・Fit 状態に加え、⊖⊕ ボタンの Enable/Disable 制御に必要な可否（<c>CanZoomIn</c> / <c>CanZoomOut</c>）を通知する。<br/>
/// 可否は <see cref="ZoomViewControl"/> 内部の段階遷移仕様で判定し、ホストは本イベントを受けて任意の UI（ボタン等）の状態を更新する。<br/>
/// <para/>
/// <b>Fit 判定</b>: <see cref="ZoomPercent"/> が 0 にならないため、<see cref="IsFit"/> で Fit 状態を判定する（イベント引数に含めるためコントロールへの再参照が不要）。<br/>
/// <b>表示文字列の生成</b>: 表示形式（"%" 表記等）はホストの都合で決まるため、ライブラリは <see cref="ZoomPercent"/>（数値）のみを通知し、文字列化はホストが行う。
/// </remarks>
public sealed record ZoomChangedEventArgs(double ZoomPercent, bool IsFit, bool CanZoomIn, bool CanZoomOut);
