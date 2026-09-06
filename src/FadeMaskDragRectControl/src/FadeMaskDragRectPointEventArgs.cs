using Estable.AvaloniaUI.Platform.Geometry;

namespace Estable.AvaloniaUI.FadeMaskDragRect;

/// <summary>ポインター位置イベントの引数（ドメイン座標の位置）。</summary>
/// <remarks>
/// ルーペ中心の更新通知（<see cref="FadeMaskDragRectControl.LoupePositionMoved"/>）のイベント引数。<br/>
/// 内包コントロール（DragRectControl）の型（DragRectPointEventArgs）に依存しない独自型とし、<br/>
/// ホストは FadeMaskDragRectControl の公開 API のみで利用できる（公開型のカプセル化）。
/// </remarks>
/// <param name="Position">ルーペ中心（ドメイン座標）。</param>
public sealed record FadeMaskDragRectPointEventArgs(PointD Position);
