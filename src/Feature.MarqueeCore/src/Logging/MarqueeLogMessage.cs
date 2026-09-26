using Microsoft.Extensions.Logging;

namespace EsUtil.Feature.MarqueeCore.Logging;

/// <summary>Marquee の操作（入力の判断・設定変更）が出力する LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// 入力に伴う判断（ドラッグ開始・確定・キャンセル・クリック・キー処理）はコアの状態管理クラスが担うため、<br/>
/// その経過のログ定義もコアが所有する。<br/>
/// <b>ホイール要求のログは本層が所有しない</b>（ズーム・スクロールはコントロール化のための要求であり、<br/>
/// 入力連携層が単一所有する。本クラスは入力判断のログだけを持つ）。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// <b>可視性</b>: 本定義を呼ぶのはコアの状態機械だけであり、UI 層・ホストはロガー（<see cref="Marquee.Log"/>）を渡すだけでよいため internal とする。
/// </remarks>
internal static partial class MarqueeLogMessage
{
    /// <summary>ポインター押下（マウス Down）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] MouseDown: pos=({X}, {Y}) clickCount={ClickCount} hasSelection={HasSelection}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    /// <param name="clickCount">クリック数（2 = ダブルクリック）。</param>
    /// <param name="hasSelection">選択矩形の有無。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] MouseDown: pos=({X}, {Y}) clickCount={ClickCount} hasSelection={HasSelection}")]
    public static partial void LogMouseDown(this ILogger logger, double x, double y, int clickCount, bool hasSelection);

    /// <summary>ポインター解放（マウス Up）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] MouseUp: pos=({X}, {Y}) wasDragging={WasDragging} hasSelection={HasSelection}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    /// <param name="wasDragging">ドラッグ中だったかどうか。</param>
    /// <param name="hasSelection">選択矩形の有無。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] MouseUp: pos=({X}, {Y}) wasDragging={WasDragging} hasSelection={HasSelection}")]
    public static partial void LogMouseUp(this ILogger logger, double x, double y, bool wasDragging, bool hasSelection);

    /// <summary>ダブルクリックのログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] DoubleClick: pos=({X}, {Y}) inside={Inside}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    /// <param name="inside">選択矩形の内側かどうか。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] DoubleClick: pos=({X}, {Y}) inside={Inside}")]
    public static partial void LogDoubleClick(this ILogger logger, double x, double y, bool inside);

    /// <summary>マウス押下直前の位置のログ（移動の高頻度ログは行わない）。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[Marquee] MouseDown 直前の位置: pos=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    [LoggerMessage(LogLevel.Trace, "[Marquee] MouseDown 直前の位置: pos=({X}, {Y})")]
    public static partial void LogPositionBeforeDown(this ILogger logger, double x, double y);

    /// <summary>マウス解放直後の位置のログ（移動の高頻度ログは行わない）。</summary>
    /// <remarks>LogLevel: Trace<br/>出力書式: <c>[Marquee] MouseUp 直後の位置: pos=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    [LoggerMessage(LogLevel.Trace, "[Marquee] MouseUp 直後の位置: pos=({X}, {Y})")]
    public static partial void LogPositionAfterUp(this ILogger logger, double x, double y);

    /// <summary>マウスドラッグ開始（新規選択・ハンドル操作）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] DragStart: handle={Handle} pos=({X}, {Y})</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="handle">操作対象のハンドル（None = 新規選択）。</param>
    /// <param name="x">ドメイン座標 X。</param>
    /// <param name="y">ドメイン座標 Y。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] DragStart: handle={Handle} pos=({X}, {Y})")]
    public static partial void LogDragStart(this ILogger logger, string handle, double x, double y);

    /// <summary>キー入力（矢印キー nudge・Enter 確定・Escape キャンセル）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] KeyDown: key={Key} handled={Handled}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="key">押下されたキー。</param>
    /// <param name="handled">処理されたかどうか。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] KeyDown: key={Key} handled={Handled}")]
    public static partial void LogKeyDown(this ILogger logger, string key, bool handled);

    /// <summary>確定要求（Enter・ダブルクリック・API 呼び出し）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] ConfirmRequested: rect={Rect}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="rect">確定する選択矩形。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] ConfirmRequested: rect={Rect}")]
    public static partial void LogConfirmRequested(this ILogger logger, string rect);

    /// <summary>キャンセル要求（Escape・枠外ダブルクリック・API 呼び出し）のログ。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] CancelRequested</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] CancelRequested")]
    public static partial void LogCancelRequested(this ILogger logger);

    /// <summary>設定値変更のログ（変更前後）。</summary>
    /// <remarks>LogLevel: Debug<br/>出力書式: <c>[Marquee] Setting: {Name} {OldValue} → {NewValue}</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    [LoggerMessage(LogLevel.Debug, "[Marquee] Setting: {Name} {OldValue} → {NewValue}")]
    public static partial void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue);
}
