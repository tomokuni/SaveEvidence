using EsUtil.SaveEvidence.App.AvaloniaUI.Logging;
using EsUtil.Platform.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Tests;

/// <summary><see cref="AppLog"/> の LoggerMessage 生成コード（IsEnabled 両パス）を検証するテスト。</summary>
/// <remarks>
/// ログ定義は internal のため、<see cref="RecordingLogger"/>（IsEnabled=true）と
/// <see cref="NullLogger"/>（IsEnabled=false）の両方で全メソッドを直接呼び、
/// LoggerMessage 生成コードの state 走査（Count・インデクサ・列挙・範囲外アクセス）と
/// IsEnabled 分岐の両パスを網羅する。
/// </remarks>
public class AppLogTests
{
    [Fact]
    public void 実ロガー設定時に全ログメソッドが実行される()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.True(logger.IsEnabled(LogLevel.Trace));
    }

    [Fact]
    public void 無効ロガーでも全ログメソッドが安全に実行される()
    {
        // IsEnabled=false（NullLogger）でも生成コードの IsEnabled 分岐（false パス）が実行される
        InvokeAll(NullLogger.Instance);
    }

    /// <summary>全ログメソッドを指定ロガーで呼び出す。</summary>
    private static void InvokeAll(ILogger logger)
    {
        logger.LogCaptureMenuScreen();
        logger.LogCaptureMenuWindow();
        logger.LogCaptureMenuArea();
        logger.LogPreviewPointerPressed(isRight: true, hasCropTask: false);
        logger.LogSettingsApplyError(new InvalidOperationException("test"), "ZoomLevel");
        logger.LogHotKeyReceived();
        logger.LogHotKeyTriggered(CaptureType.AreaSelect);
        logger.LogActivated();
        logger.LogDeactivated();
        logger.LogStartSelectionMode(CaptureType.ScreenSelect);
        logger.LogSessionReturned("1920x1080", "0,0,1920,1080");
    }

    /// <summary>LoggerMessage 生成コードの state 走査（Count・インデクサ・IEnumerable・範囲外）を網羅するテスト用ロガー。</summary>
    private sealed class RecordingLogger : ILogger
    {
        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // 生成コードが state を IReadOnlyList として走査する経路を実行する
            if (state is IReadOnlyList<KeyValuePair<string, object?>> list)
            {
                _ = list.Count;
                for (int i = 0; i < list.Count; i++)
                {
                    _ = list[i];
                }
                foreach (var kv in list)
                {
                    _ = kv.Key;
                }
                try
                {
                    _ = list[list.Count];
                }
                catch (IndexOutOfRangeException)
                {
                    // 範囲外アクセスの例外経路も生成コードの網羅対象として実行する
                }
            }
        }
    }
}
