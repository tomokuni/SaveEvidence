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
            }

            if (e.PropertyName == nameof(MainViewModel.CurrentFileNamePreview))
            {
                UpdateStatusBar();
            }
        };

        // 初期表示の更新
        _linkSaveFolder.Text = _viewModel.SaveFolderDisplayName;
        _toolTip.SetToolTip(_btnSaveFolder, _viewModel.SaveFolderPath);
        _txtFileNameTemplate.Text = _viewModel.FileNameTemplateText;
        UpdateStatusBar();
    }

    private void RegisterHotKeys()
    {
        _hotKeyManager.RegisterAll(_viewModel.Settings);
    }

    private void ExecuteCapture(CaptureType captureType)
    {
        switch (captureType)
        {
            case CaptureType.SelectScreen:
                PerformSelectScreenCapture();
                break;
            case CaptureType.ActiveWindow:
                PerformActiveWindowCapture();
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
                    EnsureVisible();
                }
            });
        });
    }

    private void PerformActiveWindowCapture()
    {
        Hide();
        System.Threading.Tasks.Task.Delay(300).ContinueWith(_ =>
        {
            BeginInvoke(() =>
            {
                try
                {
                    _viewModel.CaptureActiveWindow();
                }
                catch (Exception ex)
                {
                    Program.LogException(ex);
                }
                finally
                {
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
                    using var selectionForm = new SelectionForm(captureType);

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

        // プレビューエリアに合わせて画像を表示
        _picPreview.Image = new Bitmap(image);
        _picPreview.SizeMode = PictureBoxSizeMode.Zoom;
    }

    private void UpdateStatusBar()
    {
        _lblStatus.Text = _viewModel.StatusText;
    }

    private void BtnSelectScreen_Click(object? sender, EventArgs e)
    {
        ExecuteCapture(CaptureType.SelectScreen);
    }

    private void BtnActiveWindow_Click(object? sender, EventArgs e)
    {
        ExecuteCapture(CaptureType.ActiveWindow);
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
        _viewModel.OpenSaveFolder();
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotKeyManager?.Dispose();
        }

        base.Dispose(disposing);
    }
}
