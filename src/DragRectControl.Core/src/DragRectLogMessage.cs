using Microsoft.Extensions.Logging;

namespace Estable.DragRect;

/// <summary>DragRect の操作（入力の判断・ホイール・設定変更）が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// 入力に伴う判断（ドラッグ開始・確定・キャンセル・クリック・キー処理）はコアの状態管理クラスが担うため、<br/>
/// その経過のログ定義もコアが所有する。<br/>
/// ホイールのズーム要求と設定変更のログは、<b>両 UI で同一の書式・レベルにする必要がある</b>ため同じくコアが所有する<br/>
/// （UI 実装ごとに持つと書式がずれる。各 UI は自 UI 固有の操作のログのみを持つ）。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。
/// </remarks>
internal static partial class DragRectLogMessage
{
    /// <summary>ポインター押下（マウス Down）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[DragRect] MouseDown: pos=({X}, {Y}) clickCount={ClickCount} hasSelection={HasSelection}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    /// <param name="clickCount">クリック数（2 = ダブルクリック）。</param>
    /// <param name="hasSelection">選択矩形の有無。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] MouseDown: pos=({X}, {Y}) clickCount={ClickCount} hasSelection={HasSelection}")]
    public static partial void LogMouseDown(this ILogger logger, double x, double y, int clickCount, bool hasSelection);

    /// <summary>ポインター解放（マウス Up）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[DragRect] MouseUp: pos=({X}, {Y}) wasDragging={WasDragging} hasSelection={HasSelection}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    /// <param name="wasDragging">ドラッグ中だったかどうか。</param>
    /// <param name="hasSelection">選択矩形の有無。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] MouseUp: pos=({X}, {Y}) wasDragging={WasDragging} hasSelection={HasSelection}")]
    public static partial void LogMouseUp(this ILogger logger, double x, double y, bool wasDragging, bool hasSelection);

    /// <summary>クリック（ドラッグ移動なしの押下→解放）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[DragRect] Click: pos=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] Click: pos=({X}, {Y})")]
    public static partial void LogClick(this ILogger logger, double x, double y);

    /// <summary>ダブルクリックのログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[DragRect] DoubleClick: pos=({X}, {Y}) inside={Inside}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    /// <param name="inside">選択矩形の内側かどうか。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] DoubleClick: pos=({X}, {Y}) inside={Inside}")]
    public static partial void LogDoubleClick(this ILogger logger, double x, double y, bool inside);

    /// <summary>マウス押下直前の位置のログ（移動の高頻度ログは行わない）。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[DragRect] MouseDown 直前の位置: pos=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    [LoggerMessage(LogLevel.Trace, "[DragRect] MouseDown 直前の位置: pos=({X}, {Y})")]
    public static partial void LogPositionBeforeDown(this ILogger logger, double x, double y);

    /// <summary>マウス解放直後の位置のログ（移動の高頻度ログは行わない）。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[DragRect] MouseUp 直後の位置: pos=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    [LoggerMessage(LogLevel.Trace, "[DragRect] MouseUp 直後の位置: pos=({X}, {Y})")]
    public static partial void LogPositionAfterUp(this ILogger logger, double x, double y);

    /// <summary>マウスドラッグ開始（新規選択・ハンドル操作）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[DragRect] DragStart: handle={Handle} pos=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="handle">操作対象のハンドル（None = 新規選択）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] DragStart: handle={Handle} pos=({X}, {Y})")]
    public static partial void LogDragStart(this ILogger logger, string handle, double x, double y);

    /// <summary>キー入力（矢印キー nudge・Enter 確定・Escape キャンセル）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[DragRect] KeyDown: key={Key} handled={Handled}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="key">押下されたキー。</param>
    /// <param name="handled">処理されたかどうか。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] KeyDown: key={Key} handled={Handled}")]
    public static partial void LogKeyDown(this ILogger logger, string key, bool handled);

    /// <summary>確定要求（Enter・ダブルクリック・API 呼び出し）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[DragRect] ConfirmRequested: rect={Rect}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="rect">確定する選択矩形。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] ConfirmRequested: rect={Rect}")]
    public static partial void LogConfirmRequested(this ILogger logger, string rect);

    /// <summary>キャンセル要求（Escape・枠外ダブルクリック・API 呼び出し）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[DragRect] CancelRequested</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] CancelRequested")]
    public static partial void LogCancelRequested(this ILogger logger);

    /// <summary>Ctrl+ホイールによるズーム要求のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[DragRect] ZoomRequested: zoomIn={ZoomIn}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="zoomIn">true = ズームイン / false = ズームアウト。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] ZoomRequested: zoomIn={ZoomIn}")]
    public static partial void LogZoomRequested(this ILogger logger, bool zoomIn);

    /// <summary>設定値変更のログ（変更前後）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[DragRect] Setting: {Name} {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] Setting: {Name} {OldValue} → {NewValue}")]
    public static partial void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue);
}
