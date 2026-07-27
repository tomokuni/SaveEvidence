using Avalonia;

namespace Estable.AvaloniaUI.DragRect;

/// <summary>DragRect の対話状態（State パターン: 上位への通知状態）。</summary>
/// <remarks>
/// <see cref="Selection.DragRectState"/> の内部状態（選択矩形・ドラッグ・nudge）から合成した公開ビュー。<br/>
/// 上位（ホスト）はこの状態で「描画モード（枠線 / 十字線 / ドラッグ中）」を判断できる。<br/>
/// - <see cref="NoSelection"/>: 領域なし（初期状態）<br/>
/// - <see cref="StartPointNudging"/>: 始点 Nudge 中（未選択 + nudge 中）<br/>
/// - <see cref="Dragging"/>: ドラッグ中（新規選択・ハンドル変形）<br/>
/// - <see cref="DraggingNudging"/>: ドラッグ中の Nudge 中<br/>
/// - <see cref="Selected"/>: 領域あり（選択確定後）<br/>
/// - <see cref="SelectedNudging"/>: 領域の Nudge 中（選択確定後のキーボード微調整）<br/>
/// </remarks>
public enum DragRectInteractionState
{
    /// <summary>領域なし（初期状態）。</summary>
    NoSelection,

    /// <summary>始点 Nudge 中（キーボードで始点が設定済み）。</summary>
    StartPointNudging,

    /// <summary>ドラッグ中（新規選択・ハンドル変形）。</summary>
    Dragging,

    /// <summary>ドラッグ中の Nudge 中。</summary>
    DraggingNudging,

    /// <summary>領域あり（選択確定後）。</summary>
    Selected,

    /// <summary>領域の Nudge 中（選択確定後のキーボード微調整）。</summary>
    SelectedNudging,
}

/// <summary>選択矩形に関する通知のイベント引数。</summary>
/// <remarks>
/// 矩形（<see cref="Rect"/>）に加え、通知時点のスナップショット（<see cref="Snapshot"/>）を含む。<br/>
/// スクロール補正はイベント引数では行わず、座標変換ポート（<see cref="Platform.ICoordinatePort"/>）方式が担う
/// （<c>Compose(ImageLayout, ScrollOffset)</c> で変換に組み込む）。
/// </remarks>
/// <remarks>矩形・スナップショットで初期化する。</remarks>
/// <param name="rect">通知対象の矩形（ドメイン座標）。</param>
/// <param name="snapshot">通知時点のスナップショット。</param>
public sealed class SelectionRectEventArgs(Rect rect, DragRectSnapshot snapshot) : EventArgs
{
    /// <summary>通知対象の矩形（ドメイン座標）。</summary>
    public Rect Rect { get; } = rect;

    /// <summary>通知時点のスナップショット（状態・選択矩形・アクティブハンドル・マウス位置・操作フラグ）。</summary>
    public DragRectSnapshot Snapshot { get; } = snapshot;
}

/// <summary>座標に関する通知のイベント引数。</summary>
/// <remarks>
/// 座標（<see cref="Point"/>）に加え、通知時点のスナップショット（<see cref="Snapshot"/>）を含む。<br/>
/// スクロール補正はイベント引数では行わず、座標変換ポート（<see cref="Platform.ICoordinatePort"/>）方式が担う
/// （<c>Compose(ImageLayout, ScrollOffset)</c> で変換に組み込む）。
/// </remarks>
/// <remarks>座標・スナップショットで初期化する。</remarks>
/// <param name="point">通知対象の座標（ドメイン座標）。</param>
/// <param name="snapshot">通知時点のスナップショット。</param>
public sealed class DragRectPointEventArgs(Point point, DragRectSnapshot snapshot) : EventArgs
{
    /// <summary>通知対象の座標（ドメイン座標）。</summary>
    public Point Point { get; } = point;

    /// <summary>通知時点のスナップショット（状態・選択矩形・アクティブハンドル・マウス位置・操作フラグ）。</summary>
    public DragRectSnapshot Snapshot { get; } = snapshot;
}
