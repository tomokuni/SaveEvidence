using app.Models;
using app.Services;

namespace app.Views;

/// <summary>
/// グローバルホットキーを設定するモーダルダイアログ。
/// </summary>
/// <remarks>
/// 各キャプチャ種別（スクリーン選択 / ウィンドウ選択 / 範囲選択）に対応する
/// ホットキーを、実際のキーボード入力から対話的に設定できる。<br/>
/// ホットキーは設定が変更されるたびに <c>saveAction</c> コールバックを介して永続化される。<br/>
/// 修飾キー（Ctrl/Alt/Shift/Win）＋キーの組合せで設定する。<br/>
/// </remarks>
public partial class HotkeyForm : Form
{
    private readonly Settings _settings;
    private readonly Action _saveAction;
    private Button? _currentCapturingButton;
    private Label? _currentCapturingLabel;
    private bool _isCapturing;

    /// <summary>
    /// HotkeyForm を初期化する
    /// </summary>
    /// <param name="settings">編集する設定オブジェクト</param>
    /// <param name="saveAction">設定を永続化するためのコールバック</param>
    public HotkeyForm(Settings settings, Action saveAction)
    {
        _settings = settings;
        _saveAction = saveAction;
        InitializeComponent();
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        _lblSelectScreenHotkey.Text = _settings.SelectScreenHotKey.ToString();
        _lblWindowSelectHotkey.Text = _settings.WindowSelectHotKey.ToString();
        _lblAreaSelectHotkey.Text = _settings.AreaSelectHotKey.ToString();
    }

    private void BtnSetSelectScreen_Click(object? sender, EventArgs e)
    {
        StartCapturing(_btnSetSelectScreen, _lblSelectScreenHotkey);
    }

    private void BtnSetWindowSelect_Click(object? sender, EventArgs e)
    {
        StartCapturing(_btnSetWindowSelect, _lblWindowSelectHotkey);
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

        if (keyData is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            return true;
        }

        var modifiers = HotKeyModifiers.None;
        if (keyData.HasFlag(Keys.Control)) modifiers |= HotKeyModifiers.Control;
        if (keyData.HasFlag(Keys.Alt)) modifiers |= HotKeyModifiers.Alt;
        if (keyData.HasFlag(Keys.Shift)) modifiers |= HotKeyModifiers.Shift;
        if (keyData.HasFlag(Keys.LWin) || keyData.HasFlag(Keys.RWin)) modifiers |= HotKeyModifiers.Windows;

        if (modifiers == HotKeyModifiers.None)
        {
            return true;
        }

        var key = keyData & Keys.KeyCode;
        var setting = new HotKeySetting { Modifiers = modifiers, Key = key };

        if (_currentCapturingButton == _btnSetSelectScreen)
        {
            _settings.SelectScreenHotKey = setting;
        }
        else if (_currentCapturingButton == _btnSetWindowSelect)
        {
            _settings.WindowSelectHotKey = setting;
        }
        else if (_currentCapturingButton == _btnSetAreaSelect)
        {
            _settings.AreaSelectHotKey = setting;
        }

        _saveAction();

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
