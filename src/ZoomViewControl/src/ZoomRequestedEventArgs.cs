using Avalonia;

namespace Estable.AvaloniaUI.ZoomView;

/// <summary>ズーム要求イベントの引数。</summary>
/// <param name="ZoomIn">true = ズームイン / false = ズームアウト。</param>
/// <param name="PointerPosition">ズーム中心として使用するマウスポインタ位置（ビューポートローカル座標）。</param>
/// <remarks>ズーム率の決定（遷移ロジック）は上位（ZoomViewControl）が行い、中心は本イベントの位置を使用する。</remarks>
public sealed record ZoomRequestedEventArgs(bool ZoomIn, Point PointerPosition);
