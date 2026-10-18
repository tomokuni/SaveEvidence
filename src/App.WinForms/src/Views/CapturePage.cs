using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
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

    /// <summary>ファイル名テンプレートの最小幅（DIP）。 4 つの UI 版で共通。</summary>
    private const double TemplateMinWidth = 100;

    /// <summary>ファイル名テンプレートの文字の左右の余白（DIP）。</summary>
    private const double TemplateTextPadding = 16;

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

        // ログは撮影スタック自身のカテゴリと既定の接頭辞で記録する（他の UI 版と同じ。
        // 接頭辞をアプリのものへ差し替えると、出力元のアセンブリを読み分けられなくなる）
        var captureSession = new CaptureSessionControl(
            CaptureSettingsMapper.From(settingsService.Current, 1.0),
            AppLog.CreateLogger(nameof(CaptureSessionControl)));

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

        var canvas = new Panel { Dock = DockStyle.Fill, BackColor = AppStyles.ImageAreaBackground };
        canvas.Controls.Add(crop);

        // 画像の 4 辺・4 隅の影（幅・濃さは App.Core の ShadowPlan が単一所有する）。
        // 画像と同じ描画面へ先に描く（重ねると画像の端の混色の下地が背景になり、白い線として見える）
        crop.ZoomView.SurroundRenderer = ShadowDrawer.Draw;

        var zoomSlider = new TrackBar
        {
            Minimum = 10,
            Maximum = 500,
            TickFrequency = 10,
            SmallChange = 5,
            LargeChange = 25,
            Width = 140,
            // 既定は AutoSize（推奨サイズ 56）で Height の指定を無視するため、明示的に無効にする
            // （行の高さ 30 に収まらず、トラックが中央から外れる）
            AutoSize = false,
            // 目盛りは表示しない（Avalonia / MewUI 版のスライダーと同じ見た目にする）
            TickStyle = TickStyle.None,
            TabStop = false,
        };
        UiFactory.CenterInStatusBar(zoomSlider, 24);
        var syncingZoomSlider = false;

        var zoomText = UiFactory.CreateStatusLabel(52, "拡大率");
        var zoomOutButton = UiFactory.CenterInStatusBar(UiFactory.CreateToolButton(AppIcon.ZoomOut, "縮小", () => crop.ZoomOut()));
        var zoomInButton = UiFactory.CenterInStatusBar(UiFactory.CreateToolButton(AppIcon.ZoomIn, "拡大", () => crop.ZoomIn()));
        var zoomFitButton = UiFactory.CenterInStatusBar(UiFactory.CreateToolButton(AppIcon.ZoomFit, "ウィンドウに合わせる", () => crop.ZoomFit()));

        var pointerLabel = UiFactory.CreateStatusLabel(70, "カーソル位置（画像ピクセル）");
        var sizeLabel = UiFactory.CreateStatusLabel(80, "プレビュー画像のサイズ");
        var saveStateLabel = UiFactory.CreateStatusLabel(54, "保存済み / 未保存");
        var savedFileLabel = UiFactory.CreateStatusLabel(0, "直前に保存したファイル名");
        var messageLabel = UiFactory.CreateStatusLabel(0);
        messageLabel.ForeColor = AppStyles.Announce;

        var statusBar = UiFactory.CreateStatusBar();
        var statusFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        statusFlow.Controls.AddRange([pointerLabel, sizeLabel, saveStateLabel, savedFileLabel, messageLabel]);

        var zoomPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        zoomPanel.Controls.AddRange([zoomOutButton, zoomSlider, zoomInButton, zoomFitButton, zoomText]);

        // 塗り（Fill）を先に追加する（WinForms は後から追加したものから辺へ寄せるため）
        statusBar.Controls.Add(statusFlow);
        statusBar.Controls.Add(zoomPanel);

        var templateBox = new TextBox
        {
            Width = 260,
            MinimumSize = new Size(DpiScaler.ScaleToPixel(TemplateMinWidth, DpiScaler.ToScaling(crop.DeviceDpi)), 0),
            Text = viewModel.FileNameTemplateText,
            // ツールバーの行（30）の中で文字が垂直方向中央に来るよう、高さと上下の余白を揃える
            Height = 24,
            Margin = new Padding(3, 3, 3, 3),
        };
        UiFactory.AttachToolTip(templateBox, "ファイル名テンプレート（例: screenshot_007_{date}-{time}.png）");
        templateBox.TextChanged += (_, _) => viewModel.FileNameTemplateText = templateBox.Text;

        var menuButton = new ToolButton(AppIcon.Menu, "メニュー");
        UiFactory.AttachToolTip(menuButton, "メニュー");
        var toolbar = UiFactory.CreateToolbar();
        toolbar.Controls.Add(menuButton);
        toolbar.Controls.Add(UiFactory.CreateSeparator());
        toolbar.Controls.Add(templateBox);
        toolbar.Controls.Add(UiFactory.CreateToolButton(AppIcon.Save, "保存", SaveImage));
        toolbar.Controls.Add(UiFactory.CreateSeparator());
        toolbar.Controls.Add(UiFactory.CreateToolButton(AppIcon.ScreenSelect, "スクリーン選択で撮影", () => StartCapture(CaptureType.ScreenSelect)));
        toolbar.Controls.Add(UiFactory.CreateToolButton(AppIcon.WindowSelect, "ウィンドウ選択で撮影", () => StartCapture(CaptureType.WindowSelect)));
        toolbar.Controls.Add(UiFactory.CreateToolButton(AppIcon.AreaSelect, "範囲選択で撮影", () => StartCapture(CaptureType.AreaSelect)));
        toolbar.Controls.Add(UiFactory.CreateSeparator());
        toolbar.Controls.Add(UiFactory.CreateToolButton(AppIcon.Crop, "切り出し（開始 / 確定 / キャンセル）", ToggleCrop));

        // 幅は入力された文字列の幅に合わせる（最小 100 DIP・上限はツールバーが折り返さない幅）。
        // 上限を取るのは、幅を広げすぎるとツールバーのアイコンが 2 段になるため（他の UI 版と同じ）。
        void UpdateTemplateWidth()
        {
            var scaling = DpiScaler.ToScaling(crop.DeviceDpi);
            var minimum = DpiScaler.ScaleToPixel(TemplateMinWidth, scaling);
            var textWidth = TextRenderer.MeasureText(templateBox.Text, templateBox.Font).Width;
            var desired = Math.Max(minimum, textWidth + DpiScaler.ScaleToPixel(TemplateTextPadding, scaling));

            var others = 0;
            foreach (Control child in toolbar.Controls)
            {
                if (!ReferenceEquals(child, templateBox) && child.Visible)
                {
                    others += child.Width + child.Margin.Horizontal;
                }
            }

            var available = toolbar.ClientSize.Width - others - templateBox.Margin.Horizontal;
            templateBox.Width = available > minimum ? Math.Min(desired, available) : minimum;
        }

        templateBox.TextChanged += (_, _) => UpdateTemplateWidth();
        toolbar.SizeChanged += (_, _) => UpdateTemplateWidth();
        UpdateTemplateWidth();

        var root = new Panel { Dock = DockStyle.Fill, BackColor = AppStyles.PageBackground };
        root.Controls.Add(canvas);
        root.Controls.Add(toolbar);
        root.Controls.Add(statusBar);

        // メニューは 1 度だけ作り、以降は同じインスタンスを表示する。
        // 表示のたびに作って破棄すると、WinForms のメニュー フィルターが破棄済みのメニューを参照し、
        // ウィンドウの非アクティブ化の際に ObjectDisposedException で落ちる（実測）。
        // 破棄はページの破棄に合わせる
        var menu = CreateMenu();
        root.Disposed += (_, _) => menu.Dispose();
        menuButton.Click += (_, _) => menu.Show(menuButton, new Point(0, menuButton.Height));
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
            // 画像領域（プレビューの下地）だけは他の UI 版と同じ色にする（色は AppPalette が単一所有する）
            canvas.BackColor = AppStyles.ImageAreaBackground;
            templateBox.ForeColor = AppStyles.Foreground;
            templateBox.BackColor = pageBackground;
            zoomSlider.BackColor = pageBackground;

            crop.Appearance = ZoomViewAppearance.Default with
            {
                BackgroundColor = AppStyles.ImageAreaBackground,
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

            // 通知メッセージだけは強調色にする（上の一括設定より後に置く）
            messageLabel.ForeColor = AppStyles.Announce;

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
                // 実行できない理由の文言はコアが単一所有する
                viewModel.StatusMessage = StatusMessages.SaveUnavailable(
                    viewModel.HasInvalidTemplate, viewModel.ValidationMessage);
                return;
            }

            _ = SaveImageCoreAsync();
        }

        // 保存を実行する（失敗の通知はコア（SaveImageAsync）が行う）。
        async Task SaveImageCoreAsync()
            => await viewModel.SaveImageAsync().ConfigureAwait(true);

        // クリップボードの画像を貼り付ける（成否の通知はコアが行う）。
        async Task PasteCoreAsync()
            => await viewModel.PasteFromClipboardAsync().ConfigureAwait(true);

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
        }

        // ウィンドウ枠の自動検出を実行・キャンセルする（通知はコアが行う）。
        void AutoCrop()
        {
            // 実行中は同じ操作でキャンセルする（状態の判定はコアのプロパティに従う）
            if (viewModel.IsAutoCropActive)
            {
                viewModel.CancelAutoCropCommand.Execute(null);
                return;
            }

            viewModel.AutoCropWindowCommand.Execute(null);
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
            // クライアント座標（物理ピクセル）→ ビューポート座標（DIP）→ 対象座標（元画像ピクセル）の順に変換する
            // （座標変換の規則は CropViewControl が単一所有する）
            var scaling = DpiScaler.ToScaling(crop.ZoomView.DeviceDpi);
            var viewport = new EsPoint(
                DpiScaler.ToDip(location.X, scaling), DpiScaler.ToDip(location.Y, scaling));
            var source = crop.ClientToSource(viewport);

            // 画像の外（余白）では座標を表示しない（他の UI 版と同じ "-" 表示）。
            // 判定は変換結果の範囲で行う（表示矩形の大きさに依存せず、倍率・中央寄せの影響を受けない）
            if (viewModel.PreviewImage is not { } image ||
                source.X < 0 || source.Y < 0 ||
                source.X >= image.Size.Width || source.Y >= image.Size.Height)
            {
                viewModel.PointerDisplayText = "-";
                UpdateStatus();
                if (viewModel.IsAutoCropActive)
                {
                    viewModel.HoveredRect = null;
                    crop.HighlightSourceRect = null;
                }

                return;
            }

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
                return;
            }

            viewModel.CancelAutoCropCommand.Execute(null);
        }

        // メニュー（画像の操作・撮影・切り出し）を組み立てる。
        // 表示は menuButton の購読が行い、破棄は root の破棄に合わせる（同じインスタンスを使い回す）。
        ContextMenuStrip CreateMenu()
        {
            var menuStrip = new ContextMenuStrip();
            AddMenuItem(menuStrip, "画像を保存", "Ctrl+S", SaveImage);
            AddMenuItem(menuStrip, "コピー", "Ctrl+C", () => _ = viewModel.CopyToClipboardAsync());
            AddMenuItem(menuStrip, "貼り付け", "Ctrl+V", () => _ = PasteCoreAsync());
            menuStrip.Items.Add(new ToolStripSeparator());
            AddMenuItem(menuStrip, "元に戻す", "Ctrl+Z", () => EditHistory.Undo(services, ApplyAppearance));
            AddMenuItem(menuStrip, "元に戻すを取り消す", "Ctrl+Y", () => EditHistory.Redo(services, ApplyAppearance));
            menuStrip.Items.Add(new ToolStripSeparator());
            AddMenuItem(menuStrip, "撮影 - スクリーン選択", "", () => StartCapture(CaptureType.ScreenSelect));
            AddMenuItem(menuStrip, "撮影 - ウィンドウ選択", "", () => StartCapture(CaptureType.WindowSelect));
            AddMenuItem(menuStrip, "撮影 - 範囲選択", "", () => StartCapture(CaptureType.AreaSelect));
            menuStrip.Items.Add(new ToolStripSeparator());
            AddMenuItem(menuStrip, "切り出し", "Ctrl+R", ToggleCrop);
            AddMenuItem(menuStrip, "Crop - 自動切り出し", "", AutoCrop);
            menuStrip.Items.Add(new ToolStripSeparator());
            AddMenuItem(menuStrip, "ファイル名テンプレートを編集", "", () => templateBox.Focus());
            return menuStrip;
        }

        // ページ固有のショートカット キーを処理する。
        bool HandleShortcut(Keys keyData)
        {
            // テキスト入力中のショートカットは文字編集を優先する
            // （Ctrl+V を画像の貼り付けとして横取りすると、テンプレート欄へ文字列を貼れない）
            if (IsTextInputFocused())
            {
                return false;
            }

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

        // テキスト入力中のコントロールにフォーカスがあるかどうか。
        // 入力中はページのショートカット（Ctrl+C/V/Z/Y）を横取りしない（文字編集を優先する）。
        bool IsTextInputFocused()
        {
            var focused = owner.ActiveControl;
            return focused is TextBoxBase or ComboBox or NumericUpDown;
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
        // 通知の文言はコアが単一所有する（キャプチャ結果は画像の差し替えより後に通知する）
        viewModel.StatusMessage = StatusMessages.CaptureCompleted(captured.Width, captured.Height);
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
