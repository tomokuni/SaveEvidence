using Avalonia;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI;

/// <summary>ポインター位置イベントの引数（対象座標の位置）。</summary>
/// <remarks>
/// ルーペ中心の更新通知（<see cref="FadeMaskDragRectControl.LoupePositionMoved"/>）のイベント引数。<br/>
/// 内包コントロール（MarqueeControl）の型（MarqueePointEventArgs）に依存しない独自型とし、<br/>
/// ホストは FadeMaskDragRectControl の公開 API のみで利用できる（公開型のカプセル化）。
/// </remarks>
/// <param name="Position">ルーペ中心（対象座標）。</param>
public sealed record FadeMaskDragRectPointEventArgs(Point Position);
