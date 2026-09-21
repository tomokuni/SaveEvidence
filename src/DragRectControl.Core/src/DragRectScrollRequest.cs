using EsUtil.Platform.Core.Geometry;

namespace Estable.DragRect;

/// <summary>通常ホイール（Ctrl なし）によるスクロール要求（生の回転量・修飾キー・発生位置）。</summary>
/// <remarks>
/// 本コントロールはホイールの回転量をそのまま通知し、スクロール量やスクロール先の決定は転送先（ホスト）に委ねる。<br/>
/// 転送先は祖先 <c>ScrollViewer</c>・ズームビューポート（パン）・リスト等いずれでもよく、<br/>
/// 単位（1 ノッチあたりの移動量）と方向（縦横）の解釈は転送先が単一所有する。<br/>
/// Ctrl 併用のホイールはズーム要求（<see cref="DragRectZoomRequest"/>）として別に通知する。
/// </remarks>
/// <param name="DeltaX">横方向の回転量（1 ノッチ = 1.0）。</param>
/// <param name="DeltaY">縦方向の回転量（1 ノッチ = 1.0。正 = 上方向）。</param>
/// <param name="Shift">Shift 併用かどうか（横スクロールの意図を示す）。</param>
/// <param name="Position">ホイール操作の発生位置（コントロールローカル DIP）。</param>
public readonly record struct DragRectScrollRequest(double DeltaX, double DeltaY, bool Shift, EsPoint Position);

/// <summary>Ctrl+ホイールによるズーム要求（方向と発生位置）。</summary>
/// <remarks>
/// ズーム率の決定（段階・倍率）と表示の適用は転送先（ホスト）が行う。<br/>
/// <see cref="Position"/> はズーム中心（カーソル位置）として使用できる（コントロールローカル DIP）。
/// </remarks>
/// <param name="ZoomIn">true = ズームイン / false = ズームアウト。</param>
/// <param name="Position">ホイール操作の発生位置（コントロールローカル DIP）。</param>
public readonly record struct DragRectZoomRequest(bool ZoomIn, EsPoint Position);

