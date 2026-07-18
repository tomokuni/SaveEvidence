using app.Models;

namespace app.Views;

/// <summary>
/// グローバルホットキー設定ダイアログ
/// </summary>
public partial class HotkeyForm : Form
{
    private readonly Settings _settings;
    private Button? _currentCapturingButton;
    private Label? _currentCapturingLabel;
    private bool _isCapturing;

    /// <summary>
    /// HotkeyForm を初期化する
    /// </summary>
    public HotkeyForm(Settings settings)
    {
        _settings = settings;
        InitializeComponent();
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        _lblSelectScreenHotkey.Text = _settings.SelectScreenHotKey.ToString();
        _lblActiveWindowHotkey.Text = _settings.ActiveWindowHotKey.ToString();
        _lblAreaSelectHotkey.Text = _settings.AreaSelectHotKey.ToString();
    }

    private void BtnSetSelectScreen_Click(object? sender, EventArgs e)
    {
        StartCapturing(_btnSetSelectScreen, _lblSelectScreenHotkey);
    }

    private void BtnSetActiveWindow_Click(object? sender, EventArgs e)
    {
        StartCapturing(_btnSetActiveWindow, _lblActiveWindowHotkey);
    }

    private void BtnSetAreaSelect_Click(object? sender, EventArgs e)
    {
        StartCapturing(_btnSetAreaSelect, _lblAreaSelectHotkey);
    }

    private void StartCapturing(Button button, Label label)
    {
        if (_isCapturing)
        {
            return;
        }

        _isCapturing = true;
        _currentCapturingButton = button;
        _currentCapturingLabel = label;
        button.Text = "キー入力中...";
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!_isCapturing || _currentCapturingButton is null || _currentCapturingLabel is null)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // 修飾キーのみの場合は無視
        if (keyData is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            return true;
        }

        var modifiers = HotKeyModifiers.None;
        if (keyData.HasFlag(Keys.Control)) modifiers |= HotKeyModifiers.Control;
        if (keyData.HasFlag(Keys.Alt)) modifiers |= HotKeyModifiers.Alt;
        if (keyData.HasFlag(Keys.Shift)) modifiers |= HotKeyModifiers.Shift;
        if (keyData.HasFlag(Keys.LWin) || keyData.HasFlag(Keys.RWin)) modifiers |= HotKeyModifiers.Windows;

        // 修飾キーなしは許可しない
        if (modifiers == HotKeyModifiers.None)
        {
            return true;
        }

        var key = keyData & Keys.KeyCode;
        var setting = new HotKeySetting { Modifiers = modifiers, Key = key };

        // どの設定を更新するか判定
        if (_currentCapturingButton == _btnSetSelectScreen)
        {
            _settings.SelectScreenHotKey = setting;
        }
        else if (_currentCapturingButton == _btnSetActiveWindow)
        {
            _settings.ActiveWindowHotKey = setting;
        }
        else if (_currentCapturingButton == _btnSetAreaSelect)
        {
            _settings.AreaSelectHotKey = setting;
        }

        _settings.Save();

        _currentCapturingButton.Text = "設定";
        _currentCapturingLabel.Text = setting.ToString();
        _isCapturing = false;
        _currentCapturingButton = null;
        _currentCapturingLabel = null;

        return true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _isCapturing = false;
        _currentCapturingButton = null;
        _currentCapturingLabel = null;
        base.OnFormClosing(e);
    }
}
