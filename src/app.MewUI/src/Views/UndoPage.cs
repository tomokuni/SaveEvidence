using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Estable.Platform.Imaging;
using Estable.SaveEvidence.Models;
using Estable.SaveEvidence.Services;
using Estable.SaveEvidence.ViewModels;
using SaveEvidence.MewUI.Services;
using System.Diagnostics;

namespace SaveEvidence.MewUI.Views;

/// <summary>Undo / Redo 履歴の画像を一覧表示するページ（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>UndoPage.axaml</c> に相当する。<br/>
/// <br/>
/// <b>表示順</b>: Redo（新しい未来）→ 現在 → Undo（古い過去）の固定。現在プレビューに表示中の画像を青枠で強調する。<br/>
/// <b>表示モード</b>: 特大 / 大 / 中。<c>Settings.UndoViewModeIndex</c> へ保存・復元し、名前と範囲の補正は<br/>
/// <see cref="ViewModeDisplay"/>（コア）が単一所有する。<br/>
/// <b>サムネイル</b>: <see cref="ThumbnailView"/> が画像を正方形へ収めて描画する。<br/>
/// <b>ダブルクリック</b>: 画像を一時ファイルへ書き出し、OS の既定アプリで開く。<br/>
/// <b>画像の所有権</b>: 一覧は <see cref="UndoStack"/> と <see cref="MainViewModel"/> が保持する画像を参照するだけで、破棄しない。
/// </remarks>
internal static class UndoPage
{
    /// <summary>サムネイルとラベルの間隔（DIP）。</summary>
    private const double ItemSpacing = 2;

    /// <summary>一覧の余白（DIP）。</summary>
    private const double ListPadding = 4;

    /// <summary>ラベル領域の高さ（DIP）。</summary>
    /// <remarks>行内でサムネイルの下端を揃えるため、ラベルは 2 行分の固定高とする（Avalonia 版と同じ）。</remarks>
    private const double LabelHeight = 30;

    /// <summary>画像を一時ファイルへ書き出すライター（ダブルクリックで外部ツールを使う場合に限る）。</summary>
    private static readonly IImageWriter s_writer = new MewUIImageWriter();

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
    public static PageContent Build(MainViewModel viewModel, ISettingsService settingsService, UndoStack undoStack)
    {
        // 表示モードはラムダ間で共有するためローカルに保持する（設定が単一所有元）
        var selectedIndex = ViewModeDisplay.ClampIndex(settingsService.Current.UndoViewModeIndex);

        var list = new BottomAlignWrapPanel(ItemSpacing) { Padding = new Thickness(ListPadding) };

        var countText = new ObservableValue<string>(ViewModeDisplay.GetItemCountText(0));
        var modeText = new ObservableValue<string>(ViewModeDisplay.GetText(selectedIndex));

        var modeButtons = new ToggleButton[ViewModeDisplay.Count];

        // 表示モードの切り替え（解除は許さない。常に 1 つを選択状態に保つ）
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
        void Rebuild()
        {
            var iconSize = settingsService.Current.GetIconSize(selectedIndex);

            list.Clear();

            // 表示順を固定する: Redo（現在に近い未来から）→ 現在 → Undo（現在の直前から過去へ）
            var redo = undoStack.GetRedoHistory();
            for (var i = 0; i < redo.Count; i++)
            {
                list.Add(CreateItem(redo[i], $"Redo {i + 1}", isCurrent: false, iconSize));
            }

            if (viewModel.PreviewImage is { } current)
            {
                list.Add(CreateItem(current, "現在", isCurrent: true, iconSize));
            }

            var undo = undoStack.GetUndoHistory();
            for (var i = undo.Count - 1; i >= 0; i--)
            {
                list.Add(CreateItem(undo[i], $"Undo {undo.Count - i}", isCurrent: false, iconSize));
            }

            countText.Value = ViewModeDisplay.GetItemCountText(list.Count);
            modeText.Value = ViewModeDisplay.GetText(selectedIndex);
        }

        // 現在の画像の差し替えで青枠とサムネイルを追従させる
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.PreviewImage))
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

        // ステータスバーは 3 項目。右端のヒントのみ右寄せにする（Avalonia 版と同じ配置）
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

        // 設定から表示モードを復元し、トグルと一覧を追従させる（ページ表示のたびに呼ぶ）
        void Restore()
        {
            selectedIndex = ViewModeDisplay.ClampIndex(settingsService.Current.UndoViewModeIndex);
            SyncModeButtons();
            Rebuild();
        }

        // ツールバー（上端 6 DIP。タイトルバー領域の下） / 一覧（残余） / ステータスの 3 段。
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
        return new PageContent(content, Restore);
    }

    /// <summary>サムネイル 1 件分の要素を生成する。</summary>
    /// <param name="image">サムネイルの元画像（所有権はスタック側が保持する。本ページは破棄しない）。</param>
    /// <param name="label">サムネイル下のラベル。</param>
    /// <param name="isCurrent">現在プレビューに表示中の画像かどうか（true の場合は青枠で強調する）。</param>
    /// <param name="iconSize">サムネイルの一辺のサイズ（DIP）。</param>
    /// <returns>サムネイルとラベルを縦に並べた要素。</returns>
    /// <remarks>
    /// ダブルクリックで <paramref name="image"/> を一時ファイルへ書き出し、OS の既定アプリで開く。
    /// </remarks>
    private static Element CreateItem(EsBitmap image, string label, bool isCurrent, int iconSize)
    {
        // 枠（背景色・枠線・角丸）はスタイルが与え、ホバーのハイライトはアイテム側が切り替える
        var frame = new Border()
            .StyleName(isCurrent ? AppStyles.ThumbnailFrameCurrent : AppStyles.ThumbnailFrame)
            .Child(new ThumbnailView { RequestedSize = iconSize, Source = image });

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
    private static void OpenInDefaultApp(EsBitmap image)
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
        catch (Exception)
        {
            // 外部ツールで開けない場合でも操作を妨げない（拡張子の関連付けが無い環境など）
        }
    }
}
