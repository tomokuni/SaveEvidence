using Microsoft.Extensions.Logging;

namespace Estable.AvaloniaUI.Loupe.Logging;

/// <summary>LoupeControl の LoggerMessage 定義。</summary>
internal static partial class LogMessage
{
    /// <summary>ルーペ更新の例外エラーログ。</summary>
    /// <remarks>
    /// LogLevel: Error <br/>
    /// 出力書式: <c>[LoupeCtrl] Update exception</c> <br/>
    /// 出力例: <c>[LoupeCtrl] Update exception</c>（例外スタックトレース付き）
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="exception">発生した例外。</param>
    [LoggerMessage(LogLevel.Error, "[LoupeCtrl] Update exception")]
    public static partial void LogUpdateError(ILogger logger, Exception exception);

    /// <summary>再描画要求（RequestRender）の診断ログ。</summary>
    /// <remarks>
    /// LogLevel: Trace <br/>
    /// 出力書式: <c>[LoupeCtrl] RequestRender: visible={Visible} queued={Queued}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="visible">表示状態。</param>
    /// <param name="queued">再描画がキュー済みかどうか。</param>
    [LoggerMessage(LogLevel.Trace, "[LoupeCtrl] RequestRender: visible={Visible} queued={Queued}")]
    public static partial void LogRequestRender(ILogger logger, bool visible, bool queued);

    /// <summary>RenderCore 実行の診断ログ。</summary>
    /// <remarks>
    /// LogLevel: Trace <br/>
    /// 出力書式: <c>[LoupeCtrl] RenderCore: generation={Generation}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="generation">描画世代番号。</param>
    [LoggerMessage(LogLevel.Trace, "[LoupeCtrl] RenderCore: generation={Generation}")]
    public static partial void LogRenderCore(ILogger logger, long generation);

    /// <summary>GenerateAsync の完了・破棄の診断ログ。</summary>
    /// <remarks>
    /// LogLevel: Trace <br/>
    /// 出力書式: <c>[LoupeCtrl] GenerateAsync done: set={Set} disposed={Disposed} visible={Visible} gen={Generation} current={Current}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="set">ビットマップが設定されたかどうか。</param>
    /// <param name="disposed">ビットマップが破棄されたかどうか。</param>
    /// <param name="visible">表示状態。</param>
    /// <param name="generation">描画世代番号。</param>
    /// <param name="current">現在の世代番号。</param>
    [LoggerMessage(LogLevel.Trace, "[LoupeCtrl] GenerateAsync done: set={Set} disposed={Disposed} visible={Visible} gen={Generation} current={Current}")]
    public static partial void LogGenerateAsyncDone(ILogger logger, bool set, bool disposed, bool visible, long generation, long current);

    /// <summary>設定値変更のログ（変更前後）。</summary>
    /// <remarks>
    /// LogLevel: Debug <br/>
    /// 出力書式: <c>[LoupeCtrl] Setting: {Name} {Old} → {New}</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="name">設定名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    [LoggerMessage(LogLevel.Debug, "[LoupeCtrl] Setting: {Name} {OldValue} → {NewValue}")]
    public static partial void LogSettingChanged(ILogger logger, string name, string oldValue, string newValue);

    /// <summary>表示位置（RenderTransform）更新の診断ログ。</summary>
    /// <remarks>
    /// LogLevel: Trace <br/>
    /// 出力書式: <c>[LoupeCtrl] UpdatePosition: pos=({X}, {Y})</c>
    /// </remarks>
    /// <param name="logger">ロガー。</param>
    /// <param name="x">表示位置 X。</param>
    /// <param name="y">表示位置 Y。</param>
    [LoggerMessage(LogLevel.Trace, "[LoupeCtrl] UpdatePosition: pos=({X}, {Y})")]
    public static partial void LogUpdatePosition(ILogger logger, double x, double y);
}
