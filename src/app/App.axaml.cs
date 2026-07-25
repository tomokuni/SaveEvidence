using app.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace app;

/// <summary>
/// Avalonia アプリケーションのエントリポイント。
/// </summary>
/// <remarks>
/// DI コンテナの初期化とメインウィンドウの表示を行う。<br/>
/// <see cref="IClassicDesktopStyleApplicationLifetime"/> を使用したデスクトップアプリケーションとして動作する。<br/>
/// </remarks>
public sealed partial class App : Application
{
    /// <summary>
    /// XAML の読み込み。App.axaml で定義されたリソースとスタイルを適用する。
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// フレームワークの初期化完了後、メインウィンドウを生成して表示する。
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // DI コンテナから MainWindow を解決
            var mainWindow = Program.ServiceProvider.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
