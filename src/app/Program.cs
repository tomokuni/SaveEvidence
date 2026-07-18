using System.Diagnostics;
using app.Views;

namespace app;

/// <summary>
/// アプリケーションのメインエントリポイント
/// </summary>
internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // グローバル例外ハンドラの設定
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        // 高DPI設定を明示的に指定
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        Application.ThreadException += (sender, e) =>
        {
            LogException(e.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var exception = e.ExceptionObject as Exception;
            LogException(exception ?? new Exception("不明なエラーが発生しました"));
        };

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.SetColorMode(SystemColorMode.System);
        Application.Run(new MainForm());
    }

    /// <summary>
    /// 例外情報をコンソールとデバッグ出力に記録する
    /// </summary>
    internal static void LogException(Exception ex)
    {
        var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 例外が発生しました: {ex}";
        Console.Error.WriteLine(message);
        Debug.WriteLine(message);
    }
}
