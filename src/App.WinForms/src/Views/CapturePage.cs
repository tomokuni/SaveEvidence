using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.WinForms.Icons;
using EsUtil.SaveEvidence.App.WinForms.Logging;
using EsUtil.SaveEvidence.App.WinForms.Services;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Session;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.ZoomView;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>キャプチャページ（撮影 / 切り出し / ズーム / ルーペ / 履歴）。</summary>
/// <remarks>
/// <b>構成</b>: ツールバー（保存・撮影 3 種・切り出し・自動切り出し）/ プレビュー（ズーム可能な切り出しビュー）/<br/>
/// ステータスバー（座標・画像サイズ・保存状態・通知・拡大率）。<br/>
/// <b>撮影</b>: 実行を撮影セッション（ファサード）へ委ね、確定した画像は <c>CaptureCompleted</c>（同期）で反映する。<br/>
/// 反映はオーナーの再表示より前に起きるため、画像を差し替えてからアプリが再表示される。<br/>
/// <b>切り出し</b>: ボタン・ショートカット・メニューの 3 経路とも同じ処理を通す<br/>
/// （どの操作を行うかの判断はコアが単一所有する）。<br/>
/// <b>ルーペ</b>: 切り出しビューの内部（選択オーバーレイ）がルーペを描く。 本ページは外観の設定だけを渡す。
/// </remarks>
internal static class CapturePage
{
    /// <summary>自動切り出しの検出矩形の枠線色（水色・不透明）。</summary>
    private static readonly Color s_autoCropBorder = Color.FromArgb(255, 0, 191, 255);

    /// <summary>自動切り出しの検出矩形の枠線の太さ（DIP）。</summary>
    private const double AutoCropBorderThickness = 2.0;

    /// <summary>ロガー（設定の反映失敗を記録する）。</summary>
    private static readonly ILogger s_logger = AppLog.CreateLogger(nameof(CapturePage));

    /// <summary>ページの内容を生成する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト。</param>
    /// <param name="owner">このページを載せるウィンドウ（撮影と切り出しのオーナーになる）。</param>
    /// <returns>ページの内容。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> または <paramref name="owner"/> が null の場合。</exception>
    public static PageContent Build(AppServices services, Form owner)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(owner);

        var viewModel = services.Main;
        var settingsService = services.Settings;

        var captureSession = new CaptureSessionControl(CaptureSettingsMapper.From(settingsService.Current, 1.0))
        {
            LogPrefix = "[App.WinForms]",
        };

        var crop = new CropViewControl
        {
            Dock = DockStyle.Fill,
            Log = s_logger,
        };

        // 撮影の開始経路（ホットキー）も同じファサードを通す（撮影と切り出しの排他を 1 箇所で効かせる）
        viewModel.StartSelectionMode = type => CaptureFlow.Start(type, captureSession, owner);

        // UI 依存の操作はコールバックとして ViewModel へ渡す（UI 非依存のコアを保つ）
        viewModel.CopyToClipboardCallback = ClipboardImageService.CopyAsync;
        viewModel.PasteFromClipboardCallback = ClipboardImageService.PasteAsync;
        viewModel.ConfirmOverwriteAsync = fileName => Task.FromResult(ConfirmOverwrite(owner, fileName));

        // 撮影結果の反映はイベント（同期）で行う（オーナーの再表示より前に画像を差し替える）
        captureSession.CaptureCompleted += (_, e) => ApplyCaptureResult(services, viewModel, e);

        var canvas = new Panel { Dock = DockStyle.Fill, BackColor = AppStyles.PageBackground };
        canvas.Controls.Add(crop);

        var zoomSlider = new TrackBar
        {
            Minimum = 10,
            Maximum = 500,
            TickFrequency = 10,
            SmallChange = 5,
            LargeChange = 25,
            Width = 140,
            Height = UiFactory.StatusBarHeight,
            TabStop = false,
        };
        var syncingZoomSlider = false;

        var zoomText = UiFactory.CreateStatusLabel(52, "拡大率");
        var zoomOutButton = UiFactory.CreateToolButton(AppIcon.ZoomOut, "縮小", () => crop.ZoomOut());
        var zoomInButton = UiFactory.CreateToolButton(AppIcon.ZoomIn, "拡大", () => crop.ZoomIn());
        var zoomFitButton = UiFactory.CreateToolButton(AppIcon.ZoomFit, "ウィンドウに合わせる", () => crop.ZoomFit());

        var pointerLabel = UiFactory.CreateStatusLabel(150, "カーソル位置（画像ピクセル）");
        var sizeLabel = UiFactory.CreateStatusLabel(150, "プレビュー画像のサイズ");
        var saveStateLabel = UiFactory.CreateStatusLabel(64, "保存済み / 未保存");
        var savedFileLabel = UiFactory.CreateStatusLabel(0, "直前に保存したファイル名");
        var messageLabel = UiFactory.CreateStatusLabel(0);

        var statusBar = UiFactory.CreateStatusBar();
        var statusFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
        };
        statusFlow.Controls.AddRange([pointerLabel, sizeLabel, saveStateLabel, savedFileLabel, messageLabel]);

        var zoomPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
        };
        zoomPanel.Controls.AddRange([zoomOutButton, zoomSlider, zoomInButton, zoomFitButton, zoomText]);

        // 塗り（Fill）を先に追加する（WinForms は後から追加したものから辺へ寄せるため）
        statusBar.Controls.Add(statusFlow);
        statusBar.Controls.Add(zoomPanel);

        var templateBox = new TextBox
        {
            Width = 260,
            Text = viewModel.FileNameTemplateText,
            Margin = new Padding(1, 4, 1, 1),
        };
        UiFactory.AttachToolTip(templateBox, "ファイル名テンプレート（例: screenshot_007_{date}-{time}.png）");
        templateBox.TextChanged += (_, _) => viewModel.FileNameTemplateText = templateBox.Text;

        var menuButton = new ToolButton(AppIcon.Menu, "メニュー");
        UiFactory.AttachToolTip(menuButton, "メニュー");
        var toolbar = UiFactory.CreateToolbar();
        toolbar.Controls.Add(menuButton);
        toolbar.Controls.Add(UiFactory.CreateSeparator());
        toolbar.Controls.Add(templateBox);
        toolbar.Controls.Add(UiFactory.CreateSeparator());
        toolbar.Controls.Add(UiFactory.CreateToolButton(AppIcon.Save, "保存", SaveImage));
        toolbar.Controls.Add(UiFactory.CreateToolButton(AppIcon.ScreenSelect, "スクリーン選択で撮影", () => StartCapture(CaptureType.ScreenSelect)));
        toolbar.Controls.Add(UiFactory.CreateToolButton(AppIcon.WindowSelect, "ウィンドウ選択で撮影", () => StartCapture(CaptureType.WindowSelect)));
        toolbar.Controls.Add(UiFactory.CreateToolButton(AppIcon.AreaSelect, "範囲選択で撮影", () => StartCapture(CaptureType.AreaSelect)));
        toolbar.Controls.Add(UiFactory.CreateSeparator());
        toolbar.Controls.Add(UiFactory.CreateToolButton(AppIcon.Crop, "切り出し（開始 / 確定 / キャンセル）", ToggleCrop));
        toolbar.Controls.Add(UiFactory.CreateToolButton(AppIcon.AutoCrop, "自動切り出し（ウィンドウ枠の検出）", AutoCrop));

        var root = new Panel { Dock = DockStyle.Fill, BackColor = AppStyles.PageBackground };
        root.Controls.Add(canvas);
        root.Controls.Add(toolbar);
        root.Controls.Add(statusBar);

        menuButton.Click += (_, _) => ShowMenu();

        crop.ZoomChanged += OnZoomChanged;
        zoomSlider.ValueChanged += (_, _) =>
        {
            if (syncingZoomSlider)
            {
                return;
            }

            syncingZoomSlider = true;
            crop.ZoomPercent = zoomSlider.Value;
            syncingZoomSlider = false;
        };

        crop.ViewportChanged += (_, _) => captureSession.NotifyViewportChanged();

        // 入力はズームビューが受け取る（切り出しビュー自身は子へ入力を渡すだけのため）
        crop.ZoomView.MouseMove += (_, e) => OnPointerMoved(e.Location);
        crop.ZoomView.MouseDoubleClick += (_, e) => OnCanvasDoubleClick(e.Location);

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.PreviewImage))
            {
                crop.Source = viewModel.PreviewImage;
                crop.ClearLoupe();
                crop.NotifyViewportChanged();
            }

            if (e.PropertyName is nameof(MainViewModel.DetectedRects) or nameof(MainViewModel.IsAutoCropActive))
            {
                ApplyAutoCropRects();
            }

            UpdateStatus();
        };

        AppStyles.ThemeChanged += (_, _) => ApplyAppearance();

        ApplyAppearance();
        ApplyAutoCropRects();
        crop.Source = viewModel.PreviewImage;
        UpdateStatus();

        return new PageContent(root, OnActivated: ApplyAppearance, OnShortcut: HandleShortcut);

        // ────────────────────────────────────────────────────────────────
        // ローカル関数（操作はすべてここへ集約する。 ページの状態を閉じ込める）
        // ────────────────────────────────────────────────────────────────

        // テーマと外観の設定をコントロールへ反映する。
        void ApplyAppearance()
        {
            var pageBackground = AppStyles.PageBackground;
            toolbar.BackColor = AppStyles.BarBackground;
            statusBar.BackColor = pageBackground;
            statusFlow.BackColor = pageBackground;
            zoomPanel.BackColor = pageBackground;
            root.BackColor = pageBackground;
            canvas.BackColor = pageBackground;
            templateBox.ForeColor = AppStyles.Foreground;
            templateBox.BackColor = pageBackground;
            zoomSlider.BackColor = pageBackground;

            crop.Appearance = ZoomViewAppearance.Default with
            {
                BackgroundColor = pageBackground,
                ShowScrollBars = true,
            };
            crop.CenterAlign = viewModel.CenterAlign;

            // 選択中のラベルの文言はコアが単一所有する（アプリ固有の文言をここで組み立てない）
            crop.OperationTextProvider = () => CaptureLabelTexts.GetOperationText(crop.SelectionMode);
            crop.SizeLabelOverrideProvider = () =>
                CropLabelTexts.ResolveSizeLabel(crop.HasSelection, crop.SelectionSourceRect, crop.ZoomPercent);

            foreach (var label in new[] { pointerLabel, sizeLabel, saveStateLabel, savedFileLabel, messageLabel, zoomText })
            {
                label.ForeColor = AppStyles.Foreground;
            }

            ApplySessionSettings();
        }

        // 設定（外観・動作）を撮影セッションへ反映する。
        void ApplySessionSettings()
        {
            try
            {
                captureSession.AppearanceSettings =
                    CaptureSettingsMapper.From(settingsService.Current, owner.DeviceDpi / 96.0);
            }
            catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
            {
                // 設定の検証（範囲外の値）で例外になっても UI を壊さない
                s_logger.LogSettingsApplyError(ex, nameof(CaptureSessionControl.AppearanceSettings));
            }
        }

        // ズーム率の変化をスライダと表示へ反映する。
        void OnZoomChanged(object? sender, ZoomChangedEventArgs e)
        {
            zoomText.Text = UiFactory.FormatNumber(Math.Round(e.ZoomPercent), "0") + "%";
            viewModel.ZoomDisplayText = zoomText.Text;
            zoomInButton.Enabled = e.CanZoomIn;
            zoomOutButton.Enabled = e.CanZoomOut;

            // スライダ → ズームの同期中は値を戻さない（往復で無限ループになるのを防ぐ）
            if (syncingZoomSlider)
            {
                return;
            }

            syncingZoomSlider = true;
            zoomSlider.Value = Math.Clamp((int)Math.Round(e.ZoomPercent), zoomSlider.Minimum, zoomSlider.Maximum);
            syncingZoomSlider = false;
        }

        // ステータスバーの表示を ViewModel の状態へ合わせる。
        void UpdateStatus()
        {
            pointerLabel.Text = viewModel.PointerDisplayText;
            sizeLabel.Text = viewModel.ImageSizeDisplayText;
            saveStateLabel.Text = viewModel.SaveStatusText;
            savedFileLabel.Text = viewModel.SavedFileName;
            messageLabel.Text = viewModel.StatusMessage;
        }

        // 撮影を開始する。
        void StartCapture(CaptureType type) => CaptureFlow.Start(type, captureSession, owner);

        // プレビュー画像を保存する。
        void SaveImage()
        {
            if (!viewModel.CanSave)
            {
                viewModel.StatusMessage = viewModel.HasInvalidTemplate
                    ? $"ファイル名テンプレートが無効です: {viewModel.ValidationMessage}"
                    : "保存できる状態ではありません（画像が無い・保存済み・保存先が不正）。";
                return;
            }

            _ = SaveImageCoreAsync();
        }

        // 保存を実行し、結果をステータスへ通知する。
        async Task SaveImageCoreAsync()
        {
            var saved = await viewModel.SaveImageAsync().ConfigureAwait(true);
            if (!saved)
            {
                viewModel.StatusMessage = "保存できませんでした（保存先フォルダを確認してください）。";
            }
        }

        // クリップボードの画像を貼り付け、成否をステータスへ通知する。
        async Task PasteCoreAsync()
        {
            var pasted = await viewModel.PasteFromClipboardAsync().ConfigureAwait(true);
            viewModel.StatusMessage = pasted
                ? "クリップボードの画像を貼り付けました。"
                : "クリップボードに画像がありません。";
        }

        // 切り出しの開始・確定・キャンセルを 1 つの操作で切り替える。
        void ToggleCrop()
        {
            switch (captureSession.ResolveCropAction())
            {
                case CropSessionAction.Start:
                    _ = StartCropCoreAsync();
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

        // 切り出しセッションを開始し、確定した範囲で画像を切り出す。
        // 切り出しの対象画像は切り出しビューが保持する（画像を別引数で渡さない）。
        // 確定で切り出し範囲、キャンセルで 0x0 の空矩形、開始できなかった場合は null が返る。
        // 切り出しの直前の画像は MainViewModel.ApplyPixelEdit が履歴へ積む（ここでは積まない）。
        async Task StartCropCoreAsync()
        {
            var bounds = await captureSession.StartCropAsync(crop, owner).ConfigureAwait(true);
            if (bounds is not { } rect || rect.IsEmpty)
            {
                return;
            }

            viewModel.CropPreviewCommand.Execute(rect);
            viewModel.StatusMessage = $"切り出しました（{rect.Width} x {rect.Height}）";
        }

        // ウィンドウ枠の自動検出を実行・キャンセルする。
        void AutoCrop()
        {
            if (viewModel.IsAutoCropActive)
            {
                viewModel.CancelAutoCropCommand.Execute(null);
                viewModel.StatusMessage = "自動切り出しをキャンセルしました。";
                return;
            }

            viewModel.AutoCropWindowCommand.Execute(null);
            if (viewModel.DetectedRects.Count == 0)
            {
                viewModel.StatusMessage = "ウィンドウ枠を検出できませんでした。";
            }
        }

        // 検出矩形とホバーの強調をオーバーレイへ反映する。
        void ApplyAutoCropRects()
        {
            if (!viewModel.IsAutoCropActive)
            {
                crop.Rects = [];
                crop.HighlightSourceRect = null;
                return;
            }

            List<OverlayRect> rects = [];
            foreach (var rect in viewModel.DetectedRects)
            {
                rects.Add(new OverlayRect(
                    EsRect.FromRectInt(rect),
                    OverlayLineStyle.Solid,
                    s_autoCropBorder,
                    s_autoCropBorder,
                    AutoCropBorderThickness));
            }

            crop.Rects = rects;
            crop.HighlightSourceRect = ToEsRect(viewModel.HoveredRect);
        }

        // カーソル移動を座標表示とホバーの強調へ反映する。
        void OnPointerMoved(Point location)
        {
            var source = crop.ClientToSource(new EsPoint(location.X, location.Y));
            viewModel.PointerDisplayText =
                $"{UiFactory.FormatNumber(Math.Round(source.X), "0")}, {UiFactory.FormatNumber(Math.Round(source.Y), "0")}";
            UpdateStatus();

            if (!viewModel.IsAutoCropActive)
            {
                return;
            }

            var hovered = viewModel.HitDetectedRect(source.X, source.Y);
            if (viewModel.HoveredRect != hovered)
            {
                viewModel.HoveredRect = hovered;
                crop.HighlightSourceRect = ToEsRect(hovered);
            }
        }

        // プレビューの 2 回押しで自動切り出しの矩形を確定・キャンセルする。
        void OnCanvasDoubleClick(Point location)
        {
            if (!viewModel.IsAutoCropActive)
            {
                return;
            }

            var source = crop.ClientToSource(new EsPoint(location.X, location.Y));
            var hit = viewModel.HitDetectedRect(source.X, source.Y);
            if (hit is { } rect)
            {
                viewModel.ConfirmAutoCropCommand.Execute(rect);
                viewModel.StatusMessage = $"切り出しました（{rect.Width} x {rect.Height}）";
                return;
            }

            viewModel.CancelAutoCropCommand.Execute(null);
            viewModel.StatusMessage = "自動切り出しをキャンセルしました。";
        }

        // メニュー（画像の操作・撮影・切り出し）を表示する。
        void ShowMenu()
        {
            using var menu = new ContextMenuStrip();
            AddMenuItem(menu, "画像を保存", "Ctrl+S", SaveImage);
            AddMenuItem(menu, "コピー", "Ctrl+C", () => _ = viewModel.CopyToClipboardAsync());
            AddMenuItem(menu, "貼り付け", "Ctrl+V", () => _ = PasteCoreAsync());
            menu.Items.Add(new ToolStripSeparator());
            AddMenuItem(menu, "元に戻す", "Ctrl+Z", () => EditHistory.Undo(services, ApplyAppearance));
            AddMenuItem(menu, "元に戻すを取り消す", "Ctrl+Y", () => EditHistory.Redo(services, ApplyAppearance));
            menu.Items.Add(new ToolStripSeparator());
            AddMenuItem(menu, "撮影 - スクリーン選択", "", () => StartCapture(CaptureType.ScreenSelect));
            AddMenuItem(menu, "撮影 - ウィンドウ選択", "", () => StartCapture(CaptureType.WindowSelect));
            AddMenuItem(menu, "撮影 - 範囲選択", "", () => StartCapture(CaptureType.AreaSelect));
            menu.Items.Add(new ToolStripSeparator());
            AddMenuItem(menu, "切り出し", "Ctrl+R", ToggleCrop);
            AddMenuItem(menu, "Crop - 自動切り出し", "", AutoCrop);
            menu.Items.Add(new ToolStripSeparator());
            AddMenuItem(menu, "ファイル名テンプレートを編集", "", () => templateBox.Focus());

            menu.Show(menuButton, new Point(0, menuButton.Height));
        }

        // ページ固有のショートカット キーを処理する。
        bool HandleShortcut(Keys keyData)
        {
            var key = keyData & Keys.KeyCode;
            if ((keyData & Keys.Control) != Keys.Control)
            {
                if (key == Keys.Escape && viewModel.IsAutoCropActive)
                {
                    viewModel.CancelAutoCropCommand.Execute(null);
                    return true;
                }

                return false;
            }

            switch (key)
            {
                case Keys.S:
                    SaveImage();
                    return true;
                case Keys.C:
                    _ = viewModel.CopyToClipboardAsync();
                    return true;
                case Keys.V:
                    _ = PasteCoreAsync();
                    return true;
                case Keys.Z:
                    EditHistory.Undo(services, ApplyAppearance);
                    return true;
                case Keys.Y:
                    EditHistory.Redo(services, ApplyAppearance);
                    return true;
                case Keys.R:
                    ToggleCrop();
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>撮影結果をプレビューへ反映する。</summary>
    /// <param name="services">アプリ全体で共有するコアオブジェクト（履歴の所有者）。</param>
    /// <param name="viewModel">アプリのメイン ViewModel（画像の所有者）。</param>
    /// <param name="e">撮影結果。 キャンセル時は結果が空。</param>
    /// <remarks>
    /// 1. 撮影前に表示していた画像を履歴へ積む（撮影前の状態へ戻せるようにする）。<br/>
    /// 2. 撮影結果をプレビューへ設定する（画素は読み取りの面と共有され、実体は読み取りの面が保持する）。<br/>
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
        viewModel.StatusMessage = $"キャプチャしました（{captured.Width} x {captured.Height}）";
    }

    /// <summary>上書きの確認ダイアログを表示する。</summary>
    /// <param name="owner">ダイアログのオーナー。</param>
    /// <param name="fileName">既に存在するファイル名。</param>
    /// <returns>上書きする場合は true。</returns>
    private static bool ConfirmOverwrite(Form owner, string fileName)
        => MessageBox.Show(
            owner,
            $"ファイル「{fileName}」は既に存在します。上書きしますか？",
            "上書き確認",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    /// <summary>メニューへ 1 項目を追加する。</summary>
    /// <param name="menu">対象のメニュー。</param>
    /// <param name="text">表示する文言。</param>
    /// <param name="shortcut">右端へ表示するショートカット（表示のみ）。</param>
    /// <param name="action">選択時の処理。</param>
    private static void AddMenuItem(ContextMenuStrip menu, string text, string shortcut, Action action)
    {
        var item = new ToolStripMenuItem(text);
        if (shortcut.Length > 0)
        {
            item.ShortcutKeyDisplayString = shortcut;
        }

        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }

    /// <summary>コアの整数矩形を小数の矩形へ変換する。</summary>
    /// <param name="rect">変換する矩形。 null の場合は null。</param>
    /// <returns>変換した矩形。</returns>
    private static EsRect? ToEsRect(EsRectInt? rect)
        => rect is { } value ? EsRect.FromRectInt(value) : null;
}
