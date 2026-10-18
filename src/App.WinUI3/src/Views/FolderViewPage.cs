using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.WinUI3.Logging;
using EsUtil.SaveEvidence.App.WinUI3.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Storage;

namespace EsUtil.SaveEvidence.App.WinUI3.Views;

/// <summary>フォルダビュー ページ（WinUI 3 版）。</summary>
/// <remarks>
/// 保存先フォルダの画像をサムネイルで一覧表示する。 サムネイルの生成は背景で行い（<see cref="ThumbnailScheduler"/>）、<br/>
/// 一覧の表示を生成時間で待たせない。 表示モード（特大 / 大 / 中）とソート順は設定へ保存し、<br/>
/// ページを表示するたびに復元する。<br/>
/// <b>配置</b>: WinUI 3 の標準パネルに折り返しパネルが無いため、列数を <see cref="WrapLayoutMath"/> で<br/>
/// 計算して <c>Grid</c> へ配置する（幅の変化で並べ直す）。
/// </remarks>
internal static class FolderViewPage
{
    /// <summary>ロガー（一覧の読み込みの失敗を記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(FolderViewPage));

    /// <summary>ページの内容を生成する。</summary>
    /// <param name="services">アプリ全体で共有するコア オブジェクト。</param>
    /// <returns>ページの内容。</returns>
    /// <remarks>
    /// 1. ツールバー（表示モード・ソート順・最新の情報に更新）と一覧、ステータス バーを組み立てる。<br/>
    /// 2. 一覧は表示のたび（<c>OnActivated</c>）と起動後の先読み（<c>OnWarmUp</c>）で再構築する。<br/>
    /// 3. 表示中だけサムネイルの生成を全力にする（<c>OnActivated</c> / <c>OnDeactivated</c>）。
    /// </remarks>
    public static PageContent Build(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var main = services.Main;
        var settingsService = services.Settings;
        var viewModel = services.FolderView;
        var thumbnails = ThumbnailBackground.CreateScheduler();

        // 表示モードとソート順は設定から復元する
        viewModel.SelectedViewModeIndex = ViewModeDisplay.ClampIndex(settingsService.Current.FolderViewModeIndex);
        viewModel.IsSortAscending = settingsService.Current.FolderViewSortAscending;

        // 一覧は Undo 一覧と同じ共通コントロール（ThumbnailList）を使う
        var list = new ThumbnailList();
        var listHost = list.Host;

        // 生成中の要求と、その項目の一覧内の位置（見え方の変化で優先度を付け直す）
        var pending = new List<(ThumbnailScheduler.ThumbnailRequest Request, int Index)>();

        var itemCountText = CreateStatusLabel(viewModel.ItemCountText);
        var modeText = CreateStatusLabel(viewModel.ViewModeText);
        var sortText = CreateStatusLabel(viewModel.SortStatusText);
        var folderText = CreateStatusLabel(viewModel.FolderPathText);
        folderText.HorizontalAlignment = HorizontalAlignment.Right;
        folderText.TextTrimming = TextTrimming.CharacterEllipsis;

        var modeButtons = new List<ToggleButton>();
        var sortButtons = new List<ToggleButton>();

        // 表示モードの切り替え（解除は許さない。 常に 1 つを選択状態に保つ）
        void OnModeChanged(int index, bool isChecked)
        {
            if (!isChecked)
            {
                if (index == viewModel.SelectedViewModeIndex)
                {
                    modeButtons[index].IsChecked = true;
                }

                return;
            }

            if (index == viewModel.SelectedViewModeIndex)
            {
                return;
            }

            viewModel.SelectedViewModeIndex = index;
            settingsService.Current.FolderViewModeIndex = index;
            settingsService.Save();
            SynchronizeChecks(modeButtons, index);
            Rebuild();
        }

        for (var i = 0; i < ViewModeDisplay.Count; i++)
        {
            var index = i;
            var button = new ToggleButton
            {
                Content = ViewModeDisplay.GetText(index),
                IsChecked = index == viewModel.SelectedViewModeIndex,
                MinWidth = 48,
                Height = AppStyles.ToolbarButtonHeight,
                Margin = new Thickness(1),
            };
            button.Checked += (_, _) => OnModeChanged(index, true);
            button.Unchecked += (_, _) => OnModeChanged(index, false);
            modeButtons.Add(button);
        }

        // ソート順の切り替え（解除は許さない）
        void OnSortChanged(bool ascending, bool isChecked)
        {
            if (!isChecked)
            {
                if (ascending == viewModel.IsSortAscending)
                {
                    sortButtons[ascending ? 0 : 1].IsChecked = true;
                }

                return;
            }

            if (ascending == viewModel.IsSortAscending)
            {
                return;
            }

            viewModel.IsSortAscending = ascending;
            settingsService.Current.FolderViewSortAscending = ascending;
            settingsService.Save();
            SynchronizeChecks(sortButtons, ascending ? 0 : 1);
            Rebuild();
        }

        for (var i = 0; i < 2; i++)
        {
            var ascending = i == 0;
            var button = new ToggleButton
            {
                Content = ascending ? "▲ 昇順" : "▼ 降順",
                IsChecked = ascending == viewModel.IsSortAscending,
                Height = AppStyles.ToolbarButtonHeight,
                Margin = new Thickness(1),
            };
            button.Checked += (_, _) => OnSortChanged(ascending, true);
            button.Unchecked += (_, _) => OnSortChanged(ascending, false);
            sortButtons.Add(button);
        }

        var refresh = new Button
        {
            Content = "最新の情報に更新",
            Height = AppStyles.ToolbarButtonHeight,
            Margin = new Thickness(1),
        };
        refresh.Click += (_, _) => Rebuild();

        var toolbarItems = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        foreach (var button in modeButtons)
        {
            toolbarItems.Children.Add(button);
        }

        foreach (var button in sortButtons)
        {
            toolbarItems.Children.Add(button);
        }

        toolbarItems.Children.Add(refresh);

        var status = BuildStatusBar(itemCountText, modeText, sortText, folderText);
        var page = new Grid();
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var toolbar = new ScrollViewer
        {
            Content = toolbarItems,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(AppStyles.ToolbarPaddingX, AppStyles.ToolbarPaddingY, AppStyles.ToolbarPaddingX, AppStyles.ToolbarPaddingY),
            Margin = new Thickness(0, AppStyles.ToolbarMarginTop, 0, AppStyles.ToolbarMarginBottom),
        };


        Grid.SetRow(toolbar, 0);
        Grid.SetRow(listHost, 1);
        Grid.SetRow(status, 2);
        page.Children.Add(toolbar);
        page.Children.Add(listHost);
        page.Children.Add(status);

        // 見え方（スクロール・サイズ変更・行数の変化）が変わったら優先度を付け直す
        void UpdatePriorities()
        {
            if (pending.Count == 0)
            {
                return;
            }

            var top = list.Scroll.VerticalOffset;
            var viewportHeight = list.Scroll.ViewportHeight > 0 ? list.Scroll.ViewportHeight : listHost.ActualHeight;
            var bottom = top + viewportHeight;

            foreach (var (request, index) in pending)
            {
                if (index >= list.Tiles.Count)
                {
                    continue;
                }

                var element = list.Tiles[index];
                var itemTop = element
                    .TransformToVisual(list.Host)
                    .TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
                thumbnails.Reprioritize(
                    request, ThumbnailPriority.FromRange(itemTop, itemTop + element.ActualHeight, top, bottom, index));
            }
        }

        list.Scroll.ViewChanged += (_, _) => UpdatePriorities();

        // 一覧の幅が変わったら並べ直す（行の高さは行内の最大アイテム高さ・アイテムは下端揃え）
        void LayoutList(double availableWidth) => list.Layout(availableWidth);

        void Rebuild()
        {
            var iconSize = settingsService.Current.GetIconSize(viewModel.SelectedViewModeIndex);

            var folderPath = main.SaveFolderPathResolved;
            viewModel.FolderPathText = folderPath;
            folderText.Text = folderPath;

            thumbnails.Clear();
            pending.Clear();
            list.Clear();

            var files = ReadFiles(folderPath, viewModel.IsSortAscending, s_logger);
            var capacity = Math.Max(AppStyles.ThumbnailCacheCapacity, files.Count);

            for (var index = 0; index < files.Count; index++)
            {
                var file = files[index];

                // 高さを元画像の縦横比で決める（作る時点で確定させ、生成後に大きさを変えない）
                var (pixelWidth, pixelHeight) = ImageSizeReader.Read(file, iconSize);
                var view = new ThumbnailView
                {
                    Width = iconSize,
                    Height = ThumbnailTileMetrics.ImageHeight(iconSize, pixelWidth, pixelHeight),
                    Source = ThumbnailBackground.Placeholder(iconSize),
                };

                list.Add(view, Path.GetFileName(file), iconSize, file, () => _ = OpenInDefaultAppAsync(file));

                // 生成が終わるまでは代替画像を表示し、生成は背景へ依頼する
                // （見えている項目の優先度は、レイアウトの確定後に付け直す）
                pending.Add((
                    thumbnails.Enqueue(
                        ThumbnailPriority.Background(index),
                        MakeThumbnailWork(view, file, iconSize, capacity)),
                    index));
            }

            viewModel.ItemCount = files.Count;
            itemCountText.Text = viewModel.ItemCountText;
            modeText.Text = viewModel.ViewModeText;
            sortText.Text = viewModel.SortStatusText;

            LayoutList(listHost.ActualWidth);
        }

        listHost.SizeChanged += (_, e) => LayoutList(e.NewSize.Width);

        return new PageContent(
            page,
            OnActivated: () =>
            {
                thumbnails.IsActive = true;
                SynchronizeChecks(modeButtons, viewModel.SelectedViewModeIndex);
                SynchronizeChecks(sortButtons, viewModel.IsSortAscending ? 0 : 1);
                Rebuild();
            },
            OnDeactivated: () => thumbnails.IsActive = false,
            OnWarmUp: () =>
            {
                // 表示前に一覧を構築し、低負荷で生成を始める（キャプチャ ページの操作は妨げない）
                Rebuild();
                s_logger.LogThumbnailWarmUp("フォルダビュー", list.Tiles.Count);
            });
    }

    /// <summary>画像ファイルを名前順で列挙する。</summary>
    /// <param name="folderPath">対象のフォルダ パス。</param>
    /// <param name="isSortAscending">true の場合は昇順。</param>
    /// <param name="logger">ロガー（読み込みの失敗を記録する）。</param>
    /// <returns>画像ファイルのパスの一覧。 読み込めない場合は空。</returns>
    private static IReadOnlyList<string> ReadFiles(string folderPath, bool isSortAscending, ILogger logger)
    {
        try
        {
            return FolderFileListReader.Read(folderPath, isSortAscending);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            logger.LogFolderLoadError(ex, folderPath);
            return [];
        }
    }

    /// <summary>サムネイルを背景で生成し、UI スレッドで表示へ反映する処理を作る。</summary>
    /// <param name="view">反映先の表示要素。</param>
    /// <param name="filePath">対象のファイル パス。</param>
    /// <param name="iconSize">サムネイルの一辺（ピクセル）。</param>
    /// <param name="capacity">サムネイル キャッシュの最大保持数。</param>
    /// <returns>背景で実行する処理。</returns>
    /// <remarks>
    /// 表示要素の大きさは作る時点で決めてある（<see cref="ImageSizeReader"/>）ため、ここでは画像だけを差し替える。<br/>
    /// 大きさを変えると縮小キャンバスの表面が作り直されて内容が消えるため、生成後に寸法は変えない。
    /// </remarks>
    private static Func<CancellationToken, ValueTask> MakeThumbnailWork(
        ThumbnailView view, string filePath, int iconSize, int capacity)
        => _ =>
        {
            var thumbnail = ImageThumbnailLoader.Load(filePath, iconSize, capacity);
            if (thumbnail is not null)
            {
                ThumbnailBackground.Post(() => view.Source = thumbnail);
            }

            return ValueTask.CompletedTask;
        };

    /// <summary>ファイルを OS の既定アプリで開く。</summary>
    /// <param name="filePath">対象のファイル パス。</param>
    /// <returns>待機タスク。</returns>
    /// <remarks>失敗しても一覧の表示は継続する（利用者には通知しない。 原因はログへ残す）。</remarks>
    private static async Task OpenInDefaultAppAsync(string filePath)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(filePath);
            _ = await Windows.System.Launcher.LaunchFileAsync(file);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            s_logger.LogFolderLoadError(ex, filePath);
        }
    }

    /// <summary>トグル ボタンの選択状態を 1 つだけ true に揃える。</summary>
    /// <param name="buttons">対象のボタン。</param>
    /// <param name="selectedIndex">選択する位置。</param>
    private static void SynchronizeChecks(IReadOnlyList<ToggleButton> buttons, int selectedIndex)
    {
        for (var i = 0; i < buttons.Count; i++)
        {
            buttons[i].IsChecked = i == selectedIndex;
        }
    }

    /// <summary>ステータス バーを組み立てる。</summary>
    /// <param name="itemCountText">アイテム数の表示。</param>
    /// <param name="modeText">表示モードの表示。</param>
    /// <param name="sortText">ソート状態の表示。</param>
    /// <param name="folderText">フォルダ パスの表示。</param>
    /// <returns>組み立てたステータス バー。</returns>
    private static FrameworkElement BuildStatusBar(
        TextBlock itemCountText, TextBlock modeText, TextBlock sortText, TextBlock folderText)
    {
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(itemCountText);
        left.Children.Add(modeText);
        left.Children.Add(sortText);

        var grid = new Grid
        {
            Padding = new Thickness(AppStyles.StatusPaddingX, AppStyles.ListStatusPaddingY, AppStyles.StatusPaddingX, AppStyles.ListStatusPaddingY),
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(left, 0);
        Grid.SetColumn(folderText, 1);
        grid.Children.Add(left);
        grid.Children.Add(folderText);
        return grid;
    }

    /// <summary>ステータス バーのラベルを生成する。</summary>
    /// <param name="text">表示する文字列。</param>
    /// <returns>生成したラベル。</returns>
    private static TextBlock CreateStatusLabel(string text)
        => new() { Text = text, FontSize = AppStyles.StatusFontSize, VerticalAlignment = VerticalAlignment.Center };
}











