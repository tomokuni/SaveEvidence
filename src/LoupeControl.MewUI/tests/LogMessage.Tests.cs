using Estable.MewUI.Loupe.Logging;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using Xunit;

namespace Estable.MewUI.Loupe.Tests;

/// <summary><see cref="LogMessage"/> と <see cref="LoupeControl.Log"/> のテスト。</summary>
/// <remarks>
/// ログ定義は internal のため、<see cref="RecordingLogger"/>（IsEnabled=true）と<br/>
/// <see cref="NullLogger"/>（IsEnabled=false）の両方で全メソッドを直接呼び、<br/>
/// LoggerMessage 生成コードの state 走査と IsEnabled 分岐の両パスを網羅する。<br/>
/// 実際の設定変更からの出力は <see cref="LoupeControlLogTests"/> が検証する。
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

        Assert.Equal("[LoupeCtrl] Setting: ZoomLevel 4 → 8", logger.Messages[0]);
        Assert.Equal("[LoupeCtrl] Update exception", logger.Messages[1]);
    }

    /// <summary>全ログメソッドを指定ロガーで呼び出す。</summary>
    /// <param name="logger">ロガー。</param>
    private static void InvokeAll(ILogger logger)
    {
        LogMessage.LogSettingChanged(logger, nameof(LoupeControl.ZoomLevel), "4", "8");
        LogMessage.LogUpdateError(logger, new InvalidOperationException("test"));
    }
}

/// <summary><see cref="LoupeControl"/> の設定変更ログのテスト。</summary>
/// <remarks>描画は行わないためウィンドウを必要としない（UI 初期化なしで並列実行できる）。</remarks>
public class LoupeControlLogTests
{
    /// <summary>設定値の変更が変更前後で出力されることを検証する。</summary>
    [Fact]
    public void Log_設定値の変更を出力する()
    {
        var control = new LoupeControl();
        var logger = new RecordingLogger();
        control.Log = logger;

        control.ZoomLevel = 8;
        control.RequestedSize = 200;
        control.CenterPoint = new EsPoint(10, 20);

        Assert.Equal(
            [
                "[LoupeCtrl] Setting: ZoomLevel 4 → 8",
                "[LoupeCtrl] Setting: RequestedSize 160 → 200",
                "[LoupeCtrl] Setting: CenterPoint null → (10, 20)",
            ],
            logger.Messages);
    }

    /// <summary>補正後の値でログ出力されることを検証する。</summary>
    [Fact]
    public void Log_補正後の値で出力する()
    {
        var control = new LoupeControl();
        var logger = new RecordingLogger();
        control.Log = logger;

        // 範囲外の値は安全域へ補正される（補正後の値がログに残る）
        control.ZoomLevel = 9999;

        Assert.Equal("[LoupeCtrl] Setting: ZoomLevel 4 → 64", Assert.Single(logger.Messages));
    }

    /// <summary>同じ値の再設定では出力しないことを検証する。</summary>
    [Fact]
    public void Log_同じ値の再設定では出力しない()
    {
        var control = new LoupeControl();
        var logger = new RecordingLogger();
        control.Log = logger;

        control.ZoomLevel = 4;
        control.RequestedSize = 160;

        Assert.Empty(logger.Messages);
    }

    /// <summary>画像の設定が解像度で出力されることを検証する。</summary>
    [Fact]
    public void Log_画像の設定を出力する()
    {
        using var image = TestHelper.CreateBitmap(32, SKColors.Red);
        var control = new LoupeControl();
        var logger = new RecordingLogger();
        control.Log = logger;

        control.Source = image;

        Assert.Equal("[LoupeCtrl] Setting: Source null → 32x32", Assert.Single(logger.Messages));
    }

    /// <summary>既定（null 設定）でも例外にならないことを検証する。</summary>
    [Fact]
    public void Log_既定はNullLoggerで例外にならない()
    {
        var control = new LoupeControl { Log = null! };

        control.ZoomLevel = 8;
        control.RequestedSize = 200;
        control.CenterPoint = new EsPoint(1, 2);
    }
}
