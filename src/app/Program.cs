using System.Diagnostics;
using app.Views;

namespace app;

/// <summary>
/// アプリケーションのメインエントリポイント
/// </summary>
internal static class Program
{
    private static readonly string s_logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SaveEvidence", "debug.log");

    [STAThread]
    static void Main()
    {
        // 強制ログ：ファイルが生成されること自体を確認
        try
        {
            var dir = Path.GetDirectoryName(s_logPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(s_logPath,
                $"=== SaveEvidence Debug Log v2 started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n" +
                $"Exe path: {Environment.ProcessPath}\n" +
                $"Command line: {Environment.CommandLine}\n");
        }
        catch (Exception ex)
        {
            // ファイルが書けない場合でもデバッグ出力には残す
            Debug.WriteLine($"FAILED to write log file: {ex}");
        }

        LogDebug("Application started - main entry point reached");

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        Application.ThreadException += (sender, e) => LogException(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            LogException(ex ?? new Exception("不明なエラー"));
        };

        ApplicationConfiguration.Initialize();
        Application.SetColorMode(SystemColorMode.System);
        LogDebug("Calling Application.Run...");
        Application.Run(new MainForm());
        LogDebug("Application exited.");
    }

    internal static void LogException(Exception ex)
    {
        var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] 例外: {ex}";
        Console.Error.WriteLine(msg);
        Debug.WriteLine(msg);
        AppendLog(msg);
    }

    internal static void LogDebug(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
        Debug.WriteLine(line);
        AppendLog(line);
    }

    private static readonly object s_logLock = new();
    private static void AppendLog(string line)
    {
        try
        {
            lock (s_logLock)
            {
                File.AppendAllText(s_logPath, line + "\n");
            }
        }
        catch { /* ログ書き込み失敗は無視 */ }
    }
}
