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
        _tlp.RowCount = 8;

        for (var i = 0; i < 8; i++)
            _tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // 行0: 表示位置
        _tlp.Controls.Add(new Label { Text = "表示位置:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        _cmbAlign.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbAlign.Items.AddRange(["中央寄せ", "左上寄せ"]);
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

        // 行6: OK/Cancel
        var flowButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        _btnOk.AutoSize = true;
        _btnOk.Text = "OK";
        _btnOk.Click += BtnOk_Click;
        flowButtons.Controls.Add(_btnOk);
        _btnCancel.AutoSize = true;
        _btnCancel.Text = "キャンセル";
        _btnCancel.Click += BtnCancel_Click;
        flowButtons.Controls.Add(_btnCancel);
        _tlp.SetColumnSpan(flowButtons, 3);
        _tlp.Controls.Add(flowButtons, 0, 6);

        // SettingsForm
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(500, 300);
        Controls.Add(_tlp);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "SettingsForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "表示設定";

        _tlp.ResumeLayout(false);
        _tlp.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_numLoupeWidth).EndInit();
        ResumeLayout(false);
    }
}
