using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native.Imaging;
using EsUtil.SaveEvidence.App.AvaloniaUI.Logging;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Views.Pages;

/// <summary>
/// Undo / Redo スタックの画像履歴を一覧表示するページ。
/// </summary>
/// <remarks>
/// Undo / Redo スタック（<see cref="UndoHistory"/>・DI シングルトン）と現在のプレビュー画像
/// （<see cref="MainViewModel.PreviewImage"/>・DI シングルトン）を
/// 時系列順（新しい → 古い）にサムネイル表示する。<br/>
/// 表示順は「Redo（新しい）→ 現在 → Undo（古い）」の固定。<br/>
/// 現在のキャプチャページに表示中の画像は青枠（IsCurrent）で強調する。<br/>
/// 表示モード（特大 / 大 / 中）は <see cref="Settings.UndoViewModeIndex"/> に保存・復元される。<br/>
/// 表示モードの復元・保存、ToggleButton 同期、ページ表示時の再構築は <see cref="ViewModePageBase"/> に共通化する。<br/>
/// <b>表示用の射影（アイコンサイズへの縮小）は背景で生成する</b>（<see cref="ThumbnailScheduler"/>。
/// ページが表示されている間は並列、隠れている間は 1 件ずつ）。 生成が終わるまでの行は代替画像を表示する。<br/>
/// サムネイルのダブルクリックで一時ファイルに保存し、OS の既定アプリで開く。
/// </remarks>
public sealed partial class UndoPage : ViewModePageBase
{
    private readonly UndoViewModel _viewModel = null!;

    /// <summary>Undo / Redo スタック（DI から取得するシングルトン。 CapturePage と共有）。</summary>
    private readonly UndoHistory _undoHistory = null!;

    /// <summary>アプリケーションロガー。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger("UndoPage");

    /// <summary>背景の表示用の射影の生成（非表示では 1 件ずつ、表示中は並列）。</summary>
    /// <remarks>ページはアプリの生存期間で 1 つ生成されるため、明示的に破棄しない（終了時にプロセスごと解放される）。</remarks>
    private readonly ThumbnailScheduler _thumbnailScheduler = null!;

    /// <summary>生成中の要求（一覧の位置と対応する）。</summary>
    private readonly List<ThumbnailScheduler.ThumbnailRequest?> _thumbnailRequests = [];

    /// <summary>一覧のスクロール（可視領域の取得元。 レイアウト後に解決する）。</summary>
    private ScrollViewer? _thumbnailScroll;

    /// <summary>最後に優先度を付けた可視領域の上端（変わっていなければ付け直さない）。</summary>
    private double _lastViewportTop = double.NaN;

    /// <summary>最後に優先度を付けた可視領域の下端。</summary>
    private double _lastViewportBottom = double.NaN;

    /// <summary>UndoPage を初期化する。</summary>
    public UndoPage()
    {
        InitializeComponent();

        if (Design.IsDesignMode) return;

        _viewModel = Program.ServiceProvider.GetRequiredService<UndoViewModel>();
        _undoHistory = Program.ServiceProvider.GetRequiredService<UndoHistory>();
        DataContext = _viewModel;

        _thumbnailScheduler = new ThumbnailScheduler(OnThumbnailGenerateError);

        // 見え方が変わったら（スクロール・サイズ変更・行数の変化）優先度を付け直す
        UndoListBox.LayoutUpdated += (_, _) => UpdateThumbnailPriorities();

        InitializeViewModePage(
            Program.SettingsService,
            Program.ServiceProvider.GetRequiredService<MainViewModel>(),
            ExtraLargeButton, LargeButton, MediumButton);
    }

    /// <summary>ページの表示状態に合わせて表示用の射影の生成の負荷を切り替える。</summary>
    /// <param name="isVisible">表示された場合は true、隠された場合は false。</param>
    /// <inheritdoc/>
    protected override void OnPageVisibilityChanged(bool isVisible) => _thumbnailScheduler.IsActive = isVisible;

    /// <summary>現在の表示モードのインデックス（0=特大, 1=大, 2=中）。</summary>
    protected override int SelectedViewModeIndex
    {
        get => _viewModel.SelectedViewModeIndex;
        set => _viewModel.SelectedViewModeIndex = value;
    }

    /// <summary>保存済みの表示モードを ViewModel へ復元する。</summary>
    /// <remarks>
    /// <see cref="Settings.UndoViewModeIndex"/> を
    /// <see cref="UndoViewModel.SelectedViewModeIndex"/> へ反映する。<br/>
    /// 値の範囲チェックは <see cref="ViewModeDisplay.ClampIndex"/> に委譲する。
    /// </remarks>
    protected override void RestoreViewModeSettings()
    {
        _viewModel.SelectedViewModeIndex = ViewModeDisplay.ClampIndex(SettingsService.Current.UndoViewModeIndex);
    }

    /// <summary>現在の表示モードを設定へ保存する。</summary>
    /// <remarks>表示モード切替のたびに呼び出し、設定ファイルへ反映する。</remarks>
    protected override void SaveViewModeSettings()
    {
        SettingsService.Current.UndoViewModeIndex = _viewModel.SelectedViewModeIndex;
        SettingsService.Save();
    }

    /// <summary>Undo / Redo 履歴と現在の画像を読み込んで一覧を再構築する。</summary>
    protected override void ReloadItems() => RebuildList();

    /// <summary>一覧を再構築する（表示モード変更時・ページ表示時に呼び出し）。</summary>
    /// <remarks>
    /// <b>表示用の射影は背景で生成する</b>: 一覧の構築では縮小せず、全行を代替画像で表示してから
    /// <see cref="ThumbnailScheduler"/> へ依頼し、生成が終わった 1 行から差し替える<br/>
    /// （一覧の表示を縮小の時間で待たせない。 生成中でも生成済みのサムネイルは表示される）。<br/>
    /// <b>見えている行を先に生成する</b>（その中では上方の行が先。 <see cref="ThumbnailPriority"/>）。
    /// </remarks>
    private void RebuildList()
    {
        if (_undoHistory is null) return;

        var size = GetIconSize();

        // 前回の生成要求を破棄する（別の寸法・別の一覧向けの生成を残さない）
        _thumbnailScheduler.Clear();
        _thumbnailRequests.Clear();
        _lastViewportTop = double.NaN;
        _lastViewportBottom = double.NaN;

        // 生成の負荷はページの表示状態に従う（表示中は並列・非表示では 1 件ずつ）
        _thumbnailScheduler.IsActive = IsVisible;

        var placeholder = ThumbnailPlaceholder.Get(size);
        var items = new List<UndoListItem>();
        var sources = new List<IEsBitmap>();

        void AddItem(IEsBitmap source, string label, bool isCurrent)
        {
            items.Add(new UndoListItem
            {
                Thumbnail = placeholder,
                Label = label,
                IsCurrent = isCurrent,
                ThumbnailWidth = size
            });
            sources.Add(source);
        }

        // 1. Redo スタック（新しい順。 _redoStack[0] が現在に最も近い未来）
        // UndoHistory はコアの画像を保持し、表示境界で非所有アダプタ（IImage）へ変換する。
        // 一覧は小さく表示するため、表示用の射影（面積平均の縮小）を作る（等倍のままだと画像 1 枚につき元寸法の複製が
        // 残る。 実測: 4K で 32 MB/枚）。 射影の生成は背景へ回し、ここでは生成元の画像だけを控える。
        var redo = _undoHistory.GetRedoHistory();
        for (var i = 0; i < redo.Count; i++)
        {
            AddItem(redo[i], $"Redo {i + 1}", isCurrent: false);
        }

        // 2. 現在の画像（青枠で強調）
        var currentImage = MainViewModel.PreviewImage;
        if (currentImage is not null)
        {
            AddItem(currentImage, "現在", isCurrent: true);
        }

        // 3. Undo スタック（新しい順。 _stack[^1] が現在の直前）
        var undo = _undoHistory.GetUndoHistory();
        for (var i = undo.Count - 1; i >= 0; i--)
        {
            AddItem(undo[i], $"Undo {undo.Count - i}", isCurrent: false);
        }

        _viewModel.ItemCount = items.Count;

        // 新しい一覧を先に設定し、旧一覧のサムネイルは後から解放する
        // （IImage は非所有のため、保持する本ページが破棄する。 表示が旧サムネイルを参照している間に
        //  破棄すると、描画が破棄済みバッファへ触れて例外になる）。
        var previousItems = UndoListBox.ItemsSource as IEnumerable<UndoListItem>;
        UndoListBox.ItemsSource = items;
        if (previousItems is not null)
        {
            foreach (var old in previousItems)
            {
                // 代替画像（全行で共有する）は破棄しない（表示用の射影を持つアダプタだけを解放する）
                if (old.Thumbnail is EsBitmapAvaloniaAdapter adapter)
                {
                    adapter.Dispose();
                }
            }
        }

        // 生成の負荷はページの表示状態に従う（表示中は並列・非表示では 1 件ずつ）
        for (var i = 0; i < items.Count; i++)
        {
            // 位置は一覧の並び（上方が小さい）。 見えている帯は可視領域が確定した後に付け直す
            _thumbnailRequests.Add(_thumbnailScheduler.Enqueue(
                ThumbnailPriority.Background(i), MakeThumbnailWork(items[i], sources[i], size)));
        }

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

        // スクロールはテンプレートが作るため、最初に見つけたものを覚える
        if (_thumbnailScroll is null)
        {
            _thumbnailScroll = UndoListBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (_thumbnailScroll is { } scroll)
            {
                scroll.ScrollChanged += (_, _) => UpdateThumbnailPriorities();
            }
        }

        // スクロールが見つからない間は全件を見えている扱いにする（上方から順に生成される）
        var top = _thumbnailScroll?.Offset.Y ?? 0;
        var bottom = top + (_thumbnailScroll?.Viewport.Height ?? double.PositiveInfinity);
        if (top == _lastViewportTop && bottom == _lastViewportBottom) return;

        _lastViewportTop = top;
        _lastViewportBottom = bottom;

        for (var i = 0; i < _thumbnailRequests.Count; i++)
        {
            if (_thumbnailRequests[i] is not { } request) continue;
            if (UndoListBox.ContainerFromIndex(i) is not { } container) continue;

            var bounds = container.Bounds;
            _thumbnailScheduler.Reprioritize(
                request, ThumbnailPriority.FromRange(bounds.Y, bounds.Bottom, top, bottom, i));
        }
    }

    /// <summary>1 行分の表示用の射影（アイコンサイズへの縮小）の生成処理を生成する。</summary>
    /// <param name="item">結果を設定するアイテム。</param>
    /// <param name="source">生成元の画像（借用。 <see cref="UndoHistory"/> または <see cref="MainViewModel"/> が保持する）。</param>
    /// <param name="size">表示する長辺の上限（ピクセル）。</param>
    /// <returns>生成処理。</returns>
    /// <remarks>
    /// 縮小は背景スレッドで行い、<see cref="UndoListItem.Thumbnail"/> への設定だけを UI スレッドで行う。<br/>
    /// 一覧が差し替わった後（<see cref="ThumbnailScheduler.Clear"/> の後）の結果は、作った射影を解放して捨てる。
    /// </remarks>
    private static Func<CancellationToken, ValueTask> MakeThumbnailWork(UndoListItem item, IEsBitmap source, int size)
        => token =>
        {
            token.ThrowIfCancellationRequested();

            var adapter = new EsBitmapAvaloniaAdapter(source, size);

            // 表示用の射影（面積平均の縮小）をここ（背景）で作る
            // （射影は初回の参照で作るため、描画時に UI スレッドで作ると 1 枚あたり数 ms の描画の停止になる）
            _ = adapter.Size;

            Dispatcher.UIThread.Post(() =>
            {
                if (token.IsCancellationRequested)
                {
                    // 一覧が差し替わった後の結果は捨てる（作った射影を解放する）
                    adapter.Dispose();
                    return;
                }

                item.Thumbnail = adapter;
            });

            return ValueTask.CompletedTask;
        };

    /// <summary>背景の生成で想定外の例外が発生したときに記録する（一覧は継続する）。</summary>
    /// <param name="ex">発生した例外。</param>
    private static void OnThumbnailGenerateError(Exception ex) => s_logger.LogThumbnailGenerateError(ex);

    /// <summary>
    /// サムネイルのダブルクリック時に一時ファイルへ保存し、OS の既定アプリ（外部ツール）で開く。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">タップイベント。</param>
    /// <remarks>
    /// Undo / Redo スタックの画像はファイルではなく Bitmap のため、
    /// 一時フォルダに PNG として保存してから <see cref="Process.Start()"/> で開く。
    /// </remarks>
    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (UndoListBox.SelectedItem is not UndoListItem item) return;

        try
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"SaveEvidence_undo_{Guid.NewGuid():N}.png");

            // サムネイルは画像源アダプタ（IImage）のため、コアの画像を取り出して保存する。
            // 保存形式（拡張子からの判別）とエンコードはコアの拡張が単一 所有する。
            if (item.Thumbnail is EsBitmapAvaloniaAdapter adapter)
            {
                adapter.GetEsBitmap().Save(tempFile);
            }
            else
            {
                return;
            }

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = tempFile,
                UseShellExecute = true
            };
            process.Start();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 外部ツールで開けなくてもユーザー操作を妨げない
        }
    }
}
