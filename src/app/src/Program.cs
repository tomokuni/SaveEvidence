using app.Models;
using app.Services;
using app.ViewModels;
using app.Views;
using Avalonia;
using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.Platform.Win;
using Estable.AvaloniaUI.SnipCaptureUI;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace app;

/// <summary>
/// アプリケーションのエントリポイント。
/// </summary>
/// <remarks>
/// Avalonia のデスクトップアプリケーションとして起動する。<br/>
/// Microsoft.Extensions.DependencyInjection を使用した DI コンテナを構築し、
/// 各 ViewModel および View の依存関係を管理する。<br/>
/// ログ出力は NLog により %LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.log（7日で自動アーカイブ・削除）へ出力される。<br/>
/// </remarks>
internal static class Program
{
    /// <summary>アプリケーション全体で使用する DI サービスプロバイダー。</summary>
    public static ServiceProvider ServiceProvider { get; private set; } = null!;

    /// <summary>設定サービスを DI コンテナから取得する。</summary>
    /// <returns>設定サービス（シングルトン）。</returns>
    public static SettingsService SettingsService
        => (SettingsService)ServiceProvider.GetRequiredService<ISettingsService>();

    /// <summary>メインエントリポイント。</summary>
    [STAThread]
    private static void Main(string[] args)
    {
        // NLog を初期化（nlog.config を自動読み込み）
        var logger = LogManager.GetLogger("App");

        // グローバル例外ハンドラを登録
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            logger.Fatal(ex, "AppDomain.UnhandledException");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.Fatal(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };

        try
        {
            // DI コンテナを構築
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            // Avalonia アプリケーションを起動
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            logger.Fatal(ex, "Main で未処理の例外");
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

        // OS依存サービスの登録（実装は PlatformFactory の Windows 実装。アクション識別子は CaptureType）
        services.AddSingleton<IGlobalHotKeyService<CaptureType>, WinGlobalHotKeyService<CaptureType>>();
        services.AddSingleton<INativeWindowInterceptor, WinNativeWindowInterceptor>();

        // ViewModels
        // MainViewModel はシングルトン（ナビゲーションページ（FolderViewPage / UndoPage 等）が
        // DI から同一インスタンスを取得し、PreviewImage 等の現在状態を共有するため）
        services.AddSingleton<MainViewModel>();
        services.AddTransient<FolderViewViewModel>();
        services.AddTransient<UndoViewModel>();

        // Models
        // UndoStack はシングルトン（CapturePage と UndoPage が同一の Undo / Redo 履歴を共有するため）
        services.AddSingleton<UndoStack>();

        // Views（ウィンドウ）
        services.AddTransient<MainWindow>();
        // サブウィンドウ（Settings / Hotkey / FolderView）はナビゲーションページ（Pages 配下の UserControl）として実装
    }
}
