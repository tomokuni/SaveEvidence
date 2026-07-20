using System.Runtime.InteropServices;
using app.Models;
using app.Services;

namespace app.Views;

internal sealed class ViewOption
{
    public string Text { get; init; } = "";
    public View View { get; init; }
}

/// <summary>
/// 保存先フォルダの内容を表示するモードレスダイアログ。
/// </summary>
/// <remarks>
/// 画像ファイルは実際のサムネイルを非同期的に生成して表示する。<br/>
/// ダブルクリックで既定のアプリケーションでファイルを開く。<br/>
/// 表示モード（特大/大/中/一覧/詳細）とソート順は <see cref="ISettingsService"/> を介して
/// 設定ファイルに保存・復元される。<br/>
/// シェルアイコンとサムネイルの2段階表示により、大量ファイル時でも素早く一覧を表示する。<br/>
/// </remarks>
public sealed partial class FolderViewForm : Form
{
    private readonly string _folderPath;
    private readonly Settings _settings;
    private readonly ISettingsService _settingsService;
    private bool _sortAscending = true;
    private readonly List<ListViewItem> _items = [];

    private readonly ImageList _largeIconList = null!;   // 特大アイコン用（サイズは設定から）
    private readonly ImageList _tileIconList = null!;     // 大アイコン用（サイズは設定から）
    private readonly ImageList _mediumIconList = null!;   // 中アイコン用（サイズは設定から）
    private readonly ImageList _smallIconList = null!;    // 16x16 (一覧/詳細用)

    private static readonly HashSet<string> s_imageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif", ".webp"];

    private CancellationTokenSource? _loadCts;

    /// <summary>
    /// FolderViewForm を初期化する
    /// </summary>
    /// <param name="folderPath">表示するフォルダパス</param>
    /// <param name="settingsService">設定サービス</param>
    public FolderViewForm(string folderPath, ISettingsService settingsService)
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

        _cmbView.Items.AddRange([
            new ViewOption { Text = "特大", View = View.LargeIcon },
            new ViewOption { Text = "大", View = View.LargeIcon },
            new ViewOption { Text = "中", View = View.LargeIcon },
            new ViewOption { Text = "一覧", View = View.List },
            new ViewOption { Text = "詳細", View = View.Details }
        ]);
        _cmbView.DisplayMember = nameof(ViewOption.Text);
        _cmbView.ValueMember = nameof(ViewOption.View);

        if (_settings.FolderViewModeIndex >= 0 && _settings.FolderViewModeIndex < _cmbView.Items.Count)
        {
            _cmbView.SelectedIndex = _settings.FolderViewModeIndex;
        }
        else
        {
            _cmbView.SelectedIndex = 1;
        }

        _sortAscending = _settings.FolderSortAscending;
        _btnSort.Text = _sortAscending ? "名前 ↑" : "名前 ↓";

        if (Directory.Exists(_folderPath))
        {
            LoadFolderContents();
        }

        Text = $"保存先フォルダの内容 - {_folderPath}";

        // リンクラベルの初期化
        InitializeFolderLink();
    }

    private void InitializeFolderLink()
    {
        _linkFolderName = new LinkLabel
        {
            AutoSize = true,
            Margin = new Padding(20, 6, 3, 3),
            TabIndex = 10,
            Text = Path.GetFileName(_folderPath) ?? _folderPath
        };
        _linkFolderName.LinkClicked += LinkFolderName_LinkClicked;

        _toolTip = new ToolTip();
        _toolTip.SetToolTip(_linkFolderName, _folderPath);

        // 閉じるボタンの右側に追加
        _flowToolBar.Controls.Add(_linkFolderName);
    }

    private void LinkFolderName_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        try
        {
            if (Directory.Exists(_folderPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", _folderPath);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"フォルダを開けませんでした: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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

        // 非同期で画像サムネイルを読み込む
        // ソート順に従い、左上に表示されているものから優先的に処理する
        if (imageFiles.Count > 0)
        {
            // ListView の表示順に imageFiles を並び替え
            var sortedFiles = imageFiles
                .OrderBy(f => _listView.Items.IndexOf(f.Item))
                .ToList();
            Task.Run(() => LoadThumbnailsAsync(sortedFiles, ct), ct);
        }
    }

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
        _lblStatus.Text = $"{_items.Count} 個のアイテム | {_listView.View}";
    }

    private void CmbView_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_cmbView.SelectedItem is ViewOption option)
        {
            if (option.View == View.LargeIcon)
            {
                // インデックス: 0=特大, 1=大, 2=中
                var idx = _cmbView.SelectedIndex;
                _listView.LargeImageList = idx switch
                {
                    0 => _largeIconList,
                    1 => _tileIconList,
                    _ => _mediumIconList
                };
            }

            _listView.View = option.View;

            if (option.View == View.Details)
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

            _settings.FolderViewModeIndex = _cmbView.SelectedIndex;
            _settingsService.Save();

            UpdateStatusBar();
        }
    }

    private void ListView_ItemActivate(object? sender, EventArgs e)
    {
        OpenSelectedItem();
    }

    private void BtnSort_Click(object? sender, EventArgs e)
    {
        _sortAscending = !_sortAscending;
        _btnSort.Text = _sortAscending ? "名前 ↑" : "名前 ↓";
        _listView.BeginUpdate();
        SortAndAddItems();
        _listView.EndUpdate();

        _settings.FolderSortAscending = _sortAscending;
        _settingsService.Save();
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
