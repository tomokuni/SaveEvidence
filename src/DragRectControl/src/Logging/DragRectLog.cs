using Microsoft.Extensions.Logging;

namespace Estable.AvaloniaUI.DragRect.Logging;

/// <summary>DragRectControl の LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// マウス操作（Down / Up / Click / DoubleClick / 移動）・キー入力・ホイール（ズーム要求）・<br/>
/// 設定変更をトレースログで出力する。<br/>
/// マウス移動の高頻度ログは行わない（Down 直前・Up 直後の位置のみ Down/Up 時に記録）。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。
/// </remarks>
internal static partial class DragRectLog
{
    /// <summary>ポインター押下（マウス Down）のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[DragRect] MouseDown: pos={X}, {Y} clickCount={ClickCount} hasSelection={HasSelection}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    /// <param name="clickCount">クリック数（2 = ダブルクリック）。</param>
    /// <param name="hasSelection">選択矩形の有無。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] MouseDown: pos=({X}, {Y}) clickCount={ClickCount} hasSelection={HasSelection}")]
    public static partial void LogMouseDown(ILogger logger, double x, double y, int clickCount, bool hasSelection);

    /// <summary>ポインター解放（マウス Up）のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[DragRect] MouseUp: pos={X}, {Y} wasDragging={WasDragging} hasSelection={HasSelection}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    /// <param name="wasDragging">ドラッグ中だったかどうか。</param>
    /// <param name="hasSelection">選択矩形の有無。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] MouseUp: pos=({X}, {Y}) wasDragging={WasDragging} hasSelection={HasSelection}")]
    public static partial void LogMouseUp(ILogger logger, double x, double y, bool wasDragging, bool hasSelection);

    /// <summary>クリック（ドラッグ移動なしの押下→解放）のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[DragRect] Click: pos={X}, {Y}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] Click: pos=({X}, {Y})")]
    public static partial void LogClick(ILogger logger, double x, double y);

    /// <summary>ダブルクリックのログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[DragRect] DoubleClick: pos={X}, {Y} inside={Inside}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    /// <param name="inside">選択矩形の内側かどうか。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] DoubleClick: pos=({X}, {Y}) inside={Inside}")]
    public static partial void LogDoubleClick(ILogger logger, double x, double y, bool inside);

    /// <summary>マウス押下直前の移動位置（Down 時の現在位置）のログ。</summary>
    /// <remarks>
    /// LogLevel: Trace <br/>
    /// 出力書式: <c>[DragRect] MouseDown 直前の位置: pos={X}, {Y}</c><br/>
    /// マウス移動の高頻度ログは行わず、押下直前の位置のみ Down 処理内で記録する。
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    [LoggerMessage(LogLevel.Trace, "[DragRect] MouseDown 直前の位置: pos=({X}, {Y})")]
    public static partial void LogPositionBeforeDown(ILogger logger, double x, double y);

    /// <summary>マウス解放直後の移動位置（Up 時の現在位置）のログ。</summary>
    /// <remarks>
    /// LogLevel: Trace <br/>
    /// 出力書式: <c>[DragRect] MouseUp 直後の位置: pos={X}, {Y}</c><br/>
    /// マウス移動の高頻度ログは行わず、解放直後の位置のみ Up 処理内で記録する。
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    [LoggerMessage(LogLevel.Trace, "[DragRect] MouseUp 直後の位置: pos=({X}, {Y})")]
    public static partial void LogPositionAfterUp(ILogger logger, double x, double y);

    /// <summary>マウスドラッグ開始（新規選択・ハンドル操作）のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[DragRect] DragStart: handle={Handle} pos={X}, {Y}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="handle">操作対象のハンドル（None = 新規選択）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] DragStart: handle={Handle} pos=({X}, {Y})")]
    public static partial void LogDragStart(ILogger logger, string handle, double x, double y);

    /// <summary>キー入力（矢印キー nudge・Enter 確定・Escape キャンセル）のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[DragRect] KeyDown: key={Key} handled={Handled}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="key">押下されたキー。</param>
    /// <param name="handled">処理されたかどうか。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] KeyDown: key={Key} handled={Handled}")]
    public static partial void LogKeyDown(ILogger logger, string key, bool handled);

    /// <summary>Enter キーによる確定要求のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[DragRect] ConfirmRequested: rect={Rect}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="rect">確定する選択矩形。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] ConfirmRequested: rect={Rect}")]
    public static partial void LogConfirmRequested(ILogger logger, string rect);

    /// <summary>Escape キーによるキャンセル要求のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[DragRect] CancelRequested</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] CancelRequested")]
    public static partial void LogCancelRequested(ILogger logger);

    /// <summary>Ctrl+ホイールによるズーム要求のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[DragRect] ZoomRequested: zoomIn={ZoomIn}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="zoomIn">true = ズームイン / false = ズームアウト。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] ZoomRequested: zoomIn={ZoomIn}")]
    public static partial void LogZoomRequested(ILogger logger, bool zoomIn);

    /// <summary>通常ホイールによるスクロールのログ。</summary>
    /// <remarks>
    /// LogLevel: Trace <br/>
    /// 出力書式: <c>[DragRect] WheelScroll: delta=({Dx}, {Dy}) offset=({OffsetX}, {OffsetY})</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="dx">横スクロール量。</param>
    /// <param name="dy">縦スクロール量。</param>
    /// <param name="offsetX">スクロール後オフセット X。</param>
    /// <param name="offsetY">スクロール後オフセット Y。</param>
    [LoggerMessage(LogLevel.Trace, "[DragRect] WheelScroll: delta=({Dx}, {Dy}) offset=({OffsetX}, {OffsetY})")]
    public static partial void LogWheelScroll(ILogger logger, double dx, double dy, double offsetX, double offsetY);

    /// <summary>設定値変更のログ（変更前後）。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[DragRect] Setting: {Name} {Old} → {New}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] Setting: {Name} {OldValue} → {NewValue}")]
    public static partial void LogSettingChanged(ILogger logger, string name, string oldValue, string newValue);
}
