using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Estable.Marquee.Tests;

/// <summary><see cref="MarqueeLogMessage"/> の LoggerMessage 生成コード（IsEnabled 両パス）を検証するテスト。</summary>
/// <remarks>
/// ログ定義は internal のため、<see cref="RecordingLogger"/>（IsEnabled=true）と<br/>
/// <see cref="NullLogger"/>（IsEnabled=false）の両方で全メソッドを直接呼び、<br/>
/// LoggerMessage 生成コードの state 走査と IsEnabled 分岐の両パスを網羅する。
/// </remarks>
public class MarqueeLogMessageTests
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

    /// <summary>出力書式が両 UI で共通の書式と一致することを検証する。</summary>
    [Fact]
    public void 出力書式が期待どおりである()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.Equal("[Marquee] MouseDown: pos=(10, 20) clickCount=1 hasSelection=False", logger.Messages[0]);
        Assert.Equal("[Marquee] MouseUp: pos=(10, 20) wasDragging=True hasSelection=True", logger.Messages[1]);
        Assert.Equal("[Marquee] Click: pos=(10, 20)", logger.Messages[2]);
        Assert.Equal("[Marquee] DoubleClick: pos=(10, 20) inside=True", logger.Messages[3]);
        Assert.Equal("[Marquee] MouseDown 直前の位置: pos=(10, 20)", logger.Messages[4]);
        Assert.Equal("[Marquee] MouseUp 直後の位置: pos=(10, 20)", logger.Messages[5]);
        Assert.Equal("[Marquee] DragStart: handle=Move pos=(10, 20)", logger.Messages[6]);
        Assert.Equal("[Marquee] KeyDown: key=Left handled=True", logger.Messages[7]);
        Assert.Equal("[Marquee] ConfirmRequested: rect=(10, 20, 30, 40)", logger.Messages[8]);
        Assert.Equal("[Marquee] CancelRequested", logger.Messages[9]);
        Assert.Equal("[Marquee] ZoomRequested: zoomIn=True", logger.Messages[10]);
        Assert.Equal("[Marquee] Setting: MinSelectionSize 3 → 5", logger.Messages[11]);
    }

    /// <summary>全ログメソッドを指定ロガーで呼び出す。</summary>
    /// <param name="logger">ロガー。</param>
    private static void InvokeAll(ILogger logger)
    {
        MarqueeLogMessage.LogMouseDown(logger, 10, 20, 1, false);
        MarqueeLogMessage.LogMouseUp(logger, 10, 20, true, true);
        MarqueeLogMessage.LogClick(logger, 10, 20);
        MarqueeLogMessage.LogDoubleClick(logger, 10, 20, true);
        MarqueeLogMessage.LogPositionBeforeDown(logger, 10, 20);
        MarqueeLogMessage.LogPositionAfterUp(logger, 10, 20);
        MarqueeLogMessage.LogDragStart(logger, "Move", 10, 20);
        MarqueeLogMessage.LogKeyDown(logger, "Left", true);
        MarqueeLogMessage.LogConfirmRequested(logger, "(10, 20, 30, 40)");
        MarqueeLogMessage.LogCancelRequested(logger);
        MarqueeLogMessage.LogZoomRequested(logger, true);
        MarqueeLogMessage.LogSettingChanged(logger, "MinSelectionSize", "3", "5");
    }
}
