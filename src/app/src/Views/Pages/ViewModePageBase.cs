using app.Services;
using app.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace app.Views.Pages;

/// <summary>
/// 表示モード（特大 / 大 / 中）を持つ一覧ページの共通基盤。
/// </summary>
/// <remarks>
/// 表示モードの設定復元・保存、ToggleButton 同期、ページ表示時の一覧再構築を共通化する。<br/>
/// 派生ページ（<see cref="FolderViewPage"/> / <see cref="UndoPage"/>）は一覧の再構築（<see cref="ReloadItems"/>）と
/// 表示モードの入出力（<see cref="SelectedViewModeIndex"/>）、設定の復元・保存のみを実装する。
/// </remarks>
public abstract class ViewModePageBase : UserControl
{
    private ToggleButton _extraLargeButton = null!;

    private ToggleButton _largeButton = null!;

    private ToggleButton _mediumButton = null!;

    /// <summary>設定サービス。</summary>
    protected SettingsService SettingsService { get; private set; } = null!;

    /// <summary>メイン ViewModel。</summary>
    protected MainViewModel MainViewModel { get; private set; } = null!;

    /// <summary>現在の表示モードのインデックス（0=特大, 1=大, 2=中）。</summary>
    protected abstract int SelectedViewModeIndex { get; set; }

    /// <summary>表示モード変更・ページ表示時に一覧を再構築する。</summary>
    protected abstract void ReloadItems();

    /// <summary>設定から表示モードを復元して ViewModel へ反映する。</summary>
    protected abstract void RestoreViewModeSettings();

    /// <summary>現在の表示モードを設定へ保存する。</summary>
    protected abstract void SaveViewModeSettings();

    /// <summary>共通初期化（派生ページのコンストラクタで InitializeComponent 後に呼ぶ）。</summary>
    /// <param name="settingsService">設定サービス（DI から取得）。</param>
    /// <param name="mainViewModel">メイン ViewModel（DI から取得）。</param>
    /// <param name="extraLargeButton">特大モードの ToggleButton。</param>
    /// <param name="largeButton">大モードの ToggleButton。</param>
    /// <param name="mediumButton">中モードの ToggleButton。</param>
    protected void InitializeViewModePage(
        SettingsService settingsService, MainViewModel mainViewModel,
        ToggleButton extraLargeButton, ToggleButton largeButton, ToggleButton mediumButton)
    {
        SettingsService = settingsService;
        MainViewModel = mainViewModel;
        _extraLargeButton = extraLargeButton;
        _largeButton = largeButton;
        _mediumButton = mediumButton;

        RestoreViewModeSettings();

        // ナビゲーションで表示されるたびに保存済み設定を復元して一覧を更新する
        // （Avalonia には IsVisibleChanged イベントがないため PropertyChanged を監視する）
        PropertyChanged += OnIsVisiblePropertyChanged;
    }

    /// <summary>ページの表示状態変更時のハンドラ。表示されるたびに設定を復元して一覧を更新する。</summary>
    private void OnIsVisiblePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty && IsVisible)
        {
            RestoreViewModeSettings();
            SyncViewModeToggleStates();
            ReloadItems();
        }
    }

    /// <summary>表示モードボタンの共通クリックハンドラ。対応する表示モードに切り替え、一覧を再構築する。</summary>
    protected void OnViewModeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: string tag } && int.TryParse(tag, out var index))
        {
            SelectedViewModeIndex = index;
            SaveViewModeSettings();
            ReloadItems();
            SyncViewModeToggleStates();
        }
    }

    /// <summary>表示モードの ToggleButton の IsChecked を SelectedViewModeIndex に合わせて同期する。</summary>
    /// <remarks>各ボタンは <c>x:Name</c> で個別参照する（Tag ベースの一括走査は行わない）。</remarks>
    protected void SyncViewModeToggleStates()
    {
        _extraLargeButton.IsChecked = SelectedViewModeIndex == 0;
        _largeButton.IsChecked = SelectedViewModeIndex == 1;
        _mediumButton.IsChecked = SelectedViewModeIndex == 2;
    }

    /// <summary>表示モードに応じたアイコンサイズを取得する。</summary>
    protected int GetIconSize() => SettingsService.Current.GetIconSize(SelectedViewModeIndex);
}
