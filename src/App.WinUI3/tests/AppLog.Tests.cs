using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Input;
using EsUtil.SaveEvidence.App.WinUI3.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinUI3.Tests;

/// <summary><see cref="AppLog"/> の LoggerMessage 生成コード（IsEnabled 両パス）を検証するテスト。</summary>
/// <remarks>
/// ログ定義は internal のため、<see cref="RecordingLogger"/>（IsEnabled=true）と<br/>
/// <see cref="NullLogger"/>（IsEnabled=false）の両方で全メソッドを直接呼び、<br/>
/// LoggerMessage 生成コードの state 走査と IsEnabled 分岐の両パスを網羅する。
/// </remarks>
public class AppLogTests
{
    /// <summary>実ロガー設定時に全ログメソッドが実行され、接頭辞と書式が規約どおりになることを検証する。</summary>
    [Fact]
    internal void 実ロガー設定時に全ログメソッドが実行される()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.Equal(15, logger.Messages.Count);
        Assert.All(logger.Messages, message => Assert.StartsWith("[App.WinUI3] ", message, StringComparison.Ordinal));
    }

    /// <summary>無効ロガーでも全ログメソッドが安全に実行されることを検証する。</summary>
    [Fact]
    internal void 無効ロガーでも全ログメソッドが安全に実行される()
        => InvokeAll(NullLogger.Instance);

    /// <summary>出力書式が日本語の定型文になることを検証する。</summary>
    [Fact]
    internal void 出力書式が定型文になる()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.Equal("[App.WinUI3] アプリケーション起動 (version=1.0.0)", logger.Messages[0]);
        Assert.Equal("[App.WinUI3] アプリケーション終了 (exitCode=0)", logger.Messages[1]);
        Assert.Equal("[App.WinUI3] 一覧の読み込みで例外が発生: C:\\images", logger.Messages[3]);
        Assert.Equal("[App.WinUI3] ウィンドウ配置の保存で例外が発生", logger.Messages[4]);
        Assert.Equal("[App.WinUI3] ホットキーを登録できませんでした: ScreenSelect, key=Q", logger.Messages[9]);
        Assert.Equal("[App.WinUI3] Windows App Runtime を用意できませんでした (required=2.6.4, before=(なし), after=(なし))", logger.Messages[10]);
        Assert.Equal(LogLevel.Warning, logger.Levels[10]);
        Assert.Equal("[App.WinUI3] Windows App Runtime を用意しました (required=2.6.4, before=(なし), after=2.6.5.0, initialized=True)", logger.Messages[11]);
        Assert.Equal(LogLevel.Information, logger.Levels[11]);
        Assert.Equal(
            "[App.WinUI3] 要求する Windows App Runtime の版が指定されていません (library=2.6.4)"
            + " — アプリは自分のビルド版を指定してください",
            logger.Messages[12]);
        Assert.Equal(LogLevel.Warning, logger.Levels[12]);
        Assert.Equal(
            "[App.WinUI3] 環境情報: OS=Windows 11 (X64) / .NET=.NET 10.0.0 (10.0.0)"
            + " / Windows App Runtime=2.6.5.0, 2.6.4.0 (required=2.6.5, loaded=2.6.5.0)"
            + " / セッション=Console / クライアント=(ローカル) / 仮想スクリーン=0,0 1920x1080"
            + " / ウィンドウ倍率=1.25 / モニター倍率=1.25x / アダプター=GPU (8192 MB)",
            logger.Messages[13]);
        Assert.Equal(LogLevel.Information, logger.Levels[13]);
        Assert.Equal(
            "[App.WinUI3] 文字の描画: 文字倍率=1 / 実測=既定 18x19 / Segoe UI 18x19 / テーマ=Light",
            logger.Messages[14]);
        Assert.Equal(LogLevel.Debug, logger.Levels[14]);
    }

    /// <summary>全ログメソッドを指定ロガーで呼び出す。</summary>
    /// <param name="logger">ロガー。</param>
    private static void InvokeAll(ILogger logger)
    {
        logger.LogAppStart("1.0.0");
        logger.LogAppExit(0);
        logger.LogUnhandledException(new InvalidOperationException("test"), "AppDomain.UnhandledException");
        logger.LogFolderLoadError(new IOException("test"), @"C:\images");
        logger.LogWindowPlacementSaveError(new IOException("test"));
        logger.LogThumbnailGenerateError(new InvalidOperationException("test"));
        logger.LogThumbnailWarmUp("フォルダビュー", 3);
        logger.LogSettingsApplyError(new InvalidOperationException("test"), "LoupeSize");
        logger.LogClipboardError(new InvalidOperationException("test"), "コピー");
        logger.LogHotKeyRegisterFailed(CaptureType.ScreenSelect, EsKey.Q);
        logger.LogWindowsAppRuntimeUnavailable("2.6.4", "(なし)", "(なし)");
        logger.LogWindowsAppRuntimeReady("2.6.4", "(なし)", "2.6.5.0", true);
        logger.LogWindowsAppRuntimeVersionNotSpecified("2.6.4");
        logger.LogEnvironment(
            "Windows 11 (X64)", ".NET 10.0.0 (10.0.0)", "2.6.5.0, 2.6.4.0", "2.6.5", "2.6.5.0",
            "Console", "(ローカル)", "0,0 1920x1080", 1.25, "1.25x", "GPU (8192 MB)");
        logger.LogTextRenderProbe(1.0, "既定 18x19 / Segoe UI 18x19", "Light");
    }
}