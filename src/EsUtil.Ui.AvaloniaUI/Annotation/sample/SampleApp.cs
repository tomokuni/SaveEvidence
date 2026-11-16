using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace EsUtil.Ui.AvaloniaUI.Annotation.Sample;

/// <summary>サンプルのアプリケーション（テーマとメイン ウィンドウを用意する）。</summary>
internal sealed class SampleApp : Application
{
    /// <summary>フレームワークの初期化が完了したときにメイン ウィンドウを表示する。</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        Styles.Add(new FluentTheme());

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
