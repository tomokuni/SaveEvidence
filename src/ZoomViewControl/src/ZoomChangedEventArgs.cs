using Avalonia;

namespace Estable.AvaloniaUI.ZoomView;

/// <summary>ズーム率変更イベントの引数。</summary>
/// <param name="ZoomPercent">現在の実効ズーム率（%）。Fit 中は実効倍率（ビューポートと元画像サイズから算出）、固定倍率中はズーム率。0 にはならない（Fit 中でも実効倍率を返す）。</param>
/// <param name="IsFit">現在 Fit 表示中かどうか（内部ズーム率が 0）。</param>
/// <param name="Availability">ズーム操作の可否（ズームイン・アウト可能か。Non-Null の値オブジェクト）。</param>
/// <remarks>
/// 実効ズーム率・Fit 状態に加え、⊖⊕ ボタンの Enable/Disable 制御に必要な可否（<see cref="ZoomAvailability"/>）を通知する。<br/>
/// 可否は <see cref="ZoomTransition.CanZoomIn"/> / <see cref="ZoomTransition.CanZoomOut"/>（段階遷移仕様に基づく純粋関数）で判定し、<br/>
/// ホストは本イベントを受けて任意の UI（ボタン等）の状態を更新する。<br/>
/// <para/>
/// <b>Fit 判定</b>: <see cref="ZoomPercent"/> が 0 にならないため、<see cref="IsFit"/> で Fit 状態を判定する（イベント引数に含めるためコントロールへの再参照が不要）。<br/>
/// <b>表示文字列の生成</b>: <see cref="ZoomPercent"/> を <see cref="ZoomMath.ToDisplayZoomPercent"/>（丸め・クランプ）で整数化して <c>"100%"</c> 等を生成する（表示の仕方はホストの責務）。
/// </remarks>
public sealed record ZoomChangedEventArgs(double ZoomPercent, bool IsFit, ZoomAvailability Availability)
{
    /// <summary>ズームイン可能かどうか（<see cref="Availability"/> の <see cref="ZoomAvailability.CanZoomIn"/>）。</summary>
    public bool CanZoomIn => Availability.CanZoomIn;

    /// <summary>ズームアウト可能かどうか（<see cref="Availability"/> の <see cref="ZoomAvailability.CanZoomOut"/>）。</summary>
    public bool CanZoomOut => Availability.CanZoomOut;
}
