using Microsoft.Extensions.Logging;

namespace Estable.MewUI.DragRect.Logging;

/// <summary>DragRectControl（MewUI 版）の LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// ホイール（ズーム要求）と設定変更のログを出力する（Avalonia 版と同一の書式・レベル）。<br/>
/// 入力の判断に伴うログ（マウス操作・キー入力・確定/キャンセル）はコアの状態管理クラスが出力する（本クラスは UI 層の操作のみ）。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// <b>相違点</b>: スクロールの実処理はホストが担うため、通常ホイールのスクロールログ（Avalonia 版の <c>LogWheelScroll</c>）は持たない。
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

    /// <summary>設定値変更のログ（変更前後）。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[DragRect] Setting: {Name} {OldValue} → {NewValue}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    [LoggerMessage(LogLevel.Debug, "[DragRect] Setting: {Name} {OldValue} → {NewValue}")]
    public static partial void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue);
}
