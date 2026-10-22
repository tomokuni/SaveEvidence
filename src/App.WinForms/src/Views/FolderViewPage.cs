using EsUtil.Feature.Thumbnail;
using EsUtil.Ui.WinForms.Thumbnail;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.WinForms.Logging;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>フォルダビュー ページ（保存先フォルダの画像をサムネイルで一覧する）。</summary>
/// <remarks>
/// <b>構成</b>: ツールバー（最新の情報に更新 / 表示モード 3 種 / 並べ替え）/ サムネイル一覧 / ステータスバー。<br/>
/// <b>縮小は背景で行う</b>: 一覧の構築では縮小せず、全項目を先に並べてから縮小を依頼し、<br/>
/// 生成が終わった 1 件から差し替える（一覧の表示を縮小の時間で待たせない）。<br/>
/// <b>ファイル列挙も背景で行う</b>: フォルダ内の検索・並べ替えを UI スレッドから分離し、<br/>
/// 画面の更新は UI スレッドへ戻して行う。<br/>
/// <b>負荷の切り替え</b>: ページを表示している間は並列に、表示から外れている間は 1 件ずつ低負荷で生成する。<br/>
/// <b>並べ替え</b>: ファイル名の昇順 / 降順（設定へ保存し、次回起動時に復元する）。
/// </remarks>
internal static class FolderViewPage
{
    /// <summary>ロガー（一覧の読み込みと縮小の失敗を記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(FolderViewPage));

    /// <summary>ページの内容を生成する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <returns>ページの内容。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> が null の場合。</exception>
    public static PageContent Build(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var viewModel = services.Main;
        var folderView = services.FolderView;
        var settingsService = services.Settings;

        var selectedIndex = ViewModeDisplay.ClampIndex(settingsService.Current.FolderViewModeIndex);
        var scheduler = new ThumbnailScheduler(
            ex => s_logger.LogThumbnailGenerateError(ex), idleInterval: TimeSpan.FromMilliseconds(100));
        var requests = new List<(int Index, ThumbnailScheduler.ThumbnailRequest Request)>();

        // 直前の組み立ての条件（同じ条件では組み直さない）。
        string? builtKey = null;
        var scanGeneration = 0;

        var list = new ThumbnailControl
        {
            ImageWidth = settingsService.Current.GetIconSize(selectedIndex),
            Dock = DockStyle.Fill,
        };

        var countLabel = UiFactory.CreateStatusLabel(0, "一覧の件数");
        var modeLabel = UiFactory.CreateStatusLabel(0, "表示モード");
        var sortLabel = UiFactory.CreateStatusLabel(0, "並べ替え");
        var folderLabel = UiFactory.CreateStatusLabel(0, "一覧しているフォルダ");

        var statusBar = UiFactory.CreateStatusBar();
        var statusFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        statusFlow.Controls.AddRange([countLabel, modeLabel, sortLabel]);
        var folderPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
        };
        folderPanel.Controls.Add(folderLabel);
        statusBar.Controls.Add(statusFlow);
        statusBar.Controls.Add(folderPanel);

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

        // 並べ替えは他の UI 版と同じく「▲ 昇順」「▼ 降順」の文字ボタンで表す
        var sortButtons = new ToolButton[2];
        sortButtons[0] = UiFactory.CreateToolButton(
            null, "昇順に並べ替える", () => ChangeSort(ascending: true), "▲ 昇順");
        sortButtons[1] = UiFactory.CreateToolButton(
            null, "降順に並べ替える", () => ChangeSort(ascending: false), "▼ 降順");

        var toolbar = UiFactory.CreateToolbar();
        toolbar.Controls.Add(UiFactory.CreateToolButton(null, "最新の情報に更新", () => Rebuild(force: true), "最新の情報に更新"));
        toolbar.Controls.Add(UiFactory.CreateSeparator());
        toolbar.Controls.AddRange(modeButtons);
        toolbar.Controls.Add(UiFactory.CreateSeparator());
        toolbar.Controls.AddRange(sortButtons);

        var root = new Panel { Dock = DockStyle.Fill, BackColor = AppStyles.PageBackground };
        root.Controls.Add(list);
        root.Controls.Add(toolbar);
        root.Controls.Add(statusBar);

        list.ItemActivated += (_, e) =>
        {
            if (e.Tag is string filePath and { Length: > 0 })
            {
                OpenInDefaultApp(filePath);
            }
        };

        list.Scroll += (_, _) => Reprioritize();

        ApplyAppearance();
        SyncSortButtons();

        return new PageContent(
            root,
            OnActivated: () =>
            {
                // 保存済みの表示モード・並べ替えを復元してから組み直す
                selectedIndex = ViewModeDisplay.ClampIndex(settingsService.Current.FolderViewModeIndex);
                SyncModeButtons();
                scheduler.IsActive = true;
                Rebuild(force: false);
            },
            OnDeactivated: () => scheduler.IsActive = false,
            OnWarmUp: () =>
            {
                // 起動後は低負荷で先に生成しておく（ページを開いたときに待たせない）
                selectedIndex = ViewModeDisplay.ClampIndex(settingsService.Current.FolderViewModeIndex);
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
            folderPanel.BackColor = AppStyles.PageBackground;

            foreach (var label in new[] { countLabel, modeLabel, sortLabel, folderLabel })
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
            settingsService.Current.FolderViewModeIndex = index;
            settingsService.Save();
            SyncModeButtons();
            list.ImageWidth = settingsService.Current.GetIconSize(index);
            list.RefreshLayout();
            Rebuild(force: true);
        }

        // 表示モードのボタンの選択状態を合わせる。
        void SyncModeButtons()
        {
            for (var i = 0; i < modeButtons.Length; i++)
            {
                modeButtons[i].IsSelected = i == selectedIndex;
            }

            folderView.SelectedViewModeIndex = selectedIndex;
            modeLabel.Text = ViewModeDisplay.GetText(selectedIndex);
        }

        // 並べ替えの向きを切り替える。
        void ChangeSort(bool ascending)
        {
            if (settingsService.Current.FolderViewSortAscending == ascending)
            {
                return;
            }

            settingsService.Current.FolderViewSortAscending = ascending;
            settingsService.Save();
            SyncSortButtons();
            Rebuild(force: true);
        }

        // 並べ替えのボタンの選択状態を合わせる。
        void SyncSortButtons()
        {
            var ascending = settingsService.Current.FolderViewSortAscending;
            sortButtons[0].IsSelected = ascending;
            sortButtons[1].IsSelected = !ascending;
            folderView.IsSortAscending = ascending;
            sortLabel.Text = folderView.SortStatusText;
        }

        // 一覧を組み直す（履歴・表示モードが変わっていなければ何もしない）。
        // 同じ条件で組み直さない理由: 表示のたびに縮小し直すと、先読みした結果が無駄になる。
        void Rebuild(bool force)
        {
            var folderPath = MainViewModel.ResolveSaveFolderPath(settingsService.Current.SaveFolderPath);
            folderLabel.Text = folderPath;
            folderView.FolderPathText = folderPath;

            var iconSize = settingsService.Current.GetIconSize(selectedIndex);
            var ascending = settingsService.Current.FolderViewSortAscending;
            var generation = ++scanGeneration;
            if (list.FindForm() is not { } form)
            {
                throw new InvalidOperationException("フォルダ一覧の所有ウィンドウを取得できません。");
            }

            _ = LoadImagesAsync(form, generation, folderPath, iconSize, ascending, force);
        }

        async Task LoadImagesAsync(
            Form form, int generation, string folderPath, int iconSize, bool ascending, bool force)
        {
            try
            {
                var files = await Task.Run(() =>
                {
                    var enumeratedFiles = EnumerateImages(folderPath);
                    if (!ascending)
                    {
                        enumeratedFiles.Reverse();
                    }

                    return enumeratedFiles;
                }).ConfigureAwait(false);

                if (form.IsDisposed || !form.IsHandleCreated)
                {
                    return;
                }

                form.BeginInvoke(new Action(() =>
                {
                    if (form.IsDisposed || generation != scanGeneration)
                    {
                        return;
                    }

                    try
                    {
                        ApplyImages(files, folderPath, iconSize, ascending, force);
                    }
                    catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
                    {
                        s_logger.LogFolderLoadError(ex, folderPath);
                    }
                }));
            }
            catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
            {
                s_logger.LogFolderLoadError(ex, folderPath);
            }
        }

        void ApplyImages(List<string> files, string folderPath, int iconSize, bool ascending, bool force)
        {
            var key = $"{folderPath}|{iconSize}|{ascending}|{files.Count}";
            if (!force && string.Equals(key, builtKey, StringComparison.Ordinal))
            {
                return;
            }

            builtKey = key;

            // 前回の生成要求を破棄する（古い結果が新しい一覧へ登録されないようにする）
            scheduler.Clear();
            requests.Clear();

            if (files.Count == 0)
            {
                list.ClearItems();
                countLabel.Text = ViewModeDisplay.GetItemCountText(0);
                folderView.ItemCount = 0;
                return;
            }

            list.ClearItems();
            list.ImageWidth = iconSize;
            foreach (var file in files)
            {
                list.Add(null, Path.GetFileName(file), toolTip: file, tag: file);
            }

            countLabel.Text = ViewModeDisplay.GetItemCountText(list.ItemCount);
            folderView.ItemCount = list.ItemCount;

            // 生成は背景へ依頼する（一覧は先に表示し、できた 1 件から差し替える）
            var capacity = Math.Max(list.ItemCount, list.ItemCount);
            for (var i = 0; i < files.Count; i++)
            {
                // 添字はループのたびに新しい変数へ写す（生成は後から走るため、ループ変数を捕まえると
                // すべての生成が最後の添字を指し、一覧の範囲外になって画像が反映されない）
                var index = i;
                var path = files[i];
                var request = scheduler.Enqueue(
                    ThumbnailPriority.Background(index),
                    token => ThumbnailGeneration.GenerateAsync(
                        list,
                        index,
                        () => ThumbnailImageLoader.Load(path, iconSize, capacity),
                        token));
                requests.Add((index, request));
            }

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

        // フォルダ内の画像ファイルを列挙する。
        List<string> EnumerateImages(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                return [];
            }

            try
            {
                var files = Directory.GetFiles(folderPath, "*.*")
                    .Where(EsImageFileExtensions.IsImageFile)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return files;
            }
            catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
            {
                // アクセス拒否・パスの消失などで一覧が空になる原因を追跡できるようにする
                s_logger.LogFolderLoadError(ex, folderPath);
                return [];
            }
        }
    }

    /// <summary>ファイルを OS の既定アプリで開く。</summary>
    /// <param name="filePath">対象のファイル パス。</param>
    /// <remarks>関連付けが無い・対象が消えている場合でも操作を妨げない（記録だけ行う）。</remarks>
    internal static void OpenInDefaultApp(string filePath)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            s_logger.LogOpenFileError(ex, filePath);
        }
    }
}
