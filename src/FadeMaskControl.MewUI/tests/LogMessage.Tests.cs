using Estable.MewUI.FadeMask.Logging;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Estable.MewUI.FadeMask.Tests;

/// <summary><see cref="LogMessage"/> と <see cref="FadeMaskControl.Log"/> のテスト。</summary>
/// <remarks>
/// ログ定義は internal のため、<see cref="RecordingLogger"/>（IsEnabled=true）と<br/>
/// <see cref="NullLogger"/>（IsEnabled=false）の両方で全メソッドを直接呼び、<br/>
/// LoggerMessage 生成コードの state 走査と IsEnabled 分岐の両パスを網羅する。<br/>
/// 実際の状態変更からの出力は <see cref="FadeMaskControlTests"/> が検証する。
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
        Assert.Equal(3, logger.Messages.Count);
    }

    /// <summary>無効ロガーでも全ログメソッドが安全に実行されることを検証する。</summary>
    [Fact]
    public void 無効ロガーでも全ログメソッドが安全に実行される()
    {
        // IsEnabled=false（NullLogger）でも生成コードの IsEnabled 分岐（false パス）が実行される
        InvokeAll(NullLogger.Instance);
    }

    /// <summary>出力書式が想定どおりであることを検証する。</summary>
    [Fact]
    public void 出力書式が想定どおりである()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.Equal("[FadeMask] SelectionRect: null → (10, 20, 30, 40)", logger.Messages[0]);
        Assert.Equal("[FadeMask] HighlightRect: (1, 2, 3, 4) → null", logger.Messages[1]);
        Assert.Equal("[FadeMask] CoordinateTransform: True", logger.Messages[2]);
    }

    /// <summary>全ログメソッドを指定ロガーで呼び出す。</summary>
    /// <param name="logger">ロガー。</param>
    private static void InvokeAll(ILogger logger)
    {
        LogMessage.LogSelectionRectChanged(logger, "null", "(10, 20, 30, 40)");
        LogMessage.LogHighlightRectChanged(logger, "(1, 2, 3, 4)", "null");
        LogMessage.LogCoordinateTransform(logger, true);
    }
}

/// <summary><see cref="FadeMaskControl"/> の状態変更ログのテスト。</summary>
/// <remarks>描画は行わないためウィンドウを必要としない（UI 初期化なしで並列実行できる）。</remarks>
public class FadeMaskControlLogTests
{
    /// <summary>選択矩形の変更が変更前後で出力されることを検証する。</summary>
    [Fact]
    public void Log_選択矩形の変更を出力する()
    {
        var control = new FadeMaskControl();
        var logger = new RecordingLogger();
        control.Log = logger;

        control.SelectionRect = new EsRect(10, 20, 30, 40);

        Assert.Equal("[FadeMask] SelectionRect: null → (10, 20, 30, 40)", Assert.Single(logger.Messages));
    }

    /// <summary>同じ値の再設定では出力しないことを検証する。</summary>
    [Fact]
    public void Log_同じ値の再設定では出力しない()
    {
        var control = new FadeMaskControl { SelectionRect = new EsRect(10, 20, 30, 40) };
        var logger = new RecordingLogger();
        control.Log = logger;

        control.SelectionRect = new EsRect(10, 20, 30, 40);
        control.SelectionRect = null;
        control.SelectionRect = null;

        // 変更（null）の 1 件のみが出力される
        Assert.Equal("[FadeMask] SelectionRect: (10, 20, 30, 40) → null", Assert.Single(logger.Messages));
    }

    /// <summary>ハイライト矩形の変更が出力されることを検証する。</summary>
    [Fact]
    public void Log_ハイライト矩形の変更を出力する()
    {
        var control = new FadeMaskControl();
        var logger = new RecordingLogger();
        control.Log = logger;

        control.HighlightRect = new EsRect(1, 2, 3, 4);

        Assert.Equal("[FadeMask] HighlightRect: null → (1, 2, 3, 4)", Assert.Single(logger.Messages));
    }

    /// <summary>座標変換ポートの設定が出力されることを検証する。</summary>
    [Fact]
    public void Log_座標変換ポートの設定を出力する()
    {
        var control = new FadeMaskControl();
        var logger = new RecordingLogger();
        control.Log = logger;

        control.Port = new CoordTransform(p => p, p => p, () => new EsSize(100, 100));
        control.Port = null;

        Assert.Equal(2, logger.Messages.Count);
        Assert.Equal("[FadeMask] CoordinateTransform: True", logger.Messages[0]);
        Assert.Equal("[FadeMask] CoordinateTransform: False", logger.Messages[1]);
    }

    /// <summary>既定（null 設定）でも例外にならないことを検証する。</summary>
    [Fact]
    public void Log_既定はNullLoggerで例外にならない()
    {
        var control = new FadeMaskControl { Log = null! };

        control.SelectionRect = new EsRect(1, 2, 3, 4);
        control.HighlightRect = new EsRect(1, 2, 3, 4);
        control.Port = null;
    }
}
