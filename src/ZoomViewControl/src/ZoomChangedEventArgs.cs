using Avalonia;

namespace Estable.AvaloniaUI.ZoomView;

/// <summary>ズーム率変更イベントの引数。</summary>
/// <param name="ZoomPercent">変更後のズーム率（%）。0 = Fit（自動）。</param>
/// <param name="Availability">ズーム操作の可否（ズームイン・アウト可能か。Non-Null の値オブジェクト）。</param>
/// <remarks>
/// ズーム率に加え、⊖⊕ ボタンの Enable/Disable 制御に必要な可否（<see cref="ZoomAvailability"/>）を通知する。<br/>
/// 可否は <see cref="ZoomTransition.CanZoomIn"/> / <see cref="ZoomTransition.CanZoomOut"/>（段階遷移仕様に基づく純粋関数）で判定し、<br/>
/// ホストは本イベントを受けて任意の UI（ボタン等）の状態を更新する。
/// </remarks>
public sealed record ZoomChangedEventArgs(int ZoomPercent, ZoomAvailability Availability)
{
    /// <summary>ズームイン可能かどうか（<see cref="Availability"/> の <see cref="ZoomAvailability.CanZoomIn"/>）。</summary>
    public bool CanZoomIn => Availability.CanZoomIn;

    /// <summary>ズームアウト可能かどうか（<see cref="Availability"/> の <see cref="ZoomAvailability.CanZoomOut"/>）。</summary>
    public bool CanZoomOut => Availability.CanZoomOut;
}
