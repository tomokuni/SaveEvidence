using Microsoft.Extensions.Logging;

namespace Estable.AvaloniaUI.Loupe.Logging;

/// <summary>LoupeControl の LoggerMessage 定義（ソース生成ログ・拡張メソッド）。</summary>
/// <remarks>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、
/// 呼び出し側では logger 引数を省略する（例: <c>Log.LogUpdateError(ex)</c>）。<br/>
/// </remarks>
internal static partial class LogMessage
{
    /// <summary>ルーペ更新の例外エラーログ。</summary>
    /// <remarks>
    /// LogLevel: Error <br/>
    /// 出力書式: <c>[LoupeCtrl] Update exception</c> <br/>
    /// 出力例: <c>[LoupeCtrl] Update exception</c>（例外スタックトレース付き）
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="exception">発生した例外。</param>
    [LoggerMessage(LogLevel.Error, "[LoupeCtrl] Update exception")]
    public static partial void LogUpdateError(this ILogger logger, Exception exception);

    /// <summary>再描画要求（RequestRender）の診断ログ。</summary>
    /// <remarks>
    /// LogLevel: Trace <br/>
    /// 出力書式: <c>[LoupeCtrl] RequestRender: visible={Visible} queued={Queued}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="visible">表示状態。</param>
    /// <param name="queued">再描画がキュー済みかどうか。</param>
    [LoggerMessage(LogLevel.Trace, "[LoupeCtrl] RequestRender: visible={Visible} queued={Queued}")]
    public static partial void LogRequestRender(this ILogger logger, bool visible, bool queued);

    /// <summary>RenderCore 実行の診断ログ。</summary>
    /// <remarks>
    /// LogLevel: Trace <br/>
    /// 出力書式: <c>[LoupeCtrl] RenderCore: generation={Generation}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="generation">描画世代番号。</param>
    [LoggerMessage(LogLevel.Trace, "[LoupeCtrl] RenderCore: generation={Generation}")]
    public static partial void LogRenderCore(this ILogger logger, long generation);

    /// <summary>GenerateAsync の完了・破棄の診断ログ。</summary>
    /// <remarks>
    /// LogLevel: Trace <br/>
    /// 出力書式: <c>[LoupeCtrl] GenerateAsync done: set={Set} disposed={Disposed} visible={Visible} gen={Generation} current={Current}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="set">ビットマップが設定されたかどうか。</param>
    /// <param name="disposed">ビットマップが破棄されたかどうか。</param>
    /// <param name="visible">表示状態。</param>
    /// <param name="generation">描画世代番号。</param>
    /// <param name="current">現在の世代番号。</param>
    [LoggerMessage(LogLevel.Trace, "[LoupeCtrl] GenerateAsync done: set={Set} disposed={Disposed} visible={Visible} gen={Generation} current={Current}")]
    public static partial void LogGenerateAsyncDone(this ILogger logger, bool set, bool disposed, bool visible, long generation, long current);

    /// <summary>設定値変更のログ（変更前後）。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[LoupeCtrl] Setting: {Name} {Old} → {New}</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    [LoggerMessage(LogLevel.Debug, "[LoupeCtrl] Setting: {Name} {OldValue} → {NewValue}")]
    public static partial void LogSettingChanged(this ILogger logger, string name, string oldValue, string newValue);
}
