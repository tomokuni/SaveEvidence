#nullable disable

namespace app.Views;

partial class SettingsForm
{
    private System.ComponentModel.IContainer components = null;
    private ComboBox _cmbAlign;
    private TextBox _txtCaptureBorder;
    private Button _btnCaptureColor;
    private Label _lblCaptureBorder;
    private TextBox _txtCropBorder;
    private Button _btnCropColor;
    private Label _lblCropBorder;
    private TextBox _txtLoupeCross;
    private Button _btnCrossColor;
    private Label _lblLoupeCross;
    private TextBox _txtLoupeFrame;
    private Button _btnFrameColor;
    private Label _lblLoupeFrame;
    private NumericUpDown _numLoupeWidth;
    private Label _lblLoupeWidth;
    private Button _btnOk;
    private Button _btnCancel;
    private ComboBox _cmbCaptureMode;
    private Label _lblCaptureModeDesc;
    private ComboBox _cmbLoupeZoom;
    private NumericUpDown _numLoupeSize;
    private NumericUpDown _numFolderExtraLarge;
    private NumericUpDown _numFolderLarge;
    private NumericUpDown _numFolderMedium;
    private TableLayoutPanel _tlp;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components is not null))
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        _tlp = new TableLayoutPanel();
        _lblCaptureBorder = new Label();
        _txtCaptureBorder = new TextBox();
        _btnCaptureColor = new Button();
        _lblCropBorder = new Label();
        _txtCropBorder = new TextBox();
        _btnCropColor = new Button();
        _lblLoupeCross = new Label();
        _txtLoupeCross = new TextBox();
        _btnCrossColor = new Button();
        _lblLoupeFrame = new Label();
        _txtLoupeFrame = new TextBox();
        _btnFrameColor = new Button();
        _lblLoupeWidth = new Label();
        _numLoupeWidth = new NumericUpDown();
        _cmbAlign = new ComboBox();
        _btnOk = new Button();
        _btnCancel = new Button();

        ((System.ComponentModel.ISupportInitialize)_numLoupeWidth).BeginInit();
        _tlp.SuspendLayout();
        SuspendLayout();

        // _tlp
        _tlp.ColumnCount = 3;
        _tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        _tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        _tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        _tlp.Dock = DockStyle.Fill;
        _tlp.Location = new Point(0, 0);
        _tlp.Name = "_tlp";
        _tlp.Padding = new Padding(10);
        _tlp.RowCount = 14;

        _tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // 行0: 表示位置
        var lblAlign = new Label();
        lblAlign.Text = "表示位置:";
        lblAlign.Anchor = AnchorStyles.Left;
        lblAlign.AutoSize = true;
        _tlp.Controls.Add(lblAlign, 0, 0);
        _cmbAlign.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbAlign.Items.AddRange(new object[] { "中央寄せ", "左上寄せ" });
        _cmbAlign.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _cmbAlign.Margin = new Padding(3, 3, 3, 3);
        _tlp.Controls.Add(_cmbAlign, 1, 0);

        // 行1: キャプチャ境界線色
        _lblCaptureBorder.Text = "キャプチャ境界線色:";
        _lblCaptureBorder.Anchor = AnchorStyles.Left;
        _lblCaptureBorder.AutoSize = true;
        _tlp.Controls.Add(_lblCaptureBorder, 0, 1);
        _txtCaptureBorder.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _tlp.Controls.Add(_txtCaptureBorder, 1, 1);
        _btnCaptureColor.Text = "色選択";
        _btnCaptureColor.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _btnCaptureColor.Click += BtnCaptureColor_Click;
        _tlp.Controls.Add(_btnCaptureColor, 2, 1);

        // 行2: プレビュー範囲選択境界線色
        _lblCropBorder.Text = "切出し境界線色:";
        _lblCropBorder.Anchor = AnchorStyles.Left;
        _lblCropBorder.AutoSize = true;
        _tlp.Controls.Add(_lblCropBorder, 0, 2);
        _txtCropBorder.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _tlp.Controls.Add(_txtCropBorder, 1, 2);
        _btnCropColor.Text = "色選択";
        _btnCropColor.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _btnCropColor.Click += BtnCropColor_Click;
        _tlp.Controls.Add(_btnCropColor, 2, 2);

        // 行3: 虫眼鏡十字線色
        _lblLoupeCross.Text = "虫眼鏡十字線色:";
        _lblLoupeCross.Anchor = AnchorStyles.Left;
        _lblLoupeCross.AutoSize = true;
        _tlp.Controls.Add(_lblLoupeCross, 0, 3);
        _txtLoupeCross.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _tlp.Controls.Add(_txtLoupeCross, 1, 3);
        _btnCrossColor.Text = "色選択";
        _btnCrossColor.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _btnCrossColor.Click += BtnCrossColor_Click;
        _tlp.Controls.Add(_btnCrossColor, 2, 3);

        // 行4: 虫眼鏡外枠色
        _lblLoupeFrame.Text = "虫眼鏡外枠色:";
        _lblLoupeFrame.Anchor = AnchorStyles.Left;
        _lblLoupeFrame.AutoSize = true;
        _tlp.Controls.Add(_lblLoupeFrame, 0, 4);
        _txtLoupeFrame.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _tlp.Controls.Add(_txtLoupeFrame, 1, 4);
        _btnFrameColor.Text = "色選択";
        _btnFrameColor.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _btnFrameColor.Click += BtnFrameColor_Click;
        _tlp.Controls.Add(_btnFrameColor, 2, 4);

        // 行5: 虫眼鏡外枠太さ
        _lblLoupeWidth.Text = "虫眼鏡外枠太さ:";
        _lblLoupeWidth.Anchor = AnchorStyles.Left;
        _lblLoupeWidth.AutoSize = true;
        _tlp.Controls.Add(_lblLoupeWidth, 0, 5);
        _numLoupeWidth.Minimum = 1;
        _numLoupeWidth.Maximum = 10;
        _numLoupeWidth.Anchor = AnchorStyles.Left;
        _tlp.Controls.Add(_numLoupeWidth, 1, 5);

        // 行6: ルーペの拡大率
        var lblLoupeZoom = new Label();
        lblLoupeZoom.Text = "ルーペの拡大率:";
        lblLoupeZoom.Anchor = AnchorStyles.Left;
        lblLoupeZoom.AutoSize = true;
        _tlp.Controls.Add(lblLoupeZoom, 0, 6);
        _cmbLoupeZoom = new ComboBox();
        _cmbLoupeZoom.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbLoupeZoom.Items.AddRange(new object[] { "4", "6", "8", "10", "12", "16", "20", "24", "32" });
        _cmbLoupeZoom.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _tlp.Controls.Add(_cmbLoupeZoom, 1, 6);

        // 行7: ルーペのサイズ
        var lblLoupeSize = new Label();
        lblLoupeSize.Text = "ルーペのサイズ:";
        lblLoupeSize.Anchor = AnchorStyles.Left;
        lblLoupeSize.AutoSize = true;
        _tlp.Controls.Add(lblLoupeSize, 0, 7);
        _numLoupeSize = new NumericUpDown();
        _numLoupeSize.Minimum = 64;
        _numLoupeSize.Maximum = 512;
        _numLoupeSize.Increment = 8;
        _numLoupeSize.Anchor = AnchorStyles.Left;
        _tlp.Controls.Add(_numLoupeSize, 1, 7);

        // 行8: FolderView 特大アイコン
        var lblExtraLarge = new Label();
        lblExtraLarge.Text = "特大アイコンサイズ:";
        lblExtraLarge.Anchor = AnchorStyles.Left;
        lblExtraLarge.AutoSize = true;
        _tlp.Controls.Add(lblExtraLarge, 0, 8);
        _numFolderExtraLarge = new NumericUpDown();
        _numFolderExtraLarge.Minimum = 64;
        _numFolderExtraLarge.Maximum = 1024;
        _numFolderExtraLarge.Increment = 16;
        _numFolderExtraLarge.Anchor = AnchorStyles.Left;
        _tlp.Controls.Add(_numFolderExtraLarge, 1, 8);

        // 行9: FolderView 大アイコン
        var lblLarge = new Label();
        lblLarge.Text = "大アイコンサイズ:";
        lblLarge.Anchor = AnchorStyles.Left;
        lblLarge.AutoSize = true;
        _tlp.Controls.Add(lblLarge, 0, 9);
        _numFolderLarge = new NumericUpDown();
        _numFolderLarge.Minimum = 48;
        _numFolderLarge.Maximum = 768;
        _numFolderLarge.Increment = 16;
        _numFolderLarge.Anchor = AnchorStyles.Left;
        _tlp.Controls.Add(_numFolderLarge, 1, 9);

        // 行10: FolderView 中アイコン
        var lblMedium = new Label();
        lblMedium.Text = "中アイコンサイズ:";
        lblMedium.Anchor = AnchorStyles.Left;
        lblMedium.AutoSize = true;
        _tlp.Controls.Add(lblMedium, 0, 10);
        _numFolderMedium = new NumericUpDown();
        _numFolderMedium.Minimum = 32;
        _numFolderMedium.Maximum = 512;
        _numFolderMedium.Increment = 16;
        _numFolderMedium.Anchor = AnchorStyles.Left;
        _tlp.Controls.Add(_numFolderMedium, 1, 10);

        // 行11: キャプチャ方式
        var lblCaptureMode = new Label();
        lblCaptureMode.Text = "キャプチャ方式:";
        lblCaptureMode.Anchor = AnchorStyles.Left;
        lblCaptureMode.AutoSize = true;
        _tlp.Controls.Add(lblCaptureMode, 0, 11);
        _cmbCaptureMode = new ComboBox();
        _cmbCaptureMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbCaptureMode.Items.AddRange(new object[] { "PrintWindow 方式", "CopyFromScreen 方式" });
        _cmbCaptureMode.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _tlp.Controls.Add(_cmbCaptureMode, 1, 11);
        _cmbCaptureMode.SelectedIndexChanged += CmbCaptureMode_SelectedIndexChanged;

        // 行12: キャプチャ方式の説明
        _lblCaptureModeDesc = new Label();
        _lblCaptureModeDesc.AutoSize = true;
        _lblCaptureModeDesc.Anchor = AnchorStyles.Left;
        _lblCaptureModeDesc.MaximumSize = new Size(460, 0);
        _tlp.SetColumnSpan(_lblCaptureModeDesc, 3);
        _tlp.Controls.Add(_lblCaptureModeDesc, 0, 12);

        // 行13: OK/Cancel
        var flowButtons = new FlowLayoutPanel();
        flowButtons.Dock = DockStyle.Fill;
        flowButtons.FlowDirection = FlowDirection.RightToLeft;
        _btnOk.AutoSize = true;
        _btnOk.Text = "OK";
        _btnOk.Click += BtnOk_Click;
        flowButtons.Controls.Add(_btnOk);
        _btnCancel.AutoSize = true;
        _btnCancel.Text = "キャンセル";
        _btnCancel.Click += BtnCancel_Click;
        flowButtons.Controls.Add(_btnCancel);
        _tlp.SetColumnSpan(flowButtons, 3);
        _tlp.Controls.Add(flowButtons, 0, 13);

        // SettingsForm
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(500, 560);
        Controls.Add(_tlp);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "SettingsForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "動作設定";

        _tlp.ResumeLayout(false);
        _tlp.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_numLoupeWidth).EndInit();
        ResumeLayout(false);
    }
}
