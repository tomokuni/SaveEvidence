using EsUtil.SaveEvidence.App.MewUI.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EsUtil.SaveEvidence.App.MewUI.Tests;

/// <summary><see cref="AppLog"/> の LoggerMessage 生成コード（IsEnabled 両パス）を検証するテスト。</summary>
/// <remarks>
/// ログ定義は internal のため、<see cref="RecordingLogger"/>（IsEnabled=true）と<br/>
/// <see cref="NullLogger"/>（IsEnabled=false）の両方で全メソッドを直接呼び、<br/>
/// LoggerMessage 生成コードの state 走査と IsEnabled 分岐の両パスを網羅する。<br/>
/// 実際の書式・出力先（Serilog）は <see cref="AppLog.CreateLogger"/> 経由の実運用で確認する。
/// </remarks>
public class AppLogTests
{
    /// <summary>実ロガー設定時に全ログメソッドが実行されることを検証する。</summary>
    [Fact]
    internal void 実ロガー設定時に全ログメソッドが実行される()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.True(logger.IsEnabled(LogLevel.Trace));
        Assert.Equal(10, logger.Messages.Count);
    }

    /// <summary>無効ロガーでも全ログメソッドが安全に実行されることを検証する。</summary>
    [Fact]
    internal void 無効ロガーでも全ログメソッドが安全に実行される()
    {
        // IsEnabled=false（NullLogger）でも生成コードの IsEnabled 分岐（false パス）が実行される
        InvokeAll(NullLogger.Instance);
    }

    /// <summary>出力書式が日本語の定型文になることを検証する。</summary>
    [Fact]
    internal void 出力書式が定型文になる()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.Equal("[App.MewUI] アプリケーション起動 (version=1.0.0)", logger.Messages[0]);
        Assert.Equal("[App.MewUI] アプリケーション終了 (exitCode=0)", logger.Messages[1]);
        Assert.Equal("[App.MewUI] 撮影開始: type=AreaSelect", logger.Messages[2]);
        Assert.Equal("[App.MewUI] 撮影確定: rect=(0, 0, 100, 50)", logger.Messages[3]);
        Assert.Equal("[App.MewUI] 撮影キャンセル", logger.Messages[4]);
        Assert.Equal("[App.MewUI] 切出し開始: image=320x180", logger.Messages[5]);
        Assert.Equal("[App.MewUI] 切出し確定: rect=(10, 10, 50, 40)", logger.Messages[6]);
        Assert.Equal("[App.MewUI] 切出しキャンセル", logger.Messages[7]);
        Assert.Equal("[App.MewUI] 未処理の例外: AppDomain.UnhandledException", logger.Messages[8]);
        Assert.Equal("[App.MewUI] フォルダビュー一覧の読み込みで例外が発生: C:\\images", logger.Messages[9]);
    }

    /// <summary>全ログメソッドを指定ロガーで呼び出す。</summary>
    /// <param name="logger">ロガー。</param>
    private static void InvokeAll(ILogger logger)
    {
        logger.LogAppStart("1.0.0");
        logger.LogAppExit(0);
        logger.LogCaptureStart("AreaSelect");
        logger.LogCaptureCompleted("(0, 0, 100, 50)");
        logger.LogCaptureCancelled();
        logger.LogCropStart(320, 180);
        logger.LogCropCompleted("(10, 10, 50, 40)");
        logger.LogCropCancelled();
        logger.LogUnhandledException(new InvalidOperationException("test"), "AppDomain.UnhandledException");
        logger.LogFolderLoadError(new IOException("test"), @"C:\images");
    }
}
