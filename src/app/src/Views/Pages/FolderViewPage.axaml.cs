using System.Collections.Concurrent;
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
/// 保存先フォルダの画像一覧を表示するフォルダビューページ。
/// </summary>
/// <remarks>
/// 画像ファイルのサムネイル一覧を表示し、クリックでメイン画面のプレビューに読み込む。<br/>
/// 表示モード（特大/大/中）、ソート切替、更新機能を提供する。<br/>
/// </remarks>
public sealed partial class FolderViewPage : UserControl
{
    private FolderViewViewModel _viewModel = null!;
    private MainViewModel _mainViewModel = null!;
    private SettingsService _settingsService = null!;

    /// <summary>サムネイルキャッシュの最大保持数。</summary>
    private const int ThumbnailCacheCapacity = 100;

    private static readonly ConcurrentDictionary<(string Path, int Size, DateTime LastWrite), Bitmap> s_thumbnailCache = new();

    private static readonly HashSet<string> s_imageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif", ".webp"
    };

    /// <summary>FolderViewPage を初期化する。</summary>
    public FolderViewPage()
    {
        InitializeComponent();

        if (Design.IsDesignMode) return;

        _viewModel = Program.ServiceProvider.GetRequiredService<FolderViewViewModel>();
        _mainViewModel = Program.ServiceProvider.GetRequiredService<MainViewModel>();
        _settingsService = (SettingsService)Program.ServiceProvider.GetRequiredService<ISettingsService>();
        DataContext = _viewModel;

        // 起動時（ページ初期化時）に保存済みの表示モード・ソート順を復元する
        RestoreViewSettings();

        Loaded += OnLoaded;

        // ナビゲーションでフォルダビューが表示されるたびに、保存済み設定を復元して Toggle 状態へ反映する
        PropertyChanged += OnIsVisiblePropertyChanged;

        // コンストラクタでも初期Toggle状態を同期
        InitialSyncToggleStates();
    }

    /// <summary>保存済みの表示モード・ソート順を ViewModel へ復元する。</summary>
    /// <remarks>
    /// <see cref="Settings.FolderViewModeIndex"/> と <see cref="Settings.FolderViewSortAscending"/> を
    /// <see cref="FolderViewViewModel.SelectedViewModeIndex"/> / <see cref="FolderViewViewModel.IsSortAscending"/> へ反映する。<br/>
    /// 値の範囲チェック（0〜2）を行い、不正値は既定値（大）にフォールバックする。
    /// </remarks>
    private void RestoreViewSettings()
    {
        var settings = _settingsService.Current;
        var modeIndex = settings.FolderViewModeIndex;
        _viewModel.SelectedViewModeIndex = modeIndex is >= 0 and <= 2 ? modeIndex : 1;
        _viewModel.IsSortAscending = settings.FolderViewSortAscending;
    }

    /// <summary>現在の表示モード・ソート順を設定へ保存する。</summary>
    /// <remarks>フォルダビューの操作（表示モード切替・ソート切替）のたびに呼び出し、設定ファイルへ反映する。</remarks>
    private void SaveViewSettings()
    {
        var settings = _settingsService.Current;
        settings.FolderViewModeIndex = _viewModel.SelectedViewModeIndex;
        settings.FolderViewSortAscending = _viewModel.IsSortAscending;
        _settingsService.Save();
    }

    /// <summary>初期表示時のToggle状態をDispatcher経由で同期する。</summary>
    /// <remarks>
    /// <see cref="OnLoaded"/> で <see cref="RestoreViewSettings"/> と同期が行われるため、
    /// 本メソッドはフォールバックとして動作する（IsLoaded 判定は行わない。Loaded 前に呼ばれても
    /// ToggleButton の IsChecked は設定可能なため）。
    /// </remarks>
    private void InitialSyncToggleStates()
    {
        if (!Design.IsDesignMode)
        {
            Dispatcher.UIThread.Post(() =>
            {
                SyncViewModeToggleStates();
                SyncSortToggleStates();
            }, DispatcherPriority.Loaded);
        }
    }

    /// <summary>ページ表示時にファイル一覧を読み込み、保存済みの表示モード・ソート順を復元して同期する。</summary>
    /// <remarks>
    /// <see cref="RestoreViewSettings"/> を実行してから <see cref="SyncViewModeToggleStates"/> を呼ぶことで、
    /// 起動時に復元された表示モードが ToggleButton の選択状態（チェック）へ確実に反映される。<br/>
    /// LoadFiles は復元後の <see cref="FolderViewViewModel.SelectedViewModeIndex"/> に基づいて
    /// アイコンサイズを決定する（復元 → 読込の順で行う）。<br/>
    /// <c>Loaded</c> は visual tree 接続時に一度だけ発火するため、ナビゲーションで再表示されるたびに
    /// 復元・同期するよう <see cref="OnIsVisibleChanged"/> でも同様の処理を行う。
    /// </remarks>
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        RestoreViewSettings();
        LoadFiles();
        SyncViewModeToggleStates();
        SyncSortToggleStates();
    }

    /// <summary>ページの表示状態変更時のハンドラ。表示されるたびに保存済み設定を復元して同期する。</summary>
    /// <remarks>
    /// ナビゲーションでフォルダビューへ切り替わったとき（IsVisible = true）に、
    /// 保存済みの表示モード・ソート順を復元し、ToggleButton の選択状態へ反映する。<br/>
    /// Avalonia では <c>IsVisibleChanged</c> イベントが存在しないため、
    /// <see cref="Visual.PropertyChanged"/> で <see cref="Visual.IsVisibleProperty"/> の変更を監視する。
    /// </remarks>
    private void OnIsVisiblePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty && IsVisible)
        {
            RestoreViewSettings();
            SyncViewModeToggleStates();
            SyncSortToggleStates();
        }
    }

    /// <summary>ファイル一覧を読み込む。</summary>
    private void LoadFiles()
    {
        var folderPath = _mainViewModel.SaveFolderPathResolved;
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            _viewModel.ItemCount = 0;
            _viewModel.FolderPathText = folderPath;
            FileListBox.ItemsSource = null;
            return;
        }

        _viewModel.FolderPathText = folderPath;

        var files = Directory.GetFiles(folderPath, "*.*")
            .Where(static f => IsImageFile(f))
            .ToList();

        // ソート順を適用
        if (_viewModel.IsSortAscending)
        {
            files.Sort(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            files.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(b, a));
        }

        var iconSize = GetIconSize();
        var items = files.Select(f => new FileListItem
        {
            FilePath = f,
            FileName = Path.GetFileName(f),
            Thumbnail = LoadThumbnail(f, iconSize),
            ThumbnailWidth = iconSize
        }).ToList();

        _viewModel.ItemCount = items.Count;
        FileListBox.ItemsSource = items;
    }

    /// <summary>指定されたファイルパスが画像ファイルかどうかを判定する。</summary>
    private static bool IsImageFile(string path)
        => s_imageExtensions.Contains(Path.GetExtension(path));

    /// <summary>画像ファイルのサムネイルを読み込む。</summary>
    private static Bitmap? LoadThumbnail(string filePath, int size)
    {
        try
        {
            var lastWrite = File.GetLastWriteTimeUtc(filePath);
            (string Path, int Size, DateTime LastWrite) key = (filePath, size, lastWrite);
            return CapacityLimitedCache.GetOrAdd(
                s_thumbnailCache, ThumbnailCacheCapacity, key, static k =>
                {
                    using var stream = File.OpenRead(k.Path);
                    return Bitmap.DecodeToWidth(stream, k.Size);
                });
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>表示モードに応じたアイコンサイズを取得する。</summary>
    private int GetIconSize() => _settingsService.Current.GetIconSize(_viewModel.SelectedViewModeIndex);

    /// <summary>表示モードボタンの共通クリックハンドラ。対応する表示モードに切り替え、ファイルを再読込する。</summary>
    private void OnViewModeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: string tag } && int.TryParse(tag, out var index))
        {
            _viewModel.SelectedViewModeIndex = index;
            SaveViewSettings();
            LoadFiles();
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

    /// <summary>昇順ボタンクリック。</summary>
    private void OnSortAscendingClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.IsSortAscending = true;
        SaveViewSettings();
        LoadFiles();
        SyncSortToggleStates();
    }

    /// <summary>降順ボタンクリック。</summary>
    private void OnSortDescendingClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.IsSortAscending = false;
        SaveViewSettings();
        LoadFiles();
        SyncSortToggleStates();
    }

    /// <summary>ソート順の ToggleButton の IsChecked を IsSortAscending に合わせて同期する。</summary>
    /// <remarks>各ボタンは <c>x:Name</c> で個別参照する（Tag ベースの一括走査は行わない）。</remarks>
    private void SyncSortToggleStates()
    {
        SortAscendingButton.IsChecked = _viewModel.IsSortAscending;
        SortDescendingButton.IsChecked = !_viewModel.IsSortAscending;
    }

    /// <summary>最新情報に更新。</summary>
    private void OnRefreshClick(object? sender, RoutedEventArgs e) => LoadFiles();

    /// <summary>
    /// ファイルのダブルクリック時に規定のアプリで開く。
    /// </summary>
    private void OnFileDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (FileListBox.SelectedItem is not FileListItem item) return;
        if (string.IsNullOrEmpty(item.FilePath)) return;

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = item.FilePath,
                UseShellExecute = true
            };
            process.Start();
        }
        catch (Exception)
        {
            // ファイルを開けなくてもユーザー操作を妨げない
        }
    }
}
