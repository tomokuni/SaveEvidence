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
/// 実際の入力経路からの出力はウィンドウを必要とするため、ホスト側（<see cref="DragRectControl.Log"/>）の<br/>
/// 設定変更ログと合わせて検証する。
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
        Assert.Equal(12, logger.Messages.Count);
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

        Assert.Equal("[DragRect] MouseDown: pos=(10, 20) clickCount=1 hasSelection=False", logger.Messages[0]);
        Assert.Equal("[DragRect] MouseUp: pos=(10, 20) wasDragging=True hasSelection=True", logger.Messages[1]);
        Assert.Equal("[DragRect] Click: pos=(10, 20)", logger.Messages[2]);
        Assert.Equal("[DragRect] DoubleClick: pos=(10, 20) inside=True", logger.Messages[3]);
        Assert.Equal("[DragRect] MouseDown 直前の位置: pos=(10, 20)", logger.Messages[4]);
        Assert.Equal("[DragRect] MouseUp 直後の位置: pos=(10, 20)", logger.Messages[5]);
        Assert.Equal("[DragRect] DragStart: handle=Move pos=(10, 20)", logger.Messages[6]);
        Assert.Equal("[DragRect] KeyDown: key=Left handled=True", logger.Messages[7]);
        Assert.Equal("[DragRect] ConfirmRequested: rect=(10, 20, 30, 40)", logger.Messages[8]);
        Assert.Equal("[DragRect] CancelRequested", logger.Messages[9]);
        Assert.Equal("[DragRect] ZoomRequested: zoomIn=True", logger.Messages[10]);
        Assert.Equal("[DragRect] Setting: MinSelectionSize 3 → 5", logger.Messages[11]);
    }

    /// <summary>全ログメソッドを指定ロガーで呼び出す。</summary>
    /// <param name="logger">ロガー。</param>
    private static void InvokeAll(ILogger logger)
    {
        LogMessage.LogMouseDown(logger, 10, 20, 1, false);
        LogMessage.LogMouseUp(logger, 10, 20, true, true);
        LogMessage.LogClick(logger, 10, 20);
        LogMessage.LogDoubleClick(logger, 10, 20, true);
        LogMessage.LogPositionBeforeDown(logger, 10, 20);
        LogMessage.LogPositionAfterUp(logger, 10, 20);
        LogMessage.LogDragStart(logger, "Move", 10, 20);
        LogMessage.LogKeyDown(logger, "Left", true);
        LogMessage.LogConfirmRequested(logger, "(10, 20, 30, 40)");
        LogMessage.LogCancelRequested(logger);
        LogMessage.LogZoomRequested(logger, true);
        LogMessage.LogSettingChanged(logger, "MinSelectionSize", "3", "5");
    }
}
