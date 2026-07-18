#nullable disable

namespace app.Views;

partial class HotkeyForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel _tlpMain;
    private Label _lblSelectScreenCaption;
    private Button _btnSetSelectScreen;
    private Label _lblSelectScreenHotkey;
    private Label _lblActiveWindowCaption;
    private Button _btnSetActiveWindow;
    private Label _lblActiveWindowHotkey;
    private Label _lblAreaSelectCaption;
    private Button _btnSetAreaSelect;
    private Label _lblAreaSelectHotkey;
    private Button _btnClose;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components is not null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        _tlpMain = new TableLayoutPanel();
        _lblSelectScreenCaption = new Label();
        _btnSetSelectScreen = new Button();
        _lblSelectScreenHotkey = new Label();
        _lblActiveWindowCaption = new Label();
        _btnSetActiveWindow = new Button();
        _lblActiveWindowHotkey = new Label();
        _lblAreaSelectCaption = new Label();
        _btnSetAreaSelect = new Button();
        _lblAreaSelectHotkey = new Label();
        _btnClose = new Button();

        _tlpMain.SuspendLayout();
        SuspendLayout();

        // _tlpMain
        _tlpMain.ColumnCount = 3;
        _tlpMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _tlpMain.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _tlpMain.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _tlpMain.Controls.Add(_lblSelectScreenCaption, 0, 0);
        _tlpMain.Controls.Add(_btnSetSelectScreen, 1, 0);
        _tlpMain.Controls.Add(_lblSelectScreenHotkey, 2, 0);
        _tlpMain.Controls.Add(_lblActiveWindowCaption, 0, 1);
        _tlpMain.Controls.Add(_btnSetActiveWindow, 1, 1);
        _tlpMain.Controls.Add(_lblActiveWindowHotkey, 2, 1);
        _tlpMain.Controls.Add(_lblAreaSelectCaption, 0, 2);
        _tlpMain.Controls.Add(_btnSetAreaSelect, 1, 2);
        _tlpMain.Controls.Add(_lblAreaSelectHotkey, 2, 2);
        _tlpMain.Controls.Add(_btnClose, 1, 3);
        _tlpMain.Dock = DockStyle.Fill;
        _tlpMain.Location = new Point(0, 0);
        _tlpMain.Name = "_tlpMain";
        _tlpMain.Padding = new Padding(10);
        _tlpMain.RowCount = 4;
        _tlpMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _tlpMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _tlpMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _tlpMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _tlpMain.Size = new Size(500, 200);

        // _lblSelectScreenCaption
        _lblSelectScreenCaption.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _lblSelectScreenCaption.AutoSize = true;
        _lblSelectScreenCaption.Text = "スクリーン選択 キャプチャ:";
        _lblSelectScreenCaption.TextAlign = ContentAlignment.MiddleLeft;

        // _btnSetSelectScreen
        _btnSetSelectScreen.AutoSize = true;
        _btnSetSelectScreen.Margin = new Padding(10, 3, 10, 3);
        _btnSetSelectScreen.Text = "設定";
        _btnSetSelectScreen.UseVisualStyleBackColor = true;
        _btnSetSelectScreen.Click += BtnSetSelectScreen_Click;

        // _lblSelectScreenHotkey
        _lblSelectScreenHotkey.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _lblSelectScreenHotkey.AutoSize = true;
        _lblSelectScreenHotkey.TextAlign = ContentAlignment.MiddleLeft;

        // _lblActiveWindowCaption
        _lblActiveWindowCaption.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _lblActiveWindowCaption.AutoSize = true;
        _lblActiveWindowCaption.Text = "アクティブウィンドウ キャプチャ:";
        _lblActiveWindowCaption.TextAlign = ContentAlignment.MiddleLeft;

        // _btnSetActiveWindow
        _btnSetActiveWindow.AutoSize = true;
        _btnSetActiveWindow.Margin = new Padding(10, 3, 10, 3);
        _btnSetActiveWindow.Text = "設定";
        _btnSetActiveWindow.UseVisualStyleBackColor = true;
        _btnSetActiveWindow.Click += BtnSetActiveWindow_Click;

        // _lblActiveWindowHotkey
        _lblActiveWindowHotkey.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _lblActiveWindowHotkey.AutoSize = true;
        _lblActiveWindowHotkey.TextAlign = ContentAlignment.MiddleLeft;

        // _lblAreaSelectCaption
        _lblAreaSelectCaption.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _lblAreaSelectCaption.AutoSize = true;
        _lblAreaSelectCaption.Text = "範囲選択 キャプチャ:";
        _lblAreaSelectCaption.TextAlign = ContentAlignment.MiddleLeft;

        // _btnSetAreaSelect
        _btnSetAreaSelect.AutoSize = true;
        _btnSetAreaSelect.Margin = new Padding(10, 3, 10, 3);
        _btnSetAreaSelect.Text = "設定";
        _btnSetAreaSelect.UseVisualStyleBackColor = true;
        _btnSetAreaSelect.Click += BtnSetAreaSelect_Click;

        // _lblAreaSelectHotkey
        _lblAreaSelectHotkey.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _lblAreaSelectHotkey.AutoSize = true;
        _lblAreaSelectHotkey.TextAlign = ContentAlignment.MiddleLeft;

        // _btnClose
        _btnClose.AutoSize = true;
        _btnClose.DialogResult = DialogResult.OK;
        _btnClose.Margin = new Padding(10, 10, 10, 3);
        _btnClose.Text = "閉じる";
        _btnClose.UseVisualStyleBackColor = true;

        // HotkeyForm
        AcceptButton = _btnClose;
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(500, 200);
        Controls.Add(_tlpMain);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "HotkeyForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "グローバルホットキー設定";

        _tlpMain.ResumeLayout(false);
        _tlpMain.PerformLayout();
        ResumeLayout(false);
    }
}
