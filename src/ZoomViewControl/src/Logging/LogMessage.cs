using Microsoft.Extensions.Logging;

namespace Estable.AvaloniaUI.ZoomView.Logging;

/// <summary>ズーム操作のトレースログ定義（LoggerMessage ソース生成・拡張メソッド）。</summary>
/// <remarks>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、<br/>
/// 呼び出し側では logger 引数を省略する（例: <c>_logger.LogZoomIn(current, target)</c>）。
/// </remarks>
internal static partial class LogMessage
{
    /// <summary>ズームイン（ボタン）の実行前後を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="current">変更前のズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    [LoggerMessage(LogLevel.Information, "[ZoomIn] {current}% -> {target}%")]
    internal static partial void LogZoomIn(this ILogger logger, int current, int target);

    /// <summary>ズームアウト（ボタン）の実行前後を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="current">変更前のズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    [LoggerMessage(LogLevel.Information, "[ZoomOut] {current}% -> {target}%")]
    internal static partial void LogZoomOut(this ILogger logger, int current, int target);

    /// <summary>Ctrl+ホイールズームインの実行前後を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="current">変更前のズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    [LoggerMessage(LogLevel.Information, "[ZoomInWheel] {current}% -> {target}%")]
    internal static partial void LogZoomInWheel(this ILogger logger, int current, int target);

    /// <summary>Ctrl+ホイールズームアウトの実行前後を記録する。</summary>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="current">変更前のズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    [LoggerMessage(LogLevel.Information, "[ZoomOutWheel] {current}% -> {target}%")]
    internal static partial void LogZoomOutWheel(this ILogger logger, int current, int target);
}
