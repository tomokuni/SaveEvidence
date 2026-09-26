using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Loupe.UIBase;

namespace EsUtil.SaveEvidence.Loupe.UIBase.Tests;

/// <summary><see cref="LoupeUiLogMessage"/> の LoggerMessage 生成コード（IsEnabled 両パス）を検証するテスト。</summary>
/// <remarks>
/// ログ定義は UI が出力するものだけを本層が持つ（コアが出力する定義はコアが持つ）。<br/>
/// <see cref="RecordingLogger"/>（IsEnabled=true）と <see cref="NullLogger"/>（IsEnabled=false）の両方で<br/>
/// 全メソッドを直接呼び、LoggerMessage 生成コードの state 走査と IsEnabled 分岐の両パスを網羅する。
/// </remarks>
public class LoupeUiLogMessageTests
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

    /// <summary>出力書式が両 UI で共通の書式と一致することを検証する。</summary>
    [Fact]
    public void 出力書式が期待どおりである()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.Equal("[LoupeControl.UIBase] Setting: Source 8x6 → null", logger.Messages[0]);
        Assert.Equal("[LoupeControl.UIBase] Update exception", logger.Messages[1]);
    }

    /// <summary>拡大元画像のログが同一インスタンスの再設定では出力されないことを検証する。</summary>
    [Fact]
    public void 拡大元画像のログは同一インスタンスなら出力しない()
    {
        using var bitmap = EsBitmap.Create(4, 4);
        var logger = new RecordingLogger();

        logger.LogSourceChanged(bitmap, bitmap);
        logger.LogSourceChanged((EsBitmap?)null, null);

        Assert.Empty(logger.Messages);
    }

    /// <summary>拡大元画像のログが寸法の等しい別インスタンスへの差し替えを出力することを検証する。</summary>
    [Fact]
    public void 拡大元画像のログは別インスタンスなら出力する()
    {
        using var oldBitmap = EsBitmap.Create(8, 6);
        using var newBitmap = EsBitmap.Create(8, 6);
        var logger = new RecordingLogger();

        logger.LogSourceChanged(oldBitmap, newBitmap);
        logger.LogSourceChanged(oldBitmap, null);

        Assert.Equal(2, logger.Messages.Count);
        Assert.Equal("[LoupeControl.UIBase] Setting: Source 8x6 → 8x6", logger.Messages[0]);
        Assert.Equal("[LoupeControl.UIBase] Setting: Source 8x6 → null", logger.Messages[1]);
    }

    /// <summary>画像の寸法を各 UI が整形して渡す経路のログが出力されることを検証する。</summary>
    [Fact]
    public void 寸法を整形して渡す経路のログを出力する()
    {
        var logger = new RecordingLogger();

        logger.LogSourceChanged(LoupeLogFormat.DescribeImage(1920, 1080), LoupeLogFormat.Null);

        Assert.Single(logger.Messages);
        Assert.Equal("[LoupeControl.UIBase] Setting: Source 1920x1080 → null", logger.Messages[0]);
    }

    /// <summary>例外ログに例外が渡されることを検証する。</summary>
    [Fact]
    public void 例外ログは例外を受け取る()
    {
        var logger = new RecordingLogger();
        var exception = new InvalidOperationException("失敗");

        logger.LogUpdateError(exception);

        Assert.Single(logger.Messages);
        // メッセージ本体は例外本文を含まない（例外は構造化ログのフィールドとして渡す）
        Assert.Equal("[LoupeControl.UIBase] Update exception", logger.Messages[0]);
    }

    /// <summary>全ログメソッドを指定ロガーで呼び出す。</summary>
    /// <param name="logger">ロガー。</param>
    private static void InvokeAll(ILogger logger)
    {
        logger.LogSourceChanged("8x6", "null");
        logger.LogUpdateError(new InvalidOperationException("test"));
    }
}
