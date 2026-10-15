using EsUtil.SaveEvidence.App.WinForms.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinForms.Tests;

/// <summary><see cref="AppLog"/> の LoggerMessage 生成コード（IsEnabled 両パス）を検証するテスト。</summary>
/// <remarks>
/// ログ定義は internal のため、<see cref="RecordingLogger"/>（IsEnabled=true）と<br/>
/// <see cref="NullLogger"/>（IsEnabled=false）の両方で全メソッドを直接呼び、<br/>
/// LoggerMessage 生成コードの IsEnabled 分岐の両パスを網羅する。<br/>
/// 実際の書式・出力先（Serilog）は <c>AppLog.CreateLogger</c> 経由の実運用で確認する。
/// </remarks>
public class AppLogTests
{
    /// <summary>実ロガー設定時に全ログ メソッドが実行されることを検証する。</summary>
    [Fact]
    public void 実ロガー設定時に全ログメソッドが実行される()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.True(logger.IsEnabled(LogLevel.Trace));
        Assert.Equal(10, logger.Messages.Count);
    }

    /// <summary>無効ロガーでも全ログ メソッドが安全に実行されることを検証する。</summary>
    [Fact]
    public void 無効ロガーでも全ログメソッドが安全に実行される()
    {
        // IsEnabled=false（NullLogger）でも生成コードの IsEnabled 分岐（false パス）が実行される
        InvokeAll(NullLogger.Instance);
    }

    /// <summary>出力書式が日本語の定型文になり、接頭辞がアプリ固有になることを検証する。</summary>
    [Fact]
    public void 出力書式が定型文になる()
    {
        var logger = new RecordingLogger();
        InvokeAll(logger);

        Assert.Equal("[App.WinForms] アプリケーション起動 (version=1.0.0)", logger.Messages[0]);
        Assert.Equal("[App.WinForms] アプリケーション終了 (exitCode=0)", logger.Messages[1]);
        Assert.Equal("[App.WinForms] 未処理の例外: AppDomain.UnhandledException", logger.Messages[2]);
        Assert.Equal("[App.WinForms] 一覧の読み込みで例外が発生: C:\\images", logger.Messages[3]);
        Assert.Equal("[App.WinForms] ウィンドウ配置の保存で例外が発生", logger.Messages[4]);
        Assert.Equal("[App.WinForms] ホットキーの登録に失敗: ScreenSelect (Ctrl + Shift + Q)", logger.Messages[5]);
        Assert.Equal("[App.WinForms] サムネイルの生成で例外が発生", logger.Messages[6]);
        Assert.Equal("[App.WinForms] サムネイルの先読みを開始: FolderView 対象=6 件", logger.Messages[7]);
        Assert.Equal("[App.WinForms] ファイルを外部ツールで表示できません: C:\\images\\a.png", logger.Messages[8]);
        Assert.Equal("[App.WinForms] 設定変更の撮影セッション反映で例外が発生: AppearanceSettings", logger.Messages[9]);
    }

    /// <summary>全ログ メソッドを指定ロガーで呼び出す。</summary>
    /// <param name="logger">ロガー。</param>
    private static void InvokeAll(ILogger logger)
    {
        logger.LogAppStart("1.0.0");
        logger.LogAppExit(0);
        logger.LogUnhandledException(new InvalidOperationException("test"), "AppDomain.UnhandledException");
        logger.LogFolderLoadError(new IOException("test"), @"C:\images");
        logger.LogWindowPlacementSaveError(new IOException("test"));
        logger.LogHotKeyRegisterFailed("ScreenSelect", "Ctrl + Shift + Q");
        logger.LogThumbnailGenerateError(new InvalidOperationException("test"));
        logger.LogThumbnailWarmUp("FolderView", 6);
        logger.LogOpenFileError(new IOException("test"), @"C:\images\a.png");
        logger.LogSettingsApplyError(new ArgumentException("test"), "AppearanceSettings");
    }
}
