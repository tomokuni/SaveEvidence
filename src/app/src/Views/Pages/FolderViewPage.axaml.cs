using System.Collections.Concurrent;
using System.Diagnostics;
using app.Models;
using app.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;

namespace app.Views.Pages;

/// <summary>
/// 保存先フォルダの画像一覧を表示するフォルダビューページ。
/// </summary>
/// <remarks>
/// 画像ファイルのサムネイル一覧を表示し、クリックでメイン画面のプレビューに読み込む。<br/>
/// 表示モード（特大/大/中）、ソート切替、更新機能を提供する。<br/>
/// 表示モードの復元・保存、ToggleButton 同期、ページ表示時の再構築は <see cref="ViewModePageBase"/> に共通化する。
/// </remarks>
public sealed partial class FolderViewPage : ViewModePageBase
{
    private FolderViewViewModel _viewModel = null!;

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
        DataContext = _viewModel;

        InitializeViewModePage(
            Program.SettingsService,
            Program.ServiceProvider.GetRequiredService<MainViewModel>(),
            ExtraLargeButton, LargeButton, MediumButton);
    }

    /// <summary>現在の表示モードのインデックス（0=特大, 1=大, 2=中）。</summary>
    protected override int SelectedViewModeIndex
    {
        get => _viewModel.SelectedViewModeIndex;
        set => _viewModel.SelectedViewModeIndex = value;
    }

    /// <summary>保存済みの表示モード・ソート順を ViewModel へ復元し、ソート Toggle に反映する。</summary>
    /// <remarks>
    /// <see cref="Settings.FolderViewModeIndex"/> と <see cref="Settings.FolderViewSortAscending"/> を
    /// <see cref="FolderViewViewModel.SelectedViewModeIndex"/> / <see cref="FolderViewViewModel.IsSortAscending"/> へ反映する。<br/>
    /// 値の範囲チェックは <see cref="ViewModeDisplay.ClampIndex"/> に委譲する。
    /// </remarks>
    protected override void RestoreViewModeSettings()
    {
        var settings = SettingsService.Current;
        _viewModel.SelectedViewModeIndex = ViewModeDisplay.ClampIndex(settings.FolderViewModeIndex);
        _viewModel.IsSortAscending = settings.FolderViewSortAscending;
        SyncSortToggleStates();
    }

    /// <summary>現在の表示モード・ソート順を設定へ保存する。</summary>
    /// <remarks>フォルダビューの操作（表示モード切替・ソート切替）のたびに呼び出し、設定ファイルへ反映する。</remarks>
    protected override void SaveViewModeSettings()
    {
        var settings = SettingsService.Current;
        settings.FolderViewModeIndex = _viewModel.SelectedViewModeIndex;
        settings.FolderViewSortAscending = _viewModel.IsSortAscending;
        SettingsService.Save();
    }

    /// <summary>ファイル一覧を再読み込みする。</summary>
    protected override void ReloadItems() => LoadFiles();

    /// <summary>ファイル一覧を読み込む。</summary>
    private void LoadFiles()
    {
        var folderPath = MainViewModel.SaveFolderPathResolved;
        _viewModel.FolderPathText = folderPath;
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            _viewModel.ItemCount = 0;
            FileListBox.ItemsSource = null;
            return;
        }

        var files = Directory.GetFiles(folderPath, "*.*")
            .Where(static f => IsImageFile(f))
            .ToList();

        // ソート順を適用（比較子を選択。名前順・大文字小文字を区別しない）
        files.Sort(_viewModel.IsSortAscending
            ? StringComparer.OrdinalIgnoreCase
            : Comparer<string>.Create((a, b) => StringComparer.OrdinalIgnoreCase.Compare(b, a)));

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

    /// <summary>昇順ボタンクリック。</summary>
    private void OnSortAscendingClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.IsSortAscending = true;
        SaveViewModeSettings();
        LoadFiles();
        SyncSortToggleStates();
    }

    /// <summary>降順ボタンクリック。</summary>
    private void OnSortDescendingClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.IsSortAscending = false;
        SaveViewModeSettings();
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
