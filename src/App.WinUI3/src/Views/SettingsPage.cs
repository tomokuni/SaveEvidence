using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.WinUI3.Logging;
using EsUtil.SaveEvidence.App.WinUI3.Services;
using EsUtil.SaveEvidence.Platform.WinUI3;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace EsUtil.SaveEvidence.App.WinUI3.Views;

/// <summary>設定ページ（WinUI 3 版）。</summary>
/// <remarks>
/// 設定は変更のたびに即時保存する（保存ボタンを持たない）。 セクションと項目の構成は他の UI 版と同じにする。<br/>
/// <b>セクション</b>: テーマ / 保存 / 画像 / キャプチャ / 矩形選択範囲の制限 / ホットキー / 外観。<br/>
/// <b>外観の入力</b>: 真偽値はチェック ボックス、数値は増減入力、色は色ピッカー（現在値の表示付き）を使う。<br/>
/// <b>ファイル名テンプレート</b>はキャプチャ ページのツールバーで編集する（設定ページには置かない）。
/// </remarks>
internal static class SettingsPage
{
    /// <summary>ロガー（保存先の選択などの失敗を記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(SettingsPage));

    /// <summary>ページの内容を生成する。</summary>
    /// <param name="services">アプリ全体で共有するコア オブジェクト。</param>
    /// <param name="window">設定の対象ウィンドウ（保存先の選択とテーマの適用に使う）。</param>
    /// <returns>ページの内容。</returns>
    public static PageContent Build(AppServices services, MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(window);

        var settings = services.Settings.Current;
        var viewModel = services.Main;

        var root = new StackPanel { Spacing = 12, Padding = new Thickness(12) };
        root.Children.Add(BuildThemeSection(window, settings));
        root.Children.Add(BuildSaveSection(window, viewModel, settings));
        root.Children.Add(BuildImageSection(settings));
        root.Children.Add(BuildCaptureSection(settings));
        root.Children.Add(BuildSelectionLimitSection(settings));
        root.Children.Add(BuildHotKeySection(settings));
        root.Children.Add(BuildAppearanceSection(settings));

        var page = new ScrollViewer
        {
            Content = root,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        return new PageContent(page);
    }

    /// <summary>テーマのセクションを生成する。</summary>
    /// <param name="window">テーマを適用するウィンドウ。</param>
    /// <param name="settings">設定。</param>
    /// <returns>生成したセクション。</returns>
    /// <remarks>テーマの変更は即時に反映する（再起動を要しない）。</remarks>
    private static FrameworkElement BuildThemeSection(MainWindow window, Settings settings)
    {
        var modes = MainViewModel.ThemeModes;
        var options = modes.Select(GetThemeText).ToArray();
        var selected = Math.Max(0, modes.ToList().IndexOf(settings.ThemeMode));

        var choice = ChoiceRow(options, selected, index =>
        {
            settings.ThemeMode = modes[index];
            AppStyles.ApplyTheme(window.Content as FrameworkElement, settings.ThemeMode);
        });

        return Section("外観テーマ設定", Row("テーマ", choice));
    }

    /// <summary>テーマの表示名を取得する。</summary>
    /// <param name="mode">テーマ。</param>
    /// <returns>表示名。</returns>
    private static string GetThemeText(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => "ライト",
        ThemeMode.Dark => "ダーク",
        _ => "システム",
    };

    /// <summary>保存先のセクションを生成する。</summary>
    /// <param name="window">フォルダ選択のオーナー ウィンドウ。</param>
    /// <param name="viewModel">アプリのメイン ViewModel（解決済みのパスの保持元）。</param>
    /// <param name="settings">設定。</param>
    /// <returns>生成したセクション。</returns>
    private static FrameworkElement BuildSaveSection(MainWindow window, MainViewModel viewModel, Settings settings)
    {
        var box = new TextBox { Text = settings.SaveFolderPath, MinWidth = 320 };
        box.TextChanged += (_, _) => settings.SaveFolderPath = box.Text;

        var browse = new Button { Content = "参照...", Margin = new Thickness(4, 0, 0, 0) };
        browse.Click += (_, _) => _ = BrowseFolderAsync(window, box);

        var pathRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        pathRow.Children.Add(box);
        pathRow.Children.Add(browse);

        // 解決できない保存先は赤枠で知らせる
        void ApplyValidity()
        {
            if (viewModel.HasInvalidSaveFolder)
            {
                box.BorderBrush = new SolidColorBrush(Colors.Red);
                return;
            }

            box.ClearValue(Control.BorderBrushProperty);
        }

        ApplyValidity();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.HasInvalidSaveFolder), StringComparison.Ordinal))
            {
                ApplyValidity();
            }
        };

        return Section("保存設定", Row("保存先フォルダ", pathRow));
    }

    /// <summary>画像のセクションを生成する。</summary>
    /// <param name="settings">設定。</param>
    /// <returns>生成したセクション。</returns>
    private static FrameworkElement BuildImageSection(Settings settings)
        => Section(
            "画像設定",
            Row("画像を中央に配置", CheckRow(settings.CenterAlign, value => settings.CenterAlign = value, null)));

    /// <summary>キャプチャのセクションを生成する。</summary>
    /// <param name="settings">設定。</param>
    /// <returns>生成したセクション。</returns>
    private static FrameworkElement BuildCaptureSection(Settings settings)
        => Section(
            "キャプチャ設定",
            Row(
                "キャプチャ時にメイン ウィンドウを隠す",
                CheckRow(settings.HideMainWindowOnCapture, value => settings.HideMainWindowOnCapture = value, null)));

    /// <summary>矩形選択範囲の制限のセクションを生成する。</summary>
    /// <param name="settings">設定。</param>
    /// <returns>生成したセクション。</returns>
    private static FrameworkElement BuildSelectionLimitSection(Settings settings)
        => Section(
            "矩形選択範囲の制限",
            Row("画像外の選択を禁止", CheckRow(settings.ClampToSourceBounds, value => settings.ClampToSourceBounds = value, null)));

    /// <summary>ホットキーのセクションを生成する。</summary>
    /// <param name="settings">設定。</param>
    /// <returns>生成したセクション。</returns>
    /// <remarks>変更は次回起動時に反映する（OS への登録は起動時に 1 度だけ行う）。</remarks>
    private static FrameworkElement BuildHotKeySection(Settings settings)
        => Section(
            "ホットキー設定",
            HotKeyRow("スクリーン選択", () => settings.SelectScreenHotKey, value => settings.SelectScreenHotKey = value),
            HotKeyRow("ウィンドウ選択", () => settings.WindowSelectHotKey, value => settings.WindowSelectHotKey = value),
            HotKeyRow("範囲選択", () => settings.AreaSelectHotKey, value => settings.AreaSelectHotKey = value));

    /// <summary>外観のセクションを生成する。</summary>
    /// <param name="settings">設定。</param>
    /// <returns>生成したセクション。</returns>
    /// <remarks>見出しの右端に「デフォルト値に戻す」を置く。</remarks>
    private static FrameworkElement BuildAppearanceSection(Settings settings)
    {
        var reset = new Button { Content = "デフォルト値に戻す" };
        reset.Click += (_, _) => settings.ResetAppearanceToDefaults();

        (string Category, (string Name, FrameworkElement Control)[] Items)[] categories =
        [
            ("ラベル設定",
            [
                ("", CheckRow(settings.SizeLabelVisible, value => settings.SizeLabelVisible = value, "選択矩形のサイズを表示")),
                ("", CheckRow(settings.ShowSubLines, value => settings.ShowSubLines = value, "矩形操作を矩形内に表示")),
                ("", CheckRow(settings.InfoLabelVisible, value => settings.InfoLabelVisible = value, "選択中のウィンドウのタイトルを表示")),
                ("", CheckRow(settings.ShowCoordLabel, value => settings.ShowCoordLabel = value, "矩形操作時にカーソル位置の座標をカーソル横に表示")),
                ("", CheckRow(settings.LoupeShowCoordLabel, value => settings.LoupeShowCoordLabel = value, "ルーペの中心座標をルーペ上部に表示")),
            ]),
            ("選択枠・暗転",
            [
                ("選択枠の色", ColorRow(settings.CaptureBorderColor, value => settings.CaptureBorderColor = value)),
                ("選択枠の太さ", NumberRow(settings.CaptureBorderThickness, 0.5, 8.0, 0.5, false, value => settings.CaptureBorderThickness = value)),
                ("選択矩形の最小サイズ", NumberRow(settings.MinSelectionSize, 1, 100, 1, true, value => settings.MinSelectionSize = (int)value)),
                ("暗転オーバーレイの色", ColorRow(settings.CaptureOverlayColor, value => settings.CaptureOverlayColor = value)),
            ]),
            ("ドラッグハンドル・マーカー",
            [
                ("ハンドルの色", ColorRow(settings.HandleColor, value => settings.HandleColor = value)),
                ("ハンドルのサイズ", NumberRow(settings.HandleSize, 2, 30, 1, false, value => settings.HandleSize = value)),
                ("ハンドル判定半径", NumberRow(settings.HandleHitRadius, 2, 40, 1, false, value => settings.HandleHitRadius = value)),
                ("マーカーの色", ColorRow(settings.MarkerColor, value => settings.MarkerColor = value)),
                ("マーカーの長さ", NumberRow(settings.MarkerLength, 8, 40, 2, false, value => settings.MarkerLength = value)),
            ]),
            ("ルーペ",
            [
                ("枠色", ColorRow(settings.LoupeFrameColor, value => settings.LoupeFrameColor = value)),
                ("枠の太さ", NumberRow(settings.LoupeFrameWidth, 1, 10, 1, true, value => settings.LoupeFrameWidth = (int)value)),
                ("十字線色", ColorRow(settings.LoupeCrossColor, value => settings.LoupeCrossColor = value)),
                ("画像外の色", ColorRow(settings.LoupePasteboardColor, value => settings.LoupePasteboardColor = value)),
                ("選択矩形破線の色A", ColorRow(settings.LoupeSelectionRectColorA, value => settings.LoupeSelectionRectColorA = value)),
                ("選択矩形破線の色B", ColorRow(settings.LoupeSelectionRectColorB, value => settings.LoupeSelectionRectColorB = value)),
                ("拡大率", NumberRow(settings.LoupeZoomLevel, 4, 32, 2, true, value => settings.LoupeZoomLevel = (int)value)),
                ("サイズ", NumberRow(settings.LoupeSize, 64, 512, 8, true, value => settings.LoupeSize = (int)value)),
                ("カーソルからのオフセット", NumberRow(settings.LoupeOffset, 0, 200, 4, false, value => settings.LoupeOffset = value)),
            ]),
            ("選択矩形内のラベル",
            [
                ("1 行目 テキスト色", ColorRow(settings.CaptureTextColor, value => settings.CaptureTextColor = value)),
                ("1 行目 背景色", ColorRow(settings.SizeLabelBackgroundColor, value => settings.SizeLabelBackgroundColor = value)),
                ("1 行目 フォントサイズ", NumberRow(settings.SizeLabelFontSize, 8, 32, 1, false, value => settings.SizeLabelFontSize = value)),
                ("2 行目 テキスト色", ColorRow(settings.SubLineColor, value => settings.SubLineColor = value)),
                ("2 行目 背景色", ColorRow(settings.SubLineBackgroundColor, value => settings.SubLineBackgroundColor = value)),
                ("2 行目 フォントサイズ", NumberRow(settings.SubLineFontSize, 8, 32, 1, false, value => settings.SubLineFontSize = value)),
                ("3 行目 テキスト色", ColorRow(settings.InfoLabelColor, value => settings.InfoLabelColor = value)),
                ("3 行目 背景色", ColorRow(settings.InfoLabelBackgroundColor, value => settings.InfoLabelBackgroundColor = value)),
                ("3 行目 フォントサイズ", NumberRow(settings.InfoLabelFontSize, 8, 32, 1, false, value => settings.InfoLabelFontSize = value)),
            ]),
            ("カーソル位置の座標ラベル",
            [
                ("テキスト色", ColorRow(settings.CoordLabelColor, value => settings.CoordLabelColor = value)),
                ("背景色", ColorRow(settings.CoordLabelBackgroundColor, value => settings.CoordLabelBackgroundColor = value)),
                ("フォントサイズ", NumberRow(settings.CoordLabelFontSize, 6, 48, 1, false, value => settings.CoordLabelFontSize = value)),
            ]),
            ("ルーペの中心座標ラベル",
            [
                ("座標ラベルのテキスト色", ColorRow(settings.LoupeCoordLabelColor, value => settings.LoupeCoordLabelColor = value)),
                ("座標ラベルの背景色", ColorRow(settings.LoupeCoordLabelBackgroundColor, value => settings.LoupeCoordLabelBackgroundColor = value)),
                ("座標ラベルのフォントサイズ", NumberRow(settings.LoupeCoordLabelFontSize, 6, 48, 1, false, value => settings.LoupeCoordLabelFontSize = value)),
            ]),
        ];

        return SectionWithAction("外観設定", reset, BuildAppearanceGrid(categories));
    }

    /// <summary>外観設定のカテゴリと項目を 3 列のグリッドへ並べる。</summary>
    /// <param name="categories">カテゴリと項目（項目名が空の項目はチェック ボックスとして 2 列分を使う）。</param>
    /// <returns>グリッド。</returns>
    /// <remarks>
    /// 3 列（カテゴリ / 項目名 / 設定値）のグリッドにし、列幅を全カテゴリで共有する。<br/>
    /// カテゴリ名は左列を縦に結合して右寄せし、カテゴリの切れ目には全列をまたぐ 1 DIP の水平線を入れる。
    /// </remarks>
    private static FrameworkElement BuildAppearanceGrid(
        (string Category, (string Name, FrameworkElement Control)[] Items)[] categories)
    {
        var grid = new Grid { ColumnSpacing = 5, RowSpacing = 5 };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // 行番号: 先頭に区切り線の行を 1 つ入れる
        var row = 1;
        for (var c = 0; c < categories.Length; c++)
        {
            var category = categories[c];
            if (c > 0)
            {
                var separator = new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromArgb(64, 128, 128, 128)),
                    Margin = new Thickness(0, 5, 0, 5),
                };
                Grid.SetRow(separator, row);
                Grid.SetColumn(separator, 0);
                Grid.SetColumnSpan(separator, 3);
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.Children.Add(separator);
                row++;
            }

            var startRow = row;
            foreach (var (name, control) in category.Items)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                if (string.IsNullOrEmpty(name))
                {
                    // 項目名が無い項目（チェック ボックス）は 2 列分を使って文言を表示する
                    Grid.SetRow(control, row);
                    Grid.SetColumn(control, 1);
                    Grid.SetColumnSpan(control, 2);
                    grid.Children.Add(control);
                }
                else
                {
                    var label = new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetRow(label, row);
                    Grid.SetColumn(label, 1);
                    Grid.SetRow(control, row);
                    Grid.SetColumn(control, 2);
                    grid.Children.Add(label);
                    grid.Children.Add(control);
                }

                row++;
            }

            var categoryLabel = new TextBlock
            {
                Text = category.Category,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 5, 0, 0),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            };
            Grid.SetRow(categoryLabel, startRow);
            Grid.SetColumn(categoryLabel, 0);
            Grid.SetRowSpan(categoryLabel, row - startRow);
            grid.Children.Add(categoryLabel);
        }

        return grid;
    }

    /// <summary>セクション（枠付きの見出しと行）を生成する。</summary>
    /// <param name="title">見出し。</param>
    /// <param name="rows">行の並び。</param>
    /// <returns>生成したセクション。</returns>
    private static FrameworkElement Section(string title, params FrameworkElement[] rows) => SectionCore(title, null, rows);

    /// <summary>見出しの右端に操作を持つセクションを生成する。</summary>
    /// <param name="title">見出し。</param>
    /// <param name="action">見出しの右端に置く操作。</param>
    /// <param name="rows">行の並び。</param>
    /// <returns>生成したセクション。</returns>
    private static FrameworkElement SectionWithAction(string title, FrameworkElement action, params FrameworkElement[] rows)
        => SectionCore(title, action, rows);

    /// <summary>セクションの共通の組み立てを行う。</summary>
    /// <param name="title">見出し。</param>
    /// <param name="action">見出しの右端に置く操作。 null の場合は置かない。</param>
    /// <param name="rows">行の並び。</param>
    /// <returns>生成したセクション。</returns>
    /// <remarks>カードは背景を持たず 1 DIP の枠だけを描く（他の UI 版と同じ）。</remarks>
    private static FrameworkElement SectionCore(string title, FrameworkElement? action, params FrameworkElement[] rows)
    {
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleText = new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(titleText, 0);
        header.Children.Add(titleText);

        if (action is not null)
        {
            Grid.SetColumn(action, 1);
            header.Children.Add(action);
        }

        var content = new StackPanel { Spacing = 5, Margin = new Thickness(0, 6, 0, 0) };
        content.Children.Add(header);
        foreach (var row in rows)
        {
            content.Children.Add(row);
        }

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(64, 128, 128, 128)),
            Padding = new Thickness(8),
            Child = content,
        };
    }

    /// <summary>ラベルと設定値の行を生成する。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="control">設定値の入力。</param>
    /// <returns>生成した行。</returns>
    private static FrameworkElement Row(string label, FrameworkElement control)
    {
        var grid = new Grid { ColumnSpacing = 5 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelText = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, MinWidth = 170 };
        Grid.SetColumn(labelText, 0);
        Grid.SetColumn(control, 1);
        grid.Children.Add(labelText);
        grid.Children.Add(control);
        return grid;
    }

    /// <summary>チェック ボックスの行を生成する。</summary>
    /// <param name="initial">初期値。</param>
    /// <param name="apply">変更を設定へ反映する処理。</param>
    /// <param name="text">チェック ボックスの文言。 null の場合は空。</param>
    /// <returns>生成したチェック ボックス。</returns>
    private static FrameworkElement CheckRow(bool initial, Action<bool> apply, string? text)
    {
        var check = new CheckBox { IsChecked = initial, Content = text ?? string.Empty };
        check.Checked += (_, _) => apply(true);
        check.Unchecked += (_, _) => apply(false);
        return check;
    }

    /// <summary>数値入力の行を生成する。</summary>
    /// <param name="initial">初期値。</param>
    /// <param name="minimum">最小値。</param>
    /// <param name="maximum">最大値。</param>
    /// <param name="step">増減の単位。</param>
    /// <param name="isInteger">整数のみを受け付ける場合は true。</param>
    /// <param name="apply">変更を設定へ反映する処理。</param>
    /// <returns>生成した数値入力。</returns>
    /// <remarks>範囲外の値は入力できない（増減の範囲を制限する）。</remarks>
    private static FrameworkElement NumberRow(double initial, double minimum, double maximum, double step, bool isInteger, Action<double> apply)
    {
        var box = new NumberBox
        {
            Value = initial,
            Minimum = minimum,
            Maximum = maximum,
            SmallChange = step,
            LargeChange = step * 5,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 120,
        };

        box.ValueChanged += (_, e) =>
        {
            if (double.IsNaN(e.NewValue))
            {
                return;
            }

            var value = isInteger ? Math.Round(e.NewValue) : e.NewValue;
            apply(value);
        };

        return box;
    }

    /// <summary>色入力の行を生成する。</summary>
    /// <param name="initial">初期値（<c>#AARRGGBB</c>）。</param>
    /// <param name="apply">変更を設定へ反映する処理。</param>
    /// <returns>生成した色入力。</returns>
    /// <remarks>現在値を色見本と 16 進文字列で表示し、クリックで色ピッカーを開く。</remarks>
    private static FrameworkElement ColorRow(string initial, Action<string> apply)
    {
        var argb = SettingsColorMath.ToArgb(initial, 0xFF000000);
        var swatch = new Border
        {
            Width = 20,
            Height = 20,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Colors.Gray),
            Background = new SolidColorBrush(SettingsColorMath.ToColor(argb)),
        };

        var text = new TextBlock
        {
            Text = SettingsColorMath.ToArgbString(argb),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
            FontFamily = new FontFamily("Consolas"),
        };

        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(6, 2, 6, 2),
        };

        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(swatch);
        content.Children.Add(text);
        button.Content = content;

        var picker = new ColorPicker
        {
            IsAlphaEnabled = true,
            IsAlphaSliderVisible = true,
            IsAlphaTextInputVisible = true,
            IsColorChannelTextInputVisible = true,
            ColorSpectrumShape = ColorSpectrumShape.Ring,
            Color = SettingsColorMath.ToColor(argb),
        };

        picker.ColorChanged += (_, e) =>
        {
            var value = SettingsColorMath.ToArgbString(SettingsColorMath.ToArgb(e.NewColor));
            swatch.Background = new SolidColorBrush(e.NewColor);
            text.Text = value;
            apply(value);
        };

        var flyout = new Flyout { Content = picker };
        button.Flyout = flyout;
        return button;
    }

    /// <summary>選択肢の行を生成する。</summary>
    /// <param name="options">選択肢の表示名。</param>
    /// <param name="selectedIndex">初期の選択位置。</param>
    /// <param name="apply">変更を設定へ反映する処理。</param>
    /// <returns>生成した選択入力。</returns>
    private static FrameworkElement ChoiceRow(string[] options, int selectedIndex, Action<int> apply)
    {
        var combo = new ComboBox { MinWidth = 120 };
        foreach (var option in options)
        {
            combo.Items.Add(new ComboBoxItem { Content = option });
        }

        combo.SelectedIndex = selectedIndex;
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0)
            {
                apply(combo.SelectedIndex);
            }
        };

        return combo;
    }

    /// <summary>ホットキーの行を生成する。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="get">現在値の取得。</param>
    /// <param name="set">変更を設定へ反映する処理。</param>
    /// <returns>生成した行。</returns>
    /// <remarks>
    /// 入力欄へフォーカスして修飾キー付きのキーを押すと設定する。 テキストで入力した場合も解釈する。<br/>
    /// クリア ボタンで割り当てを解除する。
    /// </remarks>
    private static FrameworkElement HotKeyRow(string label, Func<HotKeySetting> get, Action<HotKeySetting> set)
    {
        var box = new TextBox
        {
            Text = get().ToString(),
            MinWidth = 200,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var clear = new Button { Content = "クリア", Margin = new Thickness(4, 0, 0, 0) };

        void Apply(HotKeySetting setting)
        {
            set(setting);
            box.Text = setting.ToString();
        }

        // 修飾キー付きのキーを押した場合は、その組み合わせを設定する
        box.KeyDown += (_, e) =>
        {
            if (e.Key is Windows.System.VirtualKey.Control
                or Windows.System.VirtualKey.Shift
                or Windows.System.VirtualKey.Menu
                or Windows.System.VirtualKey.LeftWindows
                or Windows.System.VirtualKey.RightWindows)
            {
                return;
            }

            var modifiers = ReadModifiers();
            var key = e.Key.ToEsKey();
            if (modifiers == HotKeyModifiers.None || key == EsKey.None)
            {
                return;
            }

            Apply(new HotKeySetting(modifiers, key));
            e.Handled = true;
        };

        // テキストで入力された場合も解釈する（"Ctrl + Shift + Q" 等）
        box.LostFocus += (_, _) =>
        {
            if (HotKeyParser.TryParse(box.Text, out var parsed))
            {
                Apply(parsed);
                return;
            }

            box.Text = get().ToString();
        };

        clear.Click += (_, _) => Apply(new HotKeySetting(HotKeyModifiers.None, EsKey.None));

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(box);
        row.Children.Add(clear);
        return Row(label, row);
    }

    /// <summary>現在押されている修飾キーを読み取る。</summary>
    /// <returns>押されている修飾キー（ビットの合成）。</returns>
    private static HotKeyModifiers ReadModifiers()
    {
        var modifiers = HotKeyModifiers.None;
        if (IsDown(Windows.System.VirtualKey.Control))
        {
            modifiers |= HotKeyModifiers.Control;
        }

        if (IsDown(Windows.System.VirtualKey.Shift))
        {
            modifiers |= HotKeyModifiers.Shift;
        }

        if (IsDown(Windows.System.VirtualKey.Menu))
        {
            modifiers |= HotKeyModifiers.Alt;
        }

        if (IsDown(Windows.System.VirtualKey.LeftWindows) || IsDown(Windows.System.VirtualKey.RightWindows))
        {
            modifiers |= HotKeyModifiers.Windows;
        }

        return modifiers;
    }

    /// <summary>指定されたキーが押されているかどうかを判定する。</summary>
    /// <param name="key">判定するキー。</param>
    /// <returns>押されている場合は true。</returns>
    /// <remarks>フラグの判定はビット演算で行う（<c>HasFlag</c> はボックス化する）。</remarks>
    private static bool IsDown(Windows.System.VirtualKey key)
        => (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key)
            & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

    /// <summary>保存先フォルダを選択して入力欄へ反映する。</summary>
    /// <param name="window">フォルダ選択のオーナー ウィンドウ。</param>
    /// <param name="box">反映先の入力欄。</param>
    /// <returns>待機タスク。</returns>
    /// <remarks>取り消された場合は何もしない。 選択できない場合（権限等）は入力欄を変更しない。</remarks>
    private static async Task BrowseFolderAsync(MainWindow window, TextBox box)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, window.ToEsPlatformHandle().Handle);

            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
            {
                box.Text = folder.Path;
            }
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            s_logger.LogSettingsApplyError(ex, nameof(Settings.SaveFolderPath));
        }
    }
}
