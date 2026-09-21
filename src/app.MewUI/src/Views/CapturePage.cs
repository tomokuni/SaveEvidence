using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Estable.CropView;
using Estable.Loupe;
using Estable.MewUI.CropView;
using Estable.MewUI.Loupe;
using Estable.MewUI.ZoomView;
using Estable.Platform;
using Estable.Platform.Geometry;
using Estable.SaveEvidence.Models;
using Estable.SaveEvidence.ViewModels;
using Microsoft.Extensions.Logging;
using SaveEvidence.MewUI.Icons;
using SaveEvidence.MewUI.Logging;
using SaveEvidence.MewUI.Services;
using SkiaSharp;
using MewUIColor = Aprillz.MewUI.Color;

namespace SaveEvidence.MewUI.Views;

/// <summary>キャプチャページ（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>CapturePage.axaml</c> に相当する。<br/>
/// プレビュー（ズーム・パン・矩形オーバーレイ）・ルーペ（カーソル追従）・切り出し・撮影・履歴操作を担う。<br/>
/// 書き出し形式の判定は <see cref="MewUIImageWriter"/>、撮影は <see cref="CaptureFlow"/>、
/// 切り出しは <see cref="PreviewCropSession"/>、履歴は <see cref="EditHistory"/> へ委譲する。
/// </remarks>
internal static class CapturePage
{
    /// <summary>ロガー（Avalonia 版と同じログファイルへ出力する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(CapturePage));

    /// <summary>検出矩形の枠線の太さ（DIP）。</summary>
    private const double DetectedRectThickness = 2;

    /// <summary>ツールバーのアイコンの一辺（DIP）。</summary>
    private const double IconSize = 16;

    /// <summary>ツールバーのアイコンを生成する解像度（ピクセル）。</summary>
    private const int IconRasterSize = 32;

    /// <summary>メニューアイコン（ハンバーガー）の大きさ（半径。DIP）。</summary>
    private const double MenuGlyphSize = 6;

    /// <summary>ズームスライダの最小値（%）。</summary>
    private const double ZoomSliderMinimum = 10;

    /// <summary>ズームスライダの最大値（%）。</summary>
    private const double ZoomSliderMaximum = 500;

    /// <summary>テーマ変更時に描き直すアイコン（画像と元の SVG ファイル名）。</summary>
    /// <param name="Image">描き直す対象の画像要素。</param>
    /// <param name="File">元の SVG ファイル名。</param>
    /// <remarks>アイコンは SVG を指定色でラスタライズするため、テーマ変更時は同じファイルから描き直す。</remarks>
    private sealed record IconImage(Image Image, string File);

    /// <summary>テーマに合わせてアイコンを描き直す。</summary>
    /// <param name="icons">描き直す対象（<see cref="IconImage"/> の一覧）。</param>
    private static void RefreshIcons(List<IconImage> icons)
    {
        var color = IconLoader.ForegroundColor();
        foreach (var entry in icons)
        {
            entry.Image.Source = IconLoader.LoadImage(entry.File, IconRasterSize, color);
        }
    }

    /// <summary>カーソルがプレビュー外にあるときのルーペとプレビューの余白（DIP）。</summary>
    private const double DefaultMargin = 16;

    /// <summary>拡大率メニューの選択肢（%。0 = 自動は別途追加する）。</summary>
    /// <remarks>Avalonia 版のフライアウトと同じ段階を使う。</remarks>
    private static readonly double[] s_zoomMenuLevels = [10, 15, 25, 35, 50, 75, 100, 150, 200, 250, 350, 500];

    /// <summary>検出矩形の前景色（赤・不透明）。</summary>
    private static readonly System.Drawing.Color s_rectForeground = System.Drawing.Color.FromArgb(255, 255, 64, 64);

    /// <summary>検出矩形の背景色（白・不透明。破線の隙間）。</summary>
    private static readonly System.Drawing.Color s_rectBackground = System.Drawing.Color.FromArgb(255, 255, 255, 255);

    /// <summary>ページの内容を生成する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <returns>ページの内容。</returns>
    /// <remarks>
    /// 1. 3 行の Grid（ツールバー / プレビュー / ステータス）を組み立てる。<br/>
    /// 2. プレビューは Star 行に置き、内容領域の残余全体を占めさせる（ズームビュー）。<br/>
    /// 3. プレビューの上へルーペ（カーソル追従）と切り出しのオーバーレイを重ねる。<br/>
    /// 4. 各コマンドは ViewModel へ直接接続する（キャプチャ開始は <see cref="CaptureFlow"/>、切り出しは
    ///    <see cref="PreviewCropSession"/>、履歴は <see cref="EditHistory"/> へ委譲する）。<br/>
    /// 5. 表示のたびの更新は不要（ViewModel の変更通知で追従する）。
    /// </remarks>
    public static PageContent Build(AppServices services)
    {
        var viewModel = services.Main;
        var settings = services.Settings.Current;
        var preview = BuildPreview(viewModel, settings, out var crop, out var cropSession);

        // ズーム率の表示はホストの都合（書式）で決まるため、コントロールのイベントから文字列化する
        var zoomText = new ObservableValue<string>(viewModel.ZoomDisplayText);
        var canZoomIn = new ObservableValue<bool>(crop.CanZoomIn);
        var canZoomOut = new ObservableValue<bool>(crop.CanZoomOut);

        // ズームスライダはステータスバーで生成する（ズーム率の変化をここから反映する）
        Slider? zoomSlider = null;
        var syncingZoomSlider = false;

        crop.ZoomChanged += (_, e) =>
        {
            zoomText.Value = $"{e.ZoomPercent:0}%";
            canZoomIn.Value = crop.CanZoomIn;
            canZoomOut.Value = crop.CanZoomOut;

            // スライダ → ズームの同期中はループを防ぐため戻さない
            if (zoomSlider is not null && !syncingZoomSlider)
            {
                syncingZoomSlider = true;
                zoomSlider.Value = crop.ZoomPercent;
                syncingZoomSlider = false;
            }
        };

        // 画像操作の可否は画像の有無に追従させる（Avalonia 版と同じ判定）
        var hasImage = new ObservableValue<bool>(viewModel.HasPreviewImage);
        var canSave = new ObservableValue<bool>(viewModel.CanSave);
        var canCopy = new ObservableValue<bool>(viewModel.CanCopy);
        viewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.HasPreviewImage):
                    hasImage.Value = viewModel.HasPreviewImage;
                    break;
                case nameof(MainViewModel.CanSave):
                    canSave.Value = viewModel.CanSave;
                    break;
                case nameof(MainViewModel.CanCopy):
                    canCopy.Value = viewModel.CanCopy;
                    break;
            }
        };

        // ── メニュー（☰ の下へ出すプルダウン。Avalonia 版の MenuFlyout に相当）──
        var menuPanel = BuildMenu(viewModel, services, cropSession);

        // ── 拡大率メニュー（% 表示のクリックで出す。Avalonia 版のフライアウトに相当）──
        var zoomMenu = BuildZoomMenu(crop);

        // テーマ変更時に描き直すアイコン（画像と元の SVG ファイル名）
        var iconImages = new List<IconImage>();

        // ── ツールバー（Avalonia 版と同じアイコン配置）──
        // ウィンドウ幅が狭い・ファイル名テンプレートが長い場合はアイコンがウィンドウ操作ボタンへ重なるため、
        // 横方向へ折り返す（Avalonia 版のツールバーも WrapPanel）。
        var menuButton = new Button
        {
            // メニューのアイコンはベクターグリフを使う（文字の ☰ はフォントの代替字形が下へ寄り、縦位置が揃わない）
            Content = new GlyphElement { Kind = GlyphKind.Hamburger, GlyphSize = MenuGlyphSize }.Center(),
            Padding = new Thickness(AppStyles.ToolbarPaddingX, AppStyles.ToolbarPaddingY),
            Margin = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Height = AppStyles.ToolbarButtonHeight,
            StyleName = AppStyles.ToolbarButton,
        };
        menuButton.Cursor(CursorType.Hand);
        menuButton.WithToolTip("メニュー", () =>
        {
            zoomMenu.IsVisible = false;
            menuPanel.IsVisible = !menuPanel.IsVisible;
        });

        // 保存するファイル名テンプレート（保存ボタンの左）
        var template = new TextBox
        {
            Text = viewModel.FileNameTemplateText,
            MinWidth = 220,
            Height = AppStyles.ToolbarButtonHeight,
            Margin = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
        };
        template.OnTextChanged(value => viewModel.FileNameTemplateText = value);

        var save = IconButton("ic_fluent_save_24_regular.svg", "画像を保存", () => _ = viewModel.SaveImageAsync(), iconImages);
        save.BindIsEnabled(canSave);

        var screenSelect = IconButton(
            "ic_fluent_rectangle_landscape_24_regular.svg", "スクリーン選択キャプチャ",
            () => CaptureFlow.Start(services, CaptureType.ScreenSelect), iconImages);
        var windowSelect = IconButton(
            "ic_fluent_window_24_regular.svg", "ウィンドウ選択キャプチャ",
            () => CaptureFlow.Start(services, CaptureType.WindowSelect), iconImages);
        var areaSelect = IconButton(
            "ic_fluent_select_object_24_regular.svg", "領域選択キャプチャ",
            () => CaptureFlow.Start(services, CaptureType.AreaSelect), iconImages);

        var cropButton = IconButton("ic_fluent_crop_24_regular.svg", "切り出し（切出しモード開始/終了）", cropSession.Toggle, iconImages);
        cropButton.BindIsEnabled(hasImage);

        var toolbar = new WrapPanel()
            .Margin(new Thickness(2, AppStyles.ToolbarMarginTop, AppStyles.ToolbarMarginRight, AppStyles.ToolbarMarginBottom))
            .Children(
                menuButton,
                AppStyles.CreateToolbarDivider(),
                template,
                save,
                AppStyles.CreateToolbarDivider(),
                screenSelect,
                windowSelect,
                areaSelect,
                AppStyles.CreateToolbarDivider(),
                cropButton);

        // ── ステータスバー ──
        // 左: ポインタ座標（固定幅） | 画像サイズ（固定幅） | 保存済み/未 | 保存ファイル名 | 通知
        // バインドはプロパティ名を推定するためインライン式で指定する
        var pointerLabel = new Label { Width = 70 };
        pointerLabel.FontSize(AppStyles.StatusFontSize);
        pointerLabel.VerticalAlignment = VerticalAlignment.Center;
        pointerLabel.Bind(Label.TextProperty, viewModel, static x => x.PointerDisplayText);
        pointerLabel.ToolTip("マウスポインタ位置（ドメイン座標 = 画像ピクセル）");

        var imageSizeLabel = new Label { Width = 80 };
        imageSizeLabel.FontSize(AppStyles.StatusFontSize);
        imageSizeLabel.VerticalAlignment = VerticalAlignment.Center;
        imageSizeLabel.Bind(Label.TextProperty, viewModel, static x => x.ImageSizeDisplayText);
        imageSizeLabel.ToolTip("画像サイズ（ドメイン座標 = 元画像ピクセル）");

        var saveStatusLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
        saveStatusLabel.FontSize(AppStyles.StatusFontSize);
        saveStatusLabel.Bind(Label.TextProperty, viewModel, static x => x.SaveStatusText);

        var savedFileLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
        savedFileLabel.FontSize(AppStyles.StatusFontSize);
        savedFileLabel.Bind(Label.TextProperty, viewModel, static x => x.SavedFileName);

        var messageLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
        messageLabel.FontSize(AppStyles.StatusFontSize);
        messageLabel.Foreground(AnnounceColor());
        messageLabel.Bind(Label.TextProperty, viewModel, static x => x.StatusMessage);

        var statusLeft = new StackPanel()
            .Horizontal()
            .Spacing(4)
            .Children(pointerLabel, imageSizeLabel, saveStatusLabel, savedFileLabel, messageLabel);

        // 右: 拡大率（⊖ / ⊕ / Fit / % 表示。% はクリックで拡大率メニューを出す）
        var zoomOut = StatusIconButton("ic_fluent_subtract_circle_24_regular.svg", "ズームアウト", crop.ZoomOut, iconImages);
        zoomOut.BindIsEnabled(canZoomOut);
        var zoomIn = StatusIconButton("ic_fluent_add_circle_24_regular.svg", "ズームイン", crop.ZoomIn, iconImages);
        zoomIn.BindIsEnabled(canZoomIn);

        var fit = new Button
        {
            Content = new TextBlock().Text("Fit").FontSize(AppStyles.StatusFontSize).Center(),
            Padding = new Thickness(4, 0),
            Margin = new Thickness(1),
            Height = AppStyles.StatusButtonHeight,
            MinWidth = 30,
            VerticalAlignment = VerticalAlignment.Center,
            StyleName = AppStyles.StatusButton,
        };
        fit.Cursor(CursorType.Hand);
        fit.WithToolTip("ウィンドウに合わせて表示（Fit）", crop.ZoomFit);

        var zoomLabel = new TextBlock().Text("拡大率:").FontSize(AppStyles.StatusFontSize);
        zoomLabel.VerticalAlignment = VerticalAlignment.Center;

        // ズームスライダ（10〜500%）。Avalonia 版と同じ範囲・幅・高さを使う
        zoomSlider = new Slider
        {
            Minimum = ZoomSliderMinimum,
            Maximum = ZoomSliderMaximum,
            Width = 120,
            Height = AppStyles.StatusSliderHeight,
            VerticalAlignment = VerticalAlignment.Center,
        };
        zoomSlider.Value = crop.ZoomPercent;
        zoomSlider.ValueChanged += value =>
        {
            // スライダ → ズームの同期中はループを防ぐため無視する
            if (syncingZoomSlider)
            {
                return;
            }

            crop.ZoomPercent = value;
        };

        var zoomPercentText = new TextBlock().FontSize(AppStyles.StatusFontSize);
        zoomPercentText.VerticalAlignment = VerticalAlignment.Center;
        zoomPercentText.HorizontalAlignment = HorizontalAlignment.Center;
        zoomPercentText.BindText(zoomText);

        var zoomPercentButton = new Button
        {
            Content = zoomPercentText.Center(),
            Padding = new Thickness(0),
            Width = 56,
            Height = AppStyles.StatusButtonHeight,
            VerticalAlignment = VerticalAlignment.Center,
            StyleName = AppStyles.StatusButton,
        };
        zoomPercentButton.Cursor(CursorType.Hand);
        zoomPercentButton.WithToolTip("クリックで拡大率を選択", () =>
        {
            menuPanel.IsVisible = false;
            zoomMenu.IsVisible = !zoomMenu.IsVisible;
        });

        var statusRight = new StackPanel()
            .Horizontal()
            .Spacing(4)
            .Children(zoomLabel, zoomOut, zoomSlider, zoomIn, fit, zoomPercentButton);

        var statusGrid = new Grid().Columns("*,Auto").Rows("Auto");
        statusLeft.VerticalAlignment = VerticalAlignment.Center;
        statusRight.VerticalAlignment = VerticalAlignment.Center;
        statusGrid.Add(statusLeft.Column(0));
        statusRight.HorizontalAlignment = HorizontalAlignment.Right;
        statusGrid.Add(statusRight.Column(1));

        // ステータスバーの高さは 3 ページ共通（テキストとボタンが収まる最小の高さ）
        var status = new Grid().Columns("*").Rows("Auto");
        status.Add(statusGrid);

        // ツールバー / プレビュー（残余） / ステータスの 3 段。余白は Avalonia 版と同じく付けない
        var statusBar = new Border()
            .Padding(AppStyles.StatusPaddingX, AppStyles.StatusPaddingY)
            .StyleName(AppStyles.StatusBar)
            .Child(status);

        var page = new Grid()
            .Rows("Auto,*,Auto")
            .Columns("*")
            .Children(toolbar, preview, statusBar);

        // ページの左上へメニューのプルダウンを重ねる（ツールバーの直下に出す）
        menuPanel.HorizontalAlignment = HorizontalAlignment.Left;
        menuPanel.VerticalAlignment = VerticalAlignment.Top;
        menuPanel.Margin = new Thickness(6, AppStyles.MenuTop, 0, 0);

        // 拡大率メニューはステータスバーの % の上へ重ねる（Avalonia 版のフライアウトと同じ位置）
        zoomMenu.HorizontalAlignment = HorizontalAlignment.Right;
        zoomMenu.VerticalAlignment = VerticalAlignment.Bottom;
        zoomMenu.Margin = new Thickness(0, 0, 8, AppStyles.StatusButtonHeight + 12);

        var root = new Grid().Columns("*").Rows("*");
        root.Add(page.Column(0));
        root.Add(menuPanel.Column(0));
        root.Add(zoomMenu.Column(0));

        // テーマが変わったらスタイルで表せない値（アイコンの色）を描き直す
        // （市松模様の背景色はプレビューの生成側で再適用する）
        AppStyles.ThemeChanged += () => RefreshIcons(iconImages);

        return new PageContent(root);
    }

    /// <summary>メニューのプルダウン（ツールバーの ☰ から開く）を生成する。</summary>
    /// <param name="viewModel">アプリのメイン ViewModel（各コマンドの接続先）。</param>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <param name="cropSession">切り出しセッション（切り出しメニューの接続先）。</param>
    /// <returns>初期状態で非表示のプルダウン。</returns>
    /// <remarks>
    /// Avalonia 版の <c>MenuFlyout</c> と同じ項目・同じ区切り・同じショートカット表記で並べる。<br/>
    /// ショートカットは表示のみ（Avalonia 版も <c>MenuItem.InputGesture</c> の表示であり、キー操作は受け付けない）。
    /// </remarks>
    private static Border BuildMenu(MainViewModel viewModel, AppServices services, PreviewCropSession cropSession)
    {
        var panel = new StackPanel().Vertical().Spacing(0).Padding(new Thickness(4));
        Border? menu = null;

        // 項目を選んだらメニューを閉じる（Avalonia 版の MenuFlyout と同じ振る舞い）
        void Close() => menu?.IsVisible = false;

        void Add(string text, Action action, bool enabled = true, string? gesture = null)
        {
            var content = new Grid().Columns("*,Auto");
            content.Add(new TextBlock().Text(text).FontSize(12).VerticalAlignment(VerticalAlignment.Center).Column(0));

            if (gesture is not null)
            {
                var key = new TextBlock().Text(gesture).FontSize(11);
                key.HorizontalAlignment = HorizontalAlignment.Right;
                key.VerticalAlignment = VerticalAlignment.Center;
                key.Margin = new Thickness(24, 0, 0, 0);
                content.Add(key.Column(1));
            }

            var button = new Button
            {
                Content = content,
                Padding = new Thickness(10, 6, 10, 6),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = 0,
                StyleName = AppStyles.FlatButton,
            };
            button.IsEnabled(enabled);
            button.Cursor(CursorType.Hand);
            button.OnClick(() =>
            {
                Close();
                action();
            });
            panel.Add(button);
        }

        // 項目群の区切り線（Avalonia 版 <Separator/> と同じ位置）
        void AddSeparator()
            => panel.Add(new Border
            {
                Height = 1,
                Margin = new Thickness(6, 4, 6, 4),
                StyleName = AppStyles.Divider,
            });

        Add("File - 画像を保存", () => _ = viewModel.SaveImageAsync(), viewModel.CanSave, "Ctrl+S");
        AddSeparator();
        Add("Clipboard - コピー", () => _ = viewModel.CopyToClipboard(), viewModel.CanCopy, "Ctrl+C");
        Add("Clipboard - 貼り付け", () => _ = viewModel.PasteFromClipboard(), viewModel.CanPaste, "Ctrl+V");
        AddSeparator();
        Add("Image - 元に戻す", () => EditHistory.Undo(services), viewModel.HasPreviewImage, "Ctrl+Z");
        Add("Image - 元に戻すを取り消す", () => EditHistory.Redo(services), viewModel.HasPreviewImage, "Ctrl+Y");
        AddSeparator();
        Add("Capture - スクリーン選択", () => CaptureFlow.Start(services, CaptureType.ScreenSelect));
        Add("Capture - ウィンドウ選択", () => CaptureFlow.Start(services, CaptureType.WindowSelect));
        Add("Capture - 範囲選択", () => CaptureFlow.Start(services, CaptureType.AreaSelect));
        AddSeparator();
        Add("Crop - 切り出し", cropSession.Toggle, viewModel.CanCopy, "Ctrl+R");
        Add("Crop - 自動切り出し", () => viewModel.AutoCropWindowCommand.Execute(null), viewModel.CanCopy);

        menu = new Border
        {
            Child = panel,
            IsVisible = false,
            StyleName = AppStyles.MenuSurface,
        };

        return menu;
    }

    /// <summary>ツールバーなどのアイコンボタンを生成する。</summary>
    /// <param name="icon">アイコンの SVG ファイル名。</param>
    /// <param name="toolTip">ツールヒント文字列。</param>
    /// <param name="onClick">クリック時の処理。</param>
    /// <returns>アイコンだけを表示するボタン。</returns>
    private static Button IconButton(string icon, string toolTip, Action onClick, List<IconImage> icons)
    {
        var image = new Image { Source = IconLoader.LoadImage(icon, IconRasterSize, IconLoader.ForegroundColor()) };
        image.Width = IconSize;
        image.Height = IconSize;
        icons.Add(new IconImage(image, icon));

        var button = new Button
        {
            // 内容は領域の中央へ置く（MewUI の Button は内容を左上へ置く）
            Content = image.Center(),
            Padding = new Thickness(AppStyles.ToolbarPaddingX, AppStyles.ToolbarPaddingY),
            Margin = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Height = AppStyles.ToolbarButtonHeight,
            StyleName = AppStyles.ToolbarButton,
        };
        button.Cursor(CursorType.Hand);
        button.WithToolTip(toolTip, onClick);
        return button;
    }

    /// <summary>ステータスバーのアイコンボタンを生成する。</summary>
    /// <param name="icon">アイコンの SVG ファイル名。</param>
    /// <param name="toolTip">ツールヒント文字列。</param>
    /// <param name="onClick">クリック時の処理。</param>
    /// <param name="icons">テーマ変更時に描き直すための登録先。</param>
    /// <returns>ステータスバーの寸法に揃えたアイコンボタン。</returns>
    /// <remarks>
    /// ツールバーのアイコンより小さい寸法（高さ 20 / アイコン 14）にし、周囲の文字と縦位置を揃える。<br/>
    /// 内容は領域の中央へ置く。
    /// </remarks>
    private static Button StatusIconButton(string icon, string toolTip, Action onClick, List<IconImage> icons)
    {
        var image = new Image { Source = IconLoader.LoadImage(icon, IconRasterSize, IconLoader.ForegroundColor()) };
        image.Width = AppStyles.StatusIconSize;
        image.Height = AppStyles.StatusIconSize;
        icons.Add(new IconImage(image, icon));

        var button = new Button
        {
            Content = image.Center(),
            Padding = new Thickness(2, 0),
            Margin = new Thickness(1),
            Height = AppStyles.StatusButtonHeight,
            MinWidth = AppStyles.StatusButtonMinWidth,
            VerticalAlignment = VerticalAlignment.Center,
            StyleName = AppStyles.StatusButton,
        };
        button.Cursor(CursorType.Hand);
        button.WithToolTip(toolTip, onClick);
        return button;
    }

    /// <summary>拡大率メニュー（% 表示のクリックで出すプルダウン）を生成する。</summary>
    /// <param name="crop">ズーム率の適用先。</param>
    /// <returns>初期状態で非表示のプルダウン。</returns>
    /// <remarks>
    /// Avalonia 版のフライアウトと同じ選択肢（自動 + 12 段階）を同じ順序で並べる。<br/>
    /// 項目を選んだらメニューを閉じる（フライアウトと同じ振る舞い）。<br/>
    /// 「自動」は Fit（ウィンドウに合わせる）へ戻すため <see cref="CropViewControl.ZoomFit"/> を使う<br/>
    /// （<c>ZoomPercent = 0</c> は 0 以下のため無視され、Fit へは戻らない）。
    /// </remarks>
    private static Border BuildZoomMenu(CropViewControl crop)
    {
        var panel = new StackPanel().Vertical().Spacing(0).Padding(new Thickness(4));
        Border? menu = null;

        void Add(string text, Action apply)
        {
            var button = new Button
            {
                Content = new TextBlock().Text(text).FontSize(12),
                Padding = new Thickness(10, 6, 10, 6),
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = 0,
                StyleName = AppStyles.FlatButton,
            };
            button.Cursor(CursorType.Hand);
            button.OnClick(() =>
            {
                if (menu is not null)
                {
                    menu.IsVisible = false;
                }

                apply();
            });
            panel.Add(button);
        }

        // 0（自動）は Fit へ戻す
        Add("自動", crop.ZoomFit);

        foreach (var level in s_zoomMenuLevels)
        {
            var zoom = level;
            Add($"{zoom:0}%", () => crop.ZoomPercent = zoom);
        }

        menu = new Border
        {
            Child = panel,
            IsVisible = false,
            StyleName = AppStyles.MenuSurface,
        };

        return menu;
    }

    /// <summary>通知メッセージの色を取得する（Avalonia 版と同じ色調）。</summary>
    /// <returns>文字色。</returns>
    private static MewUIColor AnnounceColor()
        => MewUIColor.FromArgb(255, 0, 122, 204);

    /// <summary>プレビュー（クロップビュー）と切り出しのオーバーレイを重ねた領域を生成する。</summary>
    /// <param name="viewModel">アプリのメイン ViewModel。</param>
    /// <param name="settings">設定（切り出しの外観）。</param>
    /// <param name="crop">生成したクロップビュー（ツールバーから操作するため返す）。</param>
    /// <param name="cropSession">生成した切り出しセッション（ツールバーのボタンから開始する）。</param>
    /// <returns>プレビューと切り出しを重ねた要素。</returns>
    /// <remarks>
    /// プレビューは MewUI 版の <see cref="CropViewControl"/> を使う（内部でズームビューを内包し、矩形オーバーレイを重ねる）。<br/>
    /// 自動切り出しの検出矩形（<c>DetectedRects</c>）をオーバーレイとして表示する。<br/>
    /// ルーペは切り出しセッションが所有し、<b>切り出し中のみ</b>カーソルへ追従する（Avalonia 版と同じ）。<br/>
    /// 切り出しのオーバーレイは最上層に置き、通常は非表示にする（<see cref="PreviewCropSession.Start"/> で表示する）。
    /// </remarks>
    private static FrameworkElement BuildPreview(
        MainViewModel viewModel, Settings settings, out CropViewControl crop, out PreviewCropSession cropSession)
    {
        crop = new CropViewControl();

        // out パラメーターはラムダ内で参照できないため、ローカル変数へコピーして使う
        var cropView = crop;

        // ズーム操作ログは Avalonia 版と同じくズームビューへ注入する
        cropView.Log = s_logger;

        var host = new Grid()
            .Columns("*")
            .Rows("*");

        // 自動切り出しの検出矩形をオーバーレイへ変換する（画像座標 → ドメイン座標）
        void SyncRects()
        {
            cropView.Rects = [.. viewModel.DetectedRects.Select(static r => new OverlayRect(
                new EsRect(r.X, r.Y, r.Width, r.Height), OverlayLineStyle.Dashed,
                s_rectForeground, s_rectBackground, DetectedRectThickness))];
        }

        viewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.PreviewImage):
                    cropView.Source = viewModel.PreviewImage;
                    break;
                case nameof(MainViewModel.DetectedRects):
                    SyncRects();
                    break;
            }
        };

        cropView.Source = viewModel.PreviewImage;
        SyncRects();
        ApplyCheckerboard(cropView, settings);
        host.Children(cropView);

        // テーマが変わったら市松模様の下地色（テーマの背景色）を描き直す
        AppStyles.ThemeChanged += () => ApplyCheckerboard(cropView, settings);

        // マウス移動はヒットテスト対象の要素（ズームビュー）だけが受け取るため、ズームビューへ購読する
        // （祖先の CropViewControl へは移動イベントが届かない）。ズームビューはプレビュー領域全体を占めるため、
        // 座標系は CropViewControl と一致する。
        var zoomView = cropView.ZoomView;

        zoomView.OnMouseMove(e =>
        {
            var position = e.GetPosition(zoomView);
            UpdatePointerText(viewModel, zoomView, new EsPoint(position.X, position.Y));
        });

        zoomView.OnMouseLeave(() => viewModel.PointerDisplayText = "-");

        // 市松模様の色を変えたときにプレビューへ即時反映する
        settings.PropertyChanged += (_, e) =>
        {
            if (IsCheckerSetting(e.PropertyName))
            {
                ApplyCheckerboard(cropView, settings);
            }
        };

        cropSession = new PreviewCropSession(cropView, viewModel, settings, host);
        return host;
    }

    /// <summary>マウスポインタ位置のドメイン座標（画像ピクセル）をステータスバーへ表示する。</summary>
    /// <param name="viewModel">アプリのメイン ViewModel（表示文字列の保持元）。</param>
    /// <param name="view">プレビュー（座標変換と画像矩形の取得元）。</param>
    /// <param name="cursorDip">カーソル位置（プレビューのローカル DIP）。null の場合は "-" を表示する。</param>
    /// <remarks>
    /// 1. 画像が無い・カーソルが無い場合は "-" を表示する。<br/>
    /// 2. カーソルが画像の表示矩形の外にある場合は "-" を表示する（画像外の余白を座標として扱わない）。<br/>
    /// 3. 表示座標をドメイン座標（元画像ピクセル）へ変換し、画像の範囲内へクランプして表示する。
    /// </remarks>
    private static void UpdatePointerText(MainViewModel viewModel, ZoomViewControl view, EsPoint? cursorDip)
    {
        if (viewModel.PreviewImage is not { } image || cursorDip is not { } position)
        {
            viewModel.PointerDisplayText = "-";
            return;
        }

        // 画像の表示矩形（ビューポート座標）で内外を判定する
        var imageRect = view.ImageRectViewport();
        var x = position.X - imageRect.X;
        var y = position.Y - imageRect.Y;

        if (x < 0 || y < 0 || x >= imageRect.Width || y >= imageRect.Height)
        {
            viewModel.PointerDisplayText = "-";
            return;
        }

        var content = view.ViewportToContentImage(position);
        viewModel.PointerDisplayText =
            $"{(int)Math.Clamp(content.X, 0, image.Width - 1)}, {(int)Math.Clamp(content.Y, 0, image.Height - 1)}";
    }

    /// <summary>設定の変更がプレビューの市松模様に関係するかどうかを判定する。</summary>
    /// <param name="propertyName">変更されたプロパティ名。</param>
    /// <returns>市松模様に関係する場合は true。</returns>
    private static bool IsCheckerSetting(string? propertyName)
        => propertyName is nameof(Settings.PreviewCheckerSize)
            or nameof(Settings.PreviewCheckerColorA) or nameof(Settings.PreviewCheckerColorB);

    /// <summary>設定の市松模様（サイズ・色）をプレビューへ反映する。</summary>
    /// <param name="cropView">対象のクロップビュー。</param>
    /// <param name="settings">設定。</param>
    /// <remarks>
    /// 色は設定の <c>#AARRGGBB</c> 文字列のため、パースに失敗した場合は黒（透明）として扱う（描画を止めない）。<br/>
    /// サイズが 0 以下の場合は市松模様を描画しない。<br/>
    /// 市松模様の色は半透明（既定は 30%）のため、下地を塗る（<see cref="AppStyles.CheckerBackground"/>）。
    /// </remarks>
    private static void ApplyCheckerboard(CropViewControl cropView, Settings settings)
    {
        cropView.CheckerSize = settings.PreviewCheckerSize;
        cropView.CheckerColorA = ToSkColor(settings.PreviewCheckerColorA, new SKColor(255, 255, 255, 255));
        cropView.CheckerColorB = ToSkColor(settings.PreviewCheckerColorB, new SKColor(255, 204, 204, 204));

        // 市松模様の下地（Avalonia 版のページ背景相当。明色は白 / 暗色は黒）
        var background = AppStyles.CurrentPageBackground;
        cropView.BackgroundColor = new SKColor(background.R, background.G, background.B, background.A);
    }

    /// <summary>設定の色文字列（<c>#AARRGGBB</c>）を Skia の色へ変換する。</summary>
    /// <param name="value">色文字列。</param>
    /// <param name="fallback">パースできない場合の色。</param>
    /// <returns>変換した色。</returns>
    private static SKColor ToSkColor(string? value, SKColor fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var text = value.TrimStart('#');
        return uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var argb)
            ? new SKColor(
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF),
                (byte)((argb >> 24) & 0xFF))
            : fallback;
    }
}
