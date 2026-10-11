using System.Diagnostics;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.MewUI.Logging;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>Undo / Redo 履歴の画像を一覧表示するページ（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>UndoPage.axaml</c> に相当する。<br/>
/// <br/>
/// <b>表示順</b>: Redo（新しい未来）→ 現在 → Undo（古い過去）の固定。 現在プレビューに表示中の画像を青枠で強調する。<br/>
/// <b>表示モード</b>: 特大 / 大 / 中。<c>Settings.UndoViewModeIndex</c> へ保存・復元し、名前と範囲の補正は<br/>
/// <see cref="ViewModeDisplay"/>（コア）が単一所有する。<br/>
/// <b>サムネイル</b>: <see cref="ThumbnailView"/> が画像を正方形へ収めて描画する。<br/>
/// <b>ダブルクリック</b>: 画像を一時ファイルへ書き出し、OS の既定アプリで開く。<br/>
/// <b>画像の所有権</b>: 一覧は <see cref="UndoHistory"/> と <see cref="MainViewModel"/> が保持する画像を参照するだけで、破棄しない。
/// </remarks>
internal static class UndoPage
{
    /// <summary>ロガー（一覧の準備を記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(UndoPage));

    /// <summary>サムネイルとラベルの間隔（DIP）。</summary>
    private const double ItemSpacing = 2;

    /// <summary>一覧の余白（DIP）。</summary>
    private const double ListPadding = 4;

    /// <summary>ラベル領域の高さ（DIP）。</summary>
    /// <remarks>行内でサムネイルの下端を揃えるため、ラベルは 2 行分の固定高とする（Avalonia 版と同じ）。</remarks>
    private const double LabelHeight = 30;

    /// <summary>画像を一時ファイルへ書き出すライター（ダブルクリックで外部ツールを使う場合に限る）。</summary>
    private static readonly IImageWriter s_writer = new EsBitmapImageWriter();

    /// <summary>ページの内容を生成する。</summary>
    /// <param name="viewModel">アプリのメイン ViewModel（現在の画像の取得元）。</param>
    /// <param name="settingsService">設定サービス（表示モードの読み書きに使用する）。</param>
    /// <param name="undoStack">Undo / Redo 履歴（キャプチャページと共有する）。</param>
    /// <returns>ページの内容と、表示のたびに実行する処理。</returns>
    /// <remarks>
    /// 1. 3 行の Grid（ツールバー / 一覧 / ステータス）を組み立てる。<br/>
    /// 2. 一覧はスクロールビュー内の折り返しパネルへサムネイルを並べる。<br/>
    /// 3. 表示モードの切り替えで設定を保存し、一覧を再構築する。<br/>
    /// 4. 表示のたびに保存済みの表示モードを復元し、一覧を再構築する。<br/>
    /// 5. 現在の画像が変わった時も一覧を再構築する（青枠の位置が追従する）。
    /// </remarks>
    public static PageContent Build(MainViewModel viewModel, ISettingsService settingsService, UndoHistory undoStack)
    {
        // 表示モードはラムダ間で共有するためローカルに保持する（設定が単一所有元）
        var selectedIndex = ViewModeDisplay.ClampIndex(settingsService.Current.UndoViewModeIndex);

        var list = new BottomAlignWrapPanel(ItemSpacing) { Padding = new Thickness(ListPadding) };

        var countText = new ObservableValue<string>(ViewModeDisplay.GetItemCountText(0));
        var modeText = new ObservableValue<string>(ViewModeDisplay.GetText(selectedIndex));

        var modeButtons = new ToggleButton[ViewModeDisplay.Count];

        // サムネイル用に縮小した画像（本ページが所有し、一覧の再構築のたびに破棄する）
        // （一覧を組み立てるローカル関数（CreateModeButton → OnModeChanged → Rebuild）から参照するため、 最初に宣言する）
        var thumbnails = new List<EsReadOnlyBitmap>();

        // 表示用の縮小（面積平均）は背景で行う（表示中は並列・非表示では 1 件ずつ）
        var scheduler = ThumbnailBackground.CreateScheduler();

        // 生成中の要求と、その行の要素（一覧の位置は上方が小さい）
        var requested = new List<(ThumbnailScheduler.ThumbnailRequest Request, Element Element)>();

        // 最後に優先度を付けた可視領域（変わっていなければ付け直さない）
        var lastViewportTop = double.NaN;
        var lastViewportBottom = double.NaN;

        // 最後に一覧を構築したときの履歴の状態（未構築は null）
        BuiltState? built = null;

        // 表示モードの切り替え（解除は許さない。 常に 1 つを選択状態に保つ）
        void OnModeChanged(int index, bool isChecked)
        {
            if (isChecked)
            {
                if (index == selectedIndex)
                {
                    return;
                }

                selectedIndex = index;
                settingsService.Current.UndoViewModeIndex = index;
                settingsService.Save();
                SyncModeButtons();
                Rebuild();
                return;
            }

            if (index == selectedIndex)
            {
                modeButtons[index].IsChecked = true;
            }
        }

        ToggleButton CreateModeButton(int index)
        {
            var button = new ToggleButton()
                .Content(ViewModeDisplay.GetText(index))
                .Height(AppStyles.ToolbarButtonHeight)
                .Margin(new Thickness(1));
            button.OnCheckedChanged(isChecked => OnModeChanged(index, isChecked));
            return button;
        }

        void SyncModeButtons()
        {
            for (var i = 0; i < modeButtons.Length; i++)
            {
                modeButtons[i].IsChecked = i == selectedIndex;
            }
        }

        for (var i = 0; i < modeButtons.Length; i++)
        {
            modeButtons[i] = CreateModeButton(i);
        }

        // 一覧を組み立て直す（表示モード変更時・現在の画像の変更時・ページ表示時に呼ぶ）
        // 表示用の縮小は背景で行う: 一覧の構築では縮小せず、全行を代替画像で表示してから
        // ThumbnailScheduler へ依頼し、生成が終わった 1 行から差し替える
        // （一覧の表示を縮小の時間で待たせない。 生成中でも生成済みのサムネイルは表示される）。
        // 履歴が変わっていなければ何もしない（先読みした縮小画像をそのまま表示するため）。
        void Rebuild()
        {
            var iconSize = settingsService.Current.GetIconSize(selectedIndex);
            var redo = undoStack.GetRedoHistory();
            var undo = undoStack.GetUndoHistory();
            var current = viewModel.PreviewImage;

            // 前回と同じ履歴なら再構築しない（表示するたびに縮小し直すと、先読みした結果が無駄になる）
            if (built is { } state
                && state.Size == iconSize
                && state.RedoCount == redo.Count
                && state.UndoCount == undo.Count
                && ReferenceEquals(state.Current, current))
            {
                return;
            }

            built = new BuiltState(iconSize, redo.Count, undo.Count, current);

            var previous = thumbnails;
            var created = new List<EsReadOnlyBitmap>();

            // 前回の生成要求を破棄する（別の寸法・別の一覧向けの生成を残さない）
            // （後から届く結果が破棄済みの一覧へ登録されないようにするため、先に破棄する）
            scheduler.Clear();
            requested.Clear();
            lastViewportTop = double.NaN;
            lastViewportBottom = double.NaN;

            list.Clear();

            var placeholder = ThumbnailBackground.Placeholder(iconSize);
            void AddItem(EsReadOnlyBitmap image, string label, bool isCurrent)
            {
                var view = new ThumbnailView { RequestedSize = iconSize, Source = placeholder };
                var element = CreateItem(view, image, label, isCurrent, iconSize);
                list.Add(element);

                // 位置は一覧の並び（上方が小さい）。 見えている帯は可視領域が確定した後に付け直す
                requested.Add((scheduler.Enqueue(
                    ThumbnailPriority.Background(list.Count - 1), MakeThumbnailWork(created, view, image, iconSize)), element));
            }

            // 表示順を固定する: Redo（現在に近い未来から）→ 現在 → Undo（現在の直前から過去へ）
            for (var i = 0; i < redo.Count; i++)
            {
                AddItem(redo[i], $"Redo {i + 1}", isCurrent: false);
            }

            if (current is not null)
            {
                AddItem(current, "現在", isCurrent: true);
            }

            for (var i = undo.Count - 1; i >= 0; i--)
            {
                AddItem(undo[i], $"Undo {undo.Count - i}", isCurrent: false);
            }

            thumbnails = created;

            // 新しい一覧を先に設定し、旧サムネイルは後から破棄する（表示が参照中の破棄を避ける）
            foreach (var thumbnail in previous)
            {
                thumbnail.Dispose();
            }

            countText.Value = ViewModeDisplay.GetItemCountText(list.Count);
            modeText.Value = ViewModeDisplay.GetText(selectedIndex);
        }

        // 現在の画像の差し替えで青枠とサムネイルを追従させる
        viewModel.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.PreviewImage), StringComparison.Ordinal))
            {
                Rebuild();
            }
        };

        Element[] modeElements = [.. modeButtons];

        var toolbar = new StackPanel()
            .Horizontal()
            .Spacing(2)
            .Margin(new Thickness(2, AppStyles.ToolbarMarginTop, AppStyles.ToolbarMarginRight, AppStyles.ToolbarMarginBottom))
            .Children(modeElements);

        // ステータスバーは 3 項目。 右端のヒントのみ右寄せにする（Avalonia 版と同じ配置）
        var status = new StackPanel()
            .Horizontal()
            .Spacing(12)
            .VerticalAlignment(VerticalAlignment.Center)
            .Children(
                new TextBlock().BindText(countText),
                new TextBlock().BindText(modeText));

        var hint = new TextBlock().Text("ダブルクリックで外部ツール表示");
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        hint.VerticalAlignment = VerticalAlignment.Center;

        var statusGrid = new Grid().Columns("*,Auto");
        statusGrid.Add(status.Column(0));
        statusGrid.Add(hint.Column(1));

        var scroll = new ScrollViewer().Content(list);

        // 見え方が変わったら優先度を付け直す（スクロールと、ウィンドウの大きさの変化）
        // 可視領域はスクロールの位置と高さから求め、行の位置は要素の配置から取る
        // （一覧は折り返しパネルで全件を実体化するため、行の配置がそのまま内容の座標になる）。
        scroll.ScrollChanged += UpdatePriorities;
        scroll.SizeChanged += _ => UpdatePriorities();

        void UpdatePriorities()
        {
            if (requested.Count == 0) return;

            var top = scroll.VerticalOffset;
            var bottom = top + scroll.ViewportHeight;
            if (top == lastViewportTop && bottom == lastViewportBottom) return;

            lastViewportTop = top;
            lastViewportBottom = bottom;

            for (var i = 0; i < requested.Count; i++)
            {
                var (request, element) = requested[i];
                var bounds = element.Bounds;
                scheduler.Reprioritize(
                    request, ThumbnailPriority.FromRange(bounds.Top, bounds.Bottom, top, bottom, i));
            }
        }

        // 設定から表示モードを復元し、トグルと一覧を追従させる（ページ表示のたびに呼ぶ）
        // 生成の負荷（表示中は並列 / 非表示では 1 件ずつ）は本メソッドでは決めない（OnActivated / OnDeactivated が持つ）
        void Restore()
        {
            selectedIndex = ViewModeDisplay.ClampIndex(settingsService.Current.UndoViewModeIndex);
            SyncModeButtons();
            Rebuild();
            UpdatePriorities();
        }

        // 表示されるたび: 生成を全力にする（隠れている間は 1 件ずつ）
        void Activate()
        {
            scheduler.IsActive = true;
            Restore();
        }

        // 起動後に一度だけ: ページを表示せずに一覧を構築し、サムネイルを低負荷で生成しておく
        // （最初の切り替えで生成済みのサムネイルが並ぶ。 キャプチャページの操作は妨げない）
        void WarmUp()
        {
            Restore();
            s_logger.LogThumbnailWarmUp("履歴", requested.Count);
        }

        // ツールバー（上端 6 DIP。 タイトルバー領域の下） / 一覧（残余） / ステータスの 3 段。
        // ツールバーはタイトルバー領域に重ねて置き、一覧はタイトルバー領域の下から始める（Avalonia 版と同じ）
        FrameworkElement content = new Grid()
            .Rows("Auto,*,Auto")
            .Columns("*")
            .Children(
                toolbar,
                new Border()
                    .StyleName(AppStyles.ListSurface)
                    .Child(new Grid().Rows("*").Columns("*").Padding(10).Children(scroll)),
                new Border()
                    .Padding(AppStyles.StatusPaddingX, AppStyles.ListStatusPaddingY)
                    .StyleName(AppStyles.StatusBar)
                    .Child(new Grid().Rows("Auto").Columns("*").Children(statusGrid)));

        Restore();
        return new PageContent(content, Activate, OnDeactivated: () => scheduler.IsActive = false, OnWarmUp: WarmUp);
    }

    /// <summary>最後に一覧を構築したときの履歴の状態（同じなら再構築しない）。</summary>
    /// <param name="Size">表示モードから決まる長辺の上限（ピクセル）。</param>
    /// <param name="RedoCount">Redo スタックの段数。</param>
    /// <param name="UndoCount">Undo スタックの段数。</param>
    /// <param name="Current">現在のプレビュー画像（<b>参照で比べる</b>）。</param>
    private readonly record struct BuiltState(int Size, int RedoCount, int UndoCount, EsReadOnlyBitmap? Current);

    /// <summary>1 行分の表示用の縮小（面積平均）の生成処理を生成する。</summary>
    /// <param name="owned">本ページが所有する縮小画像の登録先（一覧の再構築のたびに破棄する）。</param>
    /// <param name="view">結果を設定する要素（UI スレッドでのみ書き換える）。</param>
    /// <param name="image">縮小元の画像（所有権はスタック側が保持する。 本ページは破棄しない）。</param>
    /// <param name="iconSize">表示する一辺のサイズ（DIP）。</param>
    /// <returns>生成処理。</returns>
    /// <remarks>
    /// 縮小は背景スレッドで行い、<c>Source</c> への設定だけを UI スレッドで行う。<br/>
    /// 一覧が差し替わった後（<see cref="ThumbnailScheduler.Clear"/> の後）の結果は、
    /// 作った縮小画像を解放して捨てる（表示されない画像を保持しない）。
    /// </remarks>
    private static Func<CancellationToken, ValueTask> MakeThumbnailWork(
        List<EsReadOnlyBitmap> owned, ThumbnailView view, EsReadOnlyBitmap image, int iconSize)
        => token =>
        {
            token.ThrowIfCancellationRequested();

            // 縮小後の寸法は元画像を超えない（コアの縮小は拡大を未サポートとする）
            var longSide = Math.Max(image.Width, image.Height);
            var resized = image.Resize(Math.Min(iconSize, longSide), DownscaleAlgorithm.Box);

            // 表示は読むだけ（書き換えない）であるため、凍結して 0 コピーで読み取り専用の面へ渡す
            resized.SetReadOnly();
            var thumbnail = EsReadOnlyBitmap.FromBitmap(resized);
            resized.Dispose();

            ThumbnailBackground.Post(() =>
            {
                if (token.IsCancellationRequested)
                {
                    // 一覧が差し替わった後の結果は捨てる（作った縮小画像を解放する）
                    thumbnail.Dispose();
                    return;
                }

                owned.Add(thumbnail);
                view.Source = thumbnail;
            });

            return ValueTask.CompletedTask;
        };

    /// <summary>サムネイル 1 件分の要素を生成する。</summary>
    /// <param name="view">サムネイルを表示する要素（生成の結果は後から本要素へ設定する）。</param>
    /// <param name="image">サムネイルの元画像（所有権はスタック側が保持する。 本ページは破棄しない）。</param>
    /// <param name="label">サムネイル下のラベル。</param>
    /// <param name="isCurrent">現在プレビューに表示中の画像かどうか（true の場合は青枠で強調する）。</param>
    /// <param name="iconSize">サムネイルの一辺のサイズ（DIP）。</param>
    /// <returns>サムネイルとラベルを縦に並べた要素。</returns>
    /// <remarks>
    /// ダブルクリックで <paramref name="image"/>（<b>元の寸法のまま</b>）を一時ファイルへ書き出し、OS の既定アプリで開く。<br/>
    /// <b>表示は縮小した画像を使う</b>: コアの面積平均（<see cref="DownscaleAlgorithm.Box"/>）で <paramref name="iconSize"/> へ縮小してから描画する。<br/>
    /// 描画時に Skia へ縮小させるとサンプリング（既定は最近傍）によって<b>同じ寸法でも画質が変わる</b>（実測: 4K → 320x180 で
    /// 細線の輝度は既定 255 / 面積平均 21） ため、Avalonia 版（表示用の射影を縮小する）と同じ手順に揃える。<br/>
    /// 縮小そのものは背景（<see cref="MakeThumbnailWork"/>）が行う。
    /// </remarks>
    private static Element CreateItem(
        ThumbnailView view, EsReadOnlyBitmap image, string label, bool isCurrent, int iconSize)
    {
        // 枠（背景色・枠線・角丸）はスタイルが与え、ホバーのハイライトはアイテム側が切り替える
        var frame = new Border()
            .StyleName(isCurrent ? AppStyles.ThumbnailFrameCurrent : AppStyles.ThumbnailFrame)
            .Child(view);

        // ラベルは枠（画像 + 影の余白）と同じ幅にし、文字を画像の中央へ揃える
        // （文字は Avalonia 版と同じく領域の上端へ寄せる）
        var caption = new Label()
            .Text(label)
            .TextAlignment(TextAlignment.Center)
            .VerticalTextAlignment(TextAlignment.Top)
            .Width(iconSize + AppStyles.ThumbnailShadowBlur * 2)
            .Height(LabelHeight)
            .FontSize(11);
        caption.Padding = new Thickness(1, 1, 2, 1);

        return ThumbnailItem.Create(frame, caption, () => OpenInDefaultApp(image));
    }

    /// <summary>画像を一時ファイルへ書き出し、OS の既定アプリで開く。</summary>
    /// <param name="image">開く画像。</param>
    /// <remarks>
    /// 一覧の画像はファイルではなくビットマップのため、一時フォルダへ PNG として書き出してから開く。<br/>
    /// 一時ファイルは OS の一時領域に残す（既定アプリが読み終える時刻を確定できないため、ここでは削除しない）。
    /// </remarks>
    private static void OpenInDefaultApp(EsReadOnlyBitmap image)
    {
        try
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"SaveEvidence_undo_{Guid.NewGuid():N}.png");
            s_writer.Save(image, tempFile);

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = tempFile,
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 外部ツールで開けない場合でも操作を妨げない（拡張子の関連付けが無い環境など）
        }
    }
}
