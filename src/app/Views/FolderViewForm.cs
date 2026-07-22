using System.Runtime.InteropServices;
using app.Models;
using app.Services;
using app.ViewModels;

namespace app.Views;

/// <summary>
/// 保存先フォルダの内容を表示するモードレスダイアログ。
/// </summary>
/// <remarks>
/// 画像ファイルは実際のサムネイルを非同期的に生成して表示する。<br/>
/// ダブルクリックで既定のアプリケーションでファイルを開く。<br/>
/// 表示モード（特大/大/中/一覧/詳細）とソート順は <see cref="Services.SettingsService"/> を介して
/// 設定ファイルに保存・復元される。<br/>
/// シェルアイコンとサムネイルの2段階表示により、大量ファイル時でも素早く一覧を表示する。<br/>
/// </remarks>
public sealed partial class FolderViewForm : Form
{
    private readonly FolderViewViewModel _viewModel;
    private readonly string _folderPath;
    private readonly Settings _settings;
    private readonly SettingsService _settingsService;
    private bool _sortAscending = true;
    private readonly List<ListViewItem> _items = [];

    private readonly ImageList _largeIconList = null!;   // 特大アイコン用（サイズは設定から）
    private readonly ImageList _tileIconList = null!;     // 大アイコン用（サイズは設定から）
    private readonly ImageList _mediumIconList = null!;   // 中アイコン用（サイズは設定から）
    private readonly ImageList _smallIconList = null!;    // 16x16 (一覧/詳細用)

    private static readonly HashSet<string> s_imageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif", ".webp"];

    private CancellationTokenSource? _loadCts;

    private readonly bool _isAsyncLoadEnabled = false; // 非同期読み込みを有効にするかどうか（テスト用）

    private ContextMenuStrip _contextMenuFolder = null!;
    private ToolStripMenuItem _ctxOpenFolder = null!;
    private ToolStripMenuItem _ctxCopyFolderPath = null!;

    /// <summary>
    /// FolderViewForm を初期化する
    /// </summary>
    /// <param name="folderPath">表示するフォルダパス</param>
    /// <param name="settingsService">設定サービス</param>
    public FolderViewForm(string folderPath, SettingsService settingsService)
    {
        _folderPath = folderPath;
        _settingsService = settingsService;
        _settings = settingsService.Current;
        InitializeComponent();

        // 設定からウィンドウ位置・サイズを復元
        RestoreFormBounds();

        _largeIconList = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(_settings.FolderExtraLargeIconSize, _settings.FolderExtraLargeIconSize) };
        _tileIconList = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(_settings.FolderLargeIconSize, _settings.FolderLargeIconSize) };
        _mediumIconList = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(_settings.FolderMediumIconSize, _settings.FolderMediumIconSize) };
        _smallIconList = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(16, 16) };
        _listView.LargeImageList = _tileIconList;
        _listView.SmallImageList = _smallIconList;

        // ─── ViewModel の初期化 ──
        _viewModel = new FolderViewViewModel();

        var viewModeIndex = _settings.FolderViewModeIndex;
        if (viewModeIndex < 0 || viewModeIndex > 4)
            viewModeIndex = 1;

        _sortAscending = _settings.FolderSortAscending;

        // 初期値を設定（ViewModel プロパティ → ステータスバー・コンテキストメニュー用）
        _viewModel.SelectedViewModeIndex = viewModeIndex;
        _viewModel.IsSortAscending = _sortAscending;
        _viewModel.FolderPathText = _folderPath;
        UpdateViewModeText();
        UpdateSortStatusText();
        UpdateItemCountText();

        // DataBindings
        _lblStatus.DataBindings.Add("Text", _viewModel, nameof(FolderViewViewModel.ItemCountText));
        _lblFolderPath.DataBindings.Add("Text", _viewModel, nameof(FolderViewViewModel.FolderPathText));
        _lblViewMode.DataBindings.Add("Text", _viewModel, nameof(FolderViewViewModel.ViewModeText));
        _lblSortStatus.DataBindings.Add("Text", _viewModel, nameof(FolderViewViewModel.SortStatusText));

        // コンテキストメニュー Opening イベント（ViewModel の状態から Checked を設定）
        _contextMenuView.Opening += ContextMenuView_Opening;
        _contextMenuSort.Opening += ContextMenuSort_Opening;
        // メニューバーの表示メニュー DropDownOpening イベント
        _menuView.DropDownOpening += MenuView_DropDownOpening;

        // ─── フォルダパスリンク用コンテキストメニュー ──
        _contextMenuFolder = new ContextMenuStrip(components);
        _ctxOpenFolder = new ToolStripMenuItem("エクスプローラでフォルダを開く");
        _ctxOpenFolder.Click += CtxOpenFolder_Click;
        _contextMenuFolder.Items.Add(_ctxOpenFolder);
        _ctxCopyFolderPath = new ToolStripMenuItem("パスをコピー");
        _ctxCopyFolderPath.Click += CtxCopyFolderPath_Click;
        _contextMenuFolder.Items.Add(_ctxCopyFolderPath);
        _contextMenuFolder.Name = "_contextMenuFolder";
        // 右クリックでコンテキストメニューを表示
        _lblFolderPath.MouseUp += LblFolderPath_MouseUp;

        if (Directory.Exists(_folderPath))
        {
            LoadFolderContents();
        }

        Text = $"保存先フォルダビュー - {_folderPath}";

        // フルパスを ToolTip で表示
        _lblFolderPath.ToolTipText = $"イメージの保存先: {_folderPath}";

        // 各ステータス項目の ToolTip 初期設定
        _lblStatus.ToolTipText = $"アイテム数: {_items.Count}個";
        _lblViewMode.ToolTipText = $"表示: {_viewModel.ViewModeText}";
        _lblSortStatus.ToolTipText = $"並替: {_viewModel.SortStatusText}";

        // ViewModel のプロパティ変更時に ToolTipText を更新
        _viewModel.PropertyChanged += (s, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(FolderViewViewModel.ItemCountText):
                    _lblStatus.ToolTipText = $"アイテム数: {_viewModel.ItemCountText}";
                    break;
                case nameof(FolderViewViewModel.ViewModeText):
                    _lblViewMode.ToolTipText = $"表示: {_viewModel.ViewModeText}";
                    break;
                case nameof(FolderViewViewModel.SortStatusText):
                    _lblSortStatus.ToolTipText = $"並替: {_viewModel.SortStatusText}";
                    break;
                case nameof(FolderViewViewModel.FolderPathText):
                    _lblFolderPath.ToolTipText = $"イメージの保存先: {_viewModel.FolderPathText}";
                    break;
            }
        };

        // StatusStrip の ToolStripItem は ToolTipText を直接表示できないため
        // MouseMove で項目を特定して ToolTip を表示する
        _statusStrip.MouseMove += (_, e) =>
        {
            var item = _statusStrip.GetItemAt(e.Location);
            _toolTip.SetToolTip(_statusStrip, (item as ToolStripStatusLabel)?.ToolTipText ?? "");
        };

        UpdateStatusBar();
        _statusStrip.PerformLayout();
    }

    private void LoadFolderContents()
    {
        // 前回の非同期読み込みをキャンセル
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        var imageFiles = new List<(string Path, ListViewItem Item)>();

        _listView.BeginUpdate();
        _listView.Items.Clear();
        _items.Clear();
        _listView.LabelWrap = false;

        try
        {
            // シェルアイコンキャッシュをクリア
            _largeIconList.Images.Clear();
            _tileIconList.Images.Clear();
            _mediumIconList.Images.Clear();
            _smallIconList.Images.Clear();

            // ImageList ハンドルを強制作成（非同期追加時の NullReferenceException 防止）
            _ = _largeIconList.Handle;
            _ = _tileIconList.Handle;
            _ = _mediumIconList.Handle;
            _ = _smallIconList.Handle;

            var iconCache = new Dictionary<string, (int Large, int Tile, int Medium, int Small)>();

            try
            {
                foreach (var dir in Directory.GetDirectories(_folderPath))
                {
                    var dirName = Path.GetFileName(dir);
                    var icons = GetIconIndex(dir, true, iconCache).Medium;
                    var item = new ListViewItem(dirName, icons) { Tag = dir };
                    item.SubItems.Add("");
                    item.SubItems.Add(new DirectoryInfo(dir).LastWriteTime.ToString("yyyy/MM/dd HH:mm"));
                    _items.Add(item);
                }
            }
            catch (UnauthorizedAccessException) { }

            try
            {
                foreach (var file in Directory.GetFiles(_folderPath))
                {
                    var fileName = Path.GetFileName(file);
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    var fileInfo = new FileInfo(file);

                    // 画像ファイルはシェルアイコンで仮表示し、後で非同期でサムネイルに差し替え
                    var icons = GetIconIndex(file, false, iconCache).Medium;
                    var item = new ListViewItem(fileName, icons) { Tag = file };
                    item.SubItems.Add(FormatFileSize(fileInfo.Length));
                    item.SubItems.Add(fileInfo.LastWriteTime.ToString("yyyy/MM/dd HH:mm"));
                    _items.Add(item);

                    if (s_imageExtensions.Contains(ext))
                    {
                        imageFiles.Add((file, item));
                    }
                }
            }
            catch (UnauthorizedAccessException) { }

            SortAndAddItems();
        }
        finally
        {
            _listView.EndUpdate();
        }

        UpdateStatusBar();

        // 画像サムネイルを読み込む（非同期/同期は _isAsyncLoadEnabled で切り替え）
        // ソート順に従い、左上に表示されているものから優先的に処理する
        if (imageFiles.Count > 0)
        {
            // ListView の表示順に imageFiles を並び替え
            var sortedFiles = imageFiles
                .OrderBy(f => _listView.Items.IndexOf(f.Item))
                .ToList();

            if (_isAsyncLoadEnabled)
            {
                // 非同期で画像を読み込む
                Task.Run(() => LoadThumbnailsAsync(sortedFiles, ct), ct);
            }
            else
            {
                // 同期で画像を読み込む（テスト用）
                LoadThumbnailsSync(sortedFiles, ct);
            }
        }
    }

    /// <summary>
    /// 画像サムネイルを非同期で読み込む
    /// </summary>
    /// <param name="imageFiles">読み込む画像ファイルとListViewItemのリスト</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <remarks>
    /// バックグラウンドスレッドで画像を読み込み、サムネイルを生成してUIスレッドで表示する。<br/>
    /// ソースサイズは表示サイズ×2で読み込み、高品質ダウンスケールを実現する。<br/>
    /// </remarks>
    private async Task LoadThumbnailsAsync(List<(string Path, ListViewItem Item)> imageFiles, CancellationToken ct)
    {
        foreach (var (filePath, item) in imageFiles)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // バックグラウンドでサムネイルを生成
                // ソースサイズは表示サイズ×2（高品質ダウンスケール用）
                using var original = await Task.Run(() => Image.FromFile(filePath), ct);
                var largeSrc = Math.Max(_settings.FolderExtraLargeIconSize * 2, 256);
                var tileSrc = Math.Max(_settings.FolderLargeIconSize * 2, 192);
                var mediumSrc = Math.Max(_settings.FolderMediumIconSize * 2, 128);
                var large = ResizeImage(original, largeSrc, largeSrc);
                var tile = ResizeImage(original, tileSrc, tileSrc);
                var medium = ResizeImage(original, mediumSrc, mediumSrc);
                var small = ResizeImage(original, 16, 16);

                // UIスレッドに戻してアイコンを差し替え
                try
                {
                    BeginInvoke(() =>
                    {
                        if (IsDisposed || ct.IsCancellationRequested)
                        {
                            large.Dispose(); tile.Dispose(); medium.Dispose(); small.Dispose();
                            return;
                        }

                        var largeIdx = _largeIconList.Images.Count;
                        _largeIconList.Images.Add(large);
                        var tileIdx = _tileIconList.Images.Count;
                        _tileIconList.Images.Add(tile);
                        var mediumIdx = _mediumIconList.Images.Count;
                        _mediumIconList.Images.Add(medium);
                        var smallIdx = _smallIconList.Images.Count;
                        _smallIconList.Images.Add(small);

                        item.ImageIndex = mediumIdx;
                    });
                }
                catch (ObjectDisposedException)
                {
                    large.Dispose(); tile.Dispose(); medium.Dispose(); small.Dispose();
                }
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // 読み込み失敗は無視（シェルアイコンのまま）
            }
        }
    }

    /// <summary>
    /// 画像サムネイルを同期で読み込む（テスト用）
    /// </summary>
    /// <param name="imageFiles">読み込む画像ファイルとListViewItemのリスト</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <remarks>
    /// UIスレッドで同期的に画像を読み込み、サムネイルを生成する。<br/>
    /// テストやデバッグ時に非同期処理の複雑さを排除するために使用する。<br/>
    /// ソースサイズは表示サイズ×2で読み込み、高品質ダウンスケールを実現する。<br/>
    /// </remarks>
    private void LoadThumbnailsSync(List<(string Path, ListViewItem Item)> imageFiles, CancellationToken ct)
    {
        foreach (var (filePath, item) in imageFiles)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // 同期でサムネイルを生成
                // ソースサイズは表示サイズ×2（高品質ダウンスケール用）
                using var original = Image.FromFile(filePath);
                var largeSrc = Math.Max(_settings.FolderExtraLargeIconSize * 2, 256);
                var tileSrc = Math.Max(_settings.FolderLargeIconSize * 2, 192);
                var mediumSrc = Math.Max(_settings.FolderMediumIconSize * 2, 128);
                var large = ResizeImage(original, largeSrc, largeSrc);
                var tile = ResizeImage(original, tileSrc, tileSrc);
                var medium = ResizeImage(original, mediumSrc, mediumSrc);
                var small = ResizeImage(original, 16, 16);

                // 同じスレッドなので直接アイコンを差し替え
                if (!IsDisposed && !ct.IsCancellationRequested)
                {
                    var largeIdx = _largeIconList.Images.Count;
                    _largeIconList.Images.Add(large);
                    var tileIdx = _tileIconList.Images.Count;
                    _tileIconList.Images.Add(tile);
                    var mediumIdx = _mediumIconList.Images.Count;
                    _mediumIconList.Images.Add(medium);
                    var smallIdx = _smallIconList.Images.Count;
                    _smallIconList.Images.Add(small);

                    item.ImageIndex = mediumIdx;
                }
                else
                {
                    large.Dispose(); tile.Dispose(); medium.Dispose(); small.Dispose();
                }
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // 読み込み失敗は無視（シェルアイコンのまま）
            }
        }
    }

    private static Bitmap ResizeImage(Image original, int maxWidth, int maxHeight)
    {
        var ratio = Math.Min((double)maxWidth / original.Width, (double)maxHeight / original.Height);
        var newWidth = Math.Max(1, (int)(original.Width * ratio));
        var newHeight = Math.Max(1, (int)(original.Height * ratio));

        // 正方形のキャンバスを作成し、アスペクト比を維持して中央に描画
        var thumb = new Bitmap(maxWidth, maxHeight);
        using var g = Graphics.FromImage(thumb);
        g.Clear(Color.Transparent);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        var offsetX = (maxWidth - newWidth) / 2;
        var offsetY = (maxHeight - newHeight) / 2;
        g.DrawImage(original, offsetX, offsetY, newWidth, newHeight);
        return thumb;
    }

    private void SortAndAddItems()
    {
        var sorted = (_sortAscending
            ? _items.OrderBy(i => i.Text, StringComparer.CurrentCultureIgnoreCase)
            : _items.OrderByDescending(i => i.Text, StringComparer.CurrentCultureIgnoreCase)).ToList();

        _listView.Items.Clear();
        _listView.Items.AddRange([.. sorted]);
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    private (int Large, int Tile, int Medium, int Small) GetIconIndex(string path, bool isFolder, Dictionary<string, (int Large, int Tile, int Medium, int Small)> cache)
    {
        var key = isFolder ? "FOLDER" : Path.GetExtension(path).ToLowerInvariant();

        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var attr = isFolder ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;

        var largeIdx = -1;
        var tileIdx = -1;
        var mediumIdx = -1;
        var smallIdx = -1;

        var largeShfi = new SHFILEINFO();
        SHGetFileInfo(path, attr, ref largeShfi, Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);
        if (largeShfi.hIcon != IntPtr.Zero)
        {
            using var icon = Icon.FromHandle(largeShfi.hIcon);
            largeIdx = AddResizedIcon(icon, _largeIconList, Math.Max(_settings.FolderExtraLargeIconSize * 2, 256), Math.Max(_settings.FolderExtraLargeIconSize * 2, 256));
            DestroyIcon(largeShfi.hIcon);
        }

        var tileShfi = new SHFILEINFO();
        SHGetFileInfo(path, attr, ref tileShfi, Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);
        if (tileShfi.hIcon != IntPtr.Zero)
        {
            using var icon = Icon.FromHandle(tileShfi.hIcon);
            tileIdx = AddResizedIcon(icon, _tileIconList, Math.Max(_settings.FolderLargeIconSize * 2, 192), Math.Max(_settings.FolderLargeIconSize * 2, 192));
            DestroyIcon(tileShfi.hIcon);
        }

        var mediumShfi = new SHFILEINFO();
        SHGetFileInfo(path, attr, ref mediumShfi, Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);
        if (mediumShfi.hIcon != IntPtr.Zero)
        {
            using var icon = Icon.FromHandle(mediumShfi.hIcon);
            mediumIdx = AddResizedIcon(icon, _mediumIconList, Math.Max(_settings.FolderMediumIconSize * 2, 128), Math.Max(_settings.FolderMediumIconSize * 2, 128));
            DestroyIcon(mediumShfi.hIcon);
        }

        var smallShfi = new SHFILEINFO();
        SHGetFileInfo(path, attr, ref smallShfi, Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);
        if (smallShfi.hIcon != IntPtr.Zero)
        {
            _smallIconList.Images.Add(Icon.FromHandle(smallShfi.hIcon));
            DestroyIcon(smallShfi.hIcon);
            smallIdx = _smallIconList.Images.Count - 1;
        }

        var result = (largeIdx, tileIdx, mediumIdx, smallIdx);
        cache[key] = result;
        return result;
    }

    private static int AddResizedIcon(Icon icon, ImageList imageList, int width, int height)
    {
        using var bmp = icon.ToBitmap();
        if (bmp.Width <= width && bmp.Height <= height)
        {
            imageList.Images.Add(bmp);
        }
        else
        {
            using var resized = ResizeImage(bmp, width, height);
            imageList.Images.Add(resized);
        }
        return imageList.Images.Count - 1;
    }

    private void OpenSelectedItem()
    {
        if (_listView.SelectedItems.Count == 0) return;

        var item = _listView.SelectedItems[0];
        if (item.Tag is string path && File.Exists(path))
        {
            try
            {
                using var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = path;
                process.StartInfo.UseShellExecute = true;
                process.Start();
            }
            catch (Exception)
            {
            }
        }
    }

    private void UpdateStatusBar()
    {
        UpdateItemCountText();
    }

    /// <summary>アイテム数を ViewModel に反映する。</summary>
    private void UpdateItemCountText()
    {
        var count = _items.Count;
        _viewModel.ItemCountText = $"{count} 個のアイテム";
    }

    private static readonly string[] s_viewModeNames = ["特大", "大", "中", "一覧", "詳細"];

    /// <summary>現在の表示モードを ViewModel に反映する。</summary>
    private void UpdateViewModeText()
    {
        var idx = _viewModel.SelectedViewModeIndex;
        var modeName = (uint)idx < s_viewModeNames.Length ? s_viewModeNames[idx] : "";
        _viewModel.ViewModeText = modeName;
    }

    private void ListView_ItemActivate(object? sender, EventArgs e)
    {
        OpenSelectedItem();
    }

    /// <summary>現在のソート状態を ViewModel に反映する。</summary>
    private void UpdateSortStatusText()
    {
        _viewModel.SortStatusText = _sortAscending ? "名前の昇順(▲)" : "名前の降順(▼)";
    }

    private void SetSortOrder(bool ascending)
    {
        if (_sortAscending == ascending) return;
        _sortAscending = ascending;
        _listView.BeginUpdate();
        SortAndAddItems();
        _listView.EndUpdate();
        _viewModel.IsSortAscending = _sortAscending;
        _settings.FolderSortAscending = _sortAscending;
        _settingsService.Save();
        UpdateStatusBar();
        UpdateSortStatusText();
    }

    private void BtnClose_Click(object? sender, EventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 設定からウィンドウ位置・サイズを復元する
    /// </summary>
    private void RestoreFormBounds()
    {
        var bounds = _settings.FolderViewFormBounds;
        if (bounds.HasValue && bounds.Value.Width > 0 && bounds.Value.Height > 0)
        {
            StartPosition = FormStartPosition.Manual;
            DesktopBounds = bounds.Value;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _loadCts?.Cancel();

        if (WindowState == FormWindowState.Normal)
        {
            _settings.FolderViewFormBounds = DesktopBounds;
        }
        else
        {
            _settings.FolderViewFormBounds = RestoreBounds;
        }

        _settingsService.Save();
        base.OnFormClosing(e);
    }

    private void SetViewMode(int index)
    {
        if (index < 0 || index > 4) return;

        _viewModel.SelectedViewModeIndex = index;
        _settings.FolderViewModeIndex = index;
        _settingsService.Save();

        // ListView の表示モードを切り替え
        _listView.View = index switch
        {
            0 or 1 or 2 => View.LargeIcon,
            3 => View.List,
            _ => View.Details
        };

        if (index is 0 or 1 or 2)
        {
            _listView.LargeImageList = index switch
            {
                0 => _largeIconList,
                1 => _tileIconList,
                _ => _mediumIconList
            };
        }

        // 詳細表示時は列を設定
        if (index == 4)
        {
            _listView.Columns.Clear();
            _listView.Columns.Add("名前", 250);
            _listView.Columns.Add("サイズ", 100);
            _listView.Columns.Add("更新日時", 150);
        }
        else
        {
            _listView.Columns.Clear();
        }

        UpdateStatusBar();
        UpdateViewModeText();
    }

    private void CtxViewExtraLarge_Click(object? sender, EventArgs e) => SetViewMode(0);
    private void CtxViewLarge_Click(object? sender, EventArgs e) => SetViewMode(1);
    private void CtxViewMedium_Click(object? sender, EventArgs e) => SetViewMode(2);
    private void CtxViewList_Click(object? sender, EventArgs e) => SetViewMode(3);
    private void CtxViewDetails_Click(object? sender, EventArgs e) => SetViewMode(4);
    private void CtxSortAscending_Click(object? sender, EventArgs e) => SetSortOrder(true);
    private void CtxSortDescending_Click(object? sender, EventArgs e) => SetSortOrder(false);
    private void CtxCopyFolderPath_Click(object? sender, EventArgs e) => CopyFolderPathToClipboard();
    private void MenuFileOpenExplorer_Click(object? sender, EventArgs e) => OpenFolderInExplorer();
    private void MenuFileCopyFolderPath_Click(object? sender, EventArgs e) => CopyFolderPathToClipboard();
    private void MenuViewExtraLarge_Click(object? sender, EventArgs e) => SetViewMode(0);
    private void MenuViewLarge_Click(object? sender, EventArgs e) => SetViewMode(1);
    private void MenuViewMedium_Click(object? sender, EventArgs e) => SetViewMode(2);
    private void MenuViewList_Click(object? sender, EventArgs e) => SetViewMode(3);
    private void MenuViewDetails_Click(object? sender, EventArgs e) => SetViewMode(4);
    private void MenuSortAscending_Click(object? sender, EventArgs e) => SetSortOrder(true);
    private void MenuSortDescending_Click(object? sender, EventArgs e) => SetSortOrder(false);
    private void LblViewMode_MouseDown(object? sender, MouseEventArgs e) => _contextMenuView.Show(Cursor.Position); 
    private void LblSortStatus_MouseDown(object? sender, MouseEventArgs e) => _contextMenuSort.Show(Cursor.Position); 

    /// <summary>
    /// フォルダパスリンクを左クリックしたときにエクスプローラで開く。
    /// </summary>
    private void LblFolderPath_Click(object? sender, EventArgs e)
    {
        OpenFolderInExplorer();
    }

    /// <summary>
    /// フォルダパスリンクを右クリックしたときにコンテキストメニューを表示する。
    /// </summary>
    private void LblFolderPath_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            _contextMenuFolder.Show(_statusStrip, e.Location);
        }
    }

    /// <summary>
    /// コンテキストメニュー「エクスプローラでフォルダ開く」のクリックハンドラー。
    /// </summary>
    private void CtxOpenFolder_Click(object? sender, EventArgs e)
    {
        OpenFolderInExplorer();
    }

    /// <summary>
    /// フォルダをエクスプローラで開く。
    /// </summary>
    private void OpenFolderInExplorer()
    {
        try
        {
            if (Directory.Exists(_folderPath))
                System.Diagnostics.Process.Start("explorer.exe", _folderPath);
        }
        catch
        {
            MessageBox.Show($"フォルダを開けませんでした: {_folderPath}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// フォルダのフルパスをクリップボードにコピーする。
    /// </summary>
    private void CopyFolderPathToClipboard()
    {
        try
        {
            Clipboard.SetText(_folderPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"クリップボードへのコピーに失敗しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// ビューモードコンテキストメニュー表示直前に、現在選択中のモードに☑を付ける。
    /// </summary>
    private void ContextMenuView_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var idx = _viewModel.SelectedViewModeIndex;
        _ctxViewExtraLarge.Checked = idx == 0;
        _ctxViewLarge.Checked = idx == 1;
        _ctxViewMedium.Checked = idx == 2;
        _ctxViewList.Checked = idx == 3;
        _ctxViewDetails.Checked = idx == 4;
    }

    /// <summary>
    /// ソート順コンテキストメニュー表示直前に、現在のソート順に☑を付ける。
    /// </summary>
    private void ContextMenuSort_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _ctxSortAscending.Checked = _viewModel.IsSortAscending;
        _ctxSortDescending.Checked = !_viewModel.IsSortAscending;
    }

    /// <summary>
    /// メニューバーの表示メニュー表示直前に、現在の状態に☑を付ける。
    /// </summary>
    private void MenuView_DropDownOpening(object? sender, EventArgs e)
    {
        var idx = _viewModel.SelectedViewModeIndex;
        _menuViewExtraLarge.Checked = idx == 0;
        _menuViewLarge.Checked = idx == 1;
        _menuViewMedium.Checked = idx == 2;
        _menuViewList.Checked = idx == 3;
        _menuViewDetails.Checked = idx == 4;

        _menuSortAscending.Checked = _viewModel.IsSortAscending;
        _menuSortDescending.Checked = !_viewModel.IsSortAscending;
    }


    [DllImport("shell32.dll", CharSet = CharSet.Auto, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, int cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

}

internal sealed class ViewOption
{
    public string Text { get; init; } = "";
    public View View { get; init; }
}
