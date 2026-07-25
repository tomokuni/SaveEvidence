using app.Models;
using app.Services;
using app.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;

namespace app.Views.Pages;

/// <summary>
/// 保存先フォルダの画像一覧を表示するフォルダビューページ。
/// </summary>
/// <remarks>
/// 画像ファイルのサムネイル一覧を表示し、クリックでメイン画面のプレビューに読み込む。<br/>
/// 表示モード（特大/大/中）、ソート切替、更新機能を提供する。<br/>
/// 従来の FolderViewWindow を MainWindow 内のナビゲーションページとして移植したもの。<br/>
/// </remarks>
public sealed partial class FolderViewPage : UserControl
{
    private FolderViewViewModel _viewModel = null!;
    private MainViewModel _mainViewModel = null!;
    private SettingsService _settingsService = null!;

    /// <summary>FolderViewPage を初期化する。</summary>
    public FolderViewPage()
    {
        InitializeComponent();

        if (Avalonia.Controls.Design.IsDesignMode) return;

        _viewModel = Program.ServiceProvider.GetRequiredService<FolderViewViewModel>();
        _mainViewModel = Program.ServiceProvider.GetRequiredService<MainViewModel>();
        _settingsService = (SettingsService)Program.ServiceProvider.GetRequiredService<ISettingsService>();
        DataContext = _viewModel;

        // 設定変更時に市松模様背景を更新
        SubscribeCheckerSizeChange();

        Loaded += OnLoaded;

        // コンストラクタでも初期Toggle状態を同期
        InitialSyncToggleStates();
    }

    /// <summary>初期表示時のToggle状態をDispatcher経由で同期する。</summary>
    private void InitialSyncToggleStates()
    {
        if (!Avalonia.Controls.Design.IsDesignMode)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (this.IsLoaded)
                {
                    SyncViewModeToggleStates();
                    SyncSortToggleStates();
                }
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }

    /// <summary>ページ表示時にファイル一覧を読み込み、市松模様背景を更新する。</summary>
    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LoadFiles();
        SyncViewModeToggleStates();
        SyncSortToggleStates();
        UpdateFolderCheckerboardBackground();
    }

    /// <summary>ファイル一覧を読み込む。</summary>
    private void LoadFiles()
    {
        var folderPath = _mainViewModel.SaveFolderPath;
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
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tiff" or ".tif" or ".webp";
    }

    /// <summary>画像ファイルのサムネイルを読み込む。</summary>
    private static Bitmap? LoadThumbnail(string filePath, int size)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return Bitmap.DecodeToWidth(stream, size);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>表示モードに応じたアイコンサイズを取得する。</summary>
    private int GetIconSize() => _viewModel.SelectedViewModeIndex switch
    {
        0 => _settingsService.Current.FolderExtraLargeIconSize,
        1 => _settingsService.Current.FolderLargeIconSize,
        _ => _settingsService.Current.FolderMediumIconSize,
    };

    /// <summary>表示モードボタンの共通クリックハンドラ。対応する表示モードに切り替え、ファイルを再読込する。</summary>
    private void OnViewModeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Avalonia.Controls.Primitives.ToggleButton { Tag: string tag } && int.TryParse(tag, out var index))
        {
            _viewModel.SelectedViewModeIndex = index;
            LoadFiles();
            SyncViewModeToggleStates();
        }
    }

    /// <summary>表示モードの ToggleButton の IsChecked を SelectedViewModeIndex に合わせて同期する。</summary>
    private void SyncViewModeToggleStates()
    {
        var currentIndex = _viewModel.SelectedViewModeIndex;
        foreach (var child in ToolbarPanel.Children)
        {
            if (child is Avalonia.Controls.Primitives.ToggleButton tb
                && tb.Tag is string tag
                && int.TryParse(tag, out var btnIndex))
            {
                tb.IsChecked = btnIndex == currentIndex;
            }
        }
    }

    /// <summary>昇順ボタンクリック。</summary>
    private void OnSortAscendingClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.IsSortAscending = true;
        LoadFiles();
        SyncSortToggleStates();
    }

    /// <summary>降順ボタンクリック。</summary>
    private void OnSortDescendingClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.IsSortAscending = false;
        LoadFiles();
        SyncSortToggleStates();
    }

    /// <summary>ソート順の ToggleButton の IsChecked を IsSortAscending に合わせて同期する。</summary>
    private void SyncSortToggleStates()
    {
        var isAsc = _viewModel.IsSortAscending;
        foreach (var child in ToolbarPanel.Children)
        {
            if (child is Avalonia.Controls.Primitives.ToggleButton tb
                && tb.Tag is string tag)
            {
                tb.IsChecked = tag == "asc" ? isAsc : tag == "desc" ? !isAsc : null;
            }
        }
    }

    /// <summary>最新情報に更新。</summary>
    private void OnRefreshClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => LoadFiles();

    /// <summary>
    /// ファイルのダブルクリック時に規定のアプリで開く。
    /// </summary>
    private void OnFileDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (FileListBox.SelectedItem is not FileListItem item) return;
        if (string.IsNullOrEmpty(item.FilePath)) return;

        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
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

    /// <summary>設定の市松模様サイズ変更を購読する。</summary>
    private void SubscribeCheckerSizeChange()
    {
        _settingsService.Current.PropertyChanged += OnCheckerSizePropertyChanged;
    }

    /// <summary>設定の市松模様サイズ変更を監視する。</summary>
    private void OnCheckerSizePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings.PreviewCheckerSize))
        {
            UpdateFolderCheckerboardBackground();
        }
    }

    /// <summary>フォルダビューのListBox背景に市松模様を設定する。</summary>
    /// <remarks>
    /// 薄いグレー（#CCCCCC）とより薄いグレー（#E8E8E8）のタイルを
    /// <see cref="Settings.PreviewCheckerSize"/> の矩形サイズで描画する。<br/>
    /// MainWindow のプレビュー市松模様と同じ色・サイズ設定を使用する。<br/>
    /// </remarks>
    private void UpdateFolderCheckerboardBackground()
    {
        try
        {
            var checkerSize = _settingsService.Current.PreviewCheckerSize;
            if (checkerSize < 1) checkerSize = 2;

            var lightColor = Color.FromArgb(255, 204, 204, 204);       // #CCCCCC 薄いグレー
            var lighterColor = Color.FromArgb(255, 232, 232, 232);     // #E8E8E8 より薄いグレー

            var lightBrush = new SolidColorBrush(lightColor);
            var lighterBrush = new SolidColorBrush(lighterColor);

            var drawingGroup = new DrawingGroup();
            // ベース全体をより薄いグレーで塗りつぶし
            drawingGroup.Children.Add(new GeometryDrawing
            {
                Brush = lighterBrush,
                Geometry = new RectangleGeometry(new Rect(0, 0, checkerSize * 2, checkerSize * 2))
            });
            // 左上のタイルを薄いグレーで上書き
            drawingGroup.Children.Add(new GeometryDrawing
            {
                Brush = lightBrush,
                Geometry = new RectangleGeometry(new Rect(0, 0, checkerSize, checkerSize))
            });
            // 右下のタイルを薄いグレーで上書き
            drawingGroup.Children.Add(new GeometryDrawing
            {
                Brush = lightBrush,
                Geometry = new RectangleGeometry(new Rect(checkerSize, checkerSize, checkerSize, checkerSize))
            });

            FileListBox.Background = new DrawingBrush
            {
                Drawing = drawingGroup,
                TileMode = TileMode.Tile,
                DestinationRect = new RelativeRect(
                    0, 0, checkerSize * 2, checkerSize * 2, RelativeUnit.Absolute)
            };
        }
        catch
        {
            // 背景設定の失敗は無視（UI に影響しない）
        }
    }

}

/// <summary>
/// ファイル一覧表示用のアイテム。
/// </summary>
public sealed class FileListItem
{
    /// <summary>ファイルの完全パス。</summary>
    public required string FilePath { get; init; }

    /// <summary>ファイル名（表示用）。</summary>
    public required string FileName { get; init; }

    /// <summary>サムネイル画像。</summary>
    public Bitmap? Thumbnail { get; init; }

    /// <summary>表示幅（アイコンサイズから設定）。</summary>
    public int ThumbnailWidth { get; init; }
}
