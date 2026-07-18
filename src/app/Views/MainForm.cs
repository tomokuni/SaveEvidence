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

    /// <summary>
    /// MainForm を初期化する
    /// </summary>
    public MainForm()
    {
        _viewModel = new MainViewModel();
        _hotKeyManager = new HotKeyManager(Handle);

        InitializeComponent();
        InitializeViewModel();
        RegisterHotKeys();
        RestoreFormBounds();
    }

    private void InitializeViewModel()
    {
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.PreviewImage) && _viewModel.PreviewImage is not null)
            {
                UpdatePreviewImage(_viewModel.PreviewImage);
            }

            if (e.PropertyName == nameof(MainViewModel.SaveFolderDisplayName))
            {
                _linkSaveFolder.Text = _viewModel.SaveFolderDisplayName;
                _toolTip.SetToolTip(_btnSaveFolder, _viewModel.SaveFolderPath);
                _toolTip.SetToolTip(_linkSaveFolder, _viewModel.SaveFolderPath);
            }

            if (e.PropertyName == nameof(MainViewModel.CurrentFileNamePreview))
            {
                UpdateStatusBar();
            }

            if (e.PropertyName == nameof(MainViewModel.FileNameTemplateText))
            {
                _txtFileNameTemplate.Text = _viewModel.FileNameTemplateText;
            }

            if (e.PropertyName == nameof(MainViewModel.IsSaved))
            {
                _picPreview.Invalidate();
            }

            // ボタンの有効状態を更新
            if (e.PropertyName is nameof(MainViewModel.CanCopy) or nameof(MainViewModel.CanSave) or nameof(MainViewModel.HasPreviewImage) or nameof(MainViewModel.IsSaved))
            {
                _btnCopy.Enabled = _viewModel.CanCopy;
                _btnSave.Enabled = _viewModel.CanSave;
            }
        };

        _viewModel.StartSelectionMode = StartSelectionMode;

        // 初期表示の更新
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
        _hotKeyManager.RegisterAll(_viewModel.Settings);
    }

    private void ExecuteCapture(CaptureType captureType)
    {
        if (_isExecutingCapture)
        {
            return;
        }

        _isExecutingCapture = true;
        switch (captureType)
        {
            case CaptureType.SelectScreen:
                PerformSelectScreenCapture();
                break;
            case CaptureType.WindowSelect:
                StartSelectionMode(CaptureType.WindowSelect);
                break;
            case CaptureType.AreaSelect:
                StartSelectionMode(CaptureType.AreaSelect);
                break;
        }
    }

    private void PerformSelectScreenCapture()
    {
        var screens = CaptureManager.GetAllScreenBounds();

        // スクリーンが1つだけなら直接キャプチャ
        if (screens.Length == 1)
        {
            _viewModel.CaptureScreenArea(screens[0].Bounds);
            _isExecutingCapture = false;
            return;
        }

        // 複数スクリーンなら全画面をキャプチャして選択させる
        Hide();
        System.Threading.Tasks.Task.Delay(300).ContinueWith(_ =>
        {
            BeginInvoke(() =>
            {
                try
                {
                    // 仮想スクリーン全体を事前にキャプチャ
                    var virtualBounds = SystemInformation.VirtualScreen;
                    using var fullCapture = CaptureManager.CaptureArea(virtualBounds);

                    using var selectionForm = new SelectionForm(CaptureType.SelectScreen, fullCapture);

                    selectionForm.SelectionCompleted += (s, rect) =>
                    {
                        try
                        {
                            _viewModel.CaptureScreenArea(rect);
                        }
                        catch (Exception ex)
                        {
                            Program.LogException(ex);
                        }
                    };

                    selectionForm.Cancelled += (s, e) =>
                    {
                        // キャンセル時は何もせず、ShowDialog が閉じるのを待つ
                    };

                    selectionForm.ShowDialog();
                }
                catch (Exception ex)
                {
                    Program.LogException(ex);
                }
                finally
                {
                    _isExecutingCapture = false;
                    EnsureVisible();
                }
            });
        });
    }

    private void StartSelectionMode(CaptureType captureType)
    {
        Hide();
        System.Threading.Tasks.Task.Delay(300).ContinueWith(_ =>
        {
            BeginInvoke(() =>
            {
                try
                {
                    // 全画面を事前キャプチャ（枠線無し・暗転方式で使用するため）
                    var virtualBounds = SystemInformation.VirtualScreen;
                    using var fullCapture = CaptureManager.CaptureArea(virtualBounds);

                    using var selectionForm = new SelectionForm(captureType, fullCapture);

                    selectionForm.SelectionCompleted += (s, rect) =>
                    {
                        try
                        {
                            var bitmap = CaptureManager.CaptureArea(rect);
                            _viewModel.SetPreviewImage(bitmap);
                        }
                        catch (Exception ex)
                        {
                            Program.LogException(ex);
                        }
                    };

                    selectionForm.Cancelled += (s, e) =>
                    {
                        // キャンセル時は何もせず、ShowDialog が閉じるのを待つ
                    };

                    selectionForm.ShowDialog();
                }
                catch (Exception ex)
                {
                    Program.LogException(ex);
                }
                finally
                {
                    _isExecutingCapture = false;
                    EnsureVisible();
                }
            });
        });
    }

    /// <summary>
    /// フォームを確実に可視状態に復帰する。
    /// 例外発生時など、画面暗転状態から復旧するために使用する。
    /// </summary>
    private void EnsureVisible()
    {
        if (!Visible)
        {
            Show();
        }

        if (WindowState != FormWindowState.Normal)
        {
            WindowState = FormWindowState.Normal;
        }

        BringToFront();
    }

    private void UpdatePreviewImage(Image image)
    {
        _picPreview.Image?.Dispose();
        _picPreview.Image = null;
        _picPreview.Paint -= PicPreview_Paint!;

        // プレビューエリアに合わせて画像を表示
        _picPreview.Image = new Bitmap(image);
        _picPreview.SizeMode = PictureBoxSizeMode.Zoom;

        // 画像があれば拡大率表示用のPaintハンドラを追加
        _picPreview.Paint += PicPreview_Paint!;
    }

    /// <summary>
    /// プレビュー画像の左上に拡大率、その右隣りに保存済みをオーバーレイ表示する
    /// </summary>
    private void PicPreview_Paint(object? sender, PaintEventArgs e)
    {
        try
        {
            var pic = (PictureBox)sender!;
            if (pic.Image is null) return;

            var img = pic.Image;
            var cw = pic.ClientSize.Width;
            var ch = pic.ClientSize.Height;

            // Zoom モードでの表示倍率を計算
            var zoomX = (double)cw / img.Width;
            var zoomY = (double)ch / img.Height;
            var zoom = Math.Min(zoomX, zoomY);
            var percentage = (int)(zoom * 100);

            using var font = new Font("Segoe UI", 12, FontStyle.Bold);
            var x = 4;
            var y = 4;

            // 拡大率（プレビュー領域の左上に固定）
            var zoomText = $"{percentage}%";
            var zoomSize = e.Graphics.MeasureString(zoomText, font);
            using var bgBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
            using var textBrush = new SolidBrush(Color.White);
            var zoomRect = new Rectangle(
                x, y,
                (int)zoomSize.Width + 8, (int)zoomSize.Height + 4);
            e.Graphics.FillRectangle(bgBrush, zoomRect);
            e.Graphics.DrawString(zoomText, font, textBrush, zoomRect.X + 4, zoomRect.Y + 2);

            // 保存済み（拡大率の右隣り）
            if (_viewModel.IsSaved)
            {
                var savedText = "保存済み";
                var savedSize = e.Graphics.MeasureString(savedText, font);
                using var savedBgBrush = new SolidBrush(Color.FromArgb(180, 0, 100, 0));
                using var savedTextBrush = new SolidBrush(Color.LightGreen);
                var savedRect = new Rectangle(
                    zoomRect.Right + 4, y,
                    (int)savedSize.Width + 8, (int)savedSize.Height + 4);
                e.Graphics.FillRectangle(savedBgBrush, savedRect);
                e.Graphics.DrawString(savedText, font, savedTextBrush, savedRect.X + 4, savedRect.Y + 2);
            }
        }
        catch (Exception ex)
        {
            Program.LogException(ex);
        }
    }

    private void UpdateStatusBar()
    {
        _lblStatus.Text = _viewModel.StatusText;
    }

    private void BtnSelectScreen_Click(object? sender, EventArgs e)
    {
        ExecuteCapture(CaptureType.SelectScreen);
    }

    private void BtnWindowSelect_Click(object? sender, EventArgs e)
    {
        ExecuteCapture(CaptureType.WindowSelect);
    }

    private void BtnAreaSelect_Click(object? sender, EventArgs e)
    {
        ExecuteCapture(CaptureType.AreaSelect);
    }

    private void BtnAutoCrop_Click(object? sender, EventArgs e)
    {
        _viewModel.AutoCropWindow();
    }

    private void BtnCopy_Click(object? sender, EventArgs e)
    {
        _viewModel.CopyToClipboard();
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        if (_viewModel.SaveImage())
        {
            _lblStatus.Text = "保存しました";
        }
    }

    private void BtnSaveFolder_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog();
        dialog.Description = "保存先フォルダを選択してください";
        dialog.SelectedPath = _viewModel.SaveFolderPath;

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _viewModel.SaveFolderPath = dialog.SelectedPath;
        }
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
        {
            _hotKeyManager.RegisterAll(_viewModel.Settings);
        }
    }

    private void MenuSaveFolder_Click(object? sender, EventArgs e)
    {
        BtnSaveFolder_Click(sender, e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Ctrl+C: 画像をクリップボードにコピー
        if (keyData == (Keys.Control | Keys.C))
        {
            _viewModel.CopyToClipboard();
            return true;
        }

        // Ctrl+V: クリップボードから画像を貼り付け
        if (keyData == (Keys.Control | Keys.V))
        {
            if (_viewModel.PasteFromClipboard())
            {
                return true;
            }
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

    /// <summary>
    /// 設定からウィンドウ位置・サイズを復元する
    /// </summary>
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
        {
            WindowState = state;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (WindowState == FormWindowState.Normal)
        {
            _viewModel.Settings.MainFormBounds = DesktopBounds;
        }
        else
        {
            // 最小化/最大化時は通常状態の位置を保存
            _viewModel.Settings.MainFormBounds = RestoreBounds;
        }

        _viewModel.Settings.MainFormWindowState = WindowState == FormWindowState.Minimized
            ? FormWindowState.Normal : WindowState;

        _viewModel.Settings.Save();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotKeyManager?.Dispose();
        }

        base.Dispose(disposing);
    }
}
