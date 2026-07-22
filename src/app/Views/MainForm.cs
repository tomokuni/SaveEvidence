using System.Diagnostics;
using app.Models;
using app.Services;
using app.ViewModels;

namespace app.Views;

/// <summary>
/// メインフォーム。画面キャプチャとプレビュー表示を行うメインウィンドウ。
/// </summary>
public partial class MainForm : Form
{
    private readonly MainViewModel _viewModel;
    private readonly HotKeyManager? _hotKeyManager;
    private readonly SettingsService _settingsService;
    private bool _isExecutingCapture;
    private bool _isCropMode;
    private readonly CropSelection _cropSelection;
    private Point _cropMouseClientPos = new(-1, -1);
    private bool _menuStateDirty = true;

    // 拡大率管理（0 = 自動）
    private int _zoomPercent;
    private static readonly int[] s_zoomValues = [0, 25, 33, 50, 67, 75, 80, 90, 100, 110, 125, 150, 175, 200, 250, 300, 400, 500];
    private int _zoomIndex;

    // スクロール管理
    private Point _picBaseLocation;
    private int _scrollX, _scrollY;

    // アンドゥ管理（最大5回）
    private const int MaxUndoCount = 5;
    private readonly List<Image> _undoStack = new(MaxUndoCount);
    private bool _isUndoing;

    public MainForm(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _viewModel = new MainViewModel(settingsService);
        _cropSelection = new CropSelection();

        if (System.ComponentModel.LicenseManager.UsageMode != System.ComponentModel.LicenseUsageMode.Designtime)
        {
            _hotKeyManager = new HotKeyManager(Handle);
        }

        InitializeComponent();
        InitializeViewModel();
        RegisterHotKeys();
        RestoreFormBounds();

        // 常時マウスイベント購読（切出しモードON/OFFに関わらずドラッグ可能）
        _picPreview.MouseDown += PicPreview_CropMouseDown;
        _picPreview.MouseMove += PicPreview_CropMouseMove;
        _picPreview.MouseUp += PicPreview_CropMouseUp;
        _pnlPreview.MouseDown += PnlPreview_MouseDown;
        _pnlPreview.MouseMove += PicPreview_CropMouseMove;
        _pnlPreview.MouseUp += PicPreview_CropMouseUp;
        _picLoupe.SizeMode = PictureBoxSizeMode.StretchImage;
        _picLoupe.Enabled = true;
        _picLoupe.TabStop = false;
        _picLoupe.Size = new Size(_viewModel.Settings.LoupeSize, _viewModel.Settings.LoupeSize);
        _picLoupe.MouseMove += PicLoupe_MouseMove;
        _picLoupe.MouseDown += PicLoupe_MouseDown;
        _picLoupe.MouseUp += PicLoupe_MouseUp;
        _picPreview.Paint += PicPreview_CropPaint;
        _picPreview.MouseWheel += PicPreview_MouseWheel;
        _pnlPreview.MouseWheel += PicPreview_MouseWheel;
        _picPreview.BackColor = Color.Transparent;
        _pnlPreview.Paint += PnlPreview_Paint;
        // パネルのダブルバッファリングを有効化（チラつき防止）
        typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(_pnlPreview, true);
        _pnlPreview.Resize += (_, _) => { UpdatePictureBoxZoom(); UpdateStatusBar(); UpdateMenuStates(); UpdateScrollBars(); };
        Resize += (_, _) => { if (_zoomPercent == 0) { UpdatePictureBoxZoom(); UpdateStatusBar(); UpdateMenuStates(); } };
        UpdateStatusBar();
        UpdateMenuStates();
        UpdateScrollBars();
        ApplyLoupeModeFromSetting();

        // コンテキストメニュー表示前に状態を更新
        _contextMenuPreview.Opening += (_, _) => { if (_menuStateDirty) UpdateMenuStates(); };
        _contextMenuLink.Opening += (_, _) => { if (_menuStateDirty) UpdateMenuStates(); };

        // メニュードロップダウン表示前に状態を更新
        _menuFile.DropDownOpening += (_, _) => { if (_menuStateDirty) UpdateMenuStates(); };
        _menuEdit.DropDownOpening += (_, _) => { if (_menuStateDirty) UpdateMenuStates(); };
        _menuView.DropDownOpening += (_, _) => { if (_menuStateDirty) UpdateMenuStates(); };

        // 表示メニュー「拡大率」サブメニューにズーム率項目を追加
        foreach (var zoom in s_zoomValues)
        {
            var item = new ToolStripMenuItem();
            item.Text = zoom == 0 ? "自動" : $"{zoom}%";
            item.Click += (_, _) => SetZoomFromContext(zoom);
            _menuViewZoom.DropDownItems.Add(item);
        }

        }

    private void ResetZoomToAuto()
    {
        _zoomIndex = 0;
        _zoomPercent = 0;
        _viewModel.ZoomPercent = 0;
        UpdatePictureBoxZoom();
    }

    /// <summary>
    /// 実際の表示倍率を取得する。自動の場合はフィット倍率を計算する。
    /// </summary>
    private double GetActualZoom()
    {
        var img = _viewModel.PreviewImage;
        if (img is null) return 1.0;

        if (_zoomPercent > 0)
            return _zoomPercent / 100.0;

        // 自動 = フィット
        var cw = _pnlPreview.ClientSize.Width;
        var ch = _pnlPreview.ClientSize.Height;
        return Math.Min((double)cw / img.Width, (double)ch / img.Height);
    }

    /// <summary>
    /// PictureBox のサイズと位置を現在のズーム・表示位置に合わせて設定する
    /// </summary>
    private void UpdatePictureBoxZoom()
    {
        var img = _viewModel.PreviewImage;
        if (img is null) return;

        var zoom = GetActualZoom();
        var panelW = _pnlPreview.ClientSize.Width;
        var panelH = _pnlPreview.ClientSize.Height;
        var imgW = (int)Math.Round(img.Width * zoom);
        var imgH = (int)Math.Round(img.Height * zoom);
        var centerAlign = _viewModel.Settings.CenterAlign;

        if (_zoomPercent > 0 || !centerAlign)
        {
            // 手動ズーム または 左上寄せ：StretchImage で明示サイズ
            _picPreview.SizeMode = PictureBoxSizeMode.StretchImage;
            _picPreview.Size = new Size(imgW, imgH);
        }
        else
        {
            // 自動＋中央寄せ：Zoom でフィット
            _picPreview.SizeMode = PictureBoxSizeMode.Zoom;
            _picPreview.Size = new Size(panelW, panelH);
        }

        // スクロールリセット
        _scrollX = 0;
        _scrollY = 0;

        // 位置調整
        if (centerAlign && (_zoomPercent > 0 || _picPreview.SizeMode == PictureBoxSizeMode.StretchImage))
        {
            _picBaseLocation = new Point(
                Math.Max(0, (int)Math.Round((panelW - imgW) / 2.0)),
                Math.Max(0, (int)Math.Round((panelH - imgH) / 2.0)));
        }
        else
        {
            _picBaseLocation = Point.Empty;
        }

        _picPreview.Location = _picBaseLocation;

        // スクロールバー更新
        UpdateScrollBars();

        _picPreview.Invalidate();
    }

    /// <summary>
    /// スクロールバーの可視状態と範囲を更新する
    /// </summary>
    private void UpdateScrollBars()
    {
        var pw = _pnlPreview.ClientSize.Width;
        var ph = _pnlPreview.ClientSize.Height;

        _hScroll.Visible = true;
        _vScroll.Visible = true;

        // 画像がない場合は無効状態で表示
        if (_viewModel.PreviewImage is null)
        {
            _hScroll.Enabled = false;
            _vScroll.Enabled = false;
            _hScroll.Minimum = 0;
            _hScroll.Maximum = 0;
            _vScroll.Minimum = 0;
            _vScroll.Maximum = 0;
            return;
        }

        // スクロール不要な場合は無効状態で表示
        _hScroll.Enabled = _picPreview.Width + _picBaseLocation.X > pw;
        _vScroll.Enabled = _picPreview.Height + _picBaseLocation.Y > ph;

        _hScroll.Minimum = 0;
        _hScroll.Maximum = Math.Max(0, _picPreview.Width + _picBaseLocation.X - pw);
        _hScroll.LargeChange = Math.Max(1, _hScroll.Maximum / 10 + 1);
        _hScroll.SmallChange = 20;

        _vScroll.Minimum = 0;
        _vScroll.Maximum = Math.Max(0, _picPreview.Height + _picBaseLocation.Y - ph);
        _vScroll.LargeChange = Math.Max(1, _vScroll.Maximum / 10 + 1);
        _vScroll.SmallChange = 20;
    }

    /// <summary>
    /// スクロール位置に合わせて PictureBox の位置を更新する
    /// </summary>
    private void UpdatePictureLocation()
    {
        _picPreview.Location = new Point(
            _picBaseLocation.X - _scrollX,
            _picBaseLocation.Y - _scrollY);
    }

    private void VScroll_Scroll(object? sender, ScrollEventArgs e)
    {
        _scrollY = e.NewValue;
        UpdatePictureLocation();
    }

    private void HScroll_Scroll(object? sender, ScrollEventArgs e)
    {
        _scrollX = e.NewValue;
        UpdatePictureLocation();
    }

    private void UpdatePreviewImage(Image image)
    {
        _picPreview.Image?.Dispose();
        _picPreview.Image = null;

        _picPreview.Image = new Bitmap(image);
        UpdatePictureBoxZoom();

        // 切出しモード中なら CropPaint を再購読（UpdatePreviewImage で解除されるため）
        if (_isCropMode)
        {
            _picPreview.Paint += PicPreview_CropPaint;
        }

        _picPreview.Paint += PicPreview_Paint!;

        // マウス座標を初期化してルーペを表示
        _cropMouseClientPos = new Point(_picPreview.ClientSize.Width / 2, _picPreview.ClientSize.Height / 2);
        UpdateLoupePosition();
        NotifyMenuStateChanged();
    }

    private void PicPreview_Paint(object? sender, PaintEventArgs e)
    {
        // 画像のPaintは何もしない
    }

    private void UpdateStatusBar()
    {
        var img = _viewModel.PreviewImage;
        var hasImage = img is not null;

        // 拡大率
        var zoomText = hasImage ? $"{GetActualZoomPercent()}%" : "-%";
        _lblZoom.Text = zoomText;
        _lblZoom.ToolTipText = $"拡大率: {zoomText}";

        // イメージサイズ
        var sizeText = hasImage ? $"{img.Width}, {img.Height}" : "-, -";
        _lblImageSize.Text = sizeText;
        _lblImageSize.ToolTipText = $"イメージサイズ: ({sizeText})";

        // 保存状態
        var savedText = _viewModel.StatusText;
        if (string.IsNullOrEmpty(savedText))
        {
            _lblSavedStatus.Text = "未保存";
            _lblSavedStatus.ToolTipText = "イメージ未保存";
        }
        else
        {
            // StatusText は "保存済み {ファイル名}" 形式 → 表示用に整形
            _lblSavedStatus.Text = savedText.Replace("保存済み", "保存済:");
            _lblSavedStatus.ToolTipText = savedText.Replace("保存済み", "イメージ保存済:");
        }

        // 寄せ
        var isCenter = _viewModel.Settings.CenterAlign;
        var alignText = isCenter ? "中央寄せ" : "左上寄せ";
        _lblAlign.Text = alignText;
        _lblAlign.ToolTipText = $"イメージ位置: {alignText}";

        // ルーペ
        var loupeText = _viewModel.Settings.LoupeModeValue switch
        {
            LoupeMode.Show => "常時表示",
            LoupeMode.Auto => "範囲選択時表示",
            _ => "非表示",
        };
        _lblLoupe.Text = loupeText;
        _lblLoupe.ToolTipText = $"ルーペ: {loupeText}";

        // フォルダーパス
        _lblFolderLink.ToolTipText = $"イメージの保存先: {_viewModel.SaveFolderPath}";
    }

    // ─── ズーム共通 ─────────────────────────────

    /// <summary>
    /// ホイールDelta方向にズームを1ステップ変更する。
    /// direction = 1: 拡大, direction = -1: 縮小
    /// </summary>
    private void StepZoom(int direction)
    {
        var currentZoom = GetActualZoomPercent();
        var bestIndex = _zoomIndex;

        if (direction > 0)
        {
            for (var i = 0; i < s_zoomValues.Length; i++)
                if (s_zoomValues[i] > currentZoom) { bestIndex = i; break; }
        }
        else
        {
            for (var i = s_zoomValues.Length - 1; i >= 0; i--)
                if (s_zoomValues[i] < currentZoom && s_zoomValues[i] != 0) { bestIndex = i; break; }
        }

        if (bestIndex != _zoomIndex)
        {
            _zoomIndex = bestIndex;
            _zoomPercent = s_zoomValues[_zoomIndex];
            UpdatePictureBoxZoom();
            UpdateStatusBar();
        }
        NotifyMenuStateChanged();
    }

    // ─── マウスホイール ─────────────────────────────

    private void PicPreview_MouseWheel(object? sender, MouseEventArgs e)
    {
        if (ModifierKeys == Keys.Control)
        {
            StepZoom(e.Delta > 0 ? 1 : -1);
        }
        else if (ModifierKeys == Keys.Shift)
        {
            // Shift+ホイール → 水平スクロール
            if (_hScroll.Visible)
            {
                var newVal = Math.Clamp(_scrollX - Math.Sign(e.Delta) * _hScroll.SmallChange, _hScroll.Minimum, _hScroll.Maximum);
                if (newVal != _scrollX)
                {
                    _scrollX = newVal;
                    _hScroll.Value = newVal;
                    UpdatePictureLocation();
                }
            }
        }
        else
        {
            // 垂直スクロール
            if (_vScroll.Visible)
            {
                var newVal = Math.Clamp(_scrollY - Math.Sign(e.Delta) * _vScroll.SmallChange, _vScroll.Minimum, _vScroll.Maximum);
                if (newVal != _scrollY)
                {
                    _scrollY = newVal;
                    _vScroll.Value = newVal;
                    UpdatePictureLocation();
                }
            }
        }
    }

    /// <summary>
    /// 現在の実際の拡大率（%）を取得する
    /// </summary>
    private int GetActualZoomPercent()
    {
        if (_zoomPercent > 0) return _zoomPercent;
        var zoom = GetActualZoom();
        return (int)(zoom * 100);
    }

    // ─── キャプチャ実行 ─────────────────────────────

    private void InitializeViewModel()
    {
        // ─── MainViewModel の変更通知を DataBindings と PropertyChanged で処理 ──

        // DataBindings（シンプルな 1:1 バインディングは自動反映）
        _btnCopy.DataBindings.Add("Enabled", _viewModel, nameof(MainViewModel.CanCopy));
        _btnSave.DataBindings.Add("Enabled", _viewModel, nameof(MainViewModel.CanSave));
        _lblFolderLink.DataBindings.Add("Text", _viewModel, nameof(MainViewModel.SaveFolderPath));
        _txtFileNameTemplate.DataBindings.Add("Text", _viewModel, nameof(MainViewModel.FileNameTemplateText),
            false, DataSourceUpdateMode.OnPropertyChanged);

        // ToolTip は DataBindings 非対応のため、PropertyChanged で処理
        _viewModel.PropertyChanged += (s, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.PreviewImage):
                    if (_viewModel.PreviewImage is not null)
                    {
                        ExitCropMode();
                        UpdatePreviewImage(_viewModel.PreviewImage);
                        ResetZoomToAuto();
                    }
                    UpdateStatusBar();
                    UpdateMenuStates();
                    break;

                case nameof(MainViewModel.SaveFolderPath):
                    _toolTip.SetToolTip(_btnSaveFolder, _viewModel.SaveFolderPath);
                    _lblFolderLink.ToolTipText = $"イメージの保存先: {_viewModel.SaveFolderPath}";
                    break;

                case nameof(MainViewModel.CurrentFileNamePreview):
                    UpdateStatusBar();
                    break;

                case nameof(MainViewModel.IsSaved):
                    _picPreview.Invalidate();
                    UpdateStatusBar();
                    break;
            }
        };

        // ─── Settings の変更通知 ──────────────────────────
        _viewModel.Settings.PropertyChanged += (s, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(Settings.LoupeModeValue):
                    ApplyLoupeModeFromSetting();
                    UpdateStatusBar();
                    break;

                case nameof(Settings.LoupeSize):
                case nameof(Settings.LoupeZoomLevel):
                    ApplyLoupeSettings();
                    break;

                case nameof(Settings.CenterAlign):
                case nameof(Settings.CaptureMode):
                    UpdateStatusBar();
                    UpdateMenuStates();
                    break;
            }
        };

        _viewModel.StartSelectionMode = StartSelectionMode;

        // 初回表示設定
        _toolTip.SetToolTip(_btnSaveFolder, _viewModel.SaveFolderPath);
        _lblFolderLink.ToolTipText = $"イメージの保存先: {_viewModel.SaveFolderPath}";

        // StatusStrip の ToolStripItem は ToolTipText を直接表示できないため
        // MouseMove で項目を特定して _toolTip で表示する
        _statusStrip.MouseMove += (_, e) =>
        {
            var item = _statusStrip.GetItemAt(e.Location);
            _toolTip.SetToolTip(_statusStrip, (item as ToolStripStatusLabel)?.ToolTipText ?? "");
        };

        UpdateStatusBar();
    }

    private void RegisterHotKeys()
    {
        _hotKeyManager?.RegisterAll(_viewModel.Settings);
    }

    private void ExecuteCapture(CaptureType captureType)
    {
        if (_isExecutingCapture) return;
        _isExecutingCapture = true;
        switch (captureType)
        {
            case CaptureType.SelectScreen: PerformSelectScreenCapture(); break;
            case CaptureType.WindowSelect: StartSelectionMode(CaptureType.WindowSelect); break;
            case CaptureType.AreaSelect: StartSelectionMode(CaptureType.AreaSelect); break;
        }
    }

    private void PerformSelectScreenCapture()
    {
        var screens = CaptureManager.GetAllScreenBounds();
        if (screens.Length == 1)
        {
            _viewModel.CaptureScreenArea(screens[0].Bounds);
            _isExecutingCapture = false;
            return;
        }

        Hide();
        Task.Delay(300).ContinueWith(_ =>
        {
            BeginInvoke(() =>
            {
                try
                {
                    using var fullCapture = CaptureManager.CaptureArea(SystemInformation.VirtualScreen);
                    using var selectionForm = new SelectionForm(CaptureType.SelectScreen, fullCapture, _viewModel.Settings.CaptureBorderColor);
                    selectionForm.SelectionCompleted += (s, rect) => { try { _viewModel.CaptureScreenArea(rect); } catch (Exception) { } };
                    selectionForm.Cancelled += (s, e) => { };
                    selectionForm.ShowDialog();
                }
                catch (Exception) { }
                finally { _isExecutingCapture = false; EnsureVisible(); }
            });
        });
    }

    private void StartSelectionMode(CaptureType captureType)
    {
        Hide();
        Task.Delay(300).ContinueWith(_ =>
        {
            BeginInvoke(() =>
            {
                try
                {
                    using var fullCapture = CaptureManager.CaptureArea(SystemInformation.VirtualScreen);
                    using var selectionForm = new SelectionForm(captureType, fullCapture, _viewModel.Settings.CaptureBorderColor);
                    selectionForm.SelectionCompleted += (s, rect) =>
                    {
                        try
                        {
                            if (captureType == CaptureType.WindowSelect
                                && s is SelectionForm sf
                                && sf.SelectedWindowHandle != IntPtr.Zero)
                            {
                                _viewModel.SetPreviewImage(
                                    CaptureManager.CaptureWindow(sf.SelectedWindowHandle, _viewModel.Settings.CaptureMode));
                            }
                            else
                            {
                                _viewModel.SetPreviewImage(CaptureManager.CaptureArea(rect));
                            }
                        }
                        catch (Exception) { }
                    };
                    selectionForm.Cancelled += (s, e) => { };
                    selectionForm.ShowDialog();
                }
                catch (Exception) { }
                finally { _isExecutingCapture = false; EnsureVisible(); }
            });
        });
    }

    private void EnsureVisible()
    {
        if (!Visible) Show();
        if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
        BringToFront();
    }

    // ─── ボタンイベント ─────────────────────────────

    private void BtnSelectScreen_Click(object? sender, EventArgs e) => ExecuteCapture(CaptureType.SelectScreen);
    private void BtnWindowSelect_Click(object? sender, EventArgs e) => ExecuteCapture(CaptureType.WindowSelect);
    private void BtnAreaSelect_Click(object? sender, EventArgs e) => ExecuteCapture(CaptureType.AreaSelect);

    private void BtnAutoCrop_Click(object? sender, EventArgs e)
    {
        if (_viewModel.PreviewImage is null) return;
        try
        {
            var cropped = ImageProcessor.DetectAndCropWindow(_viewModel.PreviewImage);
            _viewModel.SetPreviewImage(cropped);
            ExitCropMode();
        }
        catch (Exception) { }
    }

    private void BtnCropApply_Click(object? sender, EventArgs e)
    {
        if (!_cropSelection.SelectionRect.HasValue) return;
        var rect = _cropSelection.SelectionRect.Value;
        if (rect.Width < 5 || rect.Height < 5) return;
        try
        {
            PushUndo();
            var cropped = ImageProcessor.Crop(_viewModel.PreviewImage!, rect);
            _viewModel.SetPreviewImage(cropped);
            ExitCropMode();
        }
        catch (Exception) { }
    }

    private void ExitCropMode()
    {
        _isCropMode = false;
        _cropSelection.Reset();
        _btnCropMode.Text = "切出し範囲";
        _pnlPreview.Cursor = Cursors.Default;
        _picLoupe.Visible = false;
        _picPreview.Invalidate();
        NotifyMenuStateChanged();
    }

    // ─── Crop モード ────────────────────────────────

    private void BtnCropMode_Click(object? sender, EventArgs e)
    {

        if (_viewModel.PreviewImage is null) { return; }

        _isCropMode = !_isCropMode;
        _btnCropMode.Text = _isCropMode ? "切出し終了" : "切出し範囲";

        if (_isCropMode)
        {
            _cropSelection.Reset();
            _picPreview.Invalidate();
            UpdateLoupePosition();
        }
        else
        {
            _cropSelection.Reset();
            _pnlPreview.Cursor = Cursors.Default;
            _picPreview.Invalidate();
            UpdateLoupePosition();
        }
    }

    private Point ClientToImage(Point clientPoint)
    {
        var img = _viewModel.PreviewImage;
        if (img is null) return Point.Empty;

        var zoom = GetActualZoom();
        var iw = img.Width;
        var ih = img.Height;

        // UpdateLoupePosition と同じオフセット計算（浮動小数点）
        double offsetX = 0, offsetY = 0;
        if (_zoomPercent == 0)
        {
            var pw = _picPreview.ClientSize.Width;
            var ph = _picPreview.ClientSize.Height;
            offsetX = (pw - iw * zoom) / 2.0;
            offsetY = (ph - ih * zoom) / 2.0;
        }

        return new Point(
            Math.Clamp((int)Math.Round((clientPoint.X - offsetX) / zoom), 0, iw - 1),
            Math.Clamp((int)Math.Round((clientPoint.Y - offsetY) / zoom), 0, ih - 1));
    }

    /// <summary>
    /// パネル座標（Preview Panel のクライアント座標）を画像座標に変換する
    /// </summary>
    private Point PanelToImage(Point panelPoint)
    {
        var picPt = _picPreview.PointToClient(_pnlPreview.PointToScreen(panelPoint));
        return ClientToImage(picPt);
    }

    private Point EventToImage(object? sender, Point clientPt)
    {
        if (sender == _pnlPreview)
            return PanelToImage(clientPt);
        return ClientToImage(clientPt);
    }

    /// <summary>
    /// パネルのマウスダウン処理。右クリック時にメニュー状態を更新し、左クリック時に切出し処理へ委譲する。
    /// </summary>
    private void PnlPreview_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            UpdateMenuStates();
            return;
        }
        PicPreview_CropMouseDown(sender, e);
    }

    private void PicPreview_CropMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (_viewModel.PreviewImage is null) { return; }

        // 自動で切出しモード開始
        _isCropMode = true;
        _btnCropMode.Text = "切出し終了";

        _cropMouseClientPos = sender == _pnlPreview
            ? _picPreview.PointToClient(_pnlPreview.PointToScreen(e.Location))
            : e.Location;
        var imgPt = EventToImage(sender, e.Location);
        _cropSelection.MouseDown(imgPt, _viewModel.PreviewImage.Size);
        _picPreview.Refresh();
        _pnlPreview.Invalidate();
    }

    private void PicPreview_CropMouseMove(object? sender, MouseEventArgs e)
    {
        if (_viewModel.PreviewImage is null) return;
        _cropMouseClientPos = sender == _pnlPreview
            ? _picPreview.PointToClient(_pnlPreview.PointToScreen(e.Location))
            : e.Location;
        var imgPt = EventToImage(sender, e.Location);
        _cropSelection.MouseMove(imgPt, _viewModel.PreviewImage.Size);
        _pnlPreview.Invalidate();

        var zoom = GetActualZoom();
        if (_cropSelection.SelectionRect.HasValue)
        {
            var r = _cropSelection.SelectionRect.Value;

            // 描画と同じ PictureBox座標系でハンドル位置とカーソル判定を行う
            var pw = _picPreview.ClientSize.Width;
            var ph = _picPreview.ClientSize.Height;
            double offsetX = 0, offsetY = 0;
            if (_zoomPercent == 0)
            {
                var img = _viewModel.PreviewImage;
                offsetX = (pw - img.Width * zoom) / 2;
                offsetY = (ph - img.Height * zoom) / 2;
            }

            var clientSel = new Rectangle(
                (int)(r.X * zoom + offsetX), (int)(r.Y * zoom + offsetY),
                (int)(r.Width * zoom), (int)(r.Height * zoom));
            var pts = GetHandleClientPoints(r, zoom, (int)offsetX, (int)offsetY);
            // カーソル判定は PictureBox座標（描画と同じ座標系）で行う
            var cursorPt = sender == _pnlPreview
                ? new Point(e.Location.X - _picPreview.Location.X, e.Location.Y - _picPreview.Location.Y)
                : e.Location;
            var cursor = _cropSelection.GetCursorClient(cursorPt, pts, clientSel);
            _pnlPreview.Cursor = cursor ?? Cursors.Cross;
        }
        else
        {
            _pnlPreview.Cursor = Cursors.Cross;
        }

        UpdateLoupePosition();
    }

    private void PicPreview_CropMouseUp(object? sender, MouseEventArgs e)
    {
        _cropMouseClientPos = sender == _pnlPreview
            ? _picPreview.PointToClient(_pnlPreview.PointToScreen(e.Location))
            : e.Location;
        _cropSelection.MouseUp();

        // 選択がキャンセルされたら
        if (!_cropSelection.SelectionRect.HasValue && !_cropSelection.IsDragging)
        {
            ExitCropMode();
        }

        _picPreview.Invalidate();
        _pnlPreview.Invalidate();
        NotifyMenuStateChanged();
    }

    /// <summary>
    /// _picLoupe上のマウスイベントを_pnlPreview座標に変換して転送する
    /// </summary>
    private void PicLoupe_MouseMove(object? sender, MouseEventArgs e)
    {
        var screenPt = _picLoupe.PointToScreen(e.Location);
        var pnlPt = _pnlPreview.PointToClient(screenPt);
        var args = new MouseEventArgs(e.Button, e.Clicks, pnlPt.X, pnlPt.Y, e.Delta);
        PicPreview_CropMouseMove(_pnlPreview, args);
    }

    private void PicLoupe_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            UpdateMenuStates();
            return;
        }
        var screenPt = _picLoupe.PointToScreen(e.Location);
        var pnlPt = _pnlPreview.PointToClient(screenPt);
        var args = new MouseEventArgs(e.Button, 0, pnlPt.X, pnlPt.Y, 0);
        PicPreview_CropMouseDown(_pnlPreview, args);
    }

    private void PicLoupe_MouseUp(object? sender, MouseEventArgs e)
    {
        var screenPt = _picLoupe.PointToScreen(e.Location);
        var pnlPt = _pnlPreview.PointToClient(screenPt);
        var args = new MouseEventArgs(e.Button, 0, pnlPt.X, pnlPt.Y, 0);
        PicPreview_CropMouseUp(_pnlPreview, args);
    }

    private void PicPreview_CropPaint(object? sender, PaintEventArgs e)
    {
        if (!_isCropMode || _viewModel.PreviewImage is null) return;

        var img = _viewModel.PreviewImage;
        var zoom = GetActualZoom();
        var pw = _picPreview.ClientSize.Width;
        var ph = _picPreview.ClientSize.Height;

        double offsetX = 0, offsetY = 0;
        if (_zoomPercent == 0)
        {
            offsetX = (pw - img.Width * zoom) / 2.0;
            offsetY = (ph - img.Height * zoom) / 2.0;
        }

        e.Graphics.SetClip(new Rectangle((int)Math.Round(offsetX), (int)Math.Round(offsetY),
            (int)Math.Round(img.Width * zoom), (int)Math.Round(img.Height * zoom)));

        if (_cropSelection.SelectionRect.HasValue)
        {
            var sel = _cropSelection.SelectionRect.Value;
            var csX = (int)Math.Round(sel.X * zoom + offsetX);
            var csY = (int)Math.Round(sel.Y * zoom + offsetY);
            var csW = (int)Math.Round(sel.Width * zoom);
            var csH = (int)Math.Round(sel.Height * zoom);

            var clipR = e.Graphics.ClipBounds;
            var clipX = (int)clipR.X;
            var clipY = (int)clipR.Y;
            var clipW = (int)clipR.Width;
            var clipH = (int)clipR.Height;

            using var dim = new SolidBrush(Color.FromArgb(100, 0, 0, 0));
            if (csY > clipY) e.Graphics.FillRectangle(dim, clipX, clipY, clipW, csY - clipY);
            if (csY + csH < clipY + clipH) e.Graphics.FillRectangle(dim, clipX, csY + csH, clipW, clipY + clipH - csY - csH);
            if (csX > clipX) e.Graphics.FillRectangle(dim, clipX, csY, csX - clipX, csH);
            if (csX + csW < clipX + clipW) e.Graphics.FillRectangle(dim, csX + csW, csY, clipX + clipW - csX - csW, csH);

            var borderColor = Color.FromName(_viewModel.Settings.CropBorderColor);
            using var pen = new Pen(Color.FromArgb(180, borderColor.R, borderColor.G, borderColor.B), Math.Max(1f, (float)zoom));
            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
            pen.DashPattern = [1f, 3f];
            e.Graphics.DrawRectangle(pen, new Rectangle(csX, csY, csW, csH));

            var hs = 12;
            var pts = GetHandleClientPoints(sel, zoom, (int)offsetX, (int)offsetY);
            foreach (var pt in pts)
            {
                e.Graphics.FillRectangle(Brushes.White, pt.X - hs / 2, pt.Y - hs / 2, hs, hs);
                using var p = new Pen(Color.Black, 1);
                e.Graphics.DrawRectangle(p, pt.X - hs / 2, pt.Y - hs / 2, hs, hs);
            }
        }
        else
        {
            using var dim = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
            e.Graphics.FillRectangle(dim, (int)offsetX, (int)offsetY,
                (int)(img.Width * zoom), (int)(img.Height * zoom));
        }

        e.Graphics.ResetClip();
    }

    private static Point[] GetHandleClientPoints(Rectangle imgRect, double zoom, int ox, int oy)
    {
        var cx = (int)((imgRect.X + imgRect.Width / 2.0) * zoom) + ox;
        var cy = (int)((imgRect.Y + imgRect.Height / 2.0) * zoom) + oy;
        var l = (int)(imgRect.X * zoom) + ox;
        var t = (int)(imgRect.Y * zoom) + oy;
        var r = (int)(imgRect.Right * zoom) + ox;
        var b = (int)(imgRect.Bottom * zoom) + oy;
        return [new(l, t), new(cx, t), new(r, t), new(l, cy), new(r, cy), new(l, b), new(cx, b), new(r, b)];
    }

    // ルーペ位置更新（MouseMove から呼ぶ）
    private void UpdateLoupePosition()
    {
        if (_viewModel.PreviewImage is null)
        {
            _picLoupe.Visible = false;
            return;
        }

        var mode = _viewModel.Settings.LoupeModeValue;
        if (mode == LoupeMode.Hide)
        {
            _picLoupe.Visible = false;
            return;
        }

        // Auto モードでは切出しモード＋選択範囲があるときのみ表示
        if (mode == LoupeMode.Auto && (!_isCropMode || !_cropSelection.SelectionRect.HasValue))
        {
            _picLoupe.Visible = false;
            return;
        }

        var img = _viewModel.PreviewImage;
        var zoom = GetActualZoom();

        var loupeSize = _viewModel.Settings.LoupeSize;
        var loupeZoom = (double)_viewModel.Settings.LoupeZoomLevel;
        var cx = _cropMouseClientPos.X;
        var cy = _cropMouseClientPos.Y;

        // ルーペを Panel 可視領域の右下に固定
        var lx = _pnlPreview.ClientSize.Width - loupeSize - 16;
        var ly = _pnlPreview.ClientSize.Height - loupeSize - 16;
        if (lx < 4) lx = 4;
        if (ly < 4) ly = 4;
        _picLoupe.Location = new Point(lx, ly);

        // マウス位置を PictureBox 座標 → 画像座標に変換（四捨五入でピクセル中心に合わせる）
        var pw = _picPreview.ClientSize.Width;
        var ph = _picPreview.ClientSize.Height;
        double offsetX = 0, offsetY = 0;
        if (_zoomPercent == 0)
        {
            offsetX = (pw - img.Width * zoom) / 2.0;
            offsetY = (ph - img.Height * zoom) / 2.0;
        }

        var imgCx = (int)Math.Round((cx - offsetX) / zoom);
        var imgCy = (int)Math.Round((cy - offsetY) / zoom);

        // マウスが画像の外にあるときはルーペを非表示
        if (imgCx < 0 || imgCy < 0 || imgCx >= img.Width || imgCy >= img.Height)
        {
            _picLoupe.Visible = false;
            return;
        }

        // マウスカーソル下の領域を拡大表示
        GenerateLoupeImage(img, loupeSize, loupeZoom, imgCx, imgCy);

        _picLoupe.Visible = true;
        _pnlPreview.Controls.SetChildIndex(_picLoupe, 0);
        _picLoupe.Refresh();
    }

    /// <summary>
    /// ルーペの画像を生成して _picLoupe に設定する。
    /// </summary>
    /// <remarks>
    /// ルーペ中心にマウスカーソル下の画像ピクセルが来るよう、
    /// 画像端では切り詰めたソース領域をルーペ上の正しい位置に描画する。<br/>
    /// 画像領域外は拡大率の半分のセルサイズの市松模様（薄いグレー／濃いグレー）で表示される。<br/>
    /// 各画像ピクセルは <c>blockSize = (int)loupeZoom</c> の正方形ブロックで描画される。<br/>
    /// 中心ピクセルのブロック中心がルーペ中心と一致するため、十字線とのずれが生じない。<br/>
    /// </remarks>
    private void GenerateLoupeImage(Image img, int loupeSize, double loupeZoom, int imgCx, int imgCy)
    {
        var blockSize = (int)loupeZoom; // 8

        // 中心ピクセルのブロックがルーペの中央に来る開始座標
        var centerStartX = (loupeSize - blockSize) / 2; // = 81
        var centerStartY = (loupeSize - blockSize) / 2;

        // 中心から左右・上下に何ピクセル分表示できるか
        var maxPixels = (loupeSize / blockSize - 1) / 2; // = 10

        var oldImage = _picLoupe.Image;
        var bmp = new Bitmap(loupeSize, loupeSize);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

        // 全面を市松模様で初期化（セルサイズ = 拡大率の半分、キャッシュ済み TextureBrush）
        var cellSize = Math.Max(1, blockSize / 2);
        using var tb = new TextureBrush(GetOrCreateCheckerPattern(cellSize), System.Drawing.Drawing2D.WrapMode.Tile);
        g.FillRectangle(tb, 0, 0, loupeSize, loupeSize);

        // 中心ピクセルから上下左右にブロックを敷き詰める
        // パフォーマンスのため、GetPixel は 1 回、FillRectangle で高速描画
        var bmpData = (Bitmap)img;
        for (var dy = -maxPixels; dy <= maxPixels; dy++)
        {
            var srcY = imgCy + dy;
            if (srcY < 0 || srcY >= img.Height) continue;

            for (var dx = -maxPixels; dx <= maxPixels; dx++)
            {
                var srcX = imgCx + dx;
                if (srcX < 0 || srcX >= img.Width) continue;

                var c = bmpData.GetPixel(srcX, srcY);
                var destX = centerStartX + dx * blockSize;
                var destY = centerStartY + dy * blockSize;

                using var brush = new SolidBrush(c);
                g.FillRectangle(brush, destX, destY, blockSize, blockSize);
            }
        }

        // 選択範囲の表示（ルーペ内）
        if (_cropSelection.SelectionRect.HasValue)
        {
            var sel = _cropSelection.SelectionRect.Value;
            var bL = centerStartX + (sel.Left - imgCx) * blockSize;
            var bT = centerStartY + (sel.Top - imgCy) * blockSize;
            var bR = centerStartX + (sel.Right - imgCx) * blockSize;
            var bB = centerStartY + (sel.Bottom - imgCy) * blockSize;
            using var bp = new Pen(Color.FromArgb(180, Color.FromName(_viewModel.Settings.LoupeFrameColor)), 1f)
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Custom,
                DashPattern = [1f, 3f]
            };
            g.DrawRectangle(bp, bL, bT, bR - bL, bB - bT);
        }

        // 十字線（ルーペ中心を通る = 中心ピクセルのブロック中心と一致）
        var crossColor = Color.FromName(_viewModel.Settings.LoupeCrossColor);
        using var cp = new Pen(Color.FromArgb(120, crossColor.R, crossColor.G, crossColor.B), blockSize);
        g.DrawLine(cp, loupeSize / 2, 0, loupeSize / 2, loupeSize);
        g.DrawLine(cp, 0, loupeSize / 2, loupeSize, loupeSize / 2);

        // 外枠
        var outerFrameColor = Color.FromName(_viewModel.Settings.LoupeFrameColor);
        var outerFrameWidth = _viewModel.Settings.LoupeFrameWidth;
        using var fp = new Pen(outerFrameColor, outerFrameWidth);
        g.DrawRectangle(fp, 0, 0, loupeSize - 1, loupeSize - 1);

        _picLoupe.Image = bmp;
        oldImage?.Dispose();
    }

    private double GetPreviewZoom() => GetActualZoom();

    private int _lastCheckerCellSize;
    private Bitmap? _checkerPattern;
    private TextureBrush? _checkerBrush;

    /// <summary>指定されたセルサイズの市松模様ビットマップを取得する（キャッシュ済みの場合は再利用）。</summary>
    private Bitmap GetOrCreateCheckerPattern(int cellSize)
    {
        if (_checkerPattern is not null && cellSize == _lastCheckerCellSize)
            return _checkerPattern;

        _checkerPattern?.Dispose();
        _checkerBrush?.Dispose();
        _checkerBrush = null;
        _checkerPattern = CreateCheckerPattern(cellSize);
        _lastCheckerCellSize = cellSize;
        return _checkerPattern;
    }

    /// <summary>指定されたセルサイズの市松模様テクスチャブラシを取得する（キャッシュ済みの場合は再利用）。</summary>
    private TextureBrush GetOrCreateCheckerBrush(int cellSize)
    {
        if (_checkerBrush is not null && cellSize == _lastCheckerCellSize)
            return _checkerBrush;

        _checkerBrush?.Dispose();
        _checkerPattern?.Dispose();
        _checkerPattern = CreateCheckerPattern(cellSize);
        _checkerBrush = new TextureBrush(_checkerPattern, System.Drawing.Drawing2D.WrapMode.Tile);
        _lastCheckerCellSize = cellSize;
        return _checkerBrush;
    }

    /// <summary>プレビューパネルの背景に市松模様を描画する（画像外の領域であることを明示）。</summary>
    private void PnlPreview_Paint(object? sender, PaintEventArgs e)
    {
        var cellSize = Math.Max(1, _viewModel.Settings.LoupeZoomLevel / 2);
        var brush = GetOrCreateCheckerBrush(cellSize);
        e.Graphics.FillRectangle(brush, e.ClipRectangle);
    }

    /// <summary>2×2 セル分の市松模様ビットマップを作成する。</summary>
    private static Bitmap CreateCheckerPattern(int cellSize)
    {
        var bmp = new Bitmap(cellSize * 2, cellSize * 2);
        using var g = Graphics.FromImage(bmp);
        var light = Color.FromArgb(200, 200, 200);
        var dark = Color.FromArgb(160, 160, 160);
        using var lightBrush = new SolidBrush(light);
        using var darkBrush = new SolidBrush(dark);
        g.FillRectangle(lightBrush, 0, 0, cellSize, cellSize);
        g.FillRectangle(darkBrush, cellSize, 0, cellSize, cellSize);
        g.FillRectangle(darkBrush, 0, cellSize, cellSize, cellSize);
        g.FillRectangle(lightBrush, cellSize, cellSize, cellSize, cellSize);
        return bmp;
    }

    // ─── クリップボード ─────────────────────────────

    private void BtnCopy_Click(object? sender, EventArgs e)
    {
        _viewModel.CopyToClipboard();
    }

    // ─── 保存 ──────────────────────────────────────

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        if (_viewModel.SaveImage())
        {
            UpdateStatusBar();
            ExitCropMode();
        }
    }

    // ─── 保存先フォルダ ────────────────────────────

    private void BtnSaveFolder_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        dialog.Description = "保存先フォルダを選択してください";
        dialog.SelectedPath = _viewModel.SaveFolderPath;
        if (dialog.ShowDialog() == DialogResult.OK)
            _viewModel.SaveFolderPath = dialog.SelectedPath;
    }



    private void TxtFileNameTemplate_TextChanged(object? sender, EventArgs e)
    {
        _viewModel.FileNameTemplateText = _txtFileNameTemplate.Text;
    }

    // ─── アンドゥ ──────────────────────────────────

    /// <summary>
    /// 現在のプレビュー画像をアンドゥスタックに退避する。
    /// </summary>
    private void PushUndo()
    {
        if (_viewModel.PreviewImage is null) return;
        if (_isUndoing) return;

        var copy = new Bitmap(_viewModel.PreviewImage);
        _undoStack.Add(copy);
        if (_undoStack.Count > MaxUndoCount)
        {
            _undoStack[0].Dispose();
            _undoStack.RemoveAt(0);
        }
        NotifyMenuStateChanged();
    }

    // ─── メニューイベント

    private void MenuFileSave_Click(object? sender, EventArgs e)
    {
        BtnSave_Click(sender, e);
    }

    private void MenuFileSaveFolder_Click(object? sender, EventArgs e)
    {
        BtnSaveFolder_Click(sender, e);
    }

    private void MenuEditUndo_Click(object? sender, EventArgs e)
    {
        if (_undoStack.Count == 0 || _viewModel.PreviewImage is null) return;

        _isUndoing = true;
        var prev = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        _viewModel.SetPreviewImage(prev);
        _picPreview.Image?.Dispose();
        _picPreview.Image = new Bitmap(prev);
        UpdatePictureBoxZoom();
        _picPreview.Invalidate();

        _isUndoing = false;
        NotifyMenuStateChanged();
    }

    private void MenuEditZoomIn_Click(object? sender, EventArgs e)
    {
        ZoomIn();
    }

    private void MenuEditZoomOut_Click(object? sender, EventArgs e)
    {
        ZoomOut();
    }

    private void MenuEditCrop_Click(object? sender, EventArgs e)
    {
        BtnCropApply_Click(sender, e);
    }

    private void MenuEditAutoCrop_Click(object? sender, EventArgs e)
    {
        BtnAutoCrop_Click(sender, e);
    }

    private void MenuEditCopy_Click(object? sender, EventArgs e)
    {
        _viewModel.CopyToClipboard();
    }

    private void MenuEditPaste_Click(object? sender, EventArgs e)
    {
        PushUndo();
        if (_viewModel.PasteFromClipboard())
        {
            NotifyMenuStateChanged();
        }
    }

    private void CtxLinkFolderView_Click(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_viewModel.SaveFolderPath))
        {
            var dialog = new FolderViewForm(_viewModel.SaveFolderPath, _settingsService);
            dialog.Show(this);
        }
    }

    /// <summary>
    /// エクスプローラでフォルダを開く（リンク右クリックメニューから）
    /// </summary>
    private void CtxLinkOpenExplorer_Click(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_viewModel.SaveFolderPath) && Directory.Exists(_viewModel.SaveFolderPath))
        {
            Process.Start("explorer.exe", _viewModel.SaveFolderPath);
        }
    }

    private void MenuFolderView_Click(object? sender, EventArgs e)
    {
        CtxLinkFolderView_Click(sender, e);
    }

    private void MenuOpenExplorer_Click(object? sender, EventArgs e)
    {
        CtxLinkOpenExplorer_Click(sender, e);
    }

    /// <summary>
    /// 保存先フォルダのフルパスをクリップボードにコピーする。
    /// </summary>
    private void MenuFileCopyFolderPath_Click(object? sender, EventArgs e)
    {
        var path = _viewModel.SaveFolderPath;
        if (string.IsNullOrEmpty(path))
            return;
        Clipboard.SetText(path);
    }

    private void MenuEditAlignLeft_Click(object? sender, EventArgs e)
    {
        _viewModel.Settings.CenterAlign = false;
        _settingsService.Save();
        UpdatePictureBoxZoom();
    }

    private void MenuEditAlignCenter_Click(object? sender, EventArgs e)
    {
        _viewModel.Settings.CenterAlign = true;
        _settingsService.Save();
        UpdatePictureBoxZoom();
    }

    private void MenuEditShowLoupe_Click(object? sender, EventArgs e)
    {
        _viewModel.Settings.LoupeModeValue = LoupeMode.Show;
        _settingsService.Save();
    }

    private void MenuViewLoupeHide_Click(object? sender, EventArgs e)
    {
        _viewModel.Settings.LoupeModeValue = LoupeMode.Hide;
        _settingsService.Save();
    }

    private void MenuViewLoupeAuto_Click(object? sender, EventArgs e)
    {
        _viewModel.Settings.LoupeModeValue = LoupeMode.Auto;
        _settingsService.Save();
    }

    /// <summary>
    /// 設定の LoupeMode に基づいてルーペの表示状態を更新する
    /// </summary>
    private void ApplyLoupeModeFromSetting()
    {
        var mode = _viewModel.Settings.LoupeModeValue;
        var hasImage = _viewModel.PreviewImage is not null;
        var show = mode switch
        {
            LoupeMode.Show => hasImage,
            LoupeMode.Auto => hasImage && _isCropMode && _cropSelection.SelectionRect.HasValue,
            _ => false,
        };

        _picLoupe.Visible = show;
        if (show) UpdateLoupePosition();
        UpdateMenuStates();
    }

    /// <summary>設定の LoupeSize / LoupeZoomLevel 変更をルーペに反映する。</summary>
    private void ApplyLoupeSettings()
    {
        var size = _viewModel.Settings.LoupeSize;
        _picLoupe.Size = new Size(size, size);
        if (_picLoupe.Visible) UpdateLoupePosition();
    }

    /// <summary>メニュー状態が変更されたことを通知し、次回メニュー表示時に更新する。</summary>
    private void NotifyMenuStateChanged()
    {
        _menuStateDirty = true;
    }

    /// <summary>
    /// メニュー項目の有効/無効状態を現在の状態に合わせて更新する
    /// </summary>
    private void UpdateMenuStates()
    {
        _menuStateDirty = false;
        var hasImage = _viewModel.PreviewImage is not null;
        var hasSelection = _cropSelection.SelectionRect.HasValue;
        var hasUndo = _undoStack.Count > 0;

        // ViewModel に状態を反映
        _viewModel.HasSelection = hasSelection;

        // 実際の拡大率（自動フィット時の倍率も含む）から拡大/縮小の可否を判定
        var canZoomOut = false;
        var canZoomIn = false;
        if (hasImage)
        {
            var currentPercent = GetActualZoomPercent();
            for (var i = 0; i < s_zoomValues.Length; i++)
            {
                if (s_zoomValues[i] > currentPercent) canZoomIn = true;
                if (s_zoomValues[i] != 0 && s_zoomValues[i] < currentPercent) canZoomOut = true;
            }
        }
        _viewModel.CanZoomIn = canZoomIn;
        _viewModel.CanZoomOut = canZoomOut;

        _menuFileSave.Enabled = hasImage;
        _menuEditUndo.Enabled = hasUndo;
        _menuEditCrop.Enabled = _viewModel.HasSelection;
        _menuEditAutoCrop.Enabled = hasImage && !_viewModel.HasSelection;
        _menuEditCopy.Enabled = hasImage;
        _menuEditPaste.Enabled = Clipboard.ContainsImage();
        _menuEditZoomIn.Enabled = _viewModel.CanZoomIn;
        _menuEditZoomOut.Enabled = _viewModel.CanZoomOut;
        _menuViewZoom.Enabled = hasImage;
        // 拡大率サブメニューのチェック状態を更新
        for (var i = 0; i < s_zoomValues.Length && i < _menuViewZoom.DropDownItems.Count; i++)
        {
            if (_menuViewZoom.DropDownItems[i] is ToolStripMenuItem menuItem)
                menuItem.Checked = hasImage && _viewModel.ZoomPercent == s_zoomValues[i];
        }
        _menuEditAlignLeft.Enabled = _viewModel.Settings.CenterAlign;
        _menuEditAlignLeft.Checked = !_viewModel.Settings.CenterAlign;
        _menuEditAlignCenter.Enabled = !_viewModel.Settings.CenterAlign;
        _menuEditAlignCenter.Checked = _viewModel.Settings.CenterAlign;
        _menuEditShowLoupe.Enabled = _viewModel.Settings.LoupeModeValue != LoupeMode.Show;
        _menuEditShowLoupe.Checked = _viewModel.Settings.LoupeModeValue == LoupeMode.Show;
        _menuViewLoupeHide.Enabled = _viewModel.Settings.LoupeModeValue != LoupeMode.Hide;
        _menuViewLoupeHide.Checked = _viewModel.Settings.LoupeModeValue == LoupeMode.Hide;
        _menuViewLoupeAuto.Enabled = _viewModel.Settings.LoupeModeValue != LoupeMode.Auto;
        _menuViewLoupeAuto.Checked = _viewModel.Settings.LoupeModeValue == LoupeMode.Auto;

        // コンテキストメニュー（プレビュー）
        _ctxCrop.Enabled = hasImage && hasSelection;
        _ctxAutoCrop.Enabled = hasImage && !hasSelection;
        _ctxCopy.Enabled = hasImage;
        _ctxPaste.Enabled = Clipboard.ContainsImage();
        _ctxZoomIn.Enabled = canZoomIn;
        _ctxZoomOut.Enabled = canZoomOut;
        _ctxPZoomIn.Enabled = canZoomIn;
        _ctxPZoomOut.Enabled = canZoomOut;
        _ctxPZoomAuto.Checked = _zoomIndex == 0;
        _ctxAlignLeft.Enabled = _viewModel.Settings.CenterAlign;
        _ctxAlignLeft.Checked = !_viewModel.Settings.CenterAlign;
        _ctxAlignCenter.Enabled = !_viewModel.Settings.CenterAlign;
        _ctxAlignCenter.Checked = _viewModel.Settings.CenterAlign;
        _ctxShowLoupe.Enabled = _viewModel.Settings.LoupeModeValue != LoupeMode.Show;
        _ctxShowLoupe.Checked = _viewModel.Settings.LoupeModeValue == LoupeMode.Show;
        _ctxLoupeHide.Enabled = _viewModel.Settings.LoupeModeValue != LoupeMode.Hide;
        _ctxLoupeHide.Checked = _viewModel.Settings.LoupeModeValue == LoupeMode.Hide;
        _ctxLoupeAuto.Enabled = _viewModel.Settings.LoupeModeValue != LoupeMode.Auto;
        _ctxLoupeAuto.Checked = _viewModel.Settings.LoupeModeValue == LoupeMode.Auto;
        _ctxZoomAuto.Checked = _zoomIndex == 0;
        _ctxZoom25.Checked = _zoomIndex == 1;
        _ctxZoom33.Checked = _zoomIndex == 2;
        _ctxZoom50.Checked = _zoomIndex == 3;
        _ctxZoom67.Checked = _zoomIndex == 4;
        _ctxZoom75.Checked = _zoomIndex == 5;
        _ctxZoom80.Checked = _zoomIndex == 6;
        _ctxZoom90.Checked = _zoomIndex == 7;
        _ctxZoom100.Checked = _zoomIndex == 8;
        _ctxZoom110.Checked = _zoomIndex == 9;
        _ctxZoom125.Checked = _zoomIndex == 10;
        _ctxZoom150.Checked = _zoomIndex == 11;
        _ctxZoom175.Checked = _zoomIndex == 12;
        _ctxZoom200.Checked = _zoomIndex == 13;
        _ctxZoom250.Checked = _zoomIndex == 14;
        _ctxZoom300.Checked = _zoomIndex == 15;
        _ctxZoom400.Checked = _zoomIndex == 16;
        _ctxZoom500.Checked = _zoomIndex == 17;
    }

    // ─── ステータスバーコンテキストメニュー ─────────────────────

    private void LblZoom_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_viewModel.PreviewImage is null)
            return;
        UpdateMenuStates();
        _contextMenuZoom.Show(_statusStrip, new Point(_lblZoom.Bounds.Left, -_statusStrip.Height));
    }

    private void LblAlign_MouseDown(object? sender, MouseEventArgs e)
    {
        _contextMenuAlign.Show(_statusStrip, new Point(_lblAlign.Bounds.Left, -_statusStrip.Height));
    }

    private void LblLoupe_MouseDown(object? sender, MouseEventArgs e)
    {
        _contextMenuLoupe.Show(_statusStrip, new Point(_lblLoupe.Bounds.Left, -_statusStrip.Height));
    }

    /// <summary>
    /// ステータスバーのフォルダパスリンクのマウスダウン処理。
    /// 左クリックでフォルダビューを表示し、右クリックでコンテキストメニューを表示する。
    /// </summary>
    private void LblFolderLink_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            _contextMenuLink.Show(_statusStrip, new Point(_lblFolderLink.Bounds.Left, -_statusStrip.Height));
            return;
        }

        if (e.Button == MouseButtons.Left && !string.IsNullOrEmpty(_viewModel.SaveFolderPath))
        {
            var dialog = new FolderViewForm(_viewModel.SaveFolderPath, _settingsService);
            dialog.Show(this);
        }
    }

    private void SetZoomFromContext(int percent)
    {
        for (var i = 0; i < s_zoomValues.Length; i++)
        {
            if (s_zoomValues[i] == percent)
            {
                _zoomIndex = i;
                _zoomPercent = percent;
                _viewModel.ZoomPercent = percent;
                UpdatePictureBoxZoom();
                UpdateStatusBar();
                NotifyMenuStateChanged();
                return;
            }
        }
    }

    private void CtxZoomAuto_Click(object? sender, EventArgs e)
    {
        _zoomIndex = 0;
        _zoomPercent = 0;
        _viewModel.ZoomPercent = 0;
        UpdatePictureBoxZoom();
        UpdateStatusBar();
        NotifyMenuStateChanged();
    }

    private void CtxZoom25_Click(object? sender, EventArgs e) { SetZoomFromContext(25); }
    private void CtxZoom33_Click(object? sender, EventArgs e) { SetZoomFromContext(33); }
    private void CtxZoom50_Click(object? sender, EventArgs e) { SetZoomFromContext(50); }
    private void CtxZoom67_Click(object? sender, EventArgs e) { SetZoomFromContext(67); }
    private void CtxZoom75_Click(object? sender, EventArgs e) { SetZoomFromContext(75); }
    private void CtxZoom80_Click(object? sender, EventArgs e) { SetZoomFromContext(80); }
    private void CtxZoom90_Click(object? sender, EventArgs e) { SetZoomFromContext(90); }
    private void CtxZoom100_Click(object? sender, EventArgs e) { SetZoomFromContext(100); }
    private void CtxZoom110_Click(object? sender, EventArgs e) { SetZoomFromContext(110); }
    private void CtxZoom125_Click(object? sender, EventArgs e) { SetZoomFromContext(125); }
    private void CtxZoom150_Click(object? sender, EventArgs e) { SetZoomFromContext(150); }
    private void CtxZoom175_Click(object? sender, EventArgs e) { SetZoomFromContext(175); }
    private void CtxZoom200_Click(object? sender, EventArgs e) { SetZoomFromContext(200); }
    private void CtxZoom250_Click(object? sender, EventArgs e) { SetZoomFromContext(250); }
    private void CtxZoom300_Click(object? sender, EventArgs e) { SetZoomFromContext(300); }
    private void CtxZoom400_Click(object? sender, EventArgs e) { SetZoomFromContext(400); }
    private void CtxZoom500_Click(object? sender, EventArgs e) { SetZoomFromContext(500); }

    private void MenuHotKeySettings_Click(object? sender, EventArgs e)
    {
        using var dialog = new HotkeyForm(_viewModel.Settings, () => _settingsService.Save());
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _hotKeyManager?.RegisterAll(_viewModel.Settings);
    }

    private void MenuSaveFolder_Click(object? sender, EventArgs e)
    {
        BtnSaveFolder_Click(sender, e);
    }

    private void MenuDisplaySettings_Click(object? sender, EventArgs e)
    {
        using var dialog = new SettingsForm(_viewModel.Settings);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _settingsService.Save();
            UpdatePictureBoxZoom();
            _picPreview.Invalidate();
            _pnlPreview.Invalidate();
        }
    }

    /// <summary>
    /// +1 してズームイン。マウスホイールと同じ動作。
    /// </summary>
    private void ZoomIn() => StepZoom(1);

    /// <summary>
    /// -1 してズームアウト。マウスホイールと同じ動作。
    /// </summary>
    private void ZoomOut() => StepZoom(-1);

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.C))
        {
            _viewModel.CopyToClipboard();
            return true;
        }
        if (keyData == (Keys.Control | Keys.V))
        {
            if (_viewModel.PasteFromClipboard())
                return true;
        }
        // Ctrl+Plus / Ctrl+Minus でズーム
        // + は Shift+Oemplus、- は OemMinus
        if (keyData == (Keys.Control | Keys.Shift | Keys.Oemplus) || keyData == (Keys.Control | Keys.Add))
        {
            ZoomIn();
            return true;
        }
        if (keyData == (Keys.Control | Keys.OemMinus) || keyData == (Keys.Control | Keys.Subtract))
        {
            ZoomOut();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_HOTKEY = 0x0312;
        if (m.Msg == WM_HOTKEY)
        {
            var captureType = _hotKeyManager?.ProcessHotKeyMessage(m);
            if (captureType.HasValue)
            {
                BeginInvoke(() => ExecuteCapture(captureType.Value));
                return;
            }
        }
        base.WndProc(ref m);
    }

    private void RestoreFormBounds()
    {
        var bounds = _viewModel.Settings.MainFormBounds;
        if (bounds.HasValue && bounds.Value.Width > 0 && bounds.Value.Height > 0)
        {
            StartPosition = FormStartPosition.Manual;
            DesktopBounds = bounds.Value;
        }
        var state = _viewModel.Settings.MainFormWindowState;
        if (state != FormWindowState.Minimized)
            WindowState = state;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (WindowState == FormWindowState.Normal)
            _viewModel.Settings.MainFormBounds = DesktopBounds;
        else
            _viewModel.Settings.MainFormBounds = RestoreBounds;

        _viewModel.Settings.MainFormWindowState = WindowState == FormWindowState.Minimized ? FormWindowState.Normal : WindowState;
        _settingsService.Save();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotKeyManager?.Dispose();
            _checkerPattern?.Dispose();
            _checkerBrush?.Dispose();
        }
        base.Dispose(disposing);
    }
}
