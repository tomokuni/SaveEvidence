using app.Services;
using app.Views;

namespace app;

/// <summary>
/// SaveEvidence アプリケーションのメインエントリポイント。
/// </summary>
/// <remarks>
/// 起動シーケンス:<br/>
/// 1. グローバル例外ハンドラの設定（UIスレッド・バックグラウンドスレッド両対応）<br/>
/// 2. HighDPI モード（PerMonitorV2）とシステムカラーモードの設定<br/>
/// 3. <see cref="Services.SettingsService"/>（JSONファイル永続化）の生成<br/>
/// 4. <see cref="MainForm"/> の生成とメインループ開始<br/>
/// </remarks>
internal static class Program
{
    /// <summary>アプリケーションのメインエントリポイント。</summary>
    [STAThread]
    static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        ApplicationConfiguration.Initialize();
        Application.SetColorMode(SystemColorMode.System);

        var settingsService = new SettingsService();

        Application.Run(new MainForm(settingsService));
    }
}
