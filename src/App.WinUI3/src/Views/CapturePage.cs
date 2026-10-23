using DrawingColor = System.Drawing.Color;
using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Common;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.App.Core.Icons;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.WinUI3.Icons;
using EsUtil.SaveEvidence.App.WinUI3.Logging;
using EsUtil.SaveEvidence.App.WinUI3.Services;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Session;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.ZoomView;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.System;
using Windows.UI;

namespace EsUtil.SaveEvidence.App.WinUI3.Views;

/// <summary>キャプチャ ページ（WinUI 3 版）。</summary>
/// <remarks>
/// プレビュー（ズーム・パン・矩形オーバーレイ）・ルーペ（カーソル追従）・切り出し・撮影・履歴操作を担う。<br/>
/// 書き出し形式の判定と保存はコアの実装（<c>IImageWriter</c>）が担い、撮影は <see cref="CaptureFlow"/>、<br/>
/// 切り出しは <see cref="CaptureSessionControl"/>（撮影セッション）、履歴は <see cref="EditHistory"/> へ委譲する。<br/>
/// <br/>
/// <b>構成</b>: 3 行（ツールバー / プレビュー / ステータス バー）。 プレビューは残余の全体を占める。<br/>
/// <b>撮影結果の反映</b>: 撮影セッションの <c>CaptureCompleted</c>（同期）で Undo 退避とプレビューの<br/>
/// 差し替えを行う（画像変更の唯一の経路）。
/// </remarks>
internal static class CapturePage
{
    /// <summary>ロガー（設定の反映など、ホスト固有の失敗を記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(CapturePage));

    /// <summary>自動切り出しの検出矩形の枠線の太さ（DIP）。</summary>
    private const double AutoCropBorderThickness = 2;

    /// <summary>ズーム スライダの最小値（%）。</summary>
    private const double ZoomSliderMinimum = 10;

    /// <summary>ズーム スライダの最大値（%）。</summary>
    private const double ZoomSliderMaximum = 500;

    /// <summary>ファイル名テンプレートの最小幅（DIP）。 Avalonia 版と同じ値。</summary>
    private const double TemplateMinWidth = 100;

    /// <summary>ファイル名テンプレートの幅に足す左右の余白（DIP・枠とカーソル分）。 Avalonia 版と同じ値。</summary>
    private const double TemplateHorizontalPadding = 24;

    /// <summary>自動切り出しの検出矩形の枠線色（水色・不透明）。</summary>
    private static readonly DrawingColor s_autoCropBorder = DrawingColor.FromArgb(255, 0, 191, 255);

    /// <summary>自動切り出しの検出矩形の枠線色（ホバー中・赤・不透明）。</summary>
    private static readonly DrawingColor s_autoCropHoverBorder = DrawingColor.FromArgb(255, 255, 59, 48);

    /// <summary>自動切り出しの検出矩形の塗り色（ホバー中・赤 16%）。</summary>
    private static readonly DrawingColor s_autoCropHoverFill = DrawingColor.FromArgb(40, 255, 59, 48);

    /// <summary>ステータスの通知メッセージの色。</summary>
    private static readonly Color s_announceColor = Color.FromArgb(255, 0, 122, 204);

    /// <summary>ページの内容を生成する。</summary>
    /// <param name="services">アプリ全体で共有するコア オブジェクト。</param>
    /// <param name="window">オーナー ウィンドウ（撮影と切り出しの実行に使う）。</param>
    /// <returns>ページの内容。</returns>
    /// <remarks>
    /// 1. 3 行の Grid（ツールバー / プレビュー / ステータス）を組み立てる。<br/>
    /// 2. 各コマンドはメイン ViewModel へ直接接続する（撮影は <see cref="CaptureFlow"/>、切り出しは撮影セッション、<br/>
    ///    履歴は <see cref="EditHistory"/> へ委譲する）。<br/>
    /// 3. 表示のたびの更新は不要（変更通知の購読で追従する）。
    /// </remarks>
    public static PageContent Build(AppServices services, MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(window);

        var viewModel = services.Main;
        var settings = services.Settings.Current;
        var preview = BuildPreview(services, window, out var cropView, out var captureSession);

        // グローバル ホットキーからの開始経路も同じ撮影セッションを通す（撮影と切り出しの排他を 1 箇所で効かせる）
        viewModel.StartSelectionMode = type => CaptureFlow.Start(type, window, captureSession);

        void ToggleCrop() => ToggleCropSession(captureSession, cropView, viewModel, services);

        // 撮影結果の反映（Undo 退避・プレビュー差し替え・ステータス通知）
        captureSession.CaptureCompleted += (_, e) => ApplyCaptureResult(services, viewModel, e);

        var synchronizingZoom = false;
        Slider? zoomSlider = null;

        var zoomText = CreateLabel(viewModel.ZoomDisplayText, AppStyles.StatusFontSize);
        var pointerText = CreateLabel(viewModel.PointerDisplayText, AppStyles.StatusFontSize);
        pointerText.Width = 70;
        var imageSizeText = CreateLabel(viewModel.ImageSizeDisplayText, AppStyles.StatusFontSize);
        imageSizeText.Width = 80;
        var saveStatusText = CreateLabel(viewModel.SaveStatusText, AppStyles.StatusFontSize);
        var savedFileNameText = CreateLabel(viewModel.SavedFileName, AppStyles.StatusFontSize);
        var messageText = CreateLabel(viewModel.StatusMessage, AppStyles.StatusFontSize);
        messageText.Foreground = new SolidColorBrush(s_announceColor);

        var zoomOut = CreateIconButton("ic_fluent_subtract_circle_24_regular.svg", "ズームアウト", cropView.ZoomOut, AppStyles.StatusIconSize);
        var zoomIn = CreateIconButton("ic_fluent_add_circle_24_regular.svg", "ズームイン", cropView.ZoomIn, AppStyles.StatusIconSize);

        var fit = CreateTextButton("Fit", "ウィンドウに合わせて表示（Fit）", cropView.ZoomFit);
        var zoomPercent = CreateTextButton("100%", "クリックで拡大率を選択", null);
        zoomPercent.MinWidth = 56;
        zoomPercent.Content = zoomText;
        zoomPercent.Flyout = BuildZoomMenu(cropView);

        zoomSlider = new Slider
        {
            Minimum = ZoomSliderMinimum,
            Maximum = ZoomSliderMaximum,
            Width = 120,
            Height = AppStyles.StatusSliderHeight,
            Value = cropView.ZoomPercent,
            VerticalAlignment = VerticalAlignment.Center,
        };
        zoomSlider.ValueChanged += (_, e) =>
        {
            // スライダ → ズームの同期中はループを防ぐため無視する
            if (!synchronizingZoom)
            {
                cropView.ZoomPercent = e.NewValue;
            }
        };

        var toolbar = BuildToolbar(services, viewModel, ToggleCrop, captureSession);
        var statusLeft = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        statusLeft.Children.Add(pointerText);
        statusLeft.Children.Add(imageSizeText);
        statusLeft.Children.Add(saveStatusText);
        statusLeft.Children.Add(savedFileNameText);
        statusLeft.Children.Add(messageText);

        var statusRight = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        statusRight.Children.Add(zoomOut);
        statusRight.Children.Add(zoomSlider);
        statusRight.Children.Add(zoomIn);
        statusRight.Children.Add(fit);
        statusRight.Children.Add(zoomPercent);

        var statusGrid = new Grid
        {
            // 行の高さを中身（スライダ 24）より少しだけ広げ、左右の要素を垂直方向中央へ置く
            Height = AppStyles.StatusBarRowHeight,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(AppStyles.StatusPaddingX, 0, AppStyles.StatusPaddingX, 0),
        };
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(statusLeft, 0);
        Grid.SetColumn(statusRight, 1);
        statusGrid.Children.Add(statusLeft);
        statusGrid.Children.Add(statusRight);

        var page = new Grid();
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(toolbar, 0);
        Grid.SetRow(preview, 1);
        Grid.SetRow(statusGrid, 2);
        page.Children.Add(toolbar);
        page.Children.Add(preview);
        page.Children.Add(statusGrid);

        // ズーム率と可否の表示をコントロールの通知へ追従させる
        cropView.ZoomChanged += (_, e) =>
        {
            zoomText.Text = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{e.ZoomPercent:0}%");
            zoomIn.IsEnabled = cropView.CanZoomIn;
            zoomOut.IsEnabled = cropView.CanZoomOut;

            if (!synchronizingZoom)
            {
                synchronizingZoom = true;
                zoomSlider.Value = cropView.ZoomPercent;
                synchronizingZoom = false;
            }
        };
        zoomIn.IsEnabled = cropView.CanZoomIn;
        zoomOut.IsEnabled = cropView.CanZoomOut;

        SubscribeViewModel(viewModel, settings, cropView, saveStatusText, savedFileNameText, messageText, imageSizeText, pointerText, zoomText);

        return new PageContent(page, OnKeyDown: args =>
        {
            // 切り出しの開始・確定・キャンセルを Ctrl+R でも操作できるようにする
            if (args.Key == VirtualKey.R && IsControlDown())
            {
                ToggleCrop();
                args.Handled = true;
            }
        });
    }

    /// <summary>ツールバーを組み立てる。</summary>
    /// <param name="services">アプリ全体で共有するコア オブジェクト。</param>
    /// <param name="viewModel">アプリのメイン ViewModel。</param>
    /// <param name="toggleCrop">切り出しの開始・確定・キャンセルを行う窓口。</param>
    /// <param name="captureSession">撮影・切り出しの撮影セッション。</param>
    /// <returns>組み立てたツールバー。</returns>
    /// <remarks>
    /// 左端にメニュー（☰）、続いてファイル名テンプレート、右側にコマンドのボタンを並べる。<br/>
    /// 幅が足りない場合は横スクロールで到達できるようにする。
    /// </remarks>
    private static FrameworkElement BuildToolbar(
        AppServices services, MainViewModel viewModel, Action toggleCrop, CaptureSessionControl captureSession)
    {
        var items = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var menuButton = CreateMenuButton(services, viewModel, toggleCrop, captureSession);
        items.Children.Add(menuButton);
        items.Children.Add(AppStyles.CreateToolbarSeparator());

        var templateBox = new TextBox
        {
            Text = viewModel.FileNameTemplateText,
            FontSize = AppStyles.ToolbarFontSize,
            MinWidth = TemplateMinWidth,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(1),
        };
        ToolTipService.SetToolTip(templateBox, "保存するファイル名のテンプレート（{date} / {time} / 連番 が使えます）");
        templateBox.TextChanged += (_, _) => viewModel.FileNameTemplateText = templateBox.Text;
        items.Children.Add(templateBox);

        var save = CreateIconButton(CaptureIconFiles.Save, "画像を保存", () => _ = viewModel.SaveImageAsync(), AppStyles.ToolbarIconSize);
        var screen = CreateIconButton(
            CaptureIconFiles.ScreenSelect, "スクリーン選択キャプチャ",
            () => CaptureFlow.Start(CaptureType.ScreenSelect, AppContext(), captureSession), AppStyles.ToolbarIconSize);
        var windowSelect = CreateIconButton(
            CaptureIconFiles.WindowSelect, "ウィンドウ選択キャプチャ",
            () => CaptureFlow.Start(CaptureType.WindowSelect, AppContext(), captureSession), AppStyles.ToolbarIconSize);
        var area = CreateIconButton(
            CaptureIconFiles.AreaSelect, "領域選択キャプチャ",
            () => CaptureFlow.Start(CaptureType.AreaSelect, AppContext(), captureSession), AppStyles.ToolbarIconSize);
        var crop = CreateIconButton(CaptureIconFiles.Crop, "切り出し（切出しモード開始/終了）", toggleCrop, AppStyles.ToolbarIconSize);

        // 並びと区切りは他の UI 版と同じにする（☰ | テンプレート | 保存 | 撮影 3 種 | 切り出し）。
        // 元に戻す・やり直しはメニューから実行する（ツールバーに置かない）
        items.Children.Add(save);
        items.Children.Add(AppStyles.CreateToolbarSeparator());
        items.Children.Add(screen);
        items.Children.Add(windowSelect);
        items.Children.Add(area);
        items.Children.Add(AppStyles.CreateToolbarSeparator());
        items.Children.Add(crop);

        // 画像の状態とテンプレートの妥当性に追従して可否を更新する
        viewModel.PropertyChanged += (_, e) =>
        {
            save.IsEnabled = viewModel.CanSave;
            crop.IsEnabled = viewModel.HasPreviewImage;

            if (!string.Equals(e.PropertyName, nameof(MainViewModel.FileNameTemplateText), StringComparison.Ordinal))
            {
                return;
            }

            // 外部（設定）から変わった場合だけ入れ直す（入力中の変更でカーソルを動かさない）
            if (!string.Equals(templateBox.Text, viewModel.FileNameTemplateText, StringComparison.Ordinal))
            {
                templateBox.Text = viewModel.FileNameTemplateText;
            }
        };

        save.IsEnabled = viewModel.CanSave;
        crop.IsEnabled = viewModel.HasPreviewImage;

        // 上書き確認で使う保存先をツールチップへ反映する
        UpdateSaveToolTip(save, viewModel);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.SaveFolderPathResolved), StringComparison.Ordinal))
            {
                UpdateSaveToolTip(save, viewModel);
            }
        };

        // テンプレートの妥当性は枠色で示す
        void ApplyTemplateValidity()
        {
            if (viewModel.HasInvalidTemplate)
            {
                templateBox.BorderBrush = new SolidColorBrush(Colors.Red);
                return;
            }

            // 妥当な場合は既定（テーマの枠色）へ戻す
            templateBox.ClearValue(Control.BorderBrushProperty);
        }

        ApplyTemplateValidity();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(MainViewModel.HasInvalidTemplate), StringComparison.Ordinal))
            {
                ApplyTemplateValidity();
            }
        };

        var toolbar = new ScrollViewer
        {
            // 横スクロールは無効にする（スクロール バーを出さず、内容も動かさない）。
            // 有効のままだと、ツールバーの余白をクリックした操作で内容が横へ流れ、
            // 左端のメニュー ボタンが画面外へ隠れる（実測）
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollMode = ScrollMode.Disabled,

            // 行の高さを中身（ボタン 30）より少しだけ広げ、中身を垂直方向中央へ置く
            Height = AppStyles.ToolbarRowHeight,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(AppStyles.ToolbarPaddingX, 0, AppStyles.ToolbarMarginRight, 0),
            Margin = new Thickness(0, AppStyles.ToolbarMarginTop, 0, AppStyles.ToolbarMarginBottom),
            Content = items,
        };

        // ファイル名テンプレートの幅をテキストの実測に合わせる（上限は右の要素が隠れない幅）。
        // ツールバーの幅が変わったら計算し直す
        var measureText = new TextBlock { FontSize = templateBox.FontSize, Text = templateBox.Text };
        void UpdateTemplateWidth()
        {
            measureText.Text = templateBox.Text;
            measureText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var width = measureText.DesiredSize.Width + TemplateHorizontalPadding;
            templateBox.Width = Math.Max(TemplateMinWidth, Math.Min(width, ResolveTemplateMaxWidth()));
        }

        double ResolveTemplateMaxWidth()
        {
            var available = toolbar.ActualWidth - toolbar.Padding.Left - toolbar.Padding.Right;
            if (available <= 1)
            {
                return TemplateMinWidth;
            }

            var others = 0.0;
            foreach (var child in items.Children)
            {
                if (ReferenceEquals(child, templateBox) || child is not FrameworkElement element)
                {
                    continue;
                }

                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                others += child.DesiredSize.Width + element.Margin.Left + element.Margin.Right;
            }

            // StackPanel の Spacing（要素間の余白）も差し引く
            others += items.Spacing * Math.Max(0, items.Children.Count - 1);
            return Math.Max(TemplateMinWidth, available - others - templateBox.Margin.Left - templateBox.Margin.Right);
        }

        toolbar.SizeChanged += (_, _) => UpdateTemplateWidth();
        templateBox.TextChanged += (_, _) => UpdateTemplateWidth();
        UpdateTemplateWidth();
        return toolbar;
    }

    /// <summary>メニュー（☰）のボタンを組み立てる。</summary>
    /// <param name="services">アプリ全体で共有するコア オブジェクト。</param>
    /// <param name="viewModel">アプリのメイン ViewModel。</param>
    /// <param name="toggleCrop">切り出しの開始・確定・キャンセルを行う窓口。</param>
    /// <param name="captureSession">撮影・切り出しの撮影セッション。</param>
    /// <returns>組み立てたボタン。</returns>
    /// <remarks>
    /// 他の UI 版と同じ項目・同じ区切り・同じショートカット表記で並べる。<br/>
    /// ショートカットは表示だけにする（<c>KeyboardAcceleratorTextOverride</c> は登録を行わない）。
    /// </remarks>
    private static Button CreateMenuButton(
        AppServices services, MainViewModel viewModel, Action toggleCrop, CaptureSessionControl captureSession)
    {
        var flyout = new MenuFlyout
        {
            // メニューはボタンの下へ開く（上へ開くと、ツールバーの上に何も無いぶん余計に視線が動く。
            // 他の UI 版はすべて下へ開く）
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
        };
        var save = CreateMenuItem("File - 画像を保存", "Ctrl+S", () => _ = viewModel.SaveImageAsync(), CaptureIconFiles.Save);
        var copy = CreateMenuItem("Clipboard - コピー", "Ctrl+C", () => _ = viewModel.CopyToClipboardAsync());
        var paste = CreateMenuItem("Clipboard - 貼り付け", "Ctrl+V", () => _ = PasteAsync(services));
        var undo = CreateMenuItem("Image - 元に戻す", "Ctrl+Z", () => EditHistory.Undo(services));
        var redo = CreateMenuItem("Image - 元に戻すを取り消す", "Ctrl+Y", () => EditHistory.Redo(services));
        var screen = CreateMenuItem("Capture - スクリーン選択", "Ctrl+Shift+Q", () => CaptureFlow.Start(CaptureType.ScreenSelect, AppContext(), captureSession), CaptureIconFiles.ScreenSelect);
        var windowSelect = CreateMenuItem("Capture - ウィンドウ選択", "Ctrl+Shift+W", () => CaptureFlow.Start(CaptureType.WindowSelect, AppContext(), captureSession), CaptureIconFiles.WindowSelect);
        var area = CreateMenuItem("Capture - 範囲選択", "Ctrl+Shift+E", () => CaptureFlow.Start(CaptureType.AreaSelect, AppContext(), captureSession), CaptureIconFiles.AreaSelect);
        var crop = CreateMenuItem("Crop - 切り出し", "Ctrl+R", toggleCrop, CaptureIconFiles.Crop);
        var autoCrop = CreateMenuItem("Crop - 自動切り出し", null, () => viewModel.AutoCropWindowCommand.Execute(null));

        flyout.Items.Add(save);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(copy);
        flyout.Items.Add(paste);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(undo);
        flyout.Items.Add(redo);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(screen);
        flyout.Items.Add(windowSelect);
        flyout.Items.Add(area);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(crop);
        flyout.Items.Add(autoCrop);

        // 画像の状態に追従して可否を更新する
        void Apply()
        {
            save.IsEnabled = viewModel.CanSave;
            copy.IsEnabled = viewModel.CanCopy;
            paste.IsEnabled = viewModel.CanPaste;
            undo.IsEnabled = viewModel.HasPreviewImage;
            redo.IsEnabled = viewModel.HasPreviewImage;
            crop.IsEnabled = viewModel.HasPreviewImage;
            autoCrop.IsEnabled = viewModel.HasPreviewImage;
        }

        Apply();
        viewModel.PropertyChanged += (_, _) => Apply();

        return new Button
        {
            Content = new TextBlock { Text = "☰", FontSize = 16, VerticalAlignment = VerticalAlignment.Center },
            Flyout = flyout,
            Width = AppStyles.ToolbarButtonHeight,
            Height = AppStyles.ToolbarButtonHeight,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(1),
        };
    }


    /// <summary>拡大率メニュー（% 表示のクリックで開く）を組み立てる。</summary>
    /// <param name="cropView">対象のプレビュー。</param>
    /// <returns>組み立てたメニュー。</returns>
    private static MenuFlyout BuildZoomMenu(CropViewControl cropView)
    {
        var flyout = new MenuFlyout();
        // 選択肢と並びは App.Core（ZoomMenuLevels）が単一所有する（他の UI 版と同じ）
        foreach (var level in ZoomMenuLevels.All)
        {
            var item = new MenuFlyoutItem { Text = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{level:0}%") };
            var percent = level;
            item.Click += (_, _) => cropView.ZoomPercent = percent;
            flyout.Items.Add(item);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());
        var fit = new MenuFlyoutItem { Text = "ウィンドウに合わせる（Fit）" };
        fit.Click += (_, _) => cropView.ZoomFit();
        flyout.Items.Add(fit);
        return flyout;
    }

    /// <summary>プレビュー（切り出しビュー）を組み立て、撮影セッションを生成する。</summary>
    /// <param name="services">アプリ全体で共有するコア オブジェクト。</param>
    /// <param name="window">オーナー ウィンドウ。</param>
    /// <param name="cropView">生成したプレビュー。</param>
    /// <param name="captureSession">生成した撮影セッション。</param>
    /// <returns>プレビューのホスト要素。</returns>
    /// <remarks>
    /// 1. 切り出しビューへ画像・中央寄せ・自動切り出しの矩形を与える。<br/>
    /// 2. カーソル移動でステータスの座標と自動切り出しのホバー対象を更新する。<br/>
    /// 3. 設定の変更（外観・中央寄せ）を撮影セッションとプレビューへ反映する。
    /// </remarks>
    private static FrameworkElement BuildPreview(
        AppServices services, MainWindow window, out CropViewControl cropView, out CaptureSessionControl captureSession)
    {
        var viewModel = services.Main;
        var settings = services.Settings.Current;

        // out パラメーターはラムダ・ローカル関数の内部で参照できないため、ローカル変数へコピーして使う
        var crop = new CropViewControl();
        cropView = crop;
        crop.Log = s_logger;

        // 自動切り出しのホバー強調の外観（内側の塗りと枠線）を与える。
        // 色・太さ・表示可否は描画にだけ必要な設定であり、描画手順はコントロールが単一所有する
        crop.HighlightAppearance = CropViewControl.HighlightDefaultAppearance with
        {
            InnerFillColor = s_autoCropHoverFill,
            InnerBorderColor = s_autoCropHoverBorder,
            InnerBorderThickness = AutoCropBorderThickness,
        };

        // 画像の無い部分（プレビューの下地）の色を他の UI 版と同じにする。
        // 色は AppPalette（App.Core）が単一所有する。 テーマが変わったら塗り直す
        // （下地は Skia で描くため、テーマのリソースでは追随しない）
        ApplyImageAreaBackground(crop);
        AppStyles.ThemeChanged += (_, _) => ApplyImageAreaBackground(crop);

        // 撮影・切り出しの撮影セッション。 撮影と切り出しの排他とセッションの寿命を単一所有する
        var session = new CaptureSessionControl(
            CaptureSettingsMapper.From(settings, ReadRenderScaling(crop)),
            AppLog.CreateLogger(nameof(CaptureSessionControl)));
        captureSession = session;

        // 設定変更を撮影セッションへ即時反映する（部分更新・検証・同値スキップはコアが単一所有する）
        settings.PropertyChanged += (_, e) =>
        {
            try
            {
                session.AppearanceSettings = CaptureSettingsMapper.From(settings, ReadRenderScaling(crop));
            }
            catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
            {
                s_logger.LogSettingsApplyError(ex, e.PropertyName ?? string.Empty);
            }
        };

        // 中央寄せの変更をプレビューへ即時反映する
        settings.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(Settings.CenterAlign), StringComparison.Ordinal))
            {
                crop.CenterAlign = settings.CenterAlign;
            }
        };

        void SyncRects()
        {
            // ホバー中の 1 件はコントロール側の強調（枠線 + 半透明の塗り）へ委ねる
            var hovered = viewModel.IsAutoCropActive ? viewModel.HoveredRect : null;
            crop.Rects = [.. viewModel.DetectedRects
                .Where(rect => rect != hovered)
                .Select(rect => new OverlayRect(
                    new EsRect(rect.X, rect.Y, rect.Width, rect.Height), OverlayLineStyle.Solid,
                    s_autoCropBorder, DrawingColor.Transparent, AutoCropBorderThickness))];

            crop.HighlightSourceRect = hovered?.ToEsRect();
        }

        viewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.PreviewImage):
                    crop.Source = viewModel.PreviewImage;
                    break;
                case nameof(MainViewModel.IsAutoCropActive):
                case nameof(MainViewModel.DetectedRects):
                case nameof(MainViewModel.HoveredRect):
                    SyncRects();
                    break;
                default:
                    break;
            }
        };

        crop.Source = viewModel.PreviewImage;
        SyncRects();
        crop.CenterAlign = settings.CenterAlign;

        // カーソル移動はヒットテスト対象の要素（ズーム ビュー）だけが受け取るため、ズーム ビューへ購読する。
        // ズーム ビューはプレビュー領域全体を占めるため、座標系は切り出しビューと一致する
        var zoomView = crop.ZoomView;
        zoomView.PointerMoved += (_, e) =>
        {
            var point = ToEsPoint(e.GetCurrentPoint(zoomView).Position);
            UpdatePointerText(viewModel, zoomView, point);

            if (!viewModel.IsAutoCropActive)
            {
                return;
            }

            var hovered = FindAutoCropHit(viewModel, zoomView, point);
            if (viewModel.HoveredRect != hovered)
            {
                viewModel.HoveredRect = hovered;
            }
        };

        zoomView.PointerExited += (_, _) =>
        {
            viewModel.PointerDisplayText = "-";
            viewModel.HoveredRect = null;
        };

        // ダブルクリックで矩形を確定し、矩形の外側ならキャンセルする
        zoomView.DoubleTapped += (_, e) =>
        {
            if (!viewModel.IsAutoCropActive)
            {
                return;
            }

            var point = ToEsPoint(e.GetPosition(zoomView));
            if (FindAutoCropHit(viewModel, zoomView, point) is { } hit)
            {
                viewModel.ConfirmAutoCropCommand.Execute(hit);
                return;
            }

            viewModel.CancelAutoCropCommand.Execute(null);
        };

        var host = new Grid { Background = new SolidColorBrush(Colors.Transparent) };
        host.Children.Add(crop);

        // 画像の 4 辺・4 隅の影（ぼかしの広がりと濃さは App.Core の ShadowPlan が単一所有する）。
        // Composition の DropShadow を使うため、画像と同じ面に合成され、要素を増やさない
        crop.ZoomView.SetShadow(ShadowPlan.BlurRadius, ShadowPlan.ColorArgb);

        // 起動直後は XamlRoot が無くスケーリングを取得できないため、読み込み後に外観を反映し直す
        crop.Loaded += (_, _) => session.AppearanceSettings = CaptureSettingsMapper.From(settings, ReadRenderScaling(crop));

        // 終了時に切り出しセッションを完了させる（待機中のタスクを残さない）
        window.Closed += (_, _) => session.Dispose();
        return host;
    }

    /// <summary>プレビューの下地色（画像領域の背景色）を反映する。</summary>
    /// <param name="crop">対象のプレビュー。</param>
    /// <remarks>
    /// 画像が無い部分はこの色の単色になる。 色は <see cref="AppPalette"/>（App.Core）が単一所有する。
    /// </remarks>
    private static void ApplyImageAreaBackground(CropViewControl crop)
    {
        // Fit は影も含めて収める（画像の周囲に影を描くため、その分だけ内側へ収めた大きさで計算する）
        crop.Appearance = crop.Appearance with
        {
            BackgroundColor = AppPalette.ImageAreaDrawingColor(AppStyles.IsDarkTheme),
            FitInset = ShadowPlan.BlurRadius,
        };
    }

    /// <summary>メイン ViewModel の変更通知をステータス バーの表示へ反映する。</summary>
    /// <param name="viewModel">アプリのメイン ViewModel。</param>
    /// <param name="settings">設定。</param>
    /// <param name="cropView">プレビュー（中央寄せの反映先）。</param>
    /// <param name="saveStatusText">保存状態の表示。</param>
    /// <param name="savedFileNameText">保存ファイル名の表示。</param>
    /// <param name="messageText">通知メッセージの表示。</param>
    /// <param name="imageSizeText">画像サイズの表示。</param>
    /// <param name="pointerText">カーソル座標の表示。</param>
    /// <param name="zoomText">拡大率の表示。</param>
    private static void SubscribeViewModel(
        MainViewModel viewModel, Settings settings, CropViewControl cropView,
        TextBlock saveStatusText, TextBlock savedFileNameText, TextBlock messageText,
        TextBlock imageSizeText, TextBlock pointerText, TextBlock zoomText)
    {
        viewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.SaveStatusText):
                    saveStatusText.Text = viewModel.SaveStatusText;
                    break;
                case nameof(MainViewModel.SavedFileName):
                    savedFileNameText.Text = viewModel.SavedFileName;
                    break;
                case nameof(MainViewModel.StatusMessage):
                    messageText.Text = viewModel.StatusMessage;
                    break;
                case nameof(MainViewModel.ImageSizeDisplayText):
                    imageSizeText.Text = viewModel.ImageSizeDisplayText;
                    break;
                case nameof(MainViewModel.PointerDisplayText):
                    pointerText.Text = viewModel.PointerDisplayText;
                    break;
                case nameof(MainViewModel.ZoomDisplayText):
                    zoomText.Text = viewModel.ZoomDisplayText;
                    break;
                default:
                    break;
            }
        };

        cropView.ZoomChanged += (_, _) => viewModel.ZoomDisplayText = string.Create(
            System.Globalization.CultureInfo.InvariantCulture, $"{cropView.ZoomPercent:0}%");

        // 設定の中央寄せは起動時にも反映する（上の購読は変更時だけを捉えるため）
        cropView.CenterAlign = settings.CenterAlign;
    }

    /// <summary>画像をクリップボードから貼り付ける。</summary>
    /// <param name="services">アプリ全体で共有するコア オブジェクト。</param>
    /// <returns>待機タスク。</returns>
    /// <remarks>結果の通知（文言）はコア（<c>MainViewModel.PasteFromClipboardAsync</c>）が行う。</remarks>
    private static async Task PasteAsync(AppServices services) => await services.Main.PasteFromClipboardAsync();

    /// <summary>撮影結果をプレビューへ反映する（撮影完了イベントのハンドラ）。</summary>
    /// <param name="services">アプリ全体で共有するコア オブジェクト（履歴の所有者）。</param>
    /// <param name="viewModel">アプリのメイン ViewModel（画像の所有者）。</param>
    /// <param name="e">撮影結果。 キャンセル時は結果が空。</param>
    /// <remarks>
    /// 1. 撮影前に表示していた画像を Undo 履歴へ積む（撮影前の状態へ戻せるようにする）。<br/>
    /// 2. 撮影結果をプレビューへ設定する（所有権はメイン ViewModel へ移る）。<br/>
    /// 3. 結果（サイズ）をステータスへ通知する。<br/>
    /// キャンセル時は何もしない（プレビュー・履歴・ステータスとも変更しない）。
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
    /// <param name="captureSession">撮影・切り出しの撮影セッション。</param>
    /// <param name="cropView">切り出しを行う複合コントロール（表示中の画像が切り出しの対象）。</param>
    /// <param name="viewModel">アプリのメイン ViewModel（切り出しの適用先）。</param>
    /// <param name="services">アプリ全体で共有するコア オブジェクト（履歴の所有者）。</param>
    /// <remarks>
    /// どの操作を行うかの判断はコア（<c>CropSessionActions</c>）が単一所有する<br/>
    /// （未開始なら開始 / 選択済みなら確定 / 未選択ならキャンセル）。
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
                captureSession.Confirm();
                break;
            default:
                if (captureSession.IsCropping)
                {
                    captureSession.Cancel();
                }

                break;
        }
    }

    /// <summary>切り出しセッションを開始し、確定した範囲で画像を切り出す。</summary>
    /// <param name="captureSession">撮影・切り出しの撮影セッション。</param>
    /// <param name="cropView">切り出しを行う複合コントロール。</param>
    /// <param name="viewModel">アプリのメイン ViewModel（切り出しの適用先）。</param>
    /// <param name="services">アプリ全体で共有するコア オブジェクト（履歴の所有者）。</param>
    /// <returns>待機タスク。</returns>
    /// <remarks>
    /// 確定（Enter / 確定操作）で切り出し範囲、キャンセルで空の矩形が返る。<br/>
    /// 開始できなかった場合（他のセッションが実行中）は結果 null が即時返る。
    /// </remarks>
    private static async Task StartCropSessionAsync(
        CaptureSessionControl captureSession, CropViewControl cropView, MainViewModel viewModel, AppServices services)
    {
        var bounds = await captureSession.StartCropAsync(cropView, AppContext());
        if (bounds is not { } rect || rect.IsEmpty)
        {
            return;
        }

        // 切り出し前の画像へ戻せるように履歴へ積む
        if (viewModel.PreviewImage is { } source)
        {
            services.UndoHistory.Push(source);
        }

        viewModel.CropPreviewCommand.Execute(rect);
    }

    /// <summary>マウス ポインタ位置の対象座標（画像ピクセル）をステータス バーへ表示する。</summary>
    /// <param name="viewModel">アプリのメイン ViewModel（表示文字列の保持元）。</param>
    /// <param name="view">プレビュー（座標変換と画像矩形の取得元）。</param>
    /// <param name="cursorDip">カーソル位置（プレビューのローカル DIP）。</param>
    /// <remarks>
    /// 画像が無い・カーソルが画像の表示矩形の外にある場合は "-" を表示する。
    /// </remarks>
    private static void UpdatePointerText(MainViewModel viewModel, ZoomViewControl view, EsPoint cursorDip)
    {
        if (viewModel.PreviewImage is not { } image)
        {
            viewModel.PointerDisplayText = "-";
            return;
        }

        var imageRect = view.ImageRectViewport();
        var x = cursorDip.X - imageRect.X;
        var y = cursorDip.Y - imageRect.Y;
        if (x < 0 || y < 0 || x >= imageRect.Width || y >= imageRect.Height)
        {
            viewModel.PointerDisplayText = "-";
            return;
        }

        var content = view.ClientToDisplayedSource(cursorDip);
        viewModel.PointerDisplayText = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{(int)Math.Clamp(content.X, 0, image.Width - 1)}, {(int)Math.Clamp(content.Y, 0, image.Height - 1)}");
    }

    /// <summary>プレビュー上のカーソル位置にある自動切り出しの矩形（面積が最小のもの）を求める。</summary>
    /// <param name="viewModel">アプリのメイン ViewModel（検出矩形の保持元）。</param>
    /// <param name="view">プレビュー（座標変換と画像矩形の取得元）。</param>
    /// <param name="cursorDip">カーソル位置（プレビューのローカル DIP）。</param>
    /// <returns>カーソル位置を含む矩形（面積が最小）。 画像外・矩形なしの場合は null。</returns>
    private static EsRectInt? FindAutoCropHit(MainViewModel viewModel, ZoomViewControl view, EsPoint cursorDip)
    {
        if (viewModel.PreviewImage is not { } image)
        {
            return null;
        }

        var imageRect = view.ImageRectViewport();
        var localX = cursorDip.X - imageRect.X;
        var localY = cursorDip.Y - imageRect.Y;
        if (localX < 0 || localY < 0 || localX >= imageRect.Width || localY >= imageRect.Height)
        {
            return null;
        }

        var content = view.ClientToDisplayedSource(cursorDip);

        // 矩形の判定（複数の中から面積が最小のものを選ぶ）はコアが単一所有する
        return viewModel.HitDetectedRect(
            Math.Clamp(content.X, 0, image.Width - 1),
            Math.Clamp(content.Y, 0, image.Height - 1));
    }

    /// <summary>ステータス バー・ツールバーのラベルを生成する。</summary>
    /// <param name="text">表示する文字列。</param>
    /// <param name="fontSize">文字サイズ。</param>
    /// <returns>生成したラベル。</returns>
    private static TextBlock CreateLabel(string text, double fontSize)
        => new() { Text = text, FontSize = fontSize, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>アイコン付きのボタンを生成する。</summary>
    /// <param name="icon">アイコンの SVG ファイル名。</param>
    /// <param name="toolTip">ツールチップの文言。</param>
    /// <param name="onClick">クリック時の処理。 null の場合は何もしない。</param>
    /// <param name="iconSize">アイコンの一辺（DIP）。</param>
    /// <returns>生成したボタン。</returns>
    private static Button CreateIconButton(string icon, string toolTip, Action? onClick, double iconSize)
    {
        var content = new Image
        {
            Source = IconLoader.LoadThemedImage(icon, AppStyles.IconRasterSize),
            Width = iconSize,
            Height = iconSize,
        };

        var button = new Button
        {
            Content = content,
            Height = AppStyles.ToolbarButtonHeight,
            MinWidth = AppStyles.ToolbarButtonHeight,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(1),
        };

        ToolTipService.SetToolTip(button, toolTip);
        if (onClick is not null)
        {
            button.Click += (_, _) => onClick();
        }

        ApplyIconButtonColors(button);

        // テーマが変わったらアイコンの色を描き直す
        AppStyles.ThemeChanged += (_, _) =>
        {
            content.Source = IconLoader.LoadThemedImage(icon, AppStyles.IconRasterSize);
            ApplyIconButtonColors(button);
        };
        return button;
    }

    /// <summary>アイコン ボタンの背景色と角丸を 4 UI 版で共通の値に揃える。</summary>
    /// <param name="button">対象のボタン。</param>
    /// <remarks>
    /// WinUI 3 の標準ボタンは通常時にも下地を塗るため、通常時は透明・ホバーで薄いグレー・押下で濃いグレーに<br/>
    /// 上書きする（値は <see cref="AppPalette.ToolbarButtonBackground"/>（App.Core）が単一所有する）。<br/>
    /// 上書きはコントロールの <c>Resources</c> へ入れる（テーマ リソースの解決より優先される）。
    /// </remarks>
    private static void ApplyIconButtonColors(Button button)
    {
        var isDark = AppStyles.IsDarkTheme;
        var transparent = new SolidColorBrush(Colors.Transparent);

        button.Resources["ButtonBackground"] = transparent;
        button.Resources["ButtonBorderBrush"] = transparent;
        button.Resources["ButtonBorderBrushPointerOver"] = transparent;
        button.Resources["ButtonBorderBrushPressed"] = transparent;
        button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(
            AppStyles.ToColor(AppPalette.ToolbarButtonBackground(isDark, isPressed: false)));
        button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(
            AppStyles.ToColor(AppPalette.ToolbarButtonBackground(isDark, isPressed: true)));
        button.CornerRadius = new CornerRadius(AppPalette.ToolbarButtonCornerRadius);
    }

    /// <summary>文字付きの小さなボタンを生成する。</summary>
    /// <param name="text">表示する文字列。</param>
    /// <param name="toolTip">ツールチップの文言。</param>
    /// <param name="onClick">クリック時の処理。 null の場合は何もしない。</param>
    /// <returns>生成したボタン。</returns>
    private static Button CreateTextButton(string text, string toolTip, Action? onClick)
    {
        var button = new Button
        {
            Content = CreateLabel(text, AppStyles.StatusFontSize),
            Height = AppStyles.StatusButtonHeight,
            MinWidth = AppStyles.StatusButtonMinWidth,
            Padding = new Thickness(4, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(1),
        };

        ToolTipService.SetToolTip(button, toolTip);
        if (onClick is not null)
        {
            button.Click += (_, _) => onClick();
        }

        return button;
    }

    /// <summary>メニューの項目を生成する。</summary>
    /// <param name="text">表示する文字列。</param>
    /// <param name="shortcut">表示するショートカット（null の場合は表示しない）。</param>
    /// <param name="onClick">選択時の処理。</param>
    /// <param name="iconFile">アイコンを表示する場合の SVG ファイル名。</param>
    /// <returns>生成した項目。</returns>
    /// <remarks>ショートカットは表示だけにする（キーの登録は行わない）。</remarks>
    private static MenuFlyoutItem CreateMenuItem(string text, string? shortcut, Action onClick, string? iconFile = null)
    {
        var item = new MenuFlyoutItem { Text = text };
        if (iconFile is not null)
        {
            item.Icon = new ImageIcon { Source = IconLoader.LoadThemedImage(iconFile, AppStyles.IconRasterSize) };
            AppStyles.ThemeChanged += (_, _) =>
                item.Icon = new ImageIcon { Source = IconLoader.LoadThemedImage(iconFile, AppStyles.IconRasterSize) };
        }

        if (shortcut is not null)
        {
            item.KeyboardAcceleratorTextOverride = shortcut;
        }

        item.Click += (_, _) => onClick();
        return item;
    }

    /// <summary>保存ボタンのツールチップへ保存先フォルダを反映する。</summary>
    /// <param name="save">対象のボタン。</param>
    /// <param name="viewModel">アプリのメイン ViewModel。</param>
    private static void UpdateSaveToolTip(Button save, MainViewModel viewModel)
        => ToolTipService.SetToolTip(save, $"画像を保存（保存先: {viewModel.SaveFolderPathResolved}）");

    /// <summary>カーソル位置をコアの座標型へ変換する。</summary>
    /// <param name="point">変換する座標。</param>
    /// <returns>変換した座標。</returns>
    private static EsPoint ToEsPoint(Windows.Foundation.Point point) => new(point.X, point.Y);

    /// <summary>プレビューのスケーリング（DIP → 物理ピクセル）を取得する。</summary>
    /// <param name="cropView">対象のプレビュー。</param>
    /// <returns>スケーリング。 取得できない場合（表示前）は 1.0。</returns>
    private static double ReadRenderScaling(CropViewControl cropView)
    {
        var scale = cropView.XamlRoot?.RasterizationScale ?? 1.0;
        return scale > 0 ? scale : 1.0;
    }

    /// <summary>オーナー ウィンドウを取得する。</summary>
    /// <returns>アプリのメイン ウィンドウ。</returns>
    private static MainWindow AppContext() => AppHost.MainWindow;

    /// <summary>Ctrl キーが押されているかどうかを判定する。</summary>
    /// <returns>押されている場合は true。</returns>
    /// <remarks>フラグの判定はビット演算で行う（<c>HasFlag</c> はボックス化する）。</remarks>
    private static bool IsControlDown()
        => (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
}
