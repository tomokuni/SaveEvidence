using System.Diagnostics;
using app.Models;
using app.Services;
using app.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace app.Views.Pages;

/// <summary>
/// Undo / Redo スタックの画像履歴を一覧表示するページ。
/// </summary>
/// <remarks>
/// Undo / Redo スタック（<see cref="UndoStack"/>・DI シングルトン）と現在のプレビュー画像
/// （<see cref="MainViewModel.PreviewImage"/>・DI シングルトン）を
/// 時系列順（新しい → 古い）にサムネイル表示する。<br/>
/// 表示順は「Redo（新しい）→ 現在 → Undo（古い）」の固定。<br/>
/// 現在のキャプチャページに表示中の画像は青枠（IsCurrent）で強調する。<br/>
/// 表示モード（特大 / 大 / 中）は <see cref="Settings.UndoViewModeIndex"/> に保存・復元される。<br/>
/// サムネイルのダブルクリックで一時ファイルに保存し、OS の既定アプリで開く。
/// </remarks>
public sealed partial class UndoPage : UserControl
{
    private UndoViewModel _viewModel = null!;
    private SettingsService _settingsService = null!;

    /// <summary>Undo / Redo スタック（DI から取得するシングルトン。CapturePage と共有）。</summary>
    private UndoStack _undoStack = null!;

    /// <summary>現在のプレビュー画像の取得元（DI から取得するシングルトン）。</summary>
    private MainViewModel _mainViewModel = null!;

    /// <summary>UndoPage を初期化する。</summary>
    public UndoPage()
    {
        InitializeComponent();

        if (Design.IsDesignMode) return;

        _viewModel = Program.ServiceProvider.GetRequiredService<UndoViewModel>();
        _settingsService = Program.SettingsService;
        _undoStack = Program.ServiceProvider.GetRequiredService<UndoStack>();
        _mainViewModel = Program.ServiceProvider.GetRequiredService<MainViewModel>();
        DataContext = _viewModel;

        // 起動時（ページ初期化時）に保存済みの表示モードを復元する
        RestoreViewSettings();

        // コンストラクタでも初期Toggle状態を同期
        InitialSyncToggleStates();

        // ナビゲーションで再表示されるたびに最新の履歴を読み込む（FolderViewPage と同じパターン）
        PropertyChanged += OnIsVisiblePropertyChanged;
    }

    /// <summary>ページの表示状態変更時のハンドラ。表示されるたびに最新の履歴を読み込む。</summary>
    /// <remarks>
    /// Avalonia では <c>IsVisibleChanged</c> イベントが存在しないため、
    /// <see cref="Visual.PropertyChanged"/> で <see cref="Visual.IsVisibleProperty"/> の変更を監視する。
    /// </remarks>
    private void OnIsVisiblePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty && IsVisible)
        {
            Refresh();
        }
    }

    /// <summary>保存済みの表示モードを ViewModel へ復元する。</summary>
    /// <remarks>
    /// <see cref="Settings.UndoViewModeIndex"/> を
    /// <see cref="UndoViewModel.SelectedViewModeIndex"/> へ反映する。<br/>
    /// 値の範囲チェック（0〜2）を行い、不正値は既定値（大）にフォールバックする。
    /// </remarks>
    private void RestoreViewSettings()
    {
        var settings = _settingsService.Current;
        var modeIndex = settings.UndoViewModeIndex;
        _viewModel.SelectedViewModeIndex = modeIndex is >= 0 and <= 2 ? modeIndex : 1;
    }

    /// <summary>現在の表示モードを設定へ保存する。</summary>
    /// <remarks>表示モード切替のたびに呼び出し、設定ファイルへ反映する。</remarks>
    private void SaveViewSettings()
    {
        var settings = _settingsService.Current;
        settings.UndoViewModeIndex = _viewModel.SelectedViewModeIndex;
        _settingsService.Save();
    }

    /// <summary>初期表示時のToggle状態をDispatcher経由で同期する。</summary>
    /// <remarks>
    /// <see cref="Refresh"/> で <see cref="RestoreViewSettings"/> と同期が行われるため、
    /// 本メソッドはフォールバックとして動作する。
    /// </remarks>
    private void InitialSyncToggleStates()
    {
        if (!Design.IsDesignMode)
        {
            Dispatcher.UIThread.Post(() => SyncViewModeToggleStates(), DispatcherPriority.Loaded);
        }
    }

    /// <summary>Undo / Redo 履歴と現在の画像を読み込んで一覧を再構築する。</summary>
    /// <remarks>
    /// 表示順は時系列順で「新しい → 古い」の固定。<br/>
    /// 1. Redo スタック（現在より新しい状態。_redoStack[0] が現在に最も近い）<br/>
    /// 2. 現在の画像（青枠で強調）<br/>
    /// 3. Undo スタック（現在より古い状態。_stack[^1] が現在の直前）
    /// </remarks>
    public void Refresh()
    {
        RestoreViewSettings();
        SyncViewModeToggleStates();
        RebuildList();
    }

    /// <summary>一覧を再構築する（表示モード変更時・ページ表示時に呼び出し）。</summary>
    private void RebuildList()
    {
        if (_undoStack is null) return;

        var size = GetIconSize();
        var items = new List<UndoListItem>();
        void AddItem(Bitmap thumbnail, string label, bool isCurrent)
            => items.Add(new UndoListItem
            {
                Thumbnail = thumbnail,
                Label = label,
                IsCurrent = isCurrent,
                ThumbnailWidth = size
            });

        // 1. Redo スタック（新しい順。_redoStack[0] が現在に最も近い未来）
        var redo = _undoStack.GetRedoHistory();
        for (var i = 0; i < redo.Count; i++)
        {
            AddItem(redo[i], $"Redo {i + 1}", isCurrent: false);
        }

        // 2. 現在の画像（青枠で強調）
        var currentImage = _mainViewModel.PreviewImage;
        if (currentImage is not null)
        {
            AddItem(currentImage, "現在", isCurrent: true);
        }

        // 3. Undo スタック（新しい順。_stack[^1] が現在の直前）
        var undo = _undoStack.GetUndoHistory();
        for (var i = undo.Count - 1; i >= 0; i--)
        {
            AddItem(undo[i], $"Undo {undo.Count - i}", isCurrent: false);
        }

        _viewModel.ItemCount = items.Count;
        UndoListBox.ItemsSource = items;
    }

    /// <summary>表示モードに応じたアイコンサイズを取得する（フォルダビューと同一サイズを使用）。</summary>
    private int GetIconSize() => _settingsService.Current.GetIconSize(_viewModel.SelectedViewModeIndex);

    /// <summary>表示モードボタンの共通クリックハンドラ。対応する表示モードに切り替え、一覧を再構築する。</summary>
    private void OnViewModeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: string tag } && int.TryParse(tag, out var index))
        {
            _viewModel.SelectedViewModeIndex = index;
            SaveViewSettings();
            RebuildList();
            SyncViewModeToggleStates();
        }
    }

    /// <summary>表示モードの ToggleButton の IsChecked を SelectedViewModeIndex に合わせて同期する。</summary>
    /// <remarks>各ボタンは <c>x:Name</c> で個別参照する（Tag ベースの一括走査は行わない）。</remarks>
    private void SyncViewModeToggleStates()
    {
        ExtraLargeButton.IsChecked = _viewModel.SelectedViewModeIndex == 0;
        LargeButton.IsChecked = _viewModel.SelectedViewModeIndex == 1;
        MediumButton.IsChecked = _viewModel.SelectedViewModeIndex == 2;
    }

    /// <summary>
    /// サムネイルのダブルクリック時に一時ファイルへ保存し、OS の既定アプリ（外部ツール）で開く。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">タップイベント。</param>
    /// <remarks>
    /// Undo / Redo スタックの画像はファイルではなく Bitmap のため、
    /// 一時フォルダに PNG として保存してから <see cref="Process.Start"/> で開く。
    /// </remarks>
    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (UndoListBox.SelectedItem is not UndoListItem item) return;

        try
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"SaveEvidence_undo_{Guid.NewGuid():N}.png");
            item.Thumbnail.Save(tempFile, PngBitmapEncoderOptions.Default);

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = tempFile,
                UseShellExecute = true
            };
            process.Start();
        }
        catch (Exception)
        {
            // 外部ツールで開けなくてもユーザー操作を妨げない
        }
    }
}
