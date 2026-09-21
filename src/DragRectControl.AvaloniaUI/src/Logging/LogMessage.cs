using Microsoft.Extensions.Logging;

namespace Estable.AvaloniaUI.DragRect.Logging;

/// <summary>DragRectControl（Avalonia 版）の LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// ホイール（ズーム要求・スクロール）と設定変更のログを出力する。<br/>
/// 入力の判断に伴うログ（マウス操作・キー入力・確定/キャンセル）はコアの状態管理クラスが出力する（本クラスは UI 層の操作のみ）。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。
/// </remarks>
internal static partial class LogMessage
{
    /// <summary>Ctrl+ホイールによるズーム要求のログ。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[DragRect] ZoomRequested: zoomIn={ZoomIn}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="zoomIn">true = ズームイン / false = ズームアウト。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] ZoomRequested: zoomIn={ZoomIn}")]
    public static partial void LogZoomRequested(this ILogger logger, bool zoomIn);

    /// <summary>通常ホイールによるスクロールのログ。</summary>
    /// <remarks>
    /// LogLevel: Trace <br/>
    /// 出力書式: <c>[DragRect] WheelScroll: delta=({Dx}, {Dy}) offset=({OffsetX}, {OffsetY})</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="dx">横スクロール量。</param>
    /// <param name="dy">縦スクロール量。</param>
    /// <param name="offsetX">スクロール後オフセット X。</param>
    /// <param name="offsetY">スクロール後オフセット Y。</param>
    [LoggerMessage(LogLevel.Trace, "[DragRect] WheelScroll: delta=({Dx}, {Dy}) offset=({OffsetX}, {OffsetY})")]
    public static partial void LogWheelScroll(this ILogger logger, double dx, double dy, double offsetX, double offsetY);

    /// <summary>設定値変更のログ（変更前後）。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[DragRect] Setting: {Name} {Old} → {New}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] Setting: {Name} {OldValue} → {NewValue}")]
    public static partial void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue);
}
