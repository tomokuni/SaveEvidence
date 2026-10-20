using System.ComponentModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using EsUtil.SaveEvidence.App.AvaloniaUI.Views;
using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EsUtil.SaveEvidence.App.AvaloniaUI;

/// <summary>Avalonia アプリケーションのエントリポイント。</summary>
/// <remarks>
/// DI コンテナの初期化とメインウィンドウの表示を行う。<br/>
/// <see cref="IClassicDesktopStyleApplicationLifetime"/> を使用したデスクトップアプリケーションとして動作する。<br/>
/// 設定のテーマモード（<see cref="EsUtil.SaveEvidence.App.Core.Models.Settings.ThemeMode"/>）を <c>RequestedThemeVariant</c> へ反映する。<br/>
/// </remarks>
public sealed partial class App : Application
{
    /// <summary>XAML の読み込み。 App.axaml で定義されたリソースとスタイルを適用する。</summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // ステータス バーの文字サイズは 4 UI 版で共通の値（App.Core が単一所有する）を使う
        Resources["StatusBarFontSize"] = StatusBarMetrics.FontSize;
        Resources["ToolbarFontSize"] = ToolbarMetrics.FontSize;

        // システム追従（ThemeVariant.Default）で OS のテーマが変わったときも配色を追随させる
        ActualThemeVariantChanged += (_, _) => ApplyPaletteResources();
    }

    /// <summary>フレームワークの初期化完了後、メインウィンドウを生成して表示する。</summary>
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
    /// 起動時に設定値を反映し、設定変更（<see cref="EsUtil.SaveEvidence.App.Core.Models.Settings.ThemeMode"/>）時は
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> を購読して即時反映する。<br/>
    /// FluentTheme は <c>RequestedThemeVariant</c> に応じて全リソース（DynamicResource）が自動的に切り替わるため、
    /// 個別のコントロールへの色設定は不要。
    /// </remarks>
    private void ApplyThemeVariant()
    {
        var settings = Program.SettingsService.Current;

        // 設定変更時にテーマを再適用する（購読解除は不要。 App はアプリ生存期間中生き続ける）
        settings.PropertyChanged -= OnSettingsPropertyChanged;
        settings.PropertyChanged += OnSettingsPropertyChanged;

        UpdateRequestedTheme(settings.ThemeMode);
    }

    /// <summary>設定プロパティ変更時のハンドラ。 テーマモード変更のみテーマへ反映する。</summary>
    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(Settings.ThemeMode), StringComparison.Ordinal) && sender is Settings settings)
        {
            UpdateRequestedTheme(settings.ThemeMode);
        }
    }

    /// <summary>指定されたテーマモードでテーマを更新する。</summary>
    /// <param name="mode">テーマモード（Light / Dark / System）。</param>
    /// <remarks>
    /// <see cref="ThemeMode.System"/> は <see cref="ThemeVariant.Default"/> を使用し、
    /// OS のダーク / ライト設定に自動追従する。<br/>
    /// FluentTheme は <c>RequestedThemeVariant</c> に応じて全リソース（DynamicResource）が自動的に切り替わるが、
    /// アプリ固有の配色（ナビゲーション パネル・画像領域）は Fluent のリソースに無いため、
    /// <see cref="ApplyPaletteResources"/> が <see cref="AppPalette"/> の値で入れ替える。
    /// </remarks>
    private void UpdateRequestedTheme(ThemeMode mode)
    {
        RequestedThemeVariant = mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default, // System: OS に追従
        };

        ApplyPaletteResources();
    }

    /// <summary>アプリ固有の配色リソース（ナビゲーション パネル・画像領域）を現在のテーマで入れ替える。</summary>
    /// <remarks>
    /// 色の値は <see cref="AppPalette"/>（App.Core）が単一所有する（4 つの UI 版で同じ色にするため）。<br/>
    /// <see cref="ThemeVariant.Default"/>（システム追従）の場合は実際のテーマが決まってから反映する必要があるため、
    /// <see cref="Application.ActualThemeVariant"/> を見る。
    /// </remarks>
    private void ApplyPaletteResources()
    {
        var isDark = ActualThemeVariant == ThemeVariant.Dark;
        Resources["NavPanelBackgroundBrush"] = new SolidColorBrush(ToColor(AppPalette.NavPanel(isDark)));
        Resources["ImageAreaBackgroundBrush"] = new SolidColorBrush(ToColor(AppPalette.ImageArea(isDark)));
        Resources["PageBackgroundBrush"] = new SolidColorBrush(ToColor(AppPalette.Page(isDark)));
        Resources["ListBackgroundBrush"] = new SolidColorBrush(ToColor(AppPalette.ListBackground(isDark)));
        Resources["NavItemSelectedBrush"] = new SolidColorBrush(ToColor(AppPalette.NavItemSelected(isDark)));
        Resources["NavSelectionAccentBrush"] = new SolidColorBrush(ToColor(AppPalette.NavItemSelectionAccent(isDark)));
        Resources["ToolbarButtonFaceBrush"] = new SolidColorBrush(ToColor(AppPalette.ButtonFace(isDark)));
        Resources["ToolbarButtonBorderBrush"] = new SolidColorBrush(ToColor(AppPalette.ButtonBorder(isDark)));
        Resources["ToolbarButtonCheckedFaceBrush"] = new SolidColorBrush(ToColor(AppPalette.ButtonCheckedFace(isDark)));
        Resources["ToolbarButtonCheckedBorderBrush"] = new SolidColorBrush(ToColor(AppPalette.ButtonCheckedBorder(isDark)));
        Resources["ToolbarButtonCheckedForegroundBrush"] = new SolidColorBrush(ToColor(AppPalette.ButtonCheckedForeground(isDark)));
        Resources["ToolbarButtonHoverBrush"] = new SolidColorBrush(
            ToColor(AppPalette.ToolbarButtonBackground(isDark, isPressed: false)));
        Resources["ToolbarButtonPressedBrush"] = new SolidColorBrush(
            ToColor(AppPalette.ToolbarButtonBackground(isDark, isPressed: true)));
    }

    /// <summary>32 ビットの色値（0xAARRGGBB）を Avalonia の色へ変換する。</summary>
    /// <param name="argb">色（0xAARRGGBB）。</param>
    /// <returns>Avalonia の色。</returns>
    private static Color ToColor(uint argb)
        => Color.FromArgb(
            (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
}
