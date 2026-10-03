using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core;

/// <summary>ズーム操作のログ定義（LoggerMessage ソース生成・拡張メソッド）。</summary>
/// <remarks>
/// ズーム操作のログは <b>両 UI で同一の書式・レベルにする必要がある</b>ため、本クラスが単一所有する<br/>
/// （UI 実装ごとに持つと書式がずれ、片方だけ修正される）。<br/>
/// ログ基盤（ファイル出力・レベル制御）はホストアプリケーションが所有する（本クラスは定義のみ）。<br/>
/// 各メソッドは <see cref="ILogger"/> の拡張メソッド（<c>this ILogger</c> 第一引数）として定義し、呼び出し側では logger 引数を省略する。<br/>
/// <b>変更前の値は「実効ズーム率」を記録する</b>: Fit 表示中は実効倍率（例: 50%）を出す（内部ズーム率の 0 を出さない）。<br/>
/// ログの % は人の可読性のため整数へ丸める（丸めは <see cref="ZoomLogFormat.Percent"/> が単一所有する）。
/// </remarks>
public static partial class ZoomLogMessage
{
    /// <summary>ズームイン（⊖⊕ ボタン）の実行前後を記録する。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[CapturePreviewControl] ZoomIn: {Current}% -&gt; {Target}%</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="current">変更前の実効ズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    [LoggerMessage(LogLevel.Information, "[CapturePreviewControl] ZoomIn: {Current}% -> {Target}%")]
    public static partial void LogZoomIn(this ILogger logger, int current, int target);

    /// <summary>ズームアウト（⊖⊕ ボタン）の実行前後を記録する。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[CapturePreviewControl] ZoomOut: {Current}% -&gt; {Target}%</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="current">変更前の実効ズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    [LoggerMessage(LogLevel.Information, "[CapturePreviewControl] ZoomOut: {Current}% -> {Target}%")]
    public static partial void LogZoomOut(this ILogger logger, int current, int target);

    /// <summary>Ctrl+ホイールのズームインの実行前後を記録する。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[CapturePreviewControl] ZoomInWheel: {Current}% -&gt; {Target}%</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="current">変更前の実効ズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    [LoggerMessage(LogLevel.Information, "[CapturePreviewControl] ZoomInWheel: {Current}% -> {Target}%")]
    public static partial void LogZoomInWheel(this ILogger logger, int current, int target);

    /// <summary>Ctrl+ホイールのズームアウトの実行前後を記録する。</summary>
    /// <remarks>LogLevel: Information<br/>出力書式: <c>[CapturePreviewControl] ZoomOutWheel: {Current}% -&gt; {Target}%</c></remarks>
    /// <param name="logger">ロガー（拡張メソッドのレシーバー。 呼び出し側で省略できる）。</param>
    /// <param name="current">変更前の実効ズーム率（%）。</param>
    /// <param name="target">変更後のズーム率（%）。</param>
    [LoggerMessage(LogLevel.Information, "[CapturePreviewControl] ZoomOutWheel: {Current}% -> {Target}%")]
    public static partial void LogZoomOutWheel(this ILogger logger, int current, int target);
}
