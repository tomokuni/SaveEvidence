using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FadeMaskControl.Sample.ViewModels;
using FadeMaskControl.Sample.Views;

namespace FadeMaskControl.Sample;

/// <summary>サンプルアプリケーションのエントリポイント。</summary>
/// <remarks>デスクトップ（WinExe）アプリケーションとして <see cref="MainWindow"/> を表示する。</remarks>
public partial class App : Application
{
    /// <summary>アプリケーションを初期化する。</summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>フレームワークの初期化後にビューを表示する。</summary>
    /// <remarks>MainWindow の DataContext に <see cref="MainWindowViewModel"/> を設定して表示する（バインドの必須条件）。</remarks>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel() };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
