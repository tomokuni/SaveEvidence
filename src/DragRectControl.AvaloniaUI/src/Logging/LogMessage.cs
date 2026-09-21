using Microsoft.Extensions.Logging;

namespace Estable.AvaloniaUI.DragRect.Logging;

/// <summary>DragRectControl（Avalonia 版）固有の LoggerMessage 定義（ソース生成ログ）。</summary>
/// <remarks>
/// 祖先 ScrollViewer へのホイール転送（通常ホイールのスクロール）のログを出力する。<br/>
/// 入力の判断に伴うログとホイールのズーム要求・設定変更のログは、Avalonia 版と MewUI 版で同一の書式にする必要があるため<br/>
/// コア（`Estable.DragRect` の `LogMessage`）が所有する。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。
/// </remarks>
internal static partial class LogMessage
{
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
}
