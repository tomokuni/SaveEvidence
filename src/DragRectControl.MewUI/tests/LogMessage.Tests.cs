using Estable.MewUI.DragRect.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Estable.MewUI.DragRect.Tests;

/// <summary><see cref="LogMessage"/> の LoggerMessage 生成コード（IsEnabled 両パス）を検証するテスト。</summary>
/// <remarks>
/// ログ定義は internal のため、<see cref="RecordingLogger"/>（IsEnabled=true）と<br/>
/// <see cref="NullLogger"/>（IsEnabled=false）の両方で全メソッドを直接呼び、<br/>
/// LoggerMessage 生成コードの state 走査と IsEnabled 分岐の両パスを網羅する。<br/>
/// 入力の判断に伴うログはコア（<c>DragRectControl.Core</c>）が出力するため、本テストは UI 層の操作のログのみを対象とする。
/// </remarks>
public class LogMessageTests
{
    /// <summary>実ロガー設定時に全ログメソッドが実行されることを検証する。</summary>
    [Fact]
    public void 実ロガー設定時に全ログメソッドが実行される()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.True(logger.IsEnabled(LogLevel.Trace));
        Assert.Equal(2, logger.Messages.Count);
    }

    /// <summary>無効ロガーでも全ログメソッドが安全に実行されることを検証する。</summary>
    [Fact]
    public void 無効ロガーでも全ログメソッドが安全に実行される()
    {
        // IsEnabled=false（NullLogger）でも生成コードの IsEnabled 分岐（false パス）が実行される
        InvokeAll(NullLogger.Instance);
    }

    /// <summary>出力書式が Avalonia 版と一致することを検証する。</summary>
    [Fact]
    public void 出力書式がAvalonia版と一致する()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.Equal("[DragRect] ZoomRequested: zoomIn=True", logger.Messages[0]);
        Assert.Equal("[DragRect] Setting: MinSelectionSize 3 → 5", logger.Messages[1]);
    }

    /// <summary>全ログメソッドを指定ロガーで呼び出す。</summary>
    /// <param name="logger">ロガー。</param>
    private static void InvokeAll(ILogger logger)
    {
        LogMessage.LogZoomRequested(logger, true);
        LogMessage.LogSettingChanged(logger, "MinSelectionSize", "3", "5");
    }
}
