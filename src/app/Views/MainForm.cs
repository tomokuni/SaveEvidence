using app.Models;
using app.ViewModels;

namespace app.Views;

/// <summary>
/// メインフォーム。画面キャプチャとプレビュー表示を行うメインウィンドウ。
/// </summary>
public partial class MainForm : Form
{
    private readonly MainViewModel _viewModel;
    private readonly HotKeyManager _hotKeyManager;
    private bool _isExecutingCapture;
    private bool _isCropMode;
    private readonly CropSelection _cropSelection = new();
    private Point _cropMouseClientPos = new(-1, -1);
    private bool _isCropHandleActive;

    // 拡大率管理（0 = 自動）
    private int _zoomPercent;
    private static readonly int[] s_zoomValues = [0, 25, 33, 50, 67, 75, 80, 90, 100, 110, 125, 150, 175, 200, 250, 300, 400, 500, 670, 800, 1000];
    private int _zoomIndex;

    // スクロール管理
    private Point _picBaseLocation;
    private int _scrollX, _scrollY;

    public MainForm()
    {
        _viewModel = new MainViewModel();

        if (System.ComponentModel.LicenseManager.UsageMode != System.ComponentModel.LicenseUsageMode.Designtime)
        {
            _hotKeyManager = new HotKeyManager(Handle);
        }

        InitializeComponent();
        InitializeZoom();
        InitializeViewModel();
        RegisterHotKeys();
        RestoreFormBounds();

        // 常時マウスイベント購読（切出しモードON/OFFに関わらずドラッグ可能）
        _picPreview.MouseDown += PicPreview_CropMouseDown;
        _picPreview.MouseMove += PicPreview_CropMouseMove;
        _picPreview.MouseUp += PicPreview_CropMouseUp;
        _pnlPreview.MouseDown += PicPreview_CropMouseDown;
        _pnlPreview.MouseMove += PicPreview_CropMouseMove;
        _pnlPreview.MouseUp += PicPreview_CropMouseUp;
        _picLoupe.SizeMode = PictureBoxSizeMode.StretchImage;
        _picLoupe.Enabled = true;
        _picLoupe.TabStop = false;
        _picLoupe.MouseMove += PicLoupe_MouseMove;
        _picLoupe.MouseDown += PicLoupe_MouseDown;
        _picLoupe.MouseUp += PicLoupe_MouseUp;
        _picPreview.Paint += PicPreview_CropPaint;
        _picPreview.MouseWheel += PicPreview_MouseWheel;
        _pnlPreview.MouseWheel += PicPreview_MouseWheel;
        _pnlPreview.Resize += (_, _) => UpdateScrollBars();
        Resize += (_, _) => { };
        UpdateStatusBar();
        Program.LogDebug("MainForm.ctor completed - crop events always subscribed");
    }

    private void InitializeZoom()
    {
        _cmbZoom.Items.Add("自動");
        foreach (var v in s_zoomValues[1..])
            _cmbZoom.Items.Add($"{v}%");
        _cmbZoom.SelectedIndex = 0;
        _zoomIndex = 0;
        _zoomPercent = 0;
    }

    private void ResetZoomToAuto()
    {
        _cmbZoom.SelectedIndex = 0;
        _zoomIndex = 0;
        _zoomPercent = 0;
        UpdatePictureBoxZoom();
    }

    private void CmbZoom_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _zoomIndex = _cmbZoom.SelectedIndex;
        _zoomPercent = s_zoomValues[_zoomIndex];
        UpdatePictureBoxZoom();
        UpdateStatusBar();
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
        var imgW = (int)(img.Width * zoom);
        var imgH = (int)(img.Height * zoom);
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
                Math.Max(0, (panelW - imgW) / 2),
                Math.Max(0, (panelH - imgH) / 2));
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

        // マウス座標を初期化して拡大鏡を表示
        _cropMouseClientPos = new Point(_picPreview.ClientSize.Width / 2, _picPreview.ClientSize.Height / 2);
        UpdateLoupePosition();
    }

    private void PicPreview_Paint(object? sender, PaintEventArgs e)
    {
        // 画像のPaintは何もしない
    }

    private void UpdateStatusBar()
    {
        var status = _viewModel.PreviewImage is not null
            ? $" {GetActualZoomPercent()}%"
            : "";
        var extra = _viewModel.StatusText;
        if (!string.IsNullOrEmpty(extra))
            status += $" | {extra}";
        _lblStatus.Text = status;
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
            _cmbZoom.SelectedIndex = _zoomIndex;
            UpdatePictureBoxZoom();
            UpdateStatusBar();
        }
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
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.PreviewImage) && _viewModel.PreviewImage is not null)
            {
                ExitCropMode();
                UpdatePreviewImage(_viewModel.PreviewImage);
                ResetZoomToAuto();
                UpdateStatusBar();
            }

            if (e.PropertyName == nameof(MainViewModel.SaveFolderDisplayName))
            {
                _linkSaveFolder.Text = _viewModel.SaveFolderDisplayName;
                _toolTip.SetToolTip(_btnSaveFolder, _viewModel.SaveFolderPath);
                _toolTip.SetToolTip(_linkSaveFolder, _viewModel.SaveFolderPath);
            }

            if (e.PropertyName == nameof(MainViewModel.CurrentFileNamePreview))
                UpdateStatusBar();

            if (e.PropertyName == nameof(MainViewModel.FileNameTemplateText))
                _txtFileNameTemplate.Text = _viewModel.FileNameTemplateText;

            if (e.PropertyName == nameof(MainViewModel.IsSaved))
            {
                _picPreview.Invalidate();
                UpdateStatusBar();
            }

            if (e.PropertyName is nameof(MainViewModel.CanCopy) or nameof(MainViewModel.CanSave) or nameof(MainViewModel.HasPreviewImage) or nameof(MainViewModel.IsSaved))
            {
                _btnCopy.Enabled = _viewModel.CanCopy;
                _btnSave.Enabled = _viewModel.CanSave;
                UpdateStatusBar();
            }
        };

        _viewModel.StartSelectionMode = StartSelectionMode;

        _linkSaveFolder.Text = _viewModel.SaveFolderDisplayName;
        _toolTip.SetToolTip(_btnSaveFolder, _viewModel.SaveFolderPath);
        _toolTip.SetToolTip(_linkSaveFolder, _viewModel.SaveFolderPath);
        _txtFileNameTemplate.Text = _viewModel.FileNameTemplateText;
        _btnCopy.Enabled = _viewModel.CanCopy;
        _btnSave.Enabled = _viewModel.CanSave;
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
                    selectionForm.SelectionCompleted += (s, rect) => { try { _viewModel.CaptureScreenArea(rect); } catch (Exception ex) { Program.LogException(ex); } };
                    selectionForm.Cancelled += (s, e) => { };
                    selectionForm.ShowDialog();
                }
                catch (Exception ex) { Program.LogException(ex); }
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
                        try { _viewModel.SetPreviewImage(CaptureManager.CaptureArea(rect)); }
                        catch (Exception ex) { Program.LogException(ex); }
                    };
                    selectionForm.Cancelled += (s, e) => { };
                    selectionForm.ShowDialog();
                }
                catch (Exception ex) { Program.LogException(ex); }
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
        catch (Exception ex) { Program.LogException(ex); }
    }

    private void BtnCropApply_Click(object? sender, EventArgs e)
    {
        Program.LogDebug($"BtnCropApply_Click: hasSel={_cropSelection.SelectionRect.HasValue}");
        if (!_cropSelection.SelectionRect.HasValue) return;
        var rect = _cropSelection.SelectionRect.Value;
        Program.LogDebug($"  rect={rect} w={rect.Width} h={rect.Height}");
        if (rect.Width < 5 || rect.Height < 5) return;
        try
        {
            var cropped = ImageProcessor.Crop(_viewModel.PreviewImage!, rect);
            Program.LogDebug($"  cropped={cropped.Width}x{cropped.Height}");
            _viewModel.SetPreviewImage(cropped);
            Program.LogDebug("  SetPreviewImage done");
            ExitCropMode();
            Program.LogDebug("  ExitCropMode done");
        }
        catch (Exception ex) { Program.LogException(ex); }
    }

    private void ExitCropMode()
    {
        _isCropMode = false;
        _cropSelection.Reset();
        _btnCropMode.Text = "切出し範囲";
        _pnlPreview.Cursor = Cursors.Default;
        _picLoupe.Visible = false;
        _picPreview.Invalidate();
    }

    // ─── Crop モード ────────────────────────────────

    private void BtnCropMode_Click(object? sender, EventArgs e)
    {
        Program.LogDebug($"BtnCropMode_Click: _isCropMode={_isCropMode} hasImage={_viewModel.PreviewImage is not null}");

        if (_viewModel.PreviewImage is null) { Program.LogDebug("  → PreviewImage is null, return"); return; }

        _isCropMode = !_isCropMode;
        _btnCropMode.Text = _isCropMode ? "切出し終了" : "切出し範囲";
        Program.LogDebug($"  → _isCropMode={_isCropMode}");

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
        Program.LogDebug($"  → 切出しモード: {(_isCropMode ? "ON" : "OFF")}");
    }

    private Point ClientToImage(Point clientPoint)
    {
        var img = _viewModel.PreviewImage;
        if (img is null) return Point.Empty;

        var zoom = GetActualZoom();
        var iw = img.Width;
        var ih = img.Height;

        if (_zoomPercent > 0)
        {
            // 手動ズーム：PictureBox の左上からの相対座標を画像座標に変換
            return new Point(
                Math.Clamp((int)(clientPoint.X / zoom), 0, iw - 1),
                Math.Clamp((int)(clientPoint.Y / zoom), 0, ih - 1));
        }
        else
        {
            // 自動ズーム：中央寄せを考慮
            var cw = _picPreview.ClientSize.Width;
            var ch = _picPreview.ClientSize.Height;
            var offsetX = (cw - (int)(iw * zoom)) / 2;
            var offsetY = (ch - (int)(ih * zoom)) / 2;
            return new Point(
                Math.Clamp((int)((clientPoint.X - offsetX) / zoom), 0, iw - 1),
                Math.Clamp((int)((clientPoint.Y - offsetY) / zoom), 0, ih - 1));
        }
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

    private void PicPreview_CropMouseDown(object? sender, MouseEventArgs e)
    {
        Program.LogDebug($"MouseDown: sender={(sender == _pnlPreview ? "Panel" : sender == _picPreview ? "PicBox" : "other")} loc=({e.Location.X},{e.Location.Y}) btn={e.Button}");
        if (e.Button != MouseButtons.Left) return;
        if (_viewModel.PreviewImage is null) { Program.LogDebug("  → PreviewImage is null, skip"); return; }

        // 自動で切出しモード開始
        _isCropMode = true;
        _btnCropMode.Text = "切出し終了";

        _cropMouseClientPos = sender == _pnlPreview
            ? _picPreview.PointToClient(_pnlPreview.PointToScreen(e.Location))
            : e.Location;
        var imgPt = EventToImage(sender, e.Location);
        _cropSelection.MouseDown(imgPt, _viewModel.PreviewImage.Size);
        _isCropHandleActive = _cropSelection.IsHandleActive;
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
        Program.LogDebug($"MouseMove: loc=({e.Location.X},{e.Location.Y}) imgPt=({imgPt.X},{imgPt.Y}) selRect={_cropSelection.SelectionRect?.ToString() ?? "null"}");
        _picPreview.Invalidate();
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
        _isCropHandleActive = _cropSelection.IsHandleActive;

        // 選択がキャンセルされたら（ドラッグ後5px未満）切出しモードを終了
        if (!_cropSelection.SelectionRect.HasValue && !_cropSelection.IsDragging)
        {
            ExitCropMode();
        }

        _picPreview.Invalidate();
        _pnlPreview.Invalidate();
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
        Program.LogDebug($"PicPreview_CropPaint called: _isCropMode={_isCropMode}, hasImage={_viewModel.PreviewImage is not null}, hasSel={_cropSelection.SelectionRect.HasValue}");
        if (!_isCropMode || _viewModel.PreviewImage is null) return;

        var img = _viewModel.PreviewImage;
        var zoom = GetActualZoom();
        var pw = _picPreview.ClientSize.Width;
        var ph = _picPreview.ClientSize.Height;

        double offsetX = 0, offsetY = 0;
        if (_zoomPercent == 0)
        {
            offsetX = (pw - img.Width * zoom) / 2;
            offsetY = (ph - img.Height * zoom) / 2;
        }

        e.Graphics.SetClip(new Rectangle((int)offsetX, (int)offsetY,
            (int)(img.Width * zoom), (int)(img.Height * zoom)));

        if (_cropSelection.SelectionRect.HasValue)
        {
            var sel = _cropSelection.SelectionRect.Value;
            var csX = (int)(sel.X * zoom + offsetX);
            var csY = (int)(sel.Y * zoom + offsetY);
            var csW = (int)(sel.Width * zoom);
            var csH = (int)(sel.Height * zoom);

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
        // 範囲選択がある切出しモード時のみ表示
        if (!_isCropMode || !_cropSelection.SelectionRect.HasValue || _viewModel.PreviewImage is null)
        {
            _picLoupe.Visible = false;
            return;
        }

        var img = _viewModel.PreviewImage;
        var zoom = GetActualZoom();

        var loupeSize = _picLoupe.Width;
        var loupeZoom = 8.0;
        var cx = _cropMouseClientPos.X;
        var cy = _cropMouseClientPos.Y;

        // ルーペを Panel 可視領域の右下に固定
        var lx = _pnlPreview.ClientSize.Width - loupeSize - 16;
        var ly = _pnlPreview.ClientSize.Height - loupeSize - 16;
        if (lx < 4) lx = 4;
        if (ly < 4) ly = 4;
        _picLoupe.Location = new Point(lx, ly);

        // マウス位置を PictureBox 座標 → 画像座標に変換
        var pw = _picPreview.ClientSize.Width;
        var ph = _picPreview.ClientSize.Height;
        double offsetX = 0, offsetY = 0;
        if (_zoomPercent == 0)
        {
            offsetX = (pw - img.Width * zoom) / 2;
            offsetY = (ph - img.Height * zoom) / 2;
        }

        var imgCx = (int)((cx - offsetX) / zoom);
        var imgCy = (int)((cy - offsetY) / zoom);

        // マウスが画像の外にあるときは拡大鏡を非表示
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
    /// 拡大鏡の画像を生成して _picLoupe に設定する
    /// </summary>
    private void GenerateLoupeImage(Image img, int loupeSize, double loupeZoom, int imgCx, int imgCy)
    {
        // 浮動小数点で正確な中心計算（整数切り捨てによるずれ防止）
        var halfSrc = loupeSize / loupeZoom / 2.0;
        var srcX = (float)Math.Max(0, imgCx - halfSrc);
        var srcY = (float)Math.Max(0, imgCy - halfSrc);
        var srcW = (float)Math.Min(halfSrc * 2, img.Width - srcX);
        var srcH = (float)Math.Min(halfSrc * 2, img.Height - srcY);

        if (srcW <= 0 || srcH <= 0)
        {
            _picLoupe.Visible = false;
            return;
        }

        var oldImage = _picLoupe.Image;
        var bmp = new Bitmap(loupeSize, loupeSize);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

        // 背景塗りつぶし
        g.FillRectangle(Brushes.Black, 0, 0, loupeSize, loupeSize);

        // 拡大描画（浮動小数点ソース領域で中心を正確に維持）
        g.DrawImage(img, new Rectangle(0, 0, loupeSize, loupeSize), new RectangleF(srcX, srcY, srcW, srcH), GraphicsUnit.Pixel);

        // 選択範囲の表示（ルーペ内）
        if (_cropSelection.SelectionRect.HasValue)
        {
            var sel = _cropSelection.SelectionRect.Value;
            var bL = (int)((sel.X - imgCx + halfSrc) * loupeZoom);
            var bT = (int)((sel.Y - imgCy + halfSrc) * loupeZoom);
            var bR = (int)((sel.Right - imgCx + halfSrc) * loupeZoom);
            var bB = (int)((sel.Bottom - imgCy + halfSrc) * loupeZoom);
            if (bL >= 0 && bT >= 0 && bR <= loupeSize && bB <= loupeSize)
            {
                var selectionColor = Color.FromName(_viewModel.Settings.LoupeFrameColor);
                using var bp = new Pen(Color.FromArgb(180, selectionColor.R, selectionColor.G, selectionColor.B), 1f);
                bp.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                bp.DashPattern = [1f, 3f];
                g.DrawRectangle(bp, bL, bT, bR - bL, bB - bT);
            }
        }

        // 十字線
        var crossColor = Color.FromName(_viewModel.Settings.LoupeCrossColor);
        using var cp = new Pen(Color.FromArgb(120, crossColor.R, crossColor.G, crossColor.B), 1f);
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

    private void LinkSaveFolder_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_viewModel.SaveFolderPath))
        {
            var dialog = new FolderViewForm(_viewModel.SaveFolderPath, _viewModel.Settings);
            dialog.Show(this);
        }
    }

    private void TxtFileNameTemplate_TextChanged(object? sender, EventArgs e)
    {
        _viewModel.FileNameTemplateText = _txtFileNameTemplate.Text;
    }

    private void MenuHotKeySettings_Click(object? sender, EventArgs e)
    {
        using var dialog = new HotkeyForm(_viewModel.Settings);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _hotKeyManager.RegisterAll(_viewModel.Settings);
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
            var captureType = _hotKeyManager.ProcessHotKeyMessage(m);
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
        _viewModel.Settings.Save();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _hotKeyManager?.Dispose();
        base.Dispose(disposing);
    }
}
