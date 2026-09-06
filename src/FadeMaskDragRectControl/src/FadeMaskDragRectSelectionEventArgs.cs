using Estable.AvaloniaUI.Platform.Geometry;

namespace Estable.AvaloniaUI.FadeMaskDragRect;

/// <summary>選択矩形イベントの引数（ドメイン座標の選択矩形）。</summary>
/// <remarks>
/// 確定要求（<see cref="FadeMaskDragRectControl.ConfirmRequested"/>）のイベント引数。<br/>
/// 内包コントロール（DragRectControl）の型（SelectionRectEventArgs）に依存しない独自型とし、<br/>
/// ホストは FadeMaskDragRectControl の公開 API のみで利用できる（公開型のカプセル化）。
/// </remarks>
/// <param name="SelectionRect">選択矩形（ドメイン座標）。</param>
public sealed record FadeMaskDragRectSelectionEventArgs(RectD SelectionRect);
