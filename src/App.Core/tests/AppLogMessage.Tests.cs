using EsUtil.SaveEvidence.App.Core.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="AppLogMessage"/> の出力書式（4 つの UI 版で共有する定型文）を検証するテスト。</summary>
/// <remarks>
/// 書式の所有者はログ定義（<see cref="AppLogMessage"/>）であり、各 UI 版は値を渡すだけである
/// （規則の所有者は <c>.github/ログ規約.md</c>）。 実環境の値に依存すると実行機で結果が変わるため、
/// ここでは固定の値を渡して書式だけを固定する。
/// </remarks>
public class AppLogMessageTests
{
    [Fact]
    internal void 環境情報_は値を定型の順に並べる()
    {
        var logger = new RecordingLogger();

        logger.LogEnvironmentCore(
            "[App.Test]",
            "Windows 11 (X64)",
            ".NET 10.0.0 (10.0.0)",
            "2.6.5.0, 2.6.4.0",
            "2.6.5",
            "2.6.5.0",
            "Console",
            "(ローカル)",
            "0,0 1920x1080",
            1.25,
            "1.25x",
            "GPU (8192 MB)");

        Assert.Equal(
            "[App.Test] 環境情報: OS=Windows 11 (X64) / .NET=.NET 10.0.0 (10.0.0)"
            + " / Windows App Runtime=2.6.5.0, 2.6.4.0 (required=2.6.5, loaded=2.6.5.0)"
            + " / セッション=Console / クライアント=(ローカル) / 仮想スクリーン=0,0 1920x1080"
            + " / ウィンドウ倍率=1.25 / モニター倍率=1.25x / アダプター=GPU (8192 MB)",
            logger.Messages[0]);
        Assert.Equal(LogLevel.Information, logger.Levels[0]);
    }

    [Fact]
    internal void 起動_はアプリとEsUtilの版を並べる()
    {
        var logger = new RecordingLogger();

        logger.LogAppStartCore("[App.Test]", "1.0.0", "0.6.5");

        Assert.Equal("[App.Test] アプリケーション起動 (version=1.0.0, esutil=0.6.5)", logger.Messages[0]);
    }

    /// <summary>整形後の文字列とレベルを記録するロガー。</summary>
    private sealed class RecordingLogger : ILogger
    {
        /// <summary>記録したメッセージ。</summary>
        public List<string> Messages { get; } = [];

        /// <summary>記録したレベル。</summary>
        public List<LogLevel> Levels { get; } = [];

        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc/>
        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            Levels.Add(logLevel);
        }
    }
}
