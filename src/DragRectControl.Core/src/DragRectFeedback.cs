using EsUtil.Platform.Core.Geometry;

namespace Estable.DragRect;

/// <summary>コア（状態層）が UI 層へ要求するカーソル操作（フラグの組み合わせ）。</summary>
/// <remarks>
/// 1 回の入力通知で複数の操作を要求できる（例: nudge の終了では「nudge 位置へ移動」してから「再表示」する）。<br/>
/// 適用順は UI 層が固定する（移動 → 非表示 → 再表示）。カーソルの表示残留・原点飛びを防ぐためこの順序を守る必要がある。
/// </remarks>
[Flags]
internal enum DragRectCursorAction
{
    /// <summary>操作なし。</summary>
    None = 0,

    /// <summary><see cref="DragRectFeedback.CursorDomainPosition"/> へカーソルを移動する。</summary>
    MoveTo = 1,

    /// <summary>カーソルを非表示にする（nudge 中）。</summary>
    Hide = 2,

    /// <summary>カーソルを再表示する（nudge の終了・外部復元）。</summary>
    Show = 4,
}

/// <summary>1 回の入力通知に対して UI 層が適用する表示上の効果（状態を持たない指示）。</summary>
/// <remarks>
/// コアは状態の更新と通知（<see cref="DragRectNotice"/>）だけを行い、画面への反映方法（再描画・フォーカス・カーソル）は
/// 本レコードで UI 層へ指示する。UI 層は指示に従って適用するだけで、判断を持たない。<br/>
/// 濃い既定値（<see cref="Handled"/> 以外は false）を持つため、効果が無い通知では <c>default</c> を返せる。<br/>
/// </remarks>
internal readonly record struct DragRectFeedback
{
    /// <summary>入力が処理されたかどうか（UI 層は自 UI の「処理済み」フラグへ設定する）。</summary>
    public bool Handled { get; init; }

    /// <summary>再描画が必要かどうか。</summary>
    public bool Redraw { get; init; }

    /// <summary>フォーカスを取得するかどうか。</summary>
    public bool RequestFocus { get; init; }

    /// <summary>マウスキャプチャを取得するかどうか。</summary>
    /// <remarks>
    /// キャプチャを明示的に行う必要がある UI（ポインターがコントロール外へ出てもドラッグを継続したい実装）が使用する。<br/>
    /// ポインターを暗黙にキャプチャする UI では無視してよい（使用しない場合は常に false として扱える）。
    /// </remarks>
    public bool CaptureMouse { get; init; }

    /// <summary>カーソル形状を更新するかどうか。</summary>
    public bool UpdateCursorShape { get; init; }

    /// <summary><see cref="UpdateCursorShape"/> が true のときのハンドル種別（UI 層が自 UI のカーソル型へ写像する）。</summary>
    public DragHandle CursorHandle { get; init; }

    /// <summary>要求するカーソル操作。</summary>
    public DragRectCursorAction Cursor { get; init; }

    /// <summary><see cref="DragRectCursorAction.MoveTo"/> の移動先（ドメイン座標）。</summary>
    public EsPoint CursorDomainPosition { get; init; }
}
