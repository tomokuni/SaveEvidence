using app.Enum;
using app.Models;

namespace app.Views;

/// <summary>
/// 動作設定（色・配置・ルーペ・キャプチャ方式・アイコンサイズ）を変更するモーダルダイアログ。
/// </summary>
/// <remarks>
/// 設定オブジェクトを直接編集するため、OK ボタン押下後に呼び出し元で
/// <see cref="Services.SettingsService.Save"/> を呼び出す必要がある。<br/>
/// </remarks>
public sealed partial class SettingsForm : Form
{
    private readonly Settings _settings;

    /// <summary>
    /// <see cref="SettingsForm"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="settings">編集対象の設定オブジェクト</param>
    public SettingsForm(Settings settings)
    {
        _settings = settings;
        InitializeComponent();
        LoadSettings();
    }

    /// <summary>設定オブジェクトの値を各コントロールに読み込む。</summary>
    private void LoadSettings()
    {
        _cmbAlign.SelectedIndex = _settings.CenterAlign ? 0 : 1;
        _txtCaptureBorder.Text = _settings.CaptureBorderColor;
        _txtCropBorder.Text = _settings.CropBorderColor;
        _txtLoupeCross.Text = _settings.LoupeCrossColor;
        _txtLoupeFrame.Text = _settings.LoupeFrameColor;
        _numLoupeWidth.Value = _settings.LoupeFrameWidth;
        _cmbCaptureMode.SelectedIndex = (int)_settings.CaptureMode;
        UpdateCaptureModeDescription();

        // ルーペ設定
        _cmbLoupeZoom.SelectedItem = _settings.LoupeZoomLevel.ToString();
        _numLoupeSize.Value = _settings.LoupeSize;

        // FolderView アイコンサイズ
        _numFolderExtraLarge.Value = _settings.FolderExtraLargeIconSize;
        _numFolderLarge.Value = _settings.FolderLargeIconSize;
        _numFolderMedium.Value = _settings.FolderMediumIconSize;
    }

    /// <summary>OKボタンクリック時、各コントロールの値を設定オブジェクトに書き戻してダイアログを閉じる。</summary>
    private void BtnOk_Click(object? sender, EventArgs e)
    {
        _settings.CenterAlign = _cmbAlign.SelectedIndex == 0;
        _settings.CaptureBorderColor = _txtCaptureBorder.Text;
        _settings.CropBorderColor = _txtCropBorder.Text;
        _settings.LoupeCrossColor = _txtLoupeCross.Text;
        _settings.LoupeFrameColor = _txtLoupeFrame.Text;
        _settings.LoupeFrameWidth = (int)_numLoupeWidth.Value;
        _settings.CaptureMode = (WindowCaptureMode)_cmbCaptureMode.SelectedIndex;
        _settings.LoupeZoomLevel = int.Parse((string)_cmbLoupeZoom.SelectedItem!);
        _settings.LoupeSize = (int)_numLoupeSize.Value;
        _settings.FolderExtraLargeIconSize = (int)_numFolderExtraLarge.Value;
        _settings.FolderLargeIconSize = (int)_numFolderLarge.Value;
        _settings.FolderMediumIconSize = (int)_numFolderMedium.Value;
        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>キャプチャモードコンボボックスの選択変更イベント。説明文を更新する。</summary>
    private void CmbCaptureMode_SelectedIndexChanged(object? sender, EventArgs e) => UpdateCaptureModeDescription();

    /// <summary>キャプチャモードの説明ラベルを現在の選択に応じて更新する。</summary>
    private void UpdateCaptureModeDescription()
    {
        _lblCaptureModeDesc.Text = _cmbCaptureMode.SelectedIndex switch
        {
            0 => "DWM（Desktop Window Manager）からウィンドウの正しい描画内容を直接取得できるため、1px枠線の写り込みが原理的に発生しません。 " +
                 "この方式は1px枠線の写り込みを防止できる代わりに、枠の色がクラシック風になります。 " +
                 "DirectX / ハードウェアアクセラレーション が有効なコンテンツ（動画、3D、一部のモダンUWPアプリ）を正しくキャプチャできない場合があります。",
            _ => "選択されたエリア内の見た目通りにイメージが取得されます。 " +
                 "Windows がウィンドウに付与する1pxの透明リサイズ境界も含めてキャプチャしてしまうため、その1px部分には背景のデスクトップや背後のウィンドウが映り込みます。 " +
                 "前面に他のウィンドウが重なっている場合には、重なっているウィンドウ部分も映り込みますが、DirectX/GPU描画コンテンツも含めて確実にキャプチャできます。",
        };
    }

    /// <summary>キャンセルボタンクリック時、変更を破棄してダイアログを閉じる。</summary>
    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    /// <summary>キャプチャ境界色選択ボタンのクリックイベント。</summary>
    private void BtnCaptureColor_Click(object? sender, EventArgs e) => PickColor(_txtCaptureBorder);
    /// <summary>切り出し境界色選択ボタンのクリックイベント。</summary>
    private void BtnCropColor_Click(object? sender, EventArgs e) => PickColor(_txtCropBorder);
    /// <summary>ルーペ十字線色選択ボタンのクリックイベント。</summary>
    private void BtnCrossColor_Click(object? sender, EventArgs e) => PickColor(_txtLoupeCross);
    /// <summary>ルーペ外枠色選択ボタンのクリックイベント。</summary>
    private void BtnFrameColor_Click(object? sender, EventArgs e) => PickColor(_txtLoupeFrame);

    /// <summary>ColorDialog を表示し、選択された色名を TextBox に設定する。</summary>
    /// <param name="tb">色名を設定する TextBox</param>
    private static void PickColor(TextBox tb)
    {
        using var dlg = new ColorDialog();
        dlg.Color = Color.FromName(tb.Text);
        if (dlg.ShowDialog() == DialogResult.OK)
            tb.Text = dlg.Color.Name;
    }
}
