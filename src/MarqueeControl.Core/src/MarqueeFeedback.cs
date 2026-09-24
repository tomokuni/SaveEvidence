using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;

namespace Estable.Marquee;

/// <summary>コア（状態層）が UI 層へ要求するカーソル操作（フラグの組み合わせ）。</summary>
/// <remarks>
/// 1 回の入力通知で複数の操作を要求できる（例: nudge の終了では「nudge 位置へ移動」してから「再表示」する）。<br/>
/// 適用順は <see cref="MarqueeFeedback.ApplyCursor"/> が固定する（移動 → 非表示 → 再表示）。<br/>
/// カーソルの表示残留・原点飛びを防ぐためこの順序を守る必要がある。
/// </remarks>
[Flags]
internal enum MarqueeCursorAction
{
    /// <summary>操作なし。</summary>
    None = 0,

    /// <summary><see cref="MarqueeFeedback.CursorDomainPosition"/> へカーソルを移動する。</summary>
    MoveTo = 1,

    /// <summary>カーソルを非表示にする（nudge 中）。</summary>
    Hide = 2,

    /// <summary>カーソルを再表示する（nudge の終了・外部復元）。</summary>
    Show = 4,
}

/// <summary>1 回の入力通知に対して UI 層が適用する表示上の効果（状態を持たない指示）。</summary>
/// <remarks>
/// コアは状態の更新と通知（<see cref="MarqueeNotice"/>）だけを行い、画面への反映方法（再描画・フォーカス・カーソル）は
/// 本レコードで UI 層へ指示する。UI 層は指示に従って適用するだけで、判断を持たない。<br/>
/// カーソル操作の適用は UI 非依存（<see cref="ICursorService"/> と画面座標の変換関数のみで完結する）ため、
/// 本レコードの <see cref="ApplyCursor"/> が単一所有する（両 UI で同一の手順・順序になる）。<br/>
/// 濃い既定値（<see cref="Handled"/> 以外は false）を持つため、効果が無い通知では <c>default</c> を返せる。<br/>
/// <br/>
/// <b>可視性</b>: 本値の適用はファサード（<see cref="Marquee"/>）が単一所有し、UI 層は値を受け取らないため internal とする。
/// </remarks>
internal readonly record struct MarqueeFeedback
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

    /// <summary><see cref="UpdateCursorShape"/> が true のときのカーソル形状の種別（UI 層が自 UI のカーソル型へ写像する）。</summary>
    public MarqueeCursorKind CursorKind { get; init; }

    /// <summary>要求するカーソル操作。</summary>
    public MarqueeCursorAction Cursor { get; init; }

    /// <summary><see cref="MarqueeCursorAction.MoveTo"/> の移動先（ドメイン座標）。</summary>
    public EsPoint CursorDomainPosition { get; init; }

    /// <summary>要求されたカーソル操作を適用する。</summary>
    /// <param name="cursor">カーソルサービス。</param>
    /// <param name="toScreen">ドメイン座標を画面物理座標へ変換する関数（UI 層が提供する）。</param>
    /// <remarks>
    /// 適用順は「移動 → 非表示 → 再表示」に固定する（nudge の終了では移動してから再表示する必要がある）。<br/>
    /// 画面座標の変換のみ UI 層に委ね、それ以外（どの操作をどの順で行うか）はコアが単一所有する。
    /// </remarks>
    public void ApplyCursor(ICursorService cursor, Func<EsPoint, EsPixelPoint> toScreen)
    {
        if (Cursor.HasFlag(MarqueeCursorAction.MoveTo))
        {
            cursor.SetCursorScreenPoint(toScreen(CursorDomainPosition));
        }

        if (Cursor.HasFlag(MarqueeCursorAction.Hide))
        {
            cursor.ShowCursor(false);
        }

        if (Cursor.HasFlag(MarqueeCursorAction.Show))
        {
            cursor.ShowCursor(true);
        }
    }

    /// <summary>要求された表示上の効果（フォーカス・カーソル形状・再描画）を適用する。</summary>
    /// <param name="requestFocus">フォーカスを取得する操作（UI 層が提供する）。</param>
    /// <param name="invalidateVisual">再描画を要求する操作（UI 層が提供する）。</param>
    /// <param name="setCursorShape">カーソル形状を設定する操作（UI 層が自 UI のカーソル型へ写像する）。</param>
    /// <remarks>
    /// 適用順は「フォーカス → カーソル形状 → 再描画」に固定する（再描画を最後にして確定後の状態を 1 回で描く）。<br/>
    /// どの効果をどの順で適用するかはコアが単一所有し、UI 層は自 UI の操作へ接続するだけにする。
    /// </remarks>
    public void ApplyVisual(Action requestFocus, Action invalidateVisual, Action<MarqueeCursorKind> setCursorShape)
    {
        if (RequestFocus)
        {
            requestFocus();
        }

        if (UpdateCursorShape)
        {
            setCursorShape(CursorKind);
        }

        if (Redraw)
        {
            invalidateVisual();
        }
    }
}
