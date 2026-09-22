using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Estable.FadeMask.Tests;

/// <summary><see cref="LogMessage"/>（コア共通のログ定義）のテスト。</summary>
/// <remarks>
/// ログ定義は internal のため、<see cref="RecordingLogger"/>（IsEnabled=true）と<br/>
/// <see cref="NullLogger"/>（IsEnabled=false）の両方で全メソッドを直接呼び、<br/>
/// LoggerMessage 生成コードの state 走査と IsEnabled 分岐の両パスを網羅する。<br/>
/// 矩形の文字列化もコアが行うため、書式（<c>(X, Y, W, H)</c>）は本テストが保証する。
/// </remarks>
public class LogMessageTests
{
    /// <summary>選択矩形の変更が変更前後で書式どおり出力されることを検証する。</summary>
    [Fact]
    public void LogSelectionRectChanged_変更前後を出力する()
    {
        var logger = new RecordingLogger();

        logger.LogSelectionRectChanged(new EsRect(1, 2, 3, 4), new EsRect(10, 20, 30, 40));

        Assert.Equal("[FadeMask] SelectionRect: (1, 2, 3, 4) → (10, 20, 30, 40)", Assert.Single(logger.Messages));
    }

    /// <summary>選択矩形の未設定（null）が <c>null</c> として出力されることを検証する。</summary>
    [Fact]
    public void LogSelectionRectChanged_未設定はnullと出力する()
    {
        var logger = new RecordingLogger();

        logger.LogSelectionRectChanged(null, new EsRect(10, 20, 30, 40));
        logger.LogSelectionRectChanged(new EsRect(10, 20, 30, 40), null);

        Assert.Equal("[FadeMask] SelectionRect: null → (10, 20, 30, 40)", logger.Messages[0]);
        Assert.Equal("[FadeMask] SelectionRect: (10, 20, 30, 40) → null", logger.Messages[1]);
    }

    /// <summary>ハイライト矩形の変更が書式どおり出力されることを検証する。</summary>
    [Fact]
    public void LogHighlightRectChanged_変更前後を出力する()
    {
        var logger = new RecordingLogger();

        logger.LogHighlightRectChanged(new EsRect(1, 2, 3, 4), null);

        Assert.Equal("[FadeMask] HighlightRect: (1, 2, 3, 4) → null", Assert.Single(logger.Messages));
    }

    /// <summary>座標変換ポートの設定が真偽で出力されることを検証する。</summary>
    [Fact]
    public void LogCoordinateTransform_設定状態を出力する()
    {
        var logger = new RecordingLogger();

        logger.LogCoordinateTransform(true);
        logger.LogCoordinateTransform(false);

        Assert.Equal(2, logger.Messages.Count);
        Assert.Equal("[FadeMask] CoordinateTransform: True", logger.Messages[0]);
        Assert.Equal("[FadeMask] CoordinateTransform: False", logger.Messages[1]);
    }

    /// <summary>無効ロガー（IsEnabled=false）でも全ログメソッドが安全に実行されることを検証する。</summary>
    [Fact]
    public void 無効ロガーでも全ログメソッドが安全に実行される()
    {
        // IsEnabled=false（NullLogger）でも生成コードの IsEnabled 分岐（false パス）が実行される
        var logger = NullLogger.Instance;

        logger.LogSelectionRectChanged(null, new EsRect(1, 2, 3, 4));
        logger.LogHighlightRectChanged(new EsRect(1, 2, 3, 4), null);
        logger.LogCoordinateTransform(false);
    }
}
