using Microsoft.Extensions.Logging;

namespace Estable.MewUI.ZoomView.Logging;

/// <summary>ズーム操作のトレースログ定義（LoggerMessage ソース生成・拡張メソッド）。</summary>
/// <remarks>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、<br/>
/// 呼び出し側では logger 引数を省略する。<br/>
/// ズーム操作のログは実行前後のズーム率（%）を Information で記録する（Avalonia 版と同一の書式）。<br/>
/// ログ基盤はホストアプリケーションが所有する（本クラスは定義のみ）。
/// </remarks>
internal static partial class ZoomViewLogMessage
{
    /// <summary>ズームイン（ボタン）の実行前後を記録する。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>[ZoomIn] {Current}% -&gt; {Target}%</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="current">変更前のズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    [LoggerMessage(LogLevel.Information, "[ZoomIn] {Current}% -> {Target}%")]
    public static partial void LogZoomIn(this ILogger logger, int current, int target);

    /// <summary>ズームアウト（ボタン）の実行前後を記録する。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>[ZoomOut] {Current}% -&gt; {Target}%</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="current">変更前のズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    [LoggerMessage(LogLevel.Information, "[ZoomOut] {Current}% -> {Target}%")]
    public static partial void LogZoomOut(this ILogger logger, int current, int target);

    /// <summary>Ctrl+ホイールズームインの実行前後を記録する。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>[ZoomInWheel] {Current}% -&gt; {Target}%</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="current">変更前のズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    [LoggerMessage(LogLevel.Information, "[ZoomInWheel] {Current}% -> {Target}%")]
    public static partial void LogZoomInWheel(this ILogger logger, int current, int target);

    /// <summary>Ctrl+ホイールズームアウトの実行前後を記録する。</summary>
    /// <remarks>
    /// LogLevel: Information <br/>
    /// 出力書式: <c>[ZoomOutWheel] {Current}% -&gt; {Target}%</c>
    /// </remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。呼び出し側で省略できる）。</param>
    /// <param name="current">変更前のズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    [LoggerMessage(LogLevel.Information, "[ZoomOutWheel] {Current}% -> {Target}%")]
    public static partial void LogZoomOutWheel(this ILogger logger, int current, int target);
}
