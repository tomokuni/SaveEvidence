using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LoupeControl.Sample.Logging;

/// <summary>デバッグ出力へログを転送する <see cref="ILogger"/> 実装（LoupeControl.Log の設定例）。</summary>
/// <remarks>
/// <see cref="LoupeControl.Log"/> に設定すると、描画エラー（<c>NotSupportedException</c> 等）や
/// 診断ログが Visual Studio の出力ウィンドウへ表示される（<c>Trace.WriteLine</c> ベース）。<br/>
/// 実アプリでは NLog 等の ILogger プロバイダーへ注入してファイル・コンソールへ出力する。
/// </remarks>
public sealed class DebugLogger : ILogger
{
    /// <summary>全ログレベルを受け入れる（フィルタなし）。</summary>
    /// <param name="logLevel">ログレベル。</param>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <summary>ログスコープは未使用（null を返す）。</summary>
    /// <typeparam name="TState">状態の型。</typeparam>
    /// <param name="state">状態。</param>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <summary>整形済みログをデバッグ出力へ書き込む。</summary>
    /// <typeparam name="TState">状態の型。</typeparam>
    /// <param name="logLevel">ログレベル。</param>
    /// <param name="eventId">イベント ID。</param>
    /// <param name="state">状態。</param>
    /// <param name="exception">例外（未使用。formatter が含める）。</param>
    /// <param name="formatter">ログメッセージ整形関数。</param>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Trace.WriteLine($"[{logLevel}] {formatter(state, exception)}");
}
