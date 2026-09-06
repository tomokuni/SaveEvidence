using Estable.Primitives;

namespace Estable.AvaloniaUI.DragRect;

/// <summary>DragRect イベントのスナップショット（通知時点の状態・選択矩形・アクティブハンドル・マウス位置・操作フラグ）。</summary>
/// <remarks>
/// 各イベント引数（<see cref="SelectionRectEventArgs"/> / <see cref="DragRectPointEventArgs"/>）に含まれ、上位は DragRect の状態をポーリングせず<br/>
/// イベントだけで状態を把握できる。<br/>
/// <see cref="SelectionRect"/> は選択矩形（ドメイン座標）。ドラッグ中はドラッグ中の矩形、nudge 中は nudge 適用後の矩形。<br/>
/// <see cref="ActiveHandle"/> はアクティブなドラッグハンドル。新規選択・移動・未選択時は <see cref="DragHandle.None"/>。<br/>
/// <see cref="IsDragging"/> はドラッグ中かどうか（新規選択・移動・リサイズのいずれか）。<see cref="State"/> が
/// <c>Dragging</c> / <c>DraggingNudging</c> の場合に true（明示的に保持し、導出を不要にする）。<br/>
/// <see cref="NudgeActive"/> は nudge（矢印キー微調整）中かどうか。ホストが nudge 中の表示切替等に使用できる。<br/>
/// 値セマンティクスで比較できる record として定義する（生成は <see cref="Selection.DragRectState.CreateSnapshot"/>）。<br/>
/// 項目の並びは「要約 → 主要データ → 位置 → 操作フラグ」の順（可読性のため）。
/// </remarks>
/// <param name="State">対話状態（State パターン）。</param>
/// <param name="SelectionRect">選択矩形（ドメイン座標）。ドラッグ中の場合はドラッグ中の矩形。</param>
/// <param name="ActiveHandle">アクティブなドラッグハンドル。None はハンドルなし（新規選択・移動・未選択）。</param>
/// <param name="MousePosition">通知時点のマウス位置（ドメイン座標）。</param>
/// <param name="IsMouseDown">マウスボタン（左）が押下中かどうか。</param>
/// <param name="IsDragging">ドラッグ中（新規選択・移動・リサイズ）かどうか。</param>
/// <param name="NudgeActive">nudge（矢印キー微調整）中かどうか。</param>
public sealed record DragRectSnapshot(
    DragRectInteractionState State,
    RectD SelectionRect,
    DragHandle ActiveHandle,
    PointD MousePosition,
    bool IsMouseDown,
    bool IsDragging,
    bool NudgeActive)
{
    /// <summary>選択矩形が有効かどうか（幅・高さが 0 より大きい選択状態）。</summary>
    /// <remarks><see cref="SelectionRect"/> から導出する（利便プロパティ。コンストラクタ引数には含めない）。</remarks>
    public bool HasSelection => SelectionRect.Width > 0 && SelectionRect.Height > 0;
}
