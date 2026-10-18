using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.MewUI.Icons;
using EsUtil.SaveEvidence.App.MewUI.Logging;
using EsUtil.SaveEvidence.App.MewUI.Services;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Session;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.ZoomView;
using Microsoft.Extensions.Logging;
using MewUIColor = Aprillz.MewUI.Color;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>キャプチャページ（MewUI 版）。</summary>
/// <remarks>
/// Avalonia 版 <c>CapturePage.axaml</c> に相当する。<br/>
/// プレビュー（ズーム・パン・矩形オーバーレイ）・ルーペ（カーソル追従）・切り出し・撮影・履歴操作を担う。<br/>
/// 書き出し形式の判定は <see cref="IImageWriter"/>（<see cref="EsBitmapImageWriter"/>。 コアの保存）が担い、撮影は <see cref="CaptureFlow"/>、
/// 切り出しは <see cref="CaptureSessionControl"/>（Facade）、履歴は <see cref="EditHistory"/> へ委譲する。
/// </remarks>
internal static class CapturePage
{
    /// <summary>ロガー（Avalonia 版と同じログファイルへ出力する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(CapturePage));

    /// <summary>自動切り出しの検出矩形の枠線の太さ（DIP。 Avalonia 版と同じ）。</summary>
    private const double AutoCropBorderThickness = 2;

    /// <summary>ツールバーのアイコンの一辺（DIP）。</summary>
    private const double IconSize = NavPanelMetrics.IconSize;

    /// <summary>ツールバーのアイコンを生成する解像度（ピクセル）。</summary>
    private const int IconRasterSize = 32;

    /// <summary>メニューアイコン（ハンバーガー）の大きさ（半径。 DIP）。</summary>
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

    /// <summary>拡大率メニューの選択肢（%。 0 = 自動は別途追加する）。</summary>
    /// <remarks>Avalonia 版のフライアウトと同じ段階を使う。</remarks>
    private static readonly double[] s_zoomMenuLevels = [10, 15, 25, 35, 50, 75, 100, 150, 200, 250, 350, 500];

    /// <summary>ファイル名テンプレートの最小幅（DIP）。 Avalonia 版と同じ値。</summary>
    private const double TemplateMinWidth = 100;

    /// <summary>ファイル名テンプレートの幅に足す左右の余白（DIP・枠とカーソル分）。 Avalonia 版と同じ値。</summary>
    private const double TemplateHorizontalPadding = 24;

    /// <summary>自動切り出しの検出矩形の枠線色（水色・不透明。 Avalonia 版と同じ）。</summary>
    private static readonly System.Drawing.Color s_autoCropBorder = System.Drawing.Color.FromArgb(255, 0, 191, 255);

    /// <summary>自動切り出しの検出矩形の枠線色（ホバー中・赤・不透明。 Avalonia 版と同じ）。</summary>
    private static readonly System.Drawing.Color s_autoCropHoverBorder = System.Drawing.Color.FromArgb(255, 255, 59, 48);

    /// <summary>自動切り出しの検出矩形の塗り色（ホバー中・赤 16%。 Avalonia 版と同じ）。</summary>
    private static readonly System.Drawing.Color s_autoCropHoverFill = System.Drawing.Color.FromArgb(40, 255, 59, 48);

    /// <summary>ページの内容を生成する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <returns>ページの内容。</returns>
    /// <remarks>
    /// 1. 3 行の Grid（ツールバー / プレビュー / ステータス）を組み立てる。<br/>
    /// 2. プレビューは Star 行に置き、内容領域の残余全体を占めさせる（ズームビュー）。<br/>
    /// 3. プレビューの上へルーペ（カーソル追従）と切り出しのオーバーレイを重ねる。<br/>
    /// 4. 各コマンドは ViewModel へ直接接続する（キャプチャ開始は <see cref="CaptureFlow"/>、切り出しは
    ///    <see cref="CaptureSessionControl"/>（Facade）、履歴は <see cref="EditHistory"/> へ委譲する）。<br/>
    /// 5. 表示のたびの更新は不要（ViewModel の変更通知で追従する）。
    /// </remarks>
    public static PageContent Build(AppServices services)
    {
        var viewModel = services.Main;
        var settings = services.Settings.Current;
        var preview = BuildPreview(viewModel, settings, out var crop, out var captureSession);

        // グローバルホットキーからの開始経路も同じ Facade を通す（撮影と切り出しの排他を 1 箇所で効かせる）
        viewModel.StartSelectionMode = type => CaptureFlow.Start(type, captureSession);

        // 切り出しの開始・確定・キャンセルの判断は 1 つの窓口へ集約する
        // （ボタン・キー・メニューの 3 経路で同じ規則を使う。 Avalonia 版と同じ）
        void ToggleCrop() => ToggleCropSession(captureSession, crop, viewModel, services);

        // 撮影結果の反映（Undo 退避・プレビュー差し替え・ステータス通知）は CaptureCompleted（同期）で行う。
        // イベントはオーナーの再表示より前に発行されるため、画像を差し替えてからアプリが再表示される
        // （フロー: APP 非表示 → 撮影 → 画像変更（イベント受信） → APP 表示。 Avalonia 版と同じ）
        captureSession.CaptureCompleted += (_, e) => ApplyCaptureResult(services, viewModel, e);

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

        // メニューの外側をクリックしたら閉じるための覆い。
        // メニューより背面・ページより前面に置き、開いている間だけ表示する
        // （MewUI のイベントは親へ伝播しないため、外側のクリックを拾うには専用の面が要る）。
        var dismissLayer = new Border
        {
            Background = MewUIColor.Transparent,
            IsVisible = false,
        };

        // ── メニュー（☰ の下へ出すプルダウン。 Avalonia 版の MenuFlyout に相当）──
        // 項目を選んで閉じたときも覆いを隠す（隠さないと次のクリックが覆いに吸われる）
        var menuPanel = BuildMenu(
            viewModel, services, captureSession, ToggleCrop, () => dismissLayer.IsVisible = false);

        // ── 拡大率メニュー（% 表示のクリックで出す。 Avalonia 版のフライアウトに相当）──
        var zoomMenu = BuildZoomMenu(crop);

        // メニューの開閉。 メニューを開いているときに同じボタンを押すと、押下は覆い（dismissLayer）が受け取り、
        // その後に届くマウス アップで ClickableToolTip の補完（ClickableToolTip のコメント参照）が走って
        // 「閉じた直後に開き直す」ため、覆いで閉じた直後のクリックは無視する（実測 2026-10-18）。
        var suppressMenuToggleUntil = DateTime.MinValue;

        // メニュー・拡大率メニューを閉じる（外側のクリック・ページの切り替え・ウィンドウが非アクティブになったとき）
        void CloseMenus()
        {
            menuPanel.IsVisible = false;
            zoomMenu.IsVisible = false;
            dismissLayer.IsVisible = false;
            suppressMenuToggleUntil = DateTime.UtcNow.AddMilliseconds(300);
        }

        dismissLayer.OnMouseDown(_ => CloseMenus());

        // メニュー・拡大率メニューの表示を切り替え、覆いの表示も合わせる
        void ShowMenu(bool show)
        {
            menuPanel.IsVisible = show;
            if (show)
            {
                zoomMenu.IsVisible = false;
            }

            dismissLayer.IsVisible = show || zoomMenu.IsVisible;
        }

        void ShowZoomMenu(bool show)
        {
            zoomMenu.IsVisible = show;
            if (show)
            {
                menuPanel.IsVisible = false;
            }

            dismissLayer.IsVisible = show || menuPanel.IsVisible;
        }

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
            if (DateTime.UtcNow < suppressMenuToggleUntil)
            {
                return;
            }

            ShowMenu(!menuPanel.IsVisible);
        });

        // 保存するファイル名テンプレート（保存ボタンの左）
        // 幅はテキストの実測に合わせ、ツールバーが折り返さない幅を上限にする（下の UpdateTemplateWidth）
        var template = new TextBox
        {
            Text = viewModel.FileNameTemplateText,
            MinWidth = TemplateMinWidth,
            Height = AppStyles.ToolbarButtonHeight,
            Margin = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
        };
        template.OnTextChanged(value => viewModel.FileNameTemplateText = value);

        var save = IconButton("ic_fluent_save_24_regular.svg", "画像を保存", () => _ = viewModel.SaveImageAsync(), iconImages);
        save.BindIsEnabled(canSave);

        var screenSelect = IconButton(
            "ic_fluent_rectangle_landscape_24_regular.svg", "スクリーン選択キャプチャ",
            () => CaptureFlow.Start(CaptureType.ScreenSelect, captureSession), iconImages);
        var windowSelect = IconButton(
            "ic_fluent_window_24_regular.svg", "ウィンドウ選択キャプチャ",
            () => CaptureFlow.Start(CaptureType.WindowSelect, captureSession), iconImages);
        var areaSelect = IconButton(
            "ic_fluent_select_object_24_regular.svg", "領域選択キャプチャ",
            () => CaptureFlow.Start(CaptureType.AreaSelect, captureSession), iconImages);

        var cropButton = IconButton("ic_fluent_crop_24_regular.svg", "切り出し（切出しモード開始/終了）", ToggleCrop, iconImages);
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

        // ファイル名テンプレートの幅をテキストの実測に合わせる（上限は折り返さない幅）。
        // ツールバーの幅が変わったら計算し直す
        var measureText = new TextBlock().FontSize(template.FontSize);
        void UpdateTemplateWidth()
        {
            measureText.Text(template.Text ?? string.Empty);
            measureText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var width = measureText.DesiredSize.Width + TemplateHorizontalPadding;
            template.Width = Math.Max(TemplateMinWidth, Math.Min(width, ResolveTemplateMaxWidth()));
        }

        double ResolveTemplateMaxWidth()
        {
            var available = toolbar.Bounds.Width;
            if (available <= 1)
            {
                return TemplateMinWidth;
            }

            var others = 0.0;
            foreach (var child in toolbar.Children)
            {
                if (ReferenceEquals(child, template) || child is not FrameworkElement element)
                {
                    continue;
                }

                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                others += child.DesiredSize.Width + element.Margin.Left + element.Margin.Right;
            }

            return Math.Max(TemplateMinWidth, available - others - template.Margin.Left - template.Margin.Right);
        }

        // 入力に合わせて幅を計算し直す（テキスト変更・ツールバーの幅の変更）
        template.OnTextChanged(value =>
        {
            viewModel.FileNameTemplateText = value;
            UpdateTemplateWidth();
        });
        toolbar.SizeChanged += _ => UpdateTemplateWidth();
        UpdateTemplateWidth();

        // ── ステータスバー ──
        // 左: ポインタ座標（固定幅） | 画像サイズ（固定幅） | 保存済み/未 | 保存ファイル名 | 通知
        // バインドはプロパティ名を推定するためインライン式で指定する
        var pointerLabel = new Label { Width = 70 };
        pointerLabel.FontSize(AppStyles.StatusFontSize);
        pointerLabel.VerticalAlignment = VerticalAlignment.Center;
        pointerLabel.Bind(Label.TextProperty, viewModel, static x => x.PointerDisplayText);
        pointerLabel.ToolTip("マウスポインタ位置（対象座標 = 画像ピクセル）");

        var imageSizeLabel = new Label { Width = 80 };
        imageSizeLabel.FontSize(AppStyles.StatusFontSize);
        imageSizeLabel.VerticalAlignment = VerticalAlignment.Center;
        imageSizeLabel.Bind(Label.TextProperty, viewModel, static x => x.ImageSizeDisplayText);
        imageSizeLabel.ToolTip("画像サイズ（対象座標 = 元画像ピクセル）");

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

        // 右: 拡大率（⊖ / ⊕ / Fit / % 表示。 % はクリックで拡大率メニューを出す）
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

        // ズームスライダ（10〜500%）。 Avalonia 版と同じ範囲・幅・高さを使う
        zoomSlider = new Slider
        {
            Minimum = ZoomSliderMinimum,
            Maximum = ZoomSliderMaximum,
            Width = 120,
            Height = AppStyles.StatusSliderHeight,
            VerticalAlignment = VerticalAlignment.Center,
            Value = crop.ZoomPercent
        };
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
        zoomPercentButton.WithToolTip("クリックで拡大率を選択", () => ShowZoomMenu(!zoomMenu.IsVisible));

        var statusRight = new StackPanel()
            .Horizontal()
            .Spacing(4)
            .Children(zoomOut, zoomSlider, zoomIn, fit, zoomPercentButton);

        var statusGrid = new Grid().Columns("*,Auto").Rows("Auto");
        statusLeft.VerticalAlignment = VerticalAlignment.Center;
        statusRight.VerticalAlignment = VerticalAlignment.Center;
        statusGrid.Add(statusLeft.Column(0));
        statusRight.HorizontalAlignment = HorizontalAlignment.Right;
        statusGrid.Add(statusRight.Column(1));

        // ステータスバーの高さは 3 ページ共通（テキストとボタンが収まる最小の高さ）
        var status = new Grid().Columns("*").Rows("Auto");
        status.Add(statusGrid);

        // ツールバー / プレビュー（残余） / ステータスの 3 段。 余白は Avalonia 版と同じく付けない
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

        // 覆いはページの上・メニューの下へ置く（開いている間だけ表示する）
        root.Add(dismissLayer.Column(0));
        root.Add(menuPanel.Column(0));
        root.Add(zoomMenu.Column(0));

        // テーマが変わったらスタイルで表せない値（アイコンの色）を描き直す
        AppStyles.ThemeChanged += (_, _) => RefreshIcons(iconImages);

        return new PageContent(
            root,
            OnKeyDown: e =>
            {
                // 切り出しの開始・確定・キャンセルを Ctrl+R でも操作できるようにする
                // （Avalonia 版はメニュー項目の InputGesture が同じ役割を担う）
                if (e.ControlKey && e.Key == Key.R)
                {
                    ToggleCrop();
                    e.Handled = true;
                    return;
                }

                // Escape でメニューを閉じる
                if (e.Key == Key.Escape && (menuPanel.IsVisible || zoomMenu.IsVisible))
                {
                    CloseMenus();
                    e.Handled = true;
                }
            },
            // ページを離れたとき・ウィンドウが非アクティブになったときにメニューを閉じる
            // （メニューを開いたままナビゲーションを操作しても閉じない、との指摘への対応）
            OnDeactivated: CloseMenus,
            OnWindowDeactivated: CloseMenus);
    }

    /// <summary>メニューのプルダウン（ツールバーの ☰ から開く）を生成する。</summary>
    /// <param name="viewModel">アプリのメイン ViewModel（各コマンドの接続先）。</param>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <param name="captureSession">撮影・切り出しの Facade（切り出しメニューの接続先）。</param>
    /// <param name="toggleCrop">切り出しの開始・確定・キャンセルを行う窓口（ツールバー・キーと同じもの）。</param>
    /// <param name="onClosed">項目の選択でメニューを閉じたときに呼ぶ処理（外側クリック用の覆いを隠す）。</param>
    /// <returns>初期状態で非表示のプルダウン。</returns>
    /// <remarks>
    /// Avalonia 版の <c>MenuFlyout</c> と同じ項目・同じ区切り・同じショートカット表記で並べる。<br/>
    /// ショートカットは表示のみ（Avalonia 版も <c>MenuItem.InputGesture</c> の表示であり、キー操作は受け付けない）。
    /// </remarks>
    private static Border BuildMenu(
        MainViewModel viewModel,
        AppServices services,
        CaptureSessionControl captureSession,
        Action toggleCrop,
        Action? onClosed = null)
    {
        var panel = new StackPanel().Vertical().Spacing(0).Padding(new Thickness(4));
        Border? menu = null;

        // 項目を選んだらメニューを閉じる（Avalonia 版の MenuFlyout と同じ振る舞い）。
        // 覆い（外側クリックの受け皿）も一緒に隠す（隠さないと次のクリックが覆いに吸われる）
        void Close()
        {
            if (menu is not null)
            {
                menu.IsVisible = false;
            }

            onClosed?.Invoke();
        }

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
        Add("Clipboard - コピー", () => _ = viewModel.CopyToClipboardAsync(), viewModel.CanCopy, "Ctrl+C");
        Add("Clipboard - 貼り付け", () => _ = viewModel.PasteFromClipboardAsync(), viewModel.CanPaste, "Ctrl+V");
        AddSeparator();
        Add("Image - 元に戻す", () => EditHistory.Undo(services), viewModel.HasPreviewImage, "Ctrl+Z");
        Add("Image - 元に戻すを取り消す", () => EditHistory.Redo(services), viewModel.HasPreviewImage, "Ctrl+Y");
        AddSeparator();
        Add("Capture - スクリーン選択", () => CaptureFlow.Start(CaptureType.ScreenSelect, captureSession));
        Add("Capture - ウィンドウ選択", () => CaptureFlow.Start(CaptureType.WindowSelect, captureSession));
        Add("Capture - 範囲選択", () => CaptureFlow.Start(CaptureType.AreaSelect, captureSession));
        AddSeparator();
        Add("Crop - 切り出し", toggleCrop, viewModel.CanCopy, "Ctrl+R");
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
    /// <param name="icons">生成したアイコン画像の登録先（テーマ変更時の描き直しに使う）。</param>
    /// <returns>アイコンだけを表示するボタン。</returns>
    private static Button IconButton(string icon, string toolTip, Action onClick, List<IconImage> icons)
    {
        var image = new Image
        {
            Source = IconLoader.LoadImage(icon, IconRasterSize, IconLoader.ForegroundColor()),
            Width = IconSize,
            Height = IconSize
        };
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

        // 押している間は背景を濃くする（ホバーより濃い）。
        // MewUI 0.21.1 はツールヒントを閉じる際に押下状態を失うため（ClickableToolTip のコメント参照）、
        // 状態ではなくスタイル名の差し替えで表す。
        button.MouseDown += e =>
        {
            if (e.Button == MouseButton.Left)
            {
                button.StyleName = AppStyles.ToolbarButtonPressed;
            }
        };
        button.MouseUp += _ => button.StyleName = AppStyles.ToolbarButton;
        button.MouseLeave += () => button.StyleName = AppStyles.ToolbarButton;

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
        var image = new Image
        {
            Source = IconLoader.LoadImage(icon, IconRasterSize, IconLoader.ForegroundColor()),
            Width = AppStyles.StatusIconSize,
            Height = AppStyles.StatusIconSize
        };
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
                menu?.IsVisible = false;

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
    /// <param name="captureSession">生成した撮影・切り出しの Facade（ツールバーのボタン・メニューから開始する）。</param>
    /// <returns>プレビューと切り出しを重ねた要素。</returns>
    /// <remarks>
    /// プレビューは MewUI 版の <see cref="CropViewControl"/> を使う（内部でズームビューを内包し、矩形オーバーレイを重ねる）。<br/>
    /// 自動切り出しの検出矩形（<c>DetectedRects</c>）をオーバーレイとして表示する。<br/>
    /// ルーペは切り出しセッションが所有し、<b>切り出し中のみ</b>カーソルへ追従する（Avalonia 版と同じ）。<br/>
    /// 切り出しのオーバーレイはセッションの開始時に最上層へ加え、終了時に取り外す（Facade が担う）。
    /// </remarks>
    private static FrameworkElement BuildPreview(
        MainViewModel viewModel, Settings settings, out CropViewControl crop, out CaptureSessionControl captureSession)
    {
        crop = new CropViewControl();

        // out パラメーターはラムダ内で参照できないため、ローカル変数へコピーして使う
        var cropView = crop;

        // ズーム操作ログは Avalonia 版と同じくズームビューへ注入する
        cropView.Log = s_logger;

        var host = new Grid()
            .Columns("*")
            .Rows("*");

        // 自動切り出しのホバー強調の外観（内側の塗りと枠線）を CropView へ与える。
        // 色・枠線の太さ・表示可否は描画にだけ必要な設定であり、強調の描画手順は CropViewControl（Parts）が単一所有する。
        cropView.HighlightAppearance = CropViewControl.HighlightDefaultAppearance with
        {
            InnerFillColor = s_autoCropHoverFill,
            InnerBorderColor = s_autoCropHoverBorder,
            InnerBorderThickness = AutoCropBorderThickness,
        };

        // 自動切り出しの検出矩形をオーバーレイへ変換する（画像座標 → 対象座標）。
        // ホバー中の 1 件は CropView 側から外し、CropView の強調（枠線 + 半透明の塗り）で強調する
        // （両 UI で同じ見た目にするため、塗りも描く）。 DIP 座標への変換は CropView が単一所有する。
        void SyncRects()
        {
            var hovered = viewModel.IsAutoCropActive ? viewModel.HoveredRect : null;
            cropView.Rects = [.. viewModel.DetectedRects
                .Where(r => r != hovered)
                .Select(r => new OverlayRect(
                    new EsRect(r.X, r.Y, r.Width, r.Height), OverlayLineStyle.Solid,
                    s_autoCropBorder, System.Drawing.Color.Transparent, AutoCropBorderThickness))];

            cropView.HighlightSourceRect = hovered?.ToEsRect();
        }

        viewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.PreviewImage):
                    cropView.Source = viewModel.PreviewImage;
                    break;
                case nameof(MainViewModel.IsAutoCropActive):
                case nameof(MainViewModel.DetectedRects):
                case nameof(MainViewModel.HoveredRect):
                    SyncRects();
                    break;
            }
        };

        cropView.Source = viewModel.PreviewImage;
        SyncRects();
        ApplyPreviewBackground(cropView);
        cropView.CenterAlign = settings.CenterAlign;
        host.Children(cropView);

        // 画像の 4 辺・4 隅の影（幅・濃さは App.Core の ShadowPlan が単一所有する）。
        // 画像と同じ描画面へ先に描く（重ねると画像の端の混色の下地が背景になり、白い線として見える）
        cropView.ZoomView.SurroundRenderer = ShadowRenderer.Render;

        // テーマが変わったら下地色（テーマの背景色）を描き直す
        AppStyles.ThemeChanged += (_, _) => ApplyPreviewBackground(cropView);

        // マウス移動はヒットテスト対象の要素（ズームビュー）だけが受け取るため、ズームビューへ購読する
        // （祖先の CropViewControl へは移動イベントが届かない）。 ズームビューはプレビュー領域全体を占めるため、
        // 座標系は CropViewControl と一致する。
        var zoomView = cropView.ZoomView;

        zoomView.OnMouseMove(e =>
        {
            var position = e.GetPosition(zoomView);
            var point = new EsPoint(position.X, position.Y);
            UpdatePointerText(viewModel, zoomView, point);

            // 自動切り出し中はホバー中の矩形を更新する（Avalonia 版と同じ判定）
            if (viewModel.IsAutoCropActive)
            {
                var hovered = FindAutoCropHit(viewModel, zoomView, point);
                if (viewModel.HoveredRect != hovered)
                {
                    viewModel.HoveredRect = hovered;
                }
            }
        });

        zoomView.OnMouseLeave(() =>
        {
            viewModel.PointerDisplayText = "-";
            viewModel.HoveredRect = null;
        });

        // ダブルクリックで矩形を確定し、矩形の外側ならキャンセルする（Avalonia 版と同じ操作）
        zoomView.OnMouseDoubleClick(e =>
        {
            if (!viewModel.IsAutoCropActive)
            {
                return;
            }

            var position = e.GetPosition(zoomView);
            if (FindAutoCropHit(viewModel, zoomView, new EsPoint(position.X, position.Y)) is { } hit)
            {
                viewModel.ConfirmAutoCropCommand.Execute(hit);
                return;
            }

            // 領域外のダブルクリックはキャンセル（切り出しモードの「矩形外クリック: キャンセル」と同じ）
            viewModel.CancelAutoCropCommand.Execute(null);
        });

        // 中央寄せを変えたときにプレビューへ即時反映する
        settings.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(Settings.CenterAlign), StringComparison.Ordinal))
            {
                cropView.CenterAlign = settings.CenterAlign;
            }
        };

        // 撮影・切り出しの Facade。 撮影と切り出しの排他（同時に 1 つだけ）とセッションの寿命を単一所有する。
        // 撮影に必要なサービス（キャプチャ・要素検出・ウィンドウ操作）の生成は Facade が担う（ホストは組み立てない）。
        // ログは Facade 自身のカテゴリで記録する（ページのログと混ざらないようにする）
        captureSession = new CaptureSessionControl(
            CaptureSettingsMapper.From(settings, DpiHelper.GetDpiForPoint(new Point(0, 0)) / 96.0),
            AppLog.CreateLogger(nameof(CaptureSessionControl)));

        // 設定変更を Facade へ即時反映する（部分更新・検証・同値スキップ・適用ログはコアが単一所有する）。
        // ページは 1 度だけ生成されるため、購読しないと設定画面での変更が切り出し・撮影へ届かない。
        // 検証（範囲外の値）で例外になっても UI を壊さないよう例外保護付きで行う（Avalonia 版と同じ扱い）。
        // out パラメーターはラムダ内で参照できないため、ローカル変数へコピーして使う
        var session = captureSession;
        settings.PropertyChanged += (_, e) =>
        {
            try
            {
                session.AppearanceSettings = CaptureSettingsMapper.From(
                    settings, DpiHelper.GetDpiForPoint(new Point(0, 0)) / 96.0);
            }
            catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
            {
                s_logger.LogSettingsApplyError(ex, e.PropertyName ?? string.Empty);
            }
        };
        return host;
    }

    /// <summary>撮影結果をプレビューへ反映する（CaptureCompleted イベントのハンドラ）。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト（履歴の所有者）。</param>
    /// <param name="viewModel">アプリのメイン ViewModel（画像の所有者）。</param>
    /// <param name="e">撮影結果。 キャンセル時は結果が空。</param>
    /// <remarks>
    /// 1. 撮影前に表示していた画像を Undo 履歴へ積む（撮影前の状態へ戻せるようにする）。<br/>
    /// 2. 撮影結果をプレビューへ設定する（所有権は ViewModel へ移る）。<br/>
    /// 3. 結果（サイズ）をステータスへ通知する。<br/>
    /// キャンセル時は何もしない（プレビュー・Undo・ステータスとも変更しない）。
    /// </remarks>
    private static void ApplyCaptureResult(AppServices services, MainViewModel viewModel, CaptureCompletedEventArgs e)
    {
        if (e.Result?.Image is not { } captured)
        {
            return;
        }

        services.UndoHistory.Push(viewModel.PreviewImage);
        viewModel.SetPreviewImage(captured);
        // 通知の文言はコアが単一所有する（キャプチャ結果は画像の差し替えより後に通知する）
        viewModel.StatusMessage = StatusMessages.CaptureCompleted(captured.Width, captured.Height);
    }

    /// <summary>切り出しの開始・確定・キャンセルを 1 つの操作で切り替える（ボタン・キー・メニュー共通）。</summary>
    /// <param name="captureSession">撮影・切り出しの Facade。</param>
    /// <param name="cropView">切り出しを行う複合コントロール（表示中の画像が切り出しの対象）。</param>
    /// <param name="viewModel">アプリのメイン ViewModel（切り出しの適用先）。</param>
    /// <param name="services">アプリ全体で共有するコアオブジェクト（履歴の所有者）。</param>
    /// <remarks>
    /// どの操作を行うかの判断はコア（<c>CropSessionActions.Resolve</c>）が単一所有する<br/>
    /// （未開始なら開始 / 選択済みなら確定 / 未選択ならキャンセル）。 Avalonia 版の切り出しボタンと同じ規則を使う。
    /// </remarks>
    private static void ToggleCropSession(
        CaptureSessionControl captureSession, CropViewControl cropView, MainViewModel viewModel, AppServices services)
    {
        switch (captureSession.ResolveCropAction())
        {
            case CropSessionAction.Start:
                _ = StartCropSessionAsync(captureSession, cropView, viewModel, services);
                break;
            case CropSessionAction.Confirm:
                // 開始済み・矩形あり → 現在の選択矩形で確定（既に待機しているタスクが完了する）
                captureSession.Confirm();
                break;
            default:
                // 開始済み・矩形なし → キャンセル（矩形選択前に操作した場合の抜け道）
                if (captureSession.IsCropping)
                {
                    captureSession.Cancel();
                }

                break;
        }
    }

    /// <summary>切り出しセッションを開始し、確定した範囲で画像を切り出す。</summary>
    /// <param name="captureSession">撮影・切り出しの Facade。</param>
    /// <param name="cropView">切り出しを行う複合コントロール（表示中の画像が切り出しの対象）。</param>
    /// <param name="viewModel">アプリのメイン ViewModel（切り出しの適用先）。</param>
    /// <param name="services">アプリ全体で共有するコアオブジェクト（履歴の所有者）。</param>
    /// <returns>待機タスク。</returns>
    /// <remarks>
    /// 切り出しの対象画像は表示中の画像（<see cref="CropViewControl.Source"/>）から解決されるため、画像を別引数で渡さない。<br/>
    /// 確定（Enter / ダブルクリック / 確定操作）で切り出し範囲（コア型）、キャンセルで 0x0 の空矩形が返る。<br/>
    /// 開始できなかった場合（他のセッションが実行中）は結果 null が即時返る（無駄な待機をしない）。
    /// </remarks>
    private static async Task StartCropSessionAsync(
        CaptureSessionControl captureSession, CropViewControl cropView, MainViewModel viewModel, AppServices services)
    {
        if (Application.Current?.MainWindow is not { } hostWindow)
        {
            return;
        }

        var bounds = await captureSession.StartCropAsync(cropView, hostWindow).ConfigureAwait(true);

        // 確定時のみ切り出す（開始できなかった場合は null、キャンセルは空の矩形。
        // 空の判定はコアの契約（EsRectInt.IsEmpty）が単一所有する）
        if (bounds is not { } rect || rect.IsEmpty)
        {
            return;
        }

        // 切り出し前の画像へ戻せるように履歴へ積む（画像の差し替えは ViewModel が単一所有する）
        if (viewModel.PreviewImage is { } source)
        {
            services.UndoHistory.Push(source);
        }

        viewModel.CropPreviewCommand.Execute(rect);
    }

    /// <summary>マウスポインタ位置の対象座標（画像ピクセル）をステータスバーへ表示する。</summary>
    /// <param name="viewModel">アプリのメイン ViewModel（表示文字列の保持元）。</param>
    /// <param name="view">プレビュー（座標変換と画像矩形の取得元）。</param>
    /// <param name="cursorDip">カーソル位置（プレビューのローカル DIP）。 null の場合は "-" を表示する。</param>
    /// <remarks>
    /// 1. 画像が無い・カーソルが無い場合は "-" を表示する。<br/>
    /// 2. カーソルが画像の表示矩形の外にある場合は "-" を表示する（画像外の余白を座標として扱わない）。<br/>
    /// 3. 表示座標を対象座標（元画像ピクセル）へ変換し、画像の範囲内へクランプして表示する。
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

        var content = view.ClientToDisplayedSource(position);
        viewModel.PointerDisplayText =
            $"{(int)Math.Clamp(content.X, 0, image.Width - 1)}, {(int)Math.Clamp(content.Y, 0, image.Height - 1)}";
    }

    /// <summary>プレビュー上のカーソル位置にある自動切り出しの矩形（面積が最小のもの）を求める。</summary>
    /// <param name="viewModel">アプリのメイン ViewModel（検出矩形の保持元）。</param>
    /// <param name="view">プレビュー（座標変換と画像矩形の取得元）。</param>
    /// <param name="cursorDip">カーソル位置（プレビューのローカル DIP）。</param>
    /// <returns>カーソル位置を含む矩形（面積が最小）。 画像外・矩形なしの場合は null。</returns>
    /// <remarks>
    /// 1. 画像が無い場合は null を返す。<br/>
    /// 2. カーソルが画像の表示矩形の外にある場合は null を返す（画像外の余白を座標として扱わない）。<br/>
    /// 3. 表示座標を対象座標（元画像ピクセル）へ変換し、<see cref="MainViewModel.HitDetectedRect"/> へ委譲する<br/>
    ///    （矩形の判定はコアが単一所有する）。
    /// </remarks>
    private static EsRectInt? FindAutoCropHit(MainViewModel viewModel, ZoomViewControl view, EsPoint cursorDip)
    {
        if (viewModel.PreviewImage is not { } image)
        {
            return null;
        }

        // 画像の表示矩形（ビューポート座標）で内外を判定する
        var imageRect = view.ImageRectViewport();
        var localX = cursorDip.X - imageRect.X;
        var localY = cursorDip.Y - imageRect.Y;
        if (localX < 0 || localY < 0 || localX >= imageRect.Width || localY >= imageRect.Height)
        {
            return null;
        }

        var content = view.ClientToDisplayedSource(cursorDip);
        return viewModel.HitDetectedRect(
            Math.Clamp(content.X, 0, image.Width - 1),
            Math.Clamp(content.Y, 0, image.Height - 1));
    }

    /// <summary>プレビューの下地色（画像領域の背景色）を反映する。</summary>
    /// <param name="cropView">対象のクロップビュー。</param>
    /// <remarks>
    /// 画像が無い部分（ペーストボード）はこの色の単色になる（市松模様は描かない）。<br/>
    /// 色は <see cref="AppStyles.CurrentImageAreaBackground"/>（実体は <see cref="AppPalette"/>（App.Core））が単一所有する。
    /// </remarks>
    private static void ApplyPreviewBackground(CropViewControl cropView)
    {
        var background = AppStyles.CurrentImageAreaBackground;

        // 下地は画像領域の背景色（S / R / G / B の並びは設定の #AARRGGBB と同じ）
        // Fit は影も含めて収める（画像の周囲に影を描くため、その分だけ内側へ収めた大きさで計算する）
        cropView.Appearance = ZoomViewAppearance.Default with
        {
            BackgroundColor = System.Drawing.Color.FromArgb(background.A, background.R, background.G, background.B),
            FitInset = ShadowPlan.BlurRadius,
        };
    }

}
