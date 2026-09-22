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

    /// <summary>設定・状態の変更で変化した矩形のみが出力されることを検証する。</summary>
    [Fact]
    public void LogOptionsChanged_変化した矩形のみを出力する()
    {
        var logger = new RecordingLogger();
        var oldValue = FadeMaskOptions.Default with
        {
            LogicalSelectionRect = new EsRect(1, 2, 3, 4),
            HasSelection = true,
        };
        var newValue = oldValue with
        {
            LogicalSelectionRect = new EsRect(10, 20, 30, 40),
            LogicalHighlightRect = new EsRect(5, 6, 7, 8),
            HasHighlight = true,
        };

        logger.LogOptionsChanged(oldValue, newValue);

        Assert.Equal(2, logger.Messages.Count);
        Assert.Equal("[FadeMask] SelectionRect: (1, 2, 3, 4) → (10, 20, 30, 40)", logger.Messages[0]);
        Assert.Equal("[FadeMask] HighlightRect: null → (5, 6, 7, 8)", logger.Messages[1]);
    }

    /// <summary>矩形が変わっていなければ何も出力しないことを検証する。</summary>
    /// <remarks>外観・モード・全面暗転・情報ラベルの文言だけの変更は出力しない（出力量を増やさない）。</remarks>
    [Fact]
    public void LogOptionsChanged_矩形以外の変更では出力しない()
    {
        var logger = new RecordingLogger();
        var oldValue = FadeMaskOptions.Default;
        var newValue = oldValue with
        {
            Mode = FadeMaskRenderMode.WindowSelect,
            ShowFullOverlay = true,
            WindowTitle = "メモ帳",
        };

        logger.LogOptionsChanged(oldValue, newValue);

        Assert.Empty(logger.Messages);
    }

    /// <summary>同じ値の再設定では何も出力しないことを検証する。</summary>
    [Fact]
    public void LogOptionsChanged_同じ値では出力しない()
    {
        var logger = new RecordingLogger();
        var options = FadeMaskOptions.Default with { LogicalSelectionRect = new EsRect(1, 2, 3, 4) };

        logger.LogOptionsChanged(options, options);

        Assert.Empty(logger.Messages);
    }

    /// <summary>無効ロガー（IsEnabled=false）でも全ログメソッドが安全に実行されることを検証する。</summary>
    [Fact]
    public void 無効ロガーでも全ログメソッドが安全に実行される()
    {
        // IsEnabled=false（NullLogger）でも生成コードの IsEnabled 分岐（false パス）が実行される
        var logger = NullLogger.Instance;

        logger.LogSelectionRectChanged(null, new EsRect(1, 2, 3, 4));
        logger.LogHighlightRectChanged(new EsRect(1, 2, 3, 4), null);
        logger.LogOptionsChanged(FadeMaskOptions.Default, FadeMaskOptions.Default with { LogicalSelectionRect = new EsRect(1, 2, 3, 4) });
        logger.LogCoordinateTransform(false);
    }
}
