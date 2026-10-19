using EsUtil.Feature.Thumbnail.WinUI3;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.WinUI3.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Storage;

namespace EsUtil.SaveEvidence.App.WinUI3.Views;

/// <summary>Undo 一覧ページ（WinUI 3 版）。</summary>
/// <remarks>
/// 編集履歴と現在の画像をサムネイルで一覧表示する。 表示順は Redo（新しい未来）→ 現在 → Undo（古い過去）で固定し、<br/>
/// 現在の画像は青い太枠で強調する。<br/>
/// <b>配置</b>: WinUI 3 の標準パネルに折り返しパネルが無いため、列数を 共通コントロール で<br/>
/// 計算して <c>Grid</c> へ配置する（幅の変化で並べ直す）。<br/>
/// <b>サムネイル</b>: 履歴が保持する画像をそのまま表示する（破棄しない）。
/// </remarks>
internal static class UndoPage
{
    /// <summary>ロガー（一時ファイルへの書き出しの失敗を記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(UndoPage));

    /// <summary>サムネイルの書き出しに使う画像ライター（表示用の一時ファイルを作る）。</summary>
    private static readonly IImageWriter s_writer = new EsBitmapImageWriter();

    /// <summary>ページの内容を生成する。</summary>
    /// <param name="services">アプリ全体で共有するコア オブジェクト。</param>
    /// <returns>ページの内容。</returns>
    /// <remarks>
    /// 1. ツールバー（表示モード）と一覧、ステータス バーを組み立てる。<br/>
    /// 2. 一覧は表示のたびに再構築する（履歴は撮影・切り出しのたびに変わるため）。<br/>
    /// 3. ダブルクリックで、その画像を一時ファイルへ書き出して OS の既定アプリで開く。
    /// </remarks>
    public static PageContent Build(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var viewModel = services.Main;
        var settingsService = services.Settings;
        var history = services.UndoHistory;

        var view = new UndoViewModel
        {
            SelectedViewModeIndex = ViewModeDisplay.ClampIndex(settingsService.Current.UndoViewModeIndex),
        };

        var iconSize = settingsService.Current.GetIconSize(view.SelectedViewModeIndex);

        // 一覧はフォルダビューと同じ共通コントロール（EsUtil.Feature.Thumbnail）を使う
        var list = new ThumbnailControl();

        var itemCountText = CreateStatusLabel(view.ItemCountText);
        var modeText = CreateStatusLabel(view.ViewModeText);
        var hintText = CreateStatusLabel("ダブルクリックで既定のアプリで開きます");
        hintText.HorizontalAlignment = HorizontalAlignment.Right;

        var modeButtons = new List<ToggleButton>();

        // 表示モードの切り替え（解除は許さない。 常に 1 つを選択状態に保つ）
        void OnModeChanged(int index, bool isChecked)
        {
            if (!isChecked)
            {
                if (index == view.SelectedViewModeIndex)
                {
                    modeButtons[index].IsChecked = true;
                }

                return;
            }

            if (index == view.SelectedViewModeIndex)
            {
                return;
            }

            view.SelectedViewModeIndex = index;
            settingsService.Current.UndoViewModeIndex = index;
            settingsService.Save();
            for (var i = 0; i < modeButtons.Count; i++)
            {
                modeButtons[i].IsChecked = i == index;
            }

            Rebuild();
        }

        for (var i = 0; i < ViewModeDisplay.Count; i++)
        {
            var index = i;
            var button = new ToggleButton
            {
                Content = ViewModeDisplay.GetText(index),
                IsChecked = index == view.SelectedViewModeIndex,
                MinWidth = 48,
                Height = AppStyles.ToolbarButtonHeight,
                Margin = new Thickness(1),
            };
            button.Checked += (_, _) => OnModeChanged(index, true);
            button.Unchecked += (_, _) => OnModeChanged(index, false);
            modeButtons.Add(button);
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

        toolbarItems.Children.Add(refresh);

        var toolbar = new ScrollViewer
        {
            Content = toolbarItems,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(AppStyles.ToolbarPaddingX, AppStyles.ToolbarPaddingY, AppStyles.ToolbarPaddingX, AppStyles.ToolbarPaddingY),
            Margin = new Thickness(0, AppStyles.ToolbarMarginTop, 0, AppStyles.ToolbarMarginBottom),
        };

        var status = BuildStatusBar(itemCountText, modeText, hintText);

        var page = new Grid();
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(toolbar, 0);
        Grid.SetRow(list, 1);
        Grid.SetRow(status, 2);
        page.Children.Add(toolbar);
        page.Children.Add(list);
        page.Children.Add(status);

        list.ItemActivated += (_, e) =>
        {
            if (e.Tag is EsReadOnlyBitmap image)
            {
                _ = OpenInDefaultAppAsync(image);
            }
        };

        void Rebuild()
        {
            iconSize = settingsService.Current.GetIconSize(view.SelectedViewModeIndex);
            list.ClearItems();
            list.ImageWidth = iconSize;

            // Redo（新しい未来）→ 現在 → Undo（古い過去）の順に並べる
            var redo = history.GetRedoHistory();
            for (var i = redo.Count - 1; i >= 0; i--)
            {
                var image = redo[i];
                list.Add(image, $"やり直し {redo.Count - i}", "履歴の画像", tag: image);
            }

            if (viewModel.PreviewImage is { } current)
            {
                list.Add(current, "現在", "現在の画像", isCurrent: true, tag: current);
            }

            var undo = history.GetUndoHistory();
            for (var i = undo.Count - 1; i >= 0; i--)
            {
                var image = undo[i];
                list.Add(image, $"元に戻す {undo.Count - i}", "履歴の画像", tag: image);
            }

            view.ItemCount = list.ItemCount;
            itemCountText.Text = view.ItemCountText;
            modeText.Text = view.ViewModeText;

        }

        return new PageContent(
            page,
            OnActivated: Rebuild,
            OnWarmUp: () =>
            {
                Rebuild();
                s_logger.LogThumbnailWarmUp("Undo 一覧", list.ItemCount);
            });
    }

    /// <summary>画像を一時ファイル（PNG）へ書き出し、OS の既定アプリで開く。</summary>
    /// <param name="image">対象の画像（借用。 破棄しない）。</param>
    /// <returns>待機タスク。</returns>
    /// <remarks>履歴の画像は保存先を持たないため、表示用の一時ファイルを作って開く。</remarks>
    private static async Task OpenInDefaultAppAsync(EsReadOnlyBitmap image)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"SaveEvidence-undo-{Guid.NewGuid():N}.png");
            s_writer.Save(image, path);

            var file = await StorageFile.GetFileFromPathAsync(path);
            _ = await Windows.System.Launcher.LaunchFileAsync(file);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            s_logger.LogThumbnailGenerateError(ex);
        }
    }

    /// <summary>ステータス バーを組み立てる。</summary>
    /// <param name="itemCountText">アイテム数の表示。</param>
    /// <param name="modeText">表示モードの表示。</param>
    /// <param name="hintText">操作のヒントの表示。</param>
    /// <returns>組み立てたステータス バー。</returns>
    private static FrameworkElement BuildStatusBar(TextBlock itemCountText, TextBlock modeText, TextBlock hintText)
    {
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(itemCountText);
        left.Children.Add(modeText);

        var grid = new Grid
        {
            Padding = new Thickness(AppStyles.StatusPaddingX, AppStyles.ListStatusPaddingY, AppStyles.StatusPaddingX, AppStyles.ListStatusPaddingY),
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(left, 0);
        Grid.SetColumn(hintText, 1);
        grid.Children.Add(left);
        grid.Children.Add(hintText);
        return grid;
    }

    /// <summary>ステータス バーのラベルを生成する。</summary>
    /// <param name="text">表示する文字列。</param>
    /// <returns>生成したラベル。</returns>
    private static TextBlock CreateStatusLabel(string text)
        => new() { Text = text, FontSize = AppStyles.StatusFontSize, VerticalAlignment = VerticalAlignment.Center };
}



