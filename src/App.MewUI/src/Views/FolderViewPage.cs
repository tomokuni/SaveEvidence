using System.Diagnostics;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.MewUI.Logging;
using EsUtil.SaveEvidence.App.MewUI.Services;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>保存先フォルダの画像一覧を表示するページ（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>FolderViewPage.axaml</c> に相当する。<br/>
/// <br/>
/// <b>一覧</b>: 保存先フォルダの画像をサムネイル + ファイル名で並べる。 ウィンドウ幅に合わせて折り返す。<br/>
/// <b>表示モード</b>: 特大 / 大 / 中。<c>Settings.FolderViewModeIndex</c> へ保存し、次回起動時に復元する。<br/>
/// <b>ソート順</b>: 名前の昇順 / 降順。<c>Settings.FolderViewSortAscending</c> へ保存する。<br/>
/// <b>サムネイル</b>: 文字列とした画像の縮小デコードとキャッシュは <see cref="ImageThumbnailLoader"/> が所有する。<br/>
/// <b>画像の抽出</b>: 一覧に含める拡張子の判定は <see cref="ImageFileExtensions"/>（コア）が単一所有する。<br/>
/// <b>ダブルクリック</b>: ファイルを OS の既定アプリで開く（Avalonia 版と同一の挙動）。
/// </remarks>
internal static class FolderViewPage
{
    /// <summary>ロガー（一覧の読み込み失敗を記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(FolderViewPage));

    /// <summary>サムネイルとファイル名の間隔（DIP）。</summary>
    private const double ItemSpacing = 2;

    /// <summary>一覧の余白（DIP）。</summary>
    private const double ListPadding = 4;

    /// <summary>ファイル名領域の高さ（DIP）。</summary>
    /// <remarks>行内でサムネイルの下端を揃えるため、ファイル名は 2 行分の固定高とする（Avalonia 版と同じ）。</remarks>
    private const double LabelHeight = 30;

    /// <summary>ページの内容を生成する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <returns>ページの内容と、表示のたびに実行する処理。</returns>
    /// <remarks>
    /// 1. 3 行の Grid（ツールバー / 一覧 / ステータス）を組み立てる。<br/>
    /// 2. 一覧はスクロールビュー内の折り返しパネルへサムネイルを並べる。<br/>
    /// 3. 表示モード・ソート順の切り替えで設定を保存し、一覧を再構築する。<br/>
    /// 4. 表示のたびに保存済みの表示モード・ソート順を復元し、一覧を再構築する<br/>
    ///    （他 UI がファイルを増やした場合にも追従する）。<br/>
    /// 5. 保存先フォルダの解決結果が変わった時も一覧を再構築する。
    /// </remarks>
    public static PageContent Build(AppServices services)
    {
        var main = services.Main;
        var settingsService = services.Settings;
        var viewModel = services.FolderView;

        var list = new BottomAlignWrapPanel(ItemSpacing) { Padding = new Thickness(ListPadding) };

        // 一覧を再構築する（表示モード・ソート変更時、更新ボタン、ページ表示時に呼ぶ）
        void Rebuild()
        {
            var folderPath = main.SaveFolderPathResolved;
            viewModel.FolderPathText = folderPath;

            list.Clear();

            var files = LoadFiles(folderPath, viewModel.IsSortAscending);
            var iconSize = settingsService.Current.GetIconSize(viewModel.SelectedViewModeIndex);

            foreach (var file in files)
            {
                list.Add(CreateItem(file, iconSize));
            }

            viewModel.ItemCount = files.Count;
        }

        // 表示モードの切り替え（解除は許さない。 常に 1 つを選択状態に保つ）
        var modeButtons = new ToggleButton[ViewModeDisplay.Count];
        void OnModeChanged(int index, bool isChecked)
        {
            if (isChecked)
            {
                if (index == viewModel.SelectedViewModeIndex)
                {
                    return;
                }

                viewModel.SelectedViewModeIndex = index;
                SaveSettings(settingsService, viewModel);
                SyncToggles(modeButtons, viewModel.SelectedViewModeIndex);
                Rebuild();
                return;
            }

            if (index == viewModel.SelectedViewModeIndex)
            {
                modeButtons[index].IsChecked = true;
            }
        }

        for (var i = 0; i < modeButtons.Length; i++)
        {
            var index = i;
            modeButtons[i] = new ToggleButton()
                .Content(ViewModeDisplay.GetText(index))
                .Height(AppStyles.ToolbarButtonHeight)
                .Margin(new Thickness(1));
            modeButtons[i].OnCheckedChanged(isChecked => OnModeChanged(index, isChecked));
        }

        // ソート順の切り替え（解除は許さない）
        var sortButtons = new ToggleButton[2];
        void OnSortChanged(bool ascending, bool isChecked)
        {
            if (isChecked)
            {
                if (ascending == viewModel.IsSortAscending)
                {
                    return;
                }

                viewModel.IsSortAscending = ascending;
                SaveSettings(settingsService, viewModel);
                SyncToggles(sortButtons, ascending ? 0 : 1);
                Rebuild();
                return;
            }

            if (ascending == viewModel.IsSortAscending)
            {
                sortButtons[ascending ? 0 : 1].IsChecked = true;
            }
        }

        for (var i = 0; i < sortButtons.Length; i++)
        {
            var ascending = i == 0;
            sortButtons[i] = new ToggleButton()
                .Content(ascending ? "▲ 昇順" : "▼ 降順")
                .Height(AppStyles.ToolbarButtonHeight)
                .Margin(new Thickness(1));
            sortButtons[i].OnCheckedChanged(isChecked => OnSortChanged(ascending, isChecked));
        }

        var refresh = new Button()
            .Content("最新の情報に更新")
            .Height(AppStyles.ToolbarButtonHeight)
            .MinWidth(AppStyles.ToolbarButtonHeight)
            .Margin(new Thickness(1));
        refresh.Cursor(CursorType.Hand);
        refresh.OnClick(Rebuild);

        Element[] toolbarItems =
        [
            refresh,
            AppStyles.CreateToolbarDivider(),
            .. modeButtons,
            AppStyles.CreateToolbarDivider(),
            .. sortButtons,
        ];

        // ステータスバーは 4 項目。 保存先フォルダのみ右端へ寄せる（Avalonia 版と同じ配置）
        var status = new StackPanel()
            .Horizontal()
            .Spacing(12)
            .VerticalAlignment(VerticalAlignment.Center)
            .Children(
                new TextBlock().Bind(TextBlock.TextProperty, viewModel, static x => x.ItemCountText),
                new TextBlock().Bind(TextBlock.TextProperty, viewModel, static x => x.ViewModeText),
                new TextBlock().Bind(TextBlock.TextProperty, viewModel, static x => x.SortStatusText));

        var folderText = new TextBlock().Bind(TextBlock.TextProperty, viewModel, static x => x.FolderPathText);
        folderText.HorizontalAlignment = HorizontalAlignment.Right;
        folderText.VerticalAlignment = VerticalAlignment.Center;

        var statusGrid = new Grid().Columns("*,Auto");
        statusGrid.Add(status.Column(0));
        statusGrid.Add(folderText.Column(1));

        var scroll = new ScrollViewer().Content(list);

        // 保存先フォルダの解決結果が変わったら一覧を追従させる
        main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.SaveFolderPathResolved)
                or nameof(MainViewModel.SaveFolderPath))
            {
                Rebuild();
            }
        };

        // 設定から表示モード・ソート順を復元し、トグルと一覧を追従させる（ページ表示のたびに呼ぶ）
        void Restore()
        {
            viewModel.SelectedViewModeIndex = ViewModeDisplay.ClampIndex(settingsService.Current.FolderViewModeIndex);
            viewModel.IsSortAscending = settingsService.Current.FolderViewSortAscending;
            SyncToggles(modeButtons, viewModel.SelectedViewModeIndex);
            SyncToggles(sortButtons, viewModel.IsSortAscending ? 0 : 1);
            Rebuild();
        }

        // ツールバー（上端 6 DIP。 タイトルバー領域の下） / 一覧（残余） / ステータスの 3 段。
        // ツールバーはタイトルバー領域に重ねて置き、一覧はタイトルバー領域の下から始める（Avalonia 版と同じ）
        var toolbar = new StackPanel()
            .Horizontal()
            .Spacing(2)
            .Margin(new Thickness(2, AppStyles.ToolbarMarginTop, AppStyles.ToolbarMarginRight, AppStyles.ToolbarMarginBottom))
            .Children(toolbarItems);

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
        return new PageContent(content, Restore);
    }

    /// <summary>表示モード・ソート順を設定へ保存する。</summary>
    /// <param name="settingsService">設定サービス。</param>
    /// <param name="viewModel">現在の表示状態を持つ ViewModel。</param>
    /// <remarks>切り替え操作のたびに呼び出し、設定ファイルへ反映する（Avalonia 版と同一のタイミング）。</remarks>
    private static void SaveSettings(ISettingsService settingsService, FolderViewViewModel viewModel)
    {
        settingsService.Current.FolderViewModeIndex = viewModel.SelectedViewModeIndex;
        settingsService.Current.FolderViewSortAscending = viewModel.IsSortAscending;
        settingsService.Save();
    }

    /// <summary>トグルボタンの選択状態を選択中のインデックスへ揃える。</summary>
    /// <param name="buttons">対象のボタン群。</param>
    /// <param name="selectedIndex">選択中のインデックス。</param>
    private static void SyncToggles(ToggleButton[] buttons, int selectedIndex)
    {
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].IsChecked = i == selectedIndex;
        }
    }

    /// <summary>保存先フォルダの画像ファイルを読み込む。</summary>
    /// <param name="folderPath">対象のフォルダパス。 未解決の場合は空文字。</param>
    /// <param name="ascending">true の場合は名前の昇順、false の場合は降順。</param>
    /// <returns>並べ替え済みのファイルパス。 フォルダが無い・読めない場合は空。</returns>
    /// <remarks>
    /// フォルダ列挙で失敗してもページを表示できるよう、例外は空の一覧として扱う<br/>
    /// （アクセス拒否のフォルダを指定した場合にページごと落ちるのを防ぐ）。
    /// </remarks>
    private static List<string> LoadFiles(string folderPath, bool ascending)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            return [];
        }

        try
        {
            var files = Directory.GetFiles(folderPath, "*.*")
                .Where(static f => ImageFileExtensions.IsImageFile(f))
                .ToList();

            files.Sort(ascending
                ? StringComparer.OrdinalIgnoreCase
                : Comparer<string>.Create(static (a, b) => StringComparer.OrdinalIgnoreCase.Compare(b, a)));

            return files;
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 一覧が空になる原因を追跡できるよう記録する（表示は空の一覧で継続する）
            s_logger.LogFolderLoadError(ex, folderPath);
            return [];
        }
    }

    /// <summary>一覧の 1 件分の要素を生成する。</summary>
    /// <param name="filePath">対象のファイルパス。</param>
    /// <param name="iconSize">サムネイルの一辺のサイズ（DIP）。</param>
    /// <returns>サムネイルとファイル名を縦に並べた要素。</returns>
    /// <remarks>
    /// ダブルクリックで <paramref name="filePath"/> を OS の既定アプリで開く。<br/>
    /// サムネイルを読み込めない場合は背景のみを表示する（項目自体は残す）。
    /// </remarks>
    private static Element CreateItem(string filePath, int iconSize)
    {
        // 枠（背景色・枠線・角丸）はスタイルが与え、ホバーのハイライトはアイテム側が切り替える
        var frame = new Border()
            .StyleName(AppStyles.ThumbnailFrame)
            .Child(new ThumbnailView
            {
                RequestedSize = iconSize,
                Source = ImageThumbnailLoader.Load(filePath, iconSize),
            });

        // ラベルは枠（画像 + 影の余白）と同じ幅にし、ファイル名を画像の中央へ揃える
        // （文字は Avalonia 版と同じく領域の上端へ寄せる）
        var label = new Label()
            .Text(Path.GetFileName(filePath))
            .TextAlignment(TextAlignment.Center)
            .VerticalTextAlignment(TextAlignment.Top)
            .Width(iconSize + AppStyles.ThumbnailShadowBlur * 2)
            .Height(LabelHeight)
            .FontSize(11);
        label.Padding = new Thickness(1, 1, 2, 1);

        return ThumbnailItem.Create(frame, label, () => OpenInDefaultApp(filePath));
    }

    /// <summary>ファイルを OS の既定アプリで開く。</summary>
    /// <param name="filePath">対象のファイルパス。</param>
    /// <remarks>関連付けが無い等で開けない場合でも操作を妨げない（失敗は無視する）。</remarks>
    private static void OpenInDefaultApp(string filePath)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 関連付けが無い・アクセス拒否などの場合は何もしない
        }
    }
}
