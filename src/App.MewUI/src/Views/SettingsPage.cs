using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core;
using EsUtil.Feature.Text.Core.Defaults;
using EsUtil.Feature.Annotation.Core.Defaults;
using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Input;
using EsUtil.Ui.MewUI.ColorSelector;
using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.MewUI.Logging;
using EsUtil.Ui.MewUI.Interop;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>アプリケーション設定ページ（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>SettingsPage.axaml</c> に相当する。 設定は <c>Settings</c>（コア）が単一所有し、<br/>
/// 本ページは各項目の編集 UI を組み立ててプロパティへ接続するだけを担う。<br/>
/// <br/>
/// <b>保存</b>: 変更のたびに <see cref="ISettingsService.Save"/> を呼ぶ（Avalonia 版と同一のタイミング）。<br/>
/// <b>テーマ</b>: <c>ThemeMode</c> は <c>Application.SetThemeMode</c> へ即時反映する。<br/>
/// <b>色</b>: 設定は <c>#AARRGGBB</c> の文字列で保持し、入力は <see cref="ColorSelectorControl"/> が担う（規則はコアの <c>Palette</c> が単一所有する）。<br/>
/// <b>ホットキー</b>: テキストボックス上で修飾キー付きのキーを押すとその組み合わせを設定する（テキスト入力でも解釈する）。<br/>
/// <b>構成</b>: 折り畳みなしの縦 1 列（テーマ / 保存 / 画像 / キャプチャ / 選択制限 / ホットキー / 外観 / 注釈 / ログ）をスクロール表示する。
/// </remarks>
internal static class SettingsPage
{
    /// <summary>項目名の列幅（DIP）。</summary>
    private const double LabelWidth = 190;

    /// <summary>外観設定のカテゴリ列の最小幅（DIP）。</summary>
    /// <remarks>Avalonia 版と同じ値（<c>MinWidth="180"</c>）。</remarks>
    /// <summary>「撮影中の枠を消す」が使えない理由（ツールヒントに出す）。</summary>
    private const string BorderControlUnsupportedReason =
        "この環境では撮影中の枠を消せません（Windows 11 / ビルド 22000 以降が必要です）";

    /// <summary>「撮影中の枠を消す」が他の設定に依存して使えない理由（ツールヒントに出す）。</summary>
    private const string BorderRequiresWgcReason =
        "「画面の取得に Windows.Graphics.Capture を使う」がオンのときだけ使えます";

    /// <summary>「撮影中の枠を消す」を置く行の段下げ（DIP）。</summary>
    private const double BorderRowIndent = 20;

    /// <summary>設定できない項目の淡色の不透明度。</summary>
    private const double DisabledOpacity = 0.45;

    private const double CategoryWidth = 180;

    /// <summary>外観設定のカテゴリ列と項目名の間隔（DIP）。</summary>
    /// <remarks>Avalonia 版と同じ値（カテゴリ名の右マージン・列間隔）。</remarks>
    private const double CategoryGap = 16;

    /// <summary>外観設定のカテゴリ区切りに空ける余白（DIP）。</summary>
    /// <remarks>Avalonia 版と同じ値（区切り直後の項目の上マージン）。</remarks>
    private const double CategorySpacing = 8;

    /// <summary>外観設定の 3 列グリッドの行間・列間（DIP）。</summary>
    /// <remarks>Avalonia 版と同じ値（<c>RowSpacing="5"</c> / <c>ColumnSpacing="5"</c>）。</remarks>
    private const double CategoryGridSpacing = 5;

    /// <summary>ページの外周余白（DIP）。</summary>
    private const double PageMargin = 10;

    /// <summary>セクション間の余白（DIP）。</summary>
    private const double SectionSpacing = 5;

    /// <summary>数値入力の幅（DIP）。</summary>
    private const double NumberWidth = 110;

    /// <summary>ロガー（ログ ファイルを開く操作の失敗を記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(SettingsPage));

    /// <summary>設定ページを構築する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <returns>ページの内容（表示のたびの更新は不要。 設定は即時反映される）。</returns>
    /// <remarks>
    /// 1. 設定の変更通知を購読し、変更のたびに設定ファイルへ保存する。<br/>
    /// 2. 各セクション（テーマ / 保存 / 画像 / キャプチャ / 選択制限 / ホットキー / 外観 / 注釈 / ログ）を縦 1 列で積む。<br/>
    /// 3. 全体をスクロールビューへ入れる（項目がウィンドウ高さを超えるため）。
    /// </remarks>
    public static PageContent Build(AppServices services)
    {
        var settings = services.Settings.Current;
        var settingsService = services.Settings;

        // 変更を即時保存する（ページは 1 度しか生成されないため購読が重複しない）
        settings.PropertyChanged += (_, _) => settingsService.Save();

        // 余白は Avalonia 版と同じ値を使う（外周 10・カード間 5）
        var root = new StackPanel()
            .Vertical()
            .Spacing(SectionSpacing)
            .Padding(new Thickness(PageMargin))
            .Children(
                BuildThemeSection(settings),
                BuildSaveSection(services),
                BuildImageSection(settings),
                BuildCaptureSection(settings, services.Main),
                BuildSelectionLimitSection(settings),
                BuildHotKeySection(settings),
                BuildAppearanceSection(settings),
                BuildAnnotationSection(settings),
                BuildLogSection(settings));

        // タイトルバー領域（上端）にはページ内容を表示しない
        // （Avalonia 版は同領域を空のツールバー帯として確保し、内容をその下から始める）
        var page = new Grid()
            .Rows($"{AppStyles.PageTopInset},*")
            .Columns("*")
            .Children(
                new Grid().Rows("*").Columns("*"),
                new ScrollViewer().Content(root));

        return new PageContent(page);
    }

    // ====================================================================
    // セクション
    // ====================================================================

    /// <summary>外観テーマのセクションを構築する。</summary>
    /// <param name="settings">設定。</param>
    /// <returns>セクションの要素。</returns>
    private static Element BuildThemeSection(Settings settings)
    {
        var modes = new[] { ThemeMode.Light, ThemeMode.Dark, ThemeMode.System };
        var texts = new[] { "ライト", "ダーク", "システム" };
        var index = Array.IndexOf(modes, settings.ThemeMode);

        return Section(
            "外観テーマ設定",
            Row("テーマ", ChoiceRow(texts, index < 0 ? texts.Length - 1 : index, selected =>
            {
                settings.ThemeMode = modes[selected];
                ApplyTheme(modes[selected]);
            })),
            Row("色の表記", ChoiceRow(
                [.. ColorTextFormatDisplay.All.Select(ColorTextFormatDisplay.Get)],
                Math.Max(0, ColorTextFormatDisplay.All.ToList().IndexOf(settings.ColorTextFormat)),
                selected => settings.ColorTextFormat = ColorTextFormatDisplay.All[selected])));
    }

    /// <summary>保存設定のセクションを構築する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <returns>セクションの要素。</returns>
    private static Element BuildSaveSection(AppServices services)
    {
        var settings = services.Settings.Current;
        var viewModel = services.Main;

        var validationText = new ObservableValue<string>("");

        void RefreshValidation()
        {
            viewModel.SaveFolderPath = settings.SaveFolderPath;
            validationText.Value = viewModel.HasInvalidSaveFolder ? "保存先フォルダが存在しません" : "";
        }

        var pathBox = new TextBox()
            .Text(settings.SaveFolderPath)
            .FontSize(SettingsMetrics.FontSize)
            .OnTextChanged(text =>
            {
                settings.SaveFolderPath = text;
                RefreshValidation();
            });

        var browse = new Button().Content("参照").FontSize(SettingsMetrics.FontSize).OnClick(() => BrowseFolder(pathBox, settings));

        // 保存先の欄は残り幅いっぱい（ラベルと参照ボタンを除いた分）にし、ウィンドウ幅へ追従させる
        var label = new Label().Text("保存先フォルダ")
            .FontSize(SettingsMetrics.FontSize)
            .Width(LabelWidth)
            .VerticalTextAlignment(TextAlignment.Center)
            .Margin(new Thickness(0, 0, 8, 0));
        var pathRow = new Grid().Columns("Auto,*,Auto");
        pathRow.Add(label.Column(0));
        pathRow.Add(pathBox.Column(1));
        pathRow.Add(browse.Column(2).Margin(new Thickness(6, 0, 0, 0)));

        return Section(
            "保存設定",
            pathRow,
            Row("", new Label().Text(SettingsMetrics.SaveFolderHint).FontSize(SettingsMetrics.HintFontSize)),
            Row("", new Label().BindText(validationText).FontSize(SettingsMetrics.HintFontSize)));
    }

    /// <summary>画像設定のセクションを構築する。</summary>
    /// <param name="settings">設定。</param>
    /// <returns>セクションの要素。</returns>
    private static Element BuildImageSection(Settings settings)
    {
        // 起動直後の画像の選択肢（表示名は列挙の説明属性が単一所有する）
        var modes = MainViewModel.StartupPreviewModes;
        var options = modes.Select(mode => EnumDisplayNames.Get(mode)).ToArray();
        var selected = Math.Max(0, modes.ToList().IndexOf(settings.StartupPreview));

        return Section(
            "画像設定",
            Row("", CheckRow(settings.CenterAlign, value => settings.CenterAlign = value, "画像を中央に配置（オフで左上配置）")),
            Row("起動時の画像", ChoiceRow(options, selected, index => settings.StartupPreview = modes[index])));
    }

    /// <summary>キャプチャ設定のセクションを構築する。</summary>
    /// <param name="settings">設定。</param>
    /// <param name="viewModel">アプリのメイン ViewModel（タスク バーの設定の単一の入口）。</param>
    /// <returns>セクションの要素。</returns>
    private static Element BuildCaptureSection(Settings settings, MainViewModel viewModel)
    {
        // キャプチャ ページのツールバーと同じ項目を編集するため、値は ViewModel を通す
        // （変更通知でツールバーのチェック表示も追従する）
        var hideTaskbar = new CheckBox().IsChecked(viewModel.HideTaskbarOnCapture).OnCheckedChanged(
            value => viewModel.HideTaskbarOnCapture = value);
        hideTaskbar.FontSize(SettingsMetrics.FontSize);
        hideTaskbar.Content("キャプチャ時にタスク バーを隠す（撮影画像に写さない）");
        viewModel.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.HideTaskbarOnCapture), StringComparison.Ordinal)
                && hideTaskbar.IsChecked != viewModel.HideTaskbarOnCapture)
            {
                hideTaskbar.IsChecked = viewModel.HideTaskbarOnCapture;
            }
        };

        var useWindowsGraphicsCapture = GatedCheckRow(
            settings.UseWindowsGraphicsCapture,
            value => settings.UseWindowsGraphicsCapture = value,
            "画面の取得に Windows.Graphics.Capture を使う",
            WindowsGraphicsCaptureSupport.IsSupported,
            WindowsGraphicsCaptureSupport.DescribeUnsupportedReason());
        var hideCaptureBorder = GatedCheckRow(
            settings.HideCaptureBorder,
            value => settings.HideCaptureBorder = value,
            "撮影中の枠を消す",
            IsBorderAvailable(),
            BorderUnavailableReason());

        // 画面の取得方式に依存することを示すため段下げする
        hideCaptureBorder.Row.Margin(new Thickness(BorderRowIndent, 0, 0, 0));

        // 枠を消せるのは Windows.Graphics.Capture のときだけなので、取得方式の選択へ追従させる
        settings.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(Settings.UseWindowsGraphicsCapture), StringComparison.Ordinal))
            {
                ApplyBorderAvailability();
            }
        };

        return Section(
            "キャプチャ設定",
            Row("", CheckRow(settings.HideMainWindowOnCapture, value => settings.HideMainWindowOnCapture = value, "キャプチャ時にメインウィンドウを隠す")),
            Row("", hideTaskbar),
            Row("", CheckRow(settings.CopyAfterCapture, value => settings.CopyAfterCapture = value, "撮影・切り出しの後にクリップボードへコピーする")),
            Row("", useWindowsGraphicsCapture.Row),
            Row("", hideCaptureBorder.Row));

        bool IsBorderAvailable()
            => WindowsGraphicsCaptureSupport.IsBorderControlSupported && settings.UseWindowsGraphicsCapture;

        string BorderUnavailableReason()
            => WindowsGraphicsCaptureSupport.IsBorderControlSupported ? BorderRequiresWgcReason : BorderControlUnsupportedReason;

        void ApplyBorderAvailability()
        {
            var isAvailable = IsBorderAvailable();
            hideCaptureBorder.Box.IsEnabled(isAvailable);
            hideCaptureBorder.Hint.Text(BorderUnavailableReason());
            hideCaptureBorder.Hint.IsVisible = !isAvailable;
        }
    }

    /// <summary>矩形選択範囲の制限のセクションを構築する。</summary>
    /// <param name="settings">設定。</param>
    /// <returns>セクションの要素。</returns>
    private static Element BuildSelectionLimitSection(Settings settings)
        => Section(
            "矩形選択範囲の制限",
            Row("", CheckRow(settings.ClampToSourceBounds, value => settings.ClampToSourceBounds = value, "画像外の選択を禁止（境界内にクリップ）")));

    /// <summary>ホットキー設定のセクションを構築する。</summary>
    /// <param name="settings">設定。</param>
    /// <returns>セクションの要素。</returns>
    private static Element BuildHotKeySection(Settings settings)
        => Section(
            "ホットキー設定",
            HotkeyRow("スクリーン選択キャプチャ",
                () => settings.SelectScreenHotKey, value => settings.SelectScreenHotKey = value),
            HotkeyRow("ウィンドウ選択キャプチャ",
                () => settings.WindowSelectHotKey, value => settings.WindowSelectHotKey = value),
            HotkeyRow("範囲選択キャプチャ",
                () => settings.AreaSelectHotKey, value => settings.AreaSelectHotKey = value),
            Row("", new Label().Text("テキストボックスへフォーカスし、修飾キー付きのキーを押すと設定されます。").FontSize(SettingsMetrics.HintFontSize)));

    /// <summary>ログのセクションを構築する。</summary>
    /// <param name="settings">設定（出力ログレベルの所有者）。</param>
    /// <returns>セクションの要素。</returns>
    /// <remarks>
    /// 出力ログレベルを選ぶと、以後の記録の下限が変わる（切り替えは <c>AppLogging</c> が単一所有する）。<br/>
    /// 「ログファイルを表示」は当日のログ ファイルを既定のアプリで開く
    /// （手順（当日分が無いときにフォルダを開く）は <see cref="LogFileActions.ShowCurrentLogFile"/> が単一所有する）。
    /// </remarks>
    private static Element BuildLogSection(Settings settings)
    {
        var levels = MainViewModel.LogOutputLevels;
        var options = levels.Select(EnumDisplayNames.Get).ToArray();
        var selected = Math.Max(0, levels.ToList().IndexOf(settings.LogOutputLevel));

        var show = new Button().Content("ログファイルを表示").FontSize(SettingsMetrics.FontSize).OnClick(ShowLogFile);

        return Section(
            "ログ",
            Row("出力ログレベル", ChoiceRow(options, selected, index => settings.LogOutputLevel = levels[index])),
            Row("", show),
            Row("", new Label().Text(SettingsMetrics.LogFileHint).FontSize(SettingsMetrics.HintFontSize)));
    }

    /// <summary>当日のログ ファイルを既定のアプリで開く。</summary>
    /// <remarks>失敗はログへ記録して続行する（ボタンの操作で他の設定を壊さない）。</remarks>
    private static void ShowLogFile()
    {
        try
        {
            LogFileActions.ShowCurrentLogFile(AppLog.AssemblyName);
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            s_logger.LogOpenLogFileError(ex);
        }
    }

    /// <summary>外観（選択枠・暗転・ハンドル・ルーペ・ラベル）のセクションを構築する。</summary>
    /// <param name="settings">設定。</param>
    /// <returns>セクションの要素。</returns>
    /// <remarks>
    /// Avalonia 版と同じカテゴリ構成（ラベル設定 / 選択枠・暗転 / ドラッグハンドル・マーカー / ルーペ /<br/>
    /// 選択矩形内のラベル / カーソル位置の座標ラベル / ルーペの中心座標ラベル）で並べる。<br/>
    /// 見出し行の右端に「デフォルト値に戻す」ボタンを置き、本体は 3 列（カテゴリ / 項目名 / 設定値）のグリッドにする。
    /// </remarks>
    private static Element BuildAppearanceSection(Settings settings)
    {
        var reset = new Button().Content("デフォルト値に戻す").FontSize(SettingsMetrics.FontSize).OnClick(() => settings.ResetAppearanceToDefaults());

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
                ("選択枠の色", ColorRow(settings, settings.CaptureBorderColor, value => settings.CaptureBorderColor = value)),
                ("選択枠の太さ", NumberRow(settings.CaptureBorderThickness, 0.5, 8.0, 0.5, false, value => settings.CaptureBorderThickness = value)),
                ("選択矩形の最小サイズ", NumberRow(settings.MinSelectionSize, 1, 100, 1, true, value => settings.MinSelectionSize = (int)value)),
                ("暗転オーバーレイの色", ColorRow(settings, settings.CaptureOverlayColor, value => settings.CaptureOverlayColor = value)),
            ]),
            ("ドラッグハンドル・マーカー",
            [
                ("ハンドルの色", ColorRow(settings, settings.HandleColor, value => settings.HandleColor = value)),
                ("ハンドルのサイズ", NumberRow(settings.HandleSize, 2, 30, 1, false, value => settings.HandleSize = value)),
                ("ハンドル判定半径", NumberRow(settings.HandleHitRadius, 2, 40, 1, false, value => settings.HandleHitRadius = value)),
                ("マーカーの色", ColorRow(settings, settings.MarkerColor, value => settings.MarkerColor = value)),
                ("マーカーの長さ", NumberRow(settings.MarkerLength, 8, 40, 2, false, value => settings.MarkerLength = value)),
            ]),
            ("ルーペ",
            [
                ("枠色", ColorRow(settings, settings.LoupeFrameColor, value => settings.LoupeFrameColor = value)),
                ("枠の太さ", NumberRow(settings.LoupeFrameWidth, 1, 10, 1, true, value => settings.LoupeFrameWidth = (int)value)),
                ("十字線色", ColorRow(settings, settings.LoupeCrossColor, value => settings.LoupeCrossColor = value)),
                ("画像外の色", ColorRow(settings, settings.LoupePasteboardColor, value => settings.LoupePasteboardColor = value)),
                ("選択矩形破線の色A", ColorRow(settings, settings.LoupeSelectionRectColorA, value => settings.LoupeSelectionRectColorA = value)),
                ("選択矩形破線の色B", ColorRow(settings, settings.LoupeSelectionRectColorB, value => settings.LoupeSelectionRectColorB = value)),
                ("拡大率", NumberRow(settings.LoupeZoomLevel, 4, 32, 2, true, value => settings.LoupeZoomLevel = (int)value)),
                ("サイズ", NumberRow(settings.LoupeSize, 64, 512, 8, true, value => settings.LoupeSize = (int)value)),
                ("カーソルからのオフセット", NumberRow(settings.LoupeOffset, 0, 200, 4, false, value => settings.LoupeOffset = value)),
            ]),
            ("選択矩形内のラベル",
            [
                ("1 行目 テキスト色", ColorRow(settings, settings.CaptureTextColor, value => settings.CaptureTextColor = value)),
                ("1 行目 背景色", ColorRow(settings, settings.SizeLabelBackgroundColor, value => settings.SizeLabelBackgroundColor = value)),
                ("1 行目 フォントサイズ", NumberRow(settings.SizeLabelFontSize, 8, 32, 1, false, value => settings.SizeLabelFontSize = value)),
                ("2 行目 テキスト色", ColorRow(settings, settings.SubLineColor, value => settings.SubLineColor = value)),
                ("2 行目 背景色", ColorRow(settings, settings.SubLineBackgroundColor, value => settings.SubLineBackgroundColor = value)),
                ("2 行目 フォントサイズ", NumberRow(settings.SubLineFontSize, 8, 32, 1, false, value => settings.SubLineFontSize = value)),
                ("3 行目 テキスト色", ColorRow(settings, settings.InfoLabelColor, value => settings.InfoLabelColor = value)),
                ("3 行目 背景色", ColorRow(settings, settings.InfoLabelBackgroundColor, value => settings.InfoLabelBackgroundColor = value)),
                ("3 行目 フォントサイズ", NumberRow(settings.InfoLabelFontSize, 8, 32, 1, false, value => settings.InfoLabelFontSize = value)),
            ]),
            ("カーソル位置の座標ラベル",
            [
                ("テキスト色", ColorRow(settings, settings.CoordLabelColor, value => settings.CoordLabelColor = value)),
                ("背景色", ColorRow(settings, settings.CoordLabelBackgroundColor, value => settings.CoordLabelBackgroundColor = value)),
                ("フォントサイズ", NumberRow(settings.CoordLabelFontSize, 6, 48, 1, false, value => settings.CoordLabelFontSize = value)),
            ]),
            ("ルーペの中心座標ラベル",
            [
                ("座標ラベルのテキスト色", ColorRow(settings, settings.LoupeCoordLabelColor, value => settings.LoupeCoordLabelColor = value)),
                ("座標ラベルの背景色", ColorRow(settings, settings.LoupeCoordLabelBackgroundColor, value => settings.LoupeCoordLabelBackgroundColor = value)),
                ("座標ラベルのフォントサイズ", NumberRow(settings.LoupeCoordLabelFontSize, 6, 48, 1, false, value => settings.LoupeCoordLabelFontSize = value)),
            ]),
        ];

        return SectionWithAction("外観設定", reset, BuildAppearanceGrid(categories));
    }

    /// <summary>注釈設定のセクションを構築する。</summary>
    /// <param name="settings">アプリ設定。</param>
    /// <returns>セクション。</returns>
    /// <remarks>
    /// 新しく追加する枠線・吹き出し・本文へ適用する（既存の注釈は変えない）。 項目は Avalonia 版と同じ並びにする。<br/>
    /// 色の表記（16 進の桁数）は <see cref="Settings.ColorTextFormat"/> が単一所有するため、ここでは持たない。
    /// </remarks>
    private static Element BuildAnnotationSection(Settings settings)
        => Section(
            "注釈",
            Row("", new Label()
                .Text("新しく追加する枠線・吹き出し・本文へ適用します（既存の注釈は変わりません）。")
                .FontSize(SettingsMetrics.HintFontSize)),
            Row("枠線の色", ColorRow(settings, settings.AnnotationFrameBorderColor, value => settings.AnnotationFrameBorderColor = value)),
            Row("枠線の太さ", NumberRow(settings.AnnotationFrameBorderThickness, AnnotationLimits.BorderThicknessMin, AnnotationLimits.BorderThicknessMax, 1, false, value => settings.AnnotationFrameBorderThickness = value)),
            Row("枠線の内側の色", ColorRow(settings, settings.AnnotationFrameFillColor, value => settings.AnnotationFrameFillColor = value)),
            Row("角丸", NumberRow(settings.AnnotationFrameCornerRadius, 0, AnnotationLimits.CornerRadiusMax, 1, false, value => settings.AnnotationFrameCornerRadius = value)),
            Row("吹き出しの枠線の色", ColorRow(settings, settings.AnnotationBubbleBorderColor, value => settings.AnnotationBubbleBorderColor = value)),
            Row("吹き出しの内側の色", ColorRow(settings, settings.AnnotationBubbleFillColor, value => settings.AnnotationBubbleFillColor = value)),
            Row("本文の文字サイズ", NumberRow(settings.AnnotationTextFontSize, TextLimits.FontSizeMin, TextLimits.FontSizeMax, 1, false, value => settings.AnnotationTextFontSize = value)),
            Row("本文の文字色", ColorRow(settings, settings.AnnotationTextColor, value => settings.AnnotationTextColor = value)),
            Row("本文の余白", NumberRow(settings.AnnotationBodyPadding, 0, AnnotationLimits.BodyPaddingMax, 1, false, value => settings.AnnotationBodyPadding = value)),
            Row("しっぽの幅", NumberRow(settings.AnnotationTailWidth, AnnotationLimits.TailWidthMin, AnnotationLimits.TailWidthMax, 1, false, value => settings.AnnotationTailWidth = value)));

    /// <summary>外観設定のカテゴリと項目を 3 列のグリッドへ並べる。</summary>
    /// <param name="categories">カテゴリと項目（項目名が空の項目はチェックボックスとして 2 列分を使う）。</param>
    /// <returns>グリッド。</returns>
    /// <remarks>
    /// Avalonia 版と同じ 3 列（カテゴリ / 項目名 / 設定値）のグリッドにし、列幅を全カテゴリで共有する。<br/>
    /// カテゴリ名は左列を縦に結合して右寄せし、カテゴリの切れ目には全列をまたぐ 1 DIP の水平線を入れる。<br/>
    /// 区切りの直後の項目とカテゴリ名は同じ分だけ上へ余白を空け（水平線と重ならず行が揃う）、<br/>
    /// 行・列の間隔は Avalonia 版の <c>RowSpacing</c> / <c>ColumnSpacing</c> と同じ値にする。
    /// </remarks>
    private static Element BuildAppearanceGrid(
        (string Category, (string Name, FrameworkElement Control)[] Items)[] categories)
    {
        var rowCount = categories.Sum(static category => category.Items.Length);
        var grid = new Grid()
            .Rows(string.Join(',', Enumerable.Repeat("Auto", rowCount)))
            .Columns($"Auto,Auto,Auto,{CategoryGap}")
            .Spacing(CategoryGridSpacing);

        var row = 0;

        foreach (var (category, items) in categories)
        {
            // カテゴリの切れ目（最初のカテゴリ以外の先頭）は水平線の分だけ下へずらす
            var categoryOffset = row == 0 ? 0 : CategorySpacing;

            var categoryLabel = new Label().Text(category).Bold().FontSize(SettingsMetrics.FontSize);
            categoryLabel.MinWidth = CategoryWidth;
            categoryLabel.HorizontalAlignment = HorizontalAlignment.Right;
            categoryLabel.TextAlignment(TextAlignment.Right);
            categoryLabel.VerticalAlignment = VerticalAlignment.Top;
            categoryLabel.Margin(new Thickness(0, categoryOffset, CategoryGap, 0));
            grid.Add(categoryLabel.GridPosition(row, 0, items.Length, 1));

            for (var i = 0; i < items.Length; i++)
            {
                // カテゴリの先頭の行だけ水平線の分だけ下へずらす（他の行はずらさない）
                var offset = i == 0 ? categoryOffset : 0;

                if (i == 0 && row > 0)
                {
                    grid.Add(new Border
                    {
                        Height = 1,
                        VerticalAlignment = VerticalAlignment.Top,
                        StyleName = AppStyles.Divider,
                    }.GridPosition(row, 0, 1, 4));
                }

                var (name, control) = items[i];
                control.Margin(new Thickness(0, offset, 0, 0));

                if (name.Length == 0)
                {
                    // 項目名が無い項目（チェックボックス）は項目名の列を空け、2 列分を使って左端を揃える
                    grid.Add(control.GridPosition(row, 1, 1, 2));
                }
                else
                {
                    var nameLabel = new Label().Text(name)
                        .FontSize(SettingsMetrics.FontSize)
                        .VerticalTextAlignment(TextAlignment.Center);
                    nameLabel.Margin(new Thickness(0, offset, 0, 0));
                    grid.Add(nameLabel.GridPosition(row, 1, 1, 1));
                    grid.Add(control.GridPosition(row, 2, 1, 1));
                }

                row++;
            }
        }

        return grid;
    }

    // ====================================================================
    // 行・部品の組み立て
    // ====================================================================

    /// <summary>セクション（見出し + 項目行）を構築する。</summary>
    /// <param name="title">見出し。</param>
    /// <param name="rows">項目行。</param>
    /// <returns>見出しと項目行を縦に並べた要素。</returns>
    private static Element Section(string title, params Element[] rows) => SectionCore(title, null, rows);

    /// <summary>見出しに操作ボタンを持つセクション（見出し + 項目行）を構築する。</summary>
    /// <param name="title">見出し。</param>
    /// <param name="action">見出しの右端へ置く操作ボタン。</param>
    /// <param name="rows">項目行。</param>
    /// <returns>見出しと項目行を縦に並べた要素。</returns>
    /// <remarks>見出し行は 2 列のグリッドとし、2 列目（内容幅）へ操作ボタンを置く。</remarks>
    private static Element SectionWithAction(string title, Element action, params Element[] rows)
        => SectionCore(title, action, rows);

    /// <summary>セクションの共通部分（枠・見出し・項目の並び）を構築する。</summary>
    /// <param name="title">見出し。</param>
    /// <param name="action">見出しの右端へ置く操作ボタン。 null の場合は置かない。</param>
    /// <param name="rows">項目行。</param>
    /// <returns>セクションの要素。</returns>
    /// <remarks>
    /// Avalonia 版と同じく、背景を持たない 1 DIP の枠（<see cref="AppStyles.SettingsCard"/>）の中へ<br/>
    /// 見出し（太字）と項目を縦に並べる（見出しの帯に背景色は付けない）。<br/>
    /// 余白は Avalonia 版（<c>Padding="5"</c>・項目間 5）と同じ値を使う。
    /// </remarks>
    private static Element SectionCore(string title, Element? action, params Element[] rows)
    {
        var headerLabel = new Label().Text(title);
        headerLabel.FontSize(SettingsMetrics.FontSize);
        headerLabel.Bold();
        headerLabel.VerticalTextAlignment(TextAlignment.Center);

        Element header = action is null
            ? headerLabel
            : new Grid().Columns("*,Auto").Children(headerLabel.Column(0), action.Column(1));

        var body = new StackPanel().Vertical().Spacing(5);
        body.Children(rows);

        var stack = new StackPanel().Vertical().Spacing(5);
        stack.Children(header, body);

        return new Border
        {
            Child = stack,
            Padding = new Thickness(5),
            StyleName = AppStyles.SettingsCard,
        };
    }

    /// <summary>項目行（項目名 + 編集 UI）を構築する。</summary>
    /// <param name="label">項目名。 空文字の場合は項目名の列を確保せず、編集 UI を左端へ置く。</param>
    /// <param name="control">編集 UI。</param>
    /// <returns>項目名と編集 UI を横に並べた要素。</returns>
    private static Element Row(string label, Element control)
    {
        var name = new Label().Text(label).FontSize(SettingsMetrics.FontSize).VerticalTextAlignment(TextAlignment.Center);

        // 項目名が無い行（チェックボックス・説明文）は項目名の列を確保しない（Avalonia 版と同じ左端寄せ）
        if (label.Length > 0)
        {
            name.Width(LabelWidth);
        }

        return new StackPanel()
            .Horizontal()
            .Spacing(8)
            .Children(name, control);
    }


    /// <summary>チェックボックスの行を構築する。</summary>
    /// <param name="initial">初期値。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <param name="text">チェックボックスのラベル。 null の場合はラベルなし。</param>
    /// <returns>チェックボックス。</returns>
    private static FrameworkElement CheckRow(bool initial, Action<bool> apply, string? text = null)
    {
        var box = new CheckBox().IsChecked(initial).OnCheckedChanged(apply);

        // 文字サイズは AvaloniaUI 版と同じ 14 DIP（SettingsMetrics が単一所有する）
        box.FontSize(SettingsMetrics.FontSize);

        if (text is not null)
        {
            box.Content(text);
        }

        return box;
    }

    /// <summary>実行環境や他の設定に依存して無効にするチェック ボックスを構築する。</summary>
    /// <param name="initial">初期値。</param>
    /// <param name="apply">変更を設定へ反映する処理。</param>
    /// <param name="text">チェック ボックスの文言。</param>
    /// <param name="isAvailable">この時点で設定できるか。</param>
    /// <param name="reason">使えない理由。</param>
    /// <returns>チェック ボックス・使えない理由・両者を並べた行。</returns>
    /// <remarks>使えない項目は値を変更させない。 理由は入力の隣へ淡色で書く（無効な要素はツールチップを出さない）。</remarks>
    private static (CheckBox Box, Label Hint, FrameworkElement Row) GatedCheckRow(
        bool initial, Action<bool> apply, string text, bool isAvailable, string reason)
    {
        var box = (CheckBox)CheckRow(initial, apply, text);
        box.IsEnabled(isAvailable);

        var hint = new Label().Text(reason).FontSize(SettingsMetrics.HintFontSize);
        hint.Opacity = DisabledOpacity;
        hint.VerticalAlignment(VerticalAlignment.Center);
        hint.Margin(new Thickness(8, 0, 0, 0));
        hint.IsVisible = !isAvailable;

        var row = new StackPanel().Orientation(Orientation.Horizontal);
        row.Children(box, hint);
        return (box, hint, row);
    }

    /// <summary>数値入力の行を構築する。</summary>
    /// <param name="initial">初期値。</param>
    /// <param name="minimum">最小値。</param>
    /// <param name="maximum">最大値。</param>
    /// <param name="step">増減の単位。</param>
    /// <param name="isInteger">整数のみを受け付けるかどうか。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <returns>数値入力。</returns>
    private static FrameworkElement NumberRow(double initial, double minimum, double maximum, double step, bool isInteger, Action<double> apply)
    {
        var input = new NumericUpDown()
            .Minimum(minimum)
            .Maximum(maximum)
            .Step(step)
            .IsInteger(isInteger)
            .FontSize(SettingsMetrics.FontSize)
            .Width(NumberWidth);
        input.Value = initial;
        input.OnValueChanged(apply);
        return input;
    }

    /// <summary>色編集の行を構築する。</summary>
    /// <param name="settings">設定（表記の取得元）。</param>
    /// <param name="initial">初期値（<c>#AARRGGBB</c>）。</param>
    /// <param name="apply">変更の反映先。</param>
    /// <returns>色選択コントロール。</returns>
    /// <remarks>
    /// 色の入力・一覧・カラーピッカーは <see cref="ColorSelectorControl"/>（EsUtil.Ui.MewUI.ColorSelector）が担う。<br/>
    /// 受理する入力と保存値の形式はコア（<c>EsUtil.Platform.Core.Palette</c>）が単一所有するため、
    /// 設定画面は値を受け渡すだけでよい（他の UI 版と同じ解釈になる）。<br/>
    /// 入力欄の表記は <c>Models.Settings.ColorTextFormat</c> に従い、設定の変更へ追従する<br/>
    /// （設定画面を作り直さずに全行の表記が変わる）。
    /// </remarks>
    private static FrameworkElement ColorRow(Settings settings, string initial, Action<string> apply)
    {
        // 文字サイズは周囲の入力欄（SettingsMetrics.FontSize）へ合わせる。
        // MewUI の要素は文字サイズを継承しないため、明示的に渡さないと 1 回り小さく表示される
        var control = new ColorSelectorControl
        {
            SelectedColor = initial,
            TextFormat = settings.ColorTextFormat,
            TextFontSize = SettingsMetrics.FontSize,
        };
        control.SelectedColorChanged += (_, _) => apply(control.SelectedColor);

        settings.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(Settings.ColorTextFormat), StringComparison.Ordinal))
            {
                control.TextFormat = settings.ColorTextFormat;
            }
        };

        return control;
    }

    /// <summary>選択肢の行を構築する。</summary>
    /// <param name="options">表示する選択肢。</param>
    /// <param name="selectedIndex">初期の選択インデックス。</param>
    /// <param name="apply">変更の反映先（選択インデックス）。</param>
    /// <returns>コンボボックス。</returns>
    private static Element ChoiceRow(string[] options, int selectedIndex, Action<int> apply)
    {
        var combo = new ComboBox().Items(options);
        combo.FontSize(SettingsMetrics.FontSize);
        combo.SelectedIndex = selectedIndex;

        // 選択変更はインデックスで受け取る（MewUI のハンドラは選択された項目を受け取る）
        combo.OnSelectionChanged(_ => apply(combo.SelectedIndex));
        return combo;
    }

    /// <summary>ホットキー入力の行を構築する。</summary>
    /// <param name="label">項目名。</param>
    /// <param name="get">現在値の取得元。</param>
    /// <param name="set">設定の反映先。</param>
    /// <returns>項目名・入力欄・クリアボタンを並べた要素。</returns>
    /// <remarks>
    /// 1. 入力欄へフォーカスした状態で修飾キー付きのキーを押すと、その組み合わせを設定する。<br/>
    /// 2. フォーカスを外した時は入力テキストを <see cref="EsInput.TryParseHotKey"/> で解釈する（手入力にも対応）。<br/>
    /// 3. 解釈できないテキストは現在値へ戻し、空にした場合は未設定にする。<br/>
    /// 4. クリアボタンは未設定（<see cref="EsKey.None"/>）へ戻す。
    /// </remarks>
    private static Element HotkeyRow(string label, Func<HotKeySetting> get, Action<HotKeySetting> set)
    {
        var box = new TextBox().Text(Format(get())).FontSize(SettingsMetrics.FontSize).Width(220);

        box.OnKeyDown(e =>
        {
            // キーなし（修飾キーの単独押下で発生し得る）は組み合わせにならないため無視する
            if (e.Key == Key.None)
            {
                e.Handled = true;
                return;
            }

            var modifiers = BuildModifiers(e.ControlKey, e.ShiftKey, e.AltKey, e.MetaKey);
            if (modifiers == HotKeyModifiers.None)
            {
                // 修飾キーなしは通常のテキスト入力として扱う
                return;
            }

            var setting = EsInput.CreateHotKey(modifiers, e.Key.ToEsKey());
            set(setting);
            box.Text = Format(setting);
            e.Handled = true;
        });

        box.OnLostFocus(() =>
        {
            var text = box.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                set(EsInput.NoHotKey);
                box.Text = Format(get());
                return;
            }

            if (EsInput.TryParseHotKey(text, out var parsed))
            {
                set(parsed);
                box.Text = Format(parsed);
                return;
            }

            // 解釈できない場合は現在の設定値へ戻す
            box.Text = Format(get());
        });

        var clear = new Button().Content("クリア").FontSize(SettingsMetrics.FontSize).OnClick(() =>
        {
            set(EsInput.NoHotKey);
            box.Text = Format(get());
        });

        return Row(label, new StackPanel().Horizontal().Spacing(6).Children(box, clear));
    }

    // ====================================================================
    // 補助
    // ====================================================================

    /// <summary>ホットキー設定を表示用の文字列へ変換する。</summary>
    /// <param name="setting">対象のホットキー設定。</param>
    /// <returns>表示文字列。 未設定（<see cref="EsKey.None"/>）の場合は「(未設定)」。</returns>
    private static string Format(HotKeySetting setting)
        => setting.Key == EsKey.None ? "(未設定)" : setting.ToString();

    /// <summary>押下中の修飾キーからホットキーの修飾を組み立てる。</summary>
    /// <param name="control">Ctrl が押されているかどうか。</param>
    /// <param name="shift">Shift が押されているかどうか。</param>
    /// <param name="alt">Alt が押されているかどうか。</param>
    /// <param name="meta">Windows キーが押されているかどうか。</param>
    /// <returns>組み立てた修飾キー。</returns>
    private static HotKeyModifiers BuildModifiers(bool control, bool shift, bool alt, bool meta)
    {
        var modifiers = HotKeyModifiers.None;
        if (control)
        {
            modifiers |= HotKeyModifiers.Control;
        }

        if (shift)
        {
            modifiers |= HotKeyModifiers.Shift;
        }

        if (alt)
        {
            modifiers |= HotKeyModifiers.Alt;
        }

        if (meta)
        {
            modifiers |= HotKeyModifiers.Windows;
        }

        return modifiers;
    }

    /// <summary>テーマモードをアプリ全体へ適用する。</summary>
    /// <param name="mode">適用するテーマモード。</param>
    /// <remarks>適用は <see cref="AppStyles.ApplyTheme"/> が単一所有する（起動時の適用と同じ経路）。</remarks>
    private static void ApplyTheme(ThemeMode mode) => AppStyles.ApplyTheme(mode);

    /// <summary>フォルダ選択ダイアログで保存先フォルダを選ぶ。</summary>
    /// <param name="box">選択結果を反映するテキストボックス。</param>
    /// <param name="settings">設定。</param>
    /// <remarks>
    /// ダイアログが利用できない環境（ネイティブ・マネージドとも未登録）では何もしない。<br/>
    /// 選択されたパスは <c>{MyPictures}</c> の展開後の値をそのまま保存する（Avalonia 版と同じ）。
    /// </remarks>
    private static void BrowseFolder(TextBox box, Settings settings)
    {
        try
        {
            if (Application.Current?.PlatformServices.FileDialog is not { } dialog)
            {
                return;
            }

            var selected = dialog.SelectFolder(new FolderDialogOptions
            {
                Title = "保存先フォルダを選択",
                InitialDirectory = Directory.Exists(settings.SaveFolderPath) ? settings.SaveFolderPath : null,
            });

            if (!string.IsNullOrEmpty(selected))
            {
                settings.SaveFolderPath = selected;
                box.Text = selected;
            }
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // ダイアログを開けない環境（サービス未登録等）では設定を変更しない
        }
    }
}

