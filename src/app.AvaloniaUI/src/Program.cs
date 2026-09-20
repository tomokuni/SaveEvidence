using System.Reflection;
using Estable.SaveEvidence.Logging;
using Estable.SaveEvidence.Models;
using Estable.SaveEvidence.Services;
using Estable.SaveEvidence.ViewModels;
using Estable.SaveEvidence.Views;
using Avalonia;
using Avalonia.Threading;
using Estable.AvaloniaUI.Platform;
using Estable.AvaloniaUI.SnipCaptureUI;
using Estable.Platform;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Estable.SaveEvidence;

/// <summary>
/// アプリケーションのエントリポイント。
/// </summary>
/// <remarks>
/// Avalonia のデスクトップアプリケーションとして起動する。<br/>
/// Microsoft.Extensions.DependencyInjection を使用した DI コンテナを構築し、
/// 各 ViewModel および View の依存関係を管理する。<br/>
/// ログ出力は Serilog により %LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.log（日次ローテーション・7日で自動削除）へ出力される。<br/>
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
        // Serilog を初期化（ファイル出力先は AppLog.CreateLoggingConfiguration と整合させる）。
        // グローバル例外ハンドラの即時ログ出力に使用するため、Log.Logger を設定する。
        var appLog = AppLog.CreateLogger("App");
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "(unknown)";
        appLog.LogAppStartup(version);

        // グローバル例外ハンドラを登録（未処理の例外をすべて補足・記録してクラッシュ原因を特定できるようにする）
        // ① AppDomain レベルの未処理例外（アプリ全体。スタックトレースを含む Critical で記録）
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            if (ex is null)
            {
                Log.Fatal("AppDomain.UnhandledException (例外オブジェクトを取得できません)");
            }
            else
            {
                appLog.LogUnhandledException(ex, "AppDomain.UnhandledException");
                Log.Fatal(ex, "AppDomain.UnhandledException");
            }
        };

        // ② Task の未観測例外（fire-and-forget のタスクで発生する例外。ルーペの非同期生成などが対象）
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            appLog.LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
            Log.Fatal(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };

        // ③ Avalonia UI スレッド（Dispatcher）の未処理例外（UI 操作・描画・ルーペ更新・ナビゲーションの例外を補足）
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            appLog.LogUnhandledException(e.Exception, "Dispatcher.UIThread.UnhandledException");
            Log.Fatal(e.Exception, "Dispatcher.UIThread.UnhandledException");
            e.Handled = true; // UI スレッドを黙って死亡させず、ウィンドウの表示を維持する（クラッシュ抑止）
        };

        // ④ 例外が最初に投げられた時点を記録する（破棄済みピクセルなどの「最初の例外」を捉える）。
        // 握りつぶされる例外も含むためログ量が膨大になる。次回の不具合調査時のみ、
        // ビルド時に <DefineConstants>FirstChanceLogging</DefineConstants> を付けて有効化する。
#if FirstChanceLogging
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
            appLog.LogFirstChanceException(e.Exception, e.Exception.Message);
#endif

        try
        {
            // DI コンテナを構築
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            // Avalonia アプリケーションを起動（戻り値 = 終了コード）
            var exitCode = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            appLog.LogAppExit(exitCode);
        }
        catch (Exception ex)
        {
            appLog.LogUnhandledException(ex, "Main で未処理の例外");
            Log.Fatal(ex, "Main で未処理の例外");
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

        // OS依存サービスの登録（実装は PlatformFactory が実行時プラットフォームに応じて生成。アクション識別子は CaptureType）
        services.AddSingleton<IGlobalHotKeyService<CaptureType>>(
            _ => PlatformFactory.CreateGlobalHotKeyService<CaptureType>());

        // 画像の書き込み（UI 依存の実装。MainViewModel（app.Core）へ注入する）
        services.AddSingleton<IImageWriter, AvaloniaImageWriter>();

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
