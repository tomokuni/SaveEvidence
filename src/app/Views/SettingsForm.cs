using app.Models;

namespace app.Views;

/// <summary>
/// 表示設定を行うモーダルダイアログ
/// </summary>
public sealed partial class SettingsForm : Form
{
    private readonly Settings _settings;

    public SettingsForm(Settings settings)
    {
        _settings = settings;
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        _cmbAlign.SelectedIndex = _settings.CenterAlign ? 0 : 1;
        _txtCaptureBorder.Text = _settings.CaptureBorderColor;
        _txtCropBorder.Text = _settings.CropBorderColor;
        _txtLoupeCross.Text = _settings.LoupeCrossColor;
        _txtLoupeFrame.Text = _settings.LoupeFrameColor;
        _numLoupeWidth.Value = _settings.LoupeFrameWidth;
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        _settings.CenterAlign = _cmbAlign.SelectedIndex == 0;
        _settings.CaptureBorderColor = _txtCaptureBorder.Text;
        _settings.CropBorderColor = _txtCropBorder.Text;
        _settings.LoupeCrossColor = _txtLoupeCross.Text;
        _settings.LoupeFrameColor = _txtLoupeFrame.Text;
        _settings.LoupeFrameWidth = (int)_numLoupeWidth.Value;
        _settings.Save();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void BtnCaptureColor_Click(object? sender, EventArgs e) => PickColor(_txtCaptureBorder);
    private void BtnCropColor_Click(object? sender, EventArgs e) => PickColor(_txtCropBorder);
    private void BtnCrossColor_Click(object? sender, EventArgs e) => PickColor(_txtLoupeCross);
    private void BtnFrameColor_Click(object? sender, EventArgs e) => PickColor(_txtLoupeFrame);

    private void PickColor(TextBox tb)
    {
        using var dlg = new ColorDialog();
        dlg.Color = Color.FromName(tb.Text);
        if (dlg.ShowDialog() == DialogResult.OK)
            tb.Text = dlg.Color.Name;
    }
}
