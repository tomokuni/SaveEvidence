using System.ComponentModel;
using Estable.SaveEvidence.Enum;
using Estable.SaveEvidence.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;

namespace Estable.SaveEvidence;

/// <summary>
/// Avalonia アプリケーションのエントリポイント。
/// </summary>
/// <remarks>
/// DI コンテナの初期化とメインウィンドウの表示を行う。<br/>
/// <see cref="IClassicDesktopStyleApplicationLifetime"/> を使用したデスクトップアプリケーションとして動作する。<br/>
/// 設定のテーマモード（<see cref="Models.Settings.ThemeMode"/>）を <c>RequestedThemeVariant</c> へ反映する。<br/>
/// </remarks>
public sealed partial class App : Application
{
    /// <summary>
    /// XAML の読み込み。Estable.SaveEvidence.axaml で定義されたリソースとスタイルを適用する。
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
        // 設定のダークモードをテーマへ反映し、変更時は即時反映する
        ApplyThemeVariant();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // DI コンテナから MainWindow を解決
            var mainWindow = Program.ServiceProvider.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>設定のテーマモードを <c>RequestedThemeVariant</c> へ反映する。</summary>
    /// <remarks>
    /// 起動時に設定値を反映し、設定変更（<see cref="Models.Settings.ThemeMode"/>）時は
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> を購読して即時反映する。<br/>
    /// FluentTheme は <c>RequestedThemeVariant</c> に応じて全リソース（DynamicResource）が自動的に切り替わるため、
    /// 個別のコントロールへの色設定は不要。
    /// </remarks>
    private void ApplyThemeVariant()
    {
        var settings = Program.SettingsService.Current;

        // 設定変更時にテーマを再適用する（購読解除は不要。App はアプリ生存期間中生き続ける）
        settings.PropertyChanged -= OnSettingsPropertyChanged;
        settings.PropertyChanged += OnSettingsPropertyChanged;

        UpdateRequestedTheme(settings.ThemeMode);
    }

    /// <summary>設定プロパティ変更時のハンドラ。テーマモード変更のみテーマへ反映する。</summary>
    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Models.Settings.ThemeMode) && sender is Models.Settings settings)
        {
            UpdateRequestedTheme(settings.ThemeMode);
        }
    }

    /// <summary>指定されたテーマモードでテーマを更新する。</summary>
    /// <param name="mode">テーマモード（Light / Dark / System）。</param>
    /// <remarks>
    /// <see cref="ThemeMode.System"/> は <see cref="ThemeVariant.Default"/> を使用し、
    /// OS のダーク / ライト設定に自動追従する。<br/>
    /// FluentTheme は <c>RequestedThemeVariant</c> に応じて全リソース（DynamicResource）が自動的に切り替わるため、
    /// 個別のコントロールへの色設定は不要。
    /// </remarks>
    private void UpdateRequestedTheme(ThemeMode mode)
    {
        RequestedThemeVariant = mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default, // System: OS に追従
        };
    }
}
