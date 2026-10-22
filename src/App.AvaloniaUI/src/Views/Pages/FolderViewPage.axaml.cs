using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using EsUtil.Feature.Thumbnail;
using EsUtil.Platform.Core.Caching;
using EsUtil.Ui.AvaloniaUI.Interop.Imaging;
using EsUtil.Ui.AvaloniaUI.Thumbnail;
using EsUtil.SaveEvidence.App.AvaloniaUI.Logging;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Views.Pages;

/// <summary>保存先フォルダの画像一覧を表示するフォルダビューページ。</summary>
/// <remarks>
/// 画像ファイルのサムネイル一覧を表示し、クリックでメイン画面のプレビューに読み込む。<br/>
/// 表示モード（特大/大/中）、ソート切替、更新機能を提供する。<br/>
/// 表示モードの復元・保存、ToggleButton 同期、ページ表示時の再構築は <see cref="ViewModePageBase"/> に共通化する。<br/>
/// <b>保持する画像はサムネイルだけ</b>である（原寸は保持せず、必要な操作はファイルから読み込む）。<br/>
/// <b>サムネイルは背景で生成する</b>（<see cref="ThumbnailScheduler"/>。 ページが表示されている間は並列、
/// 隠れている間は 1 件ずつ）。 生成が終わるまでの行は代替画像を表示する。<br/>
/// <b>見えている行を先に生成する</b>（その中では上方の行が先。 <see cref="ThumbnailPriority"/>）。
/// スクロールで見え方が変わったら未実行の要求の優先度を付け直す。<br/>
/// <b>起動後に一度だけ先読みする</b>（<see cref="WarmUp"/>。 キャプチャページの動作に影響しない低負荷で、
/// ページへ切り替える前にサムネイルを生成しておく）。
/// </remarks>
public sealed partial class FolderViewPage : ViewModePageBase
{
    private readonly FolderViewViewModel _viewModel = null!;

    /// <summary>アプリケーションロガー。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger("FolderViewPage");

    /// <summary>サムネイルのキャッシュの最小保持数。</summary>
    /// <remarks>一覧が表示する件数がこれより少ない場合でも、この数までは保持する（一覧間の行き来で再復号しない）。</remarks>
    private const int ThumbnailCacheCapacity = 100;

    private static readonly CapacityLimitedCache<(string Path, int Size, DateTime LastWrite), Bitmap?> s_thumbnailCache =
        new(static key => LoadBitmap(key.Path, key.Size), ThumbnailCacheCapacity);

    /// <summary>背景のサムネイル生成（非表示では 1 件ずつ、表示中は並列）。</summary>
    /// <remarks>ページはアプリの生存期間で 1 つ生成されるため、明示的に破棄しない（終了時にプロセスごと解放される）。</remarks>
    private readonly ThumbnailScheduler _thumbnailScheduler = null!;

    /// <summary>生成中の要求（一覧の位置と対応する。 生成済みの位置は null）。</summary>
    private readonly List<ThumbnailScheduler.ThumbnailRequest?> _thumbnailRequests = [];

    /// <summary>最後に優先度を付けた可視領域の上端（変わっていなければ付け直さない）。</summary>
    private double _lastViewportTop = double.NaN;

    /// <summary>最後に優先度を付けた可視領域の下端。</summary>
    private double _lastViewportBottom = double.NaN;

    /// <summary>FolderViewPage を初期化する。</summary>
    public FolderViewPage()
    {
        InitializeComponent();

        if (Design.IsDesignMode) return;

        _viewModel = Program.ServiceProvider.GetRequiredService<FolderViewViewModel>();
        DataContext = _viewModel;

        _thumbnailScheduler = new ThumbnailScheduler(OnThumbnailGenerateError);

        // 見え方が変わったら（スクロール・サイズ変更・行数の変化）優先度を付け直す
        FileListBox.LayoutUpdated += (_, _) => UpdateThumbnailPriorities();
        FileListBox.ScrollChanged += (_, _) => UpdateThumbnailPriorities();
        FileListBox.ItemActivated += OnItemActivated;

        InitializeViewModePage(
            Program.SettingsService,
            Program.ServiceProvider.GetRequiredService<MainViewModel>(),
            ExtraLargeButton, LargeButton, MediumButton);
    }

    /// <summary>ページの表示状態に合わせてサムネイル生成の負荷を切り替える。</summary>
    /// <param name="isVisible">表示された場合は true、隠された場合は false。</param>
    /// <inheritdoc/>
    protected override void OnPageVisibilityChanged(bool isVisible) => _thumbnailScheduler.IsActive = isVisible;

    /// <summary>起動後にページを表示する前に一覧を構築し、サムネイルを低負荷で生成しておく。</summary>
    /// <remarks>
    /// <b>起動を遅くしない</b>: 呼ぶのはウィンドウの表示と最初の描画が終わった後（<see cref="MainWindow"/>）。<br/>
    /// <b>キャプチャページの動作に影響しない</b>: ページは表示されていないため、生成の負荷は 1 件ずつ（低負荷）になる
    /// （負荷の判断は <see cref="OnPageVisibilityChanged"/> が持つ）。<br/>
    /// 一覧を構築しておくことで、最初の切り替えでは生成済みのサムネイルがそのまま並ぶ（列挙と復号を待たない）。
    /// </remarks>
    internal void WarmUp()
    {
        LoadFiles();
        s_logger.LogThumbnailWarmUp("フォルダビュー", _viewModel.ItemCount);
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
    /// <remarks>
    /// フォルダ列挙・サムネイル生成で例外が発生してもナビゲーションを継続できるよう、全体を try/catch で保護する<br/>
    /// （ファイルアクセス拒否・破損画像等で「ナビからフォルダビューのクリックで落ちる」ことを防ぐ）。<br/>
    /// 失敗時はアイテム数を 0 にし、一覧を空にする。
    /// </remarks>
    private void LoadFiles()
    {
        try
        {
            LoadFilesCore();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            s_logger.LogFolderViewLoadError(ex, MainViewModel.SaveFolderPathResolved);
            _viewModel.ItemCount = 0;
            FileListBox.ClearItems();
        }
    }

    /// <summary>ファイル一覧を読み込む（本体）。</summary>
    /// <remarks>
    /// <b>サムネイルは背景で生成する</b>: 一覧の構築では生成せず、生成済み（キャッシュ済 み）の分だけをその場で表示する。<br/>
    /// 生成が必要な分は代替画像を表示して <see cref="ThumbnailScheduler"/> へ依頼し、生成が終わった 1 件から差し替える<br/>
    /// （一覧の表示を生成時間で待たせない。 生成中でも生成済みのサムネイルは表示される）。
    /// </remarks>
    private void LoadFilesCore()
    {
        var folderPath = MainViewModel.SaveFolderPathResolved;
        _viewModel.FolderPathText = folderPath;

        // 前回の生成要求を破棄する（別のフォルダ・別の寸法向けの生成を残さない）
        _thumbnailScheduler.Clear();
        _thumbnailRequests.Clear();
        _lastViewportTop = double.NaN;
        _lastViewportBottom = double.NaN;

        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            _viewModel.ItemCount = 0;
            FileListBox.ClearItems();
            return;
        }

        var files = Directory.GetFiles(folderPath, "*.*")
            .Where(static f => EsImageFileExtensions.IsImageFile(f))
            .ToList();

        // ソート順を適用（比較子を選択。 名前順・大文字小文字を区別しない）
        IComparer<string> comparer = _viewModel.IsSortAscending
            ? StringComparer.OrdinalIgnoreCase
            : Comparer<string>.Create(static (a, b) => StringComparer.OrdinalIgnoreCase.Compare(b, a));
        files.Sort(comparer);

        var iconSize = GetIconSize();
        var placeholder = ThumbnailPlaceholder.Get(iconSize);

        // 保持数は表示する件数以上にする（生成の途中で表示中のサムネイルが破棄されないようにする）
        var capacity = Math.Max(ThumbnailCacheCapacity, files.Count);

        // 生成の負荷はページの表示状態に従う（表示中は並列・非表示では 1 件ずつ）
        _thumbnailScheduler.IsActive = IsVisible;

        FileListBox.ClearItems();
        FileListBox.ImageWidth = iconSize;

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];

            // 生成済みはその場で表示し、未生成は代替画像を表示して背景へ回す
            if (GetCachedThumbnail(file, iconSize) is { } cached)
            {
                FileListBox.Add(cached, Path.GetFileName(file), toolTip: file, tag: file);
                _thumbnailRequests.Add(null);
            }
            else
            {
                FileListBox.Add(placeholder, Path.GetFileName(file), toolTip: file, tag: file);

                // 位置は一覧の並び（上方が小さい）。 見えている帯は可視領域が確定した後に付け直す
                _thumbnailRequests.Add(_thumbnailScheduler.Enqueue(
                    ThumbnailPriority.Background(i), MakeThumbnailWork(i, file, iconSize, capacity)));
            }
        }

        _viewModel.ItemCount = files.Count;

        // レイアウト済みなら直ちに、未なら LayoutUpdated が可視領域の確定後に呼ぶ
        UpdateThumbnailPriorities();
    }

    /// <summary>見えている行を先に生成するよう、未実行の要求の優先度を付け直す。</summary>
    /// <remarks>
    /// 可視領域はスクロールの位置と高さから求め、行の位置は一覧のコンテナの配置から取る
    /// （一覧は折り返しパネルで全件を実体化するため、行の配置がそのまま内容の座標になる）。<br/>
    /// <b>見え方が変わっていない場合は何もしない</b>（レイアウトのたびに全件を走査しない）。
    /// </remarks>
    private void UpdateThumbnailPriorities()
    {
        if (_thumbnailRequests.Count == 0) return;

        var top = FileListBox.Offset.Y;
        var bottom = top + FileListBox.Viewport.Height;
        if (top == _lastViewportTop && bottom == _lastViewportBottom) return;

        _lastViewportTop = top;
        _lastViewportBottom = bottom;

        for (var i = 0; i < _thumbnailRequests.Count; i++)
        {
            if (_thumbnailRequests[i] is not { } request) continue;

            var bounds = FileListBox.SlotBounds(i);
            if (bounds.IsEmpty) continue;

            _thumbnailScheduler.Reprioritize(
                request, ThumbnailPriority.FromRange(bounds.Y, bounds.Bottom, top, bottom, i));
        }
    }

    /// <summary>生成済みのサムネイルをキャッシュから取得する（生成はしない）。</summary>
    /// <param name="filePath">対象のファイルパス。</param>
    /// <param name="size">サムネイルの幅（ピクセル）。</param>
    /// <returns>キャッシュにあるサムネイル。 無い場合は null。</returns>
    /// <remarks>キーに更新日時を含めるため、ファイルの内容が変われば再生成される。</remarks>
    private static Bitmap? GetCachedThumbnail(string filePath, int size)
    {
        try
        {
            (string Path, int Size, DateTime LastWrite) key = (filePath, size, File.GetLastWriteTimeUtc(filePath));
            return s_thumbnailCache.TryGetValue(key, out var cached) ? cached : null;
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 更新日時を取得できないファイルは未生成として扱う（一覧は継続する）
            s_logger.LogThumbnailLoadError(ex, filePath);
            return null;
        }
    }
    /// <summary>1 件分のサムネイル生成の処理を生成する。</summary>
    /// <param name="index">結果を設定する項目の添字。</param>
    /// <param name="filePath">対象のファイルパス。</param>
    /// <param name="iconSize">サムネイルの幅（ピクセル）。</param>
    /// <param name="capacity">サムネイルのキャッシュの最大保持数（一覧が表示する件数）。</param>
    /// <returns>生成処理。</returns>
    /// <remarks>
    /// 復号は背景スレッドで行い、一覧への設定だけを UI スレッドで行う。<br/>
    /// 一覧が差し替わった後（<see cref="ThumbnailScheduler.Clear"/> の後）の結果は反映しない
    /// （消えた行へ画像を入れない）。
    /// </remarks>
    private Func<CancellationToken, ValueTask> MakeThumbnailWork(
        int index, string filePath, int iconSize, int capacity)
        => token =>
        {
            token.ThrowIfCancellationRequested();

            // 復号できない画像は null で返る（記録は LoadThumbnail が行う）
            var thumbnail = LoadThumbnail(filePath, iconSize, capacity);
            var fallback = ThumbnailPlaceholder.Get(iconSize);

            Dispatcher.UIThread.Post(() =>
            {
                if (token.IsCancellationRequested) return;

                FileListBox.SetImage(index, thumbnail ?? fallback);
            });

            return ValueTask.CompletedTask;
        };

    /// <summary>背景の生成で想定外の例外が発生したときに記録する（一覧は継続する）。</summary>
    /// <param name="ex">発生した例外。</param>
    private static void OnThumbnailGenerateError(Exception ex) => s_logger.LogThumbnailGenerateError(ex);

    /// <summary>画像ファイルのサムネイルを読み込む（背景スレッドから呼ぶ）。</summary>
    /// <param name="filePath">対象のファイルパス。</param>
    /// <param name="size">サムネイルの幅（ピクセル）。</param>
    /// <param name="capacity">サムネイルのキャッシュの最大保持数（一覧が表示する件数）。</param>
    /// <returns>サムネイル。 復号できない場合は null。</returns>
    /// <remarks>
    /// <b>保持数を呼び出し側が渡す理由</b>: 一覧が表示する件数より小さい上限だと、生成の途中で表示中の
    /// サムネイルが破棄される（上限超過時はキャッシュが保持する値を破棄するため、描画が破棄済みの
    /// バッファへ触れる）。
    /// </remarks>
    private static Bitmap? LoadThumbnail(string filePath, int size, int capacity)
    {
        try
        {
            var lastWrite = File.GetLastWriteTimeUtc(filePath);
            (string Path, int Size, DateTime LastWrite) key = (filePath, size, lastWrite);

            // 保持数は呼び出しのたびに決まる（表示する件数）ため、上限を先に更新してから取得する
            s_thumbnailCache.Capacity = Math.Max(1, capacity);
            return s_thumbnailCache.GetOrAdd(key);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 復号できない画像はサムネイルを出さない（一覧は継続する）
            s_logger.LogThumbnailLoadError(ex, filePath);
            return null;
        }
    }

    /// <summary>画像ファイルを縮小デコードして Avalonia の画像へ変換する。</summary>
    /// <param name="filePath">対象のファイルパス。</param>
    /// <param name="size">サムネイルの幅（ピクセル）。</param>
    /// <returns>サムネイル。 復号できない場合は null。</returns>
    /// <remarks>
    /// <b>縮小デコードは <see cref="ThumbnailImageLoader"/>（EsUtil）が単一所有する</b>
    /// （MewUI / WinUI 3 / WinForms 版と同じ画質・同じ EXIF の扱いになる）。
    /// 本クラスは<b>変換した画像のキャッシュ</b>だけを持つ（一覧の行は描画のたびに画像を参照するため）。
    /// </remarks>
    private static Bitmap? LoadBitmap(string filePath, int size)
        => ThumbnailImageLoader.Load(filePath, size, s_thumbnailCache.Capacity) is { } source
            ? source.ToAvaloniaBitmap()
            : null;

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

    /// <summary>ファイルのダブルクリック時に規定のアプリで開く。</summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">2 回押しのイベント引数（対象のファイルパスは <see cref="ThumbnailActivatedEventArgs.Tag"/>）。</param>
    private void OnItemActivated(object? sender, ThumbnailActivatedEventArgs e)
    {
        if (e.Tag is not string filePath || string.IsNullOrEmpty(filePath)) return;

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };
            process.Start();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // ファイルを開けなくてもユーザー操作を妨げない
        }
    }
}



