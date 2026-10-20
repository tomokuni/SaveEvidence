using EsUtil.Ui.WinForms.Thumbnail;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.WinForms.Logging;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>Undo 一覧ページ（編集履歴の画像をサムネイルで一覧する）。</summary>
/// <remarks>
/// <b>並び順</b>: やり直し（現在に近い未来から）→ 現在 → 取り消し（現在の直前から過去へ）。<br/>
/// キャプチャページ・設定ページと同じ履歴（<see cref="UndoHistory"/>）を共有する。<br/>
/// <b>縮小は背景で行う</b>: 一覧の構築では縮小せず、全項目を先に並べてから縮小を依頼し、<br/>
/// 生成が終わった 1 件から差し替える。<br/>
/// <b>2 回押し</b>: 元の寸法のまま一時ファイルへ書き出し、OS の既定アプリで開く（履歴はファイルを持たないため）。
/// </remarks>
internal static class UndoPage
{
    /// <summary>ロガー（縮小と外部表示の失敗を記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(UndoPage));

    /// <summary>履歴の画像をファイルとして書き出す書き込みサービス。</summary>
    private static readonly IImageWriter s_writer = new EsBitmapImageWriter();

    /// <summary>ページの内容を生成する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <returns>ページの内容。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> が null の場合。</exception>
    public static PageContent Build(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var viewModel = services.Main;
        var undoViewModel = services.Undo;
        var undoStack = services.UndoHistory;
        var settingsService = services.Settings;

        var selectedIndex = ViewModeDisplay.ClampIndex(settingsService.Current.UndoViewModeIndex);
        var scheduler = new ThumbnailScheduler(
            ex => s_logger.LogThumbnailGenerateError(ex), idleInterval: TimeSpan.FromMilliseconds(100));
        var requests = new List<(int Index, ThumbnailScheduler.ThumbnailRequest Request)>();

        // 項目と元画像の対応（2 回押しで元の寸法の画像を書き出すために使う）。
        var itemSources = new List<EsReadOnlyBitmap>();

        // 直前の組み立ての条件（同じ履歴では組み直さない）。
        (int IconSize, int RedoCount, int UndoCount, EsReadOnlyBitmap? Current)? built = null;

        var list = new ThumbnailControl
        {
            ImageWidth = settingsService.Current.GetIconSize(selectedIndex),
            Dock = DockStyle.Fill,
        };

        var countLabel = UiFactory.CreateStatusLabel(0, "項目数");
        var modeLabel = UiFactory.CreateStatusLabel(0, "表示モード");
        var hintLabel = UiFactory.CreateStatusLabel(0);

        var statusBar = UiFactory.CreateStatusBar();
        var statusFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        statusFlow.Controls.AddRange([countLabel, modeLabel]);
        var hintPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
        };
        hintPanel.Controls.Add(hintLabel);
        statusBar.Controls.Add(statusFlow);
        statusBar.Controls.Add(hintPanel);

        var modeButtons = new ToolButton[ViewModeDisplay.Count];
        for (var i = 0; i < modeButtons.Length; i++)
        {
            var index = i;
            modeButtons[index] = UiFactory.CreateToolButton(
                null,
                $"表示モード: {ViewModeDisplay.GetText(index)}",
                () => ChangeMode(index),
                ViewModeDisplay.GetText(index));
        }

        var toolbar = UiFactory.CreateToolbar();
        toolbar.Controls.AddRange(modeButtons);

        var root = new Panel { Dock = DockStyle.Fill, BackColor = AppStyles.PageBackground };
        root.Controls.Add(list);
        root.Controls.Add(toolbar);
        root.Controls.Add(statusBar);

        list.ItemActivated += (_, e) =>
        {
            if (e.Tag is EsReadOnlyBitmap image)
            {
                OpenInDefaultApp(image);
            }
        };

        list.Scroll += (_, _) => Reprioritize();

        viewModel.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.PreviewImage), StringComparison.Ordinal))
            {
                Rebuild(force: true);
            }
        };

        ApplyAppearance();
        SyncModeButtons();
        Rebuild(force: true);

        return new PageContent(
            root,
            OnActivated: () =>
            {
                selectedIndex = ViewModeDisplay.ClampIndex(settingsService.Current.UndoViewModeIndex);
                SyncModeButtons();
                scheduler.IsActive = true;
                Rebuild(force: false);
            },
            OnDeactivated: () => scheduler.IsActive = false,
            OnWarmUp: () =>
            {
                selectedIndex = ViewModeDisplay.ClampIndex(settingsService.Current.UndoViewModeIndex);
                SyncModeButtons();
                Rebuild(force: false);
            });

        // ────────────────────────────────────────────────────────────────

        // 配色を現在のテーマへ合わせる。
        void ApplyAppearance()
        {
            // ページは下地（白）のまま、一覧だけ 4 UI 版で共通の一覧色にする
            root.BackColor = AppStyles.PageBackground;
            list.BackColor = AppStyles.ListBackground;
            toolbar.BackColor = AppStyles.BarBackground;
            statusBar.BackColor = AppStyles.PageBackground;
            statusFlow.BackColor = AppStyles.PageBackground;
            hintPanel.BackColor = AppStyles.PageBackground;
            hintLabel.Text = "ダブルクリックで外部ツール表示";
            hintLabel.ForeColor = AppStyles.Foreground;

            foreach (var label in new[] { countLabel, modeLabel })
            {
                label.ForeColor = AppStyles.Foreground;
            }
        }

        // 表示モードを変更する。
        void ChangeMode(int index)
        {
            if (selectedIndex == index)
            {
                return;
            }

            selectedIndex = index;
            settingsService.Current.UndoViewModeIndex = index;
            settingsService.Save();
            SyncModeButtons();
            list.ImageWidth = settingsService.Current.GetIconSize(index);
            Rebuild(force: true);
            Rebuild(force: true);
        }

        // 表示モードのボタンの選択状態を合わせる。
        void SyncModeButtons()
        {
            for (var i = 0; i < modeButtons.Length; i++)
            {
                modeButtons[i].IsSelected = i == selectedIndex;
            }

            undoViewModel.SelectedViewModeIndex = selectedIndex;
            modeLabel.Text = ViewModeDisplay.GetText(selectedIndex);
        }

        // 一覧を組み直す（履歴が変わっていなければ何もしない）。
        void Rebuild(bool force)
        {
            var iconSize = settingsService.Current.GetIconSize(selectedIndex);
            var redo = undoStack.GetRedoHistory();
            var undo = undoStack.GetUndoHistory();
            var current = viewModel.PreviewImage;

            // 前回と同じ履歴なら組み直さない（表示のたびに縮小し直すと、先読みした結果が無駄になる）
            if (!force
                && built is { } state
                && state.IconSize == iconSize
                && state.RedoCount == redo.Count
                && state.UndoCount == undo.Count
                && ReferenceEquals(state.Current, current))
            {
                return;
            }

            built = (iconSize, redo.Count, undo.Count, current);

            // 前回の生成要求を破棄する（古い結果が新しい一覧へ登録されないようにする）
            scheduler.Clear();
            requests.Clear();
            itemSources.Clear();

            list.ClearItems();
            list.ImageWidth = iconSize;

            void Add(EsReadOnlyBitmap image, string label, bool isCurrent)
            {
                list.Add(null, label, isCurrent: isCurrent, tag: image);
                itemSources.Add(image);
            }

            for (var i = 0; i < redo.Count; i++)
            {
                Add(redo[i], $"やり直し {i + 1}", isCurrent: false);
            }

            if (current is not null)
            {
                Add(current, "現在", isCurrent: true);
            }

            for (var i = undo.Count - 1; i >= 0; i--)
            {
                Add(undo[i], $"元に戻す {undo.Count - i}", isCurrent: false);
            }

            countLabel.Text = ViewModeDisplay.GetItemCountText(list.ItemCount);
            undoViewModel.ItemCount = list.ItemCount;

            // 縮小は背景へ依頼する（一覧は先に表示し、できた 1 件から差し替える）
            for (var i = 0; i < itemSources.Count; i++)
            {
                var index = i;
                var source = itemSources[i];
                var request = scheduler.Enqueue(
                    ThumbnailPriority.Background(index),
                    token => ThumbnailGeneration.GenerateAsync(list, index, () => source, iconSize, token));
                requests.Add((index, request));
            }

            Reprioritize();

            Reprioritize();
        }

        // 可視範囲に応じて未実行の要求の優先度を付け直す。
        void Reprioritize()
        {
            for (var i = 0; i < requests.Count; i++)
            {
                var (index, request) = requests[i];
                var bounds = list.ItemBounds(index);
                var priority = ThumbnailPriority.FromRange(
                    bounds.Top, bounds.Bottom, 0, list.ClientSize.Height, i);
                _ = scheduler.Reprioritize(request, priority);
            }
        }

        // 履歴の画像を一時ファイルへ書き出し、外部ツールで開く。
        // 履歴は画像（ファイルではない）のため、一時フォルダへ PNG として書き出してから開く。
        // 一時ファイルは OS の一時領域に残す（既定アプリが読み終える時刻を確定できないため）。
        void OpenInDefaultApp(EsReadOnlyBitmap image)
        {
            try
            {
                var tempFile = Path.Combine(Path.GetTempPath(), $"SaveEvidence_undo_{Guid.NewGuid():N}.png");
                s_writer.Save(image, tempFile);
                FolderViewPage.OpenInDefaultApp(tempFile);
            }
            catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
            {
                // 書き出せない場合でも操作を妨げない
                s_logger.LogOpenFileError(ex, "履歴の画像");
            }
        }
    }
}
