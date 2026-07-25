using app.Helpers;
using app.Services;
using app.ViewModels;
using app.Views;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;

namespace app;

/// <summary>
/// アプリケーションのエントリポイント。
/// </summary>
/// <remarks>
/// Avalonia のデスクトップアプリケーションとして起動する。<br/>
/// Microsoft.Extensions.DependencyInjection を使用した DI コンテナを構築し、
/// 各 ViewModel および View の依存関係を管理する。<br/>
/// </remarks>
internal static class Program
{
    /// <summary>アプリケーション全体で使用する DI サービスプロバイダー。</summary>
    public static ServiceProvider ServiceProvider { get; private set; } = null!;

    /// <summary>メインエントリポイント。</summary>
    [STAThread]
    private static void Main(string[] args)
    {
        // グローバル例外ハンドラを登録
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            CaptureLogger.Log($"[FATAL] AppDomain.UnhandledException: {ex?.Message}");
            CaptureLogger.Log($"[FATAL] 詳細: {ex}");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            CaptureLogger.Log($"[FATAL] TaskScheduler.UnobservedTaskException: {e.Exception.Message}");
            CaptureLogger.Log($"[FATAL] 詳細: {e.Exception}");
            e.SetObserved();
        };

        try
        {
            // 起動時処理: 古いキャプチャログを削除
            CaptureLogger.CleanupOldLogs();

            // DI コンテナを構築
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            // Avalonia アプリケーションを起動
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            CaptureLogger.Log($"[FATAL] Main で未処理の例外: {ex.Message}");
            CaptureLogger.Log($"[FATAL] 詳細: {ex}");
            throw;
        }
    }

    /// <summary>Avalonia アプリケーションビルダーを構成する。</summary>
    /// <returns>Avalonia アプリケーションビルダー</returns>
    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    /// <summary>DI コンテナにサービスを登録する。</summary>
    /// <param name="services">サービスコレクション</param>
    private static void ConfigureServices(IServiceCollection services)
    {
        // Settings（シングルトンとして共有インスタンスを管理）
        services.AddSingleton<ISettingsService, SettingsService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<FolderViewViewModel>();

        // Views（ウィンドウ）
        services.AddTransient<MainWindow>();
        services.AddTransient<SelectionWindow>();
        // SettingsWindow / HotkeyWindow / FolderViewWindow は
        // ナビゲーションページ（Pages 配下の UserControl）に移行済み
    }
}
