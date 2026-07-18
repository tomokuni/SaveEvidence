#nullable disable

namespace app.Views;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    // メニュー
    private MenuStrip _menuStrip;
    private ToolStripMenuItem _menuSettings;
    private ToolStripMenuItem _menuSaveFolder;
    private ToolStripMenuItem _menuHotKeySettings;

    // ツールバー
    private FlowLayoutPanel _flowToolBar;
    private Button _btnSelectScreen;
    private Button _btnWindowSelect;
    private Button _btnAreaSelect;
    private Button _btnAutoCrop;
    private Button _btnCopy;
    private Button _btnSave;
    private TextBox _txtFileNameTemplate;
    private Button _btnSaveFolder;
    private LinkLabel _linkSaveFolder;

    // プレビュー
    private PictureBox _picPreview;

    // ステータスバー
    private StatusStrip _statusStrip;
    private ToolStripStatusLabel _lblStatus;

    // ToolTip
    private ToolTip _toolTip;

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        _menuStrip = new MenuStrip();
        _menuSettings = new ToolStripMenuItem();
        _menuSaveFolder = new ToolStripMenuItem();
        _menuHotKeySettings = new ToolStripMenuItem();

        _flowToolBar = new FlowLayoutPanel();
        _btnSelectScreen = new Button();
        _btnWindowSelect = new Button();
        _btnAreaSelect = new Button();
        _btnAutoCrop = new Button();
        _btnCopy = new Button();
        _btnSave = new Button();
        _txtFileNameTemplate = new TextBox();
        _btnSaveFolder = new Button();
        _linkSaveFolder = new LinkLabel();

        _picPreview = new PictureBox();

        _statusStrip = new StatusStrip();
        _lblStatus = new ToolStripStatusLabel();

        _toolTip = new ToolTip(components);

        _menuStrip.SuspendLayout();
        _flowToolBar.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_picPreview).BeginInit();
        _statusStrip.SuspendLayout();
        SuspendLayout();

        // _menuStrip
        _menuStrip.Items.AddRange(new ToolStripItem[] { _menuSettings });
        _menuStrip.Location = new Point(0, 0);
        _menuStrip.Name = "_menuStrip";
        _menuStrip.Size = new Size(1000, 33);
        _menuStrip.TabIndex = 0;
        _menuStrip.Text = "MenuStrip";

        // _menuSettings
        _menuSettings.DropDownItems.AddRange(new ToolStripItem[] { _menuSaveFolder, _menuHotKeySettings });
        _menuSettings.Name = "_menuSettings";
        _menuSettings.Size = new Size(80, 29);
        _menuSettings.Text = "設定(&S)";

        // _menuSaveFolder
        _menuSaveFolder.Name = "_menuSaveFolder";
        _menuSaveFolder.Size = new Size(270, 34);
        _menuSaveFolder.Text = "保存先のフォルダ(&F)...";
        _menuSaveFolder.Click += MenuSaveFolder_Click;

        // _menuHotKeySettings
        _menuHotKeySettings.Name = "_menuHotKeySettings";
        _menuHotKeySettings.Size = new Size(270, 34);
        _menuHotKeySettings.Text = "グローバルホットキー(&H)...";
        _menuHotKeySettings.Click += MenuHotKeySettings_Click;

        // _flowToolBar
        _flowToolBar.AutoSize = true;
        _flowToolBar.Controls.Add(_btnSelectScreen);
        _flowToolBar.Controls.Add(_btnWindowSelect);
        _flowToolBar.Controls.Add(_btnAreaSelect);
        _flowToolBar.Controls.Add(_btnAutoCrop);
        _flowToolBar.Controls.Add(_btnCopy);
        _flowToolBar.Controls.Add(_btnSave);
        _flowToolBar.Controls.Add(_txtFileNameTemplate);
        _flowToolBar.Controls.Add(_btnSaveFolder);
        _flowToolBar.Controls.Add(_linkSaveFolder);
        _flowToolBar.Dock = DockStyle.Top;
        _flowToolBar.Location = new Point(0, 33);
        _flowToolBar.Name = "_flowToolBar";
        _flowToolBar.Padding = new Padding(5, 5, 5, 5);
        _flowToolBar.Size = new Size(1000, 50);
        _flowToolBar.TabIndex = 1;

        // _btnSelectScreen
        _btnSelectScreen.AutoSize = true;
        _btnSelectScreen.Margin = new Padding(3, 3, 3, 3);
        _btnSelectScreen.Text = "スクリーン選択";
        _btnSelectScreen.UseVisualStyleBackColor = true;
        _btnSelectScreen.Click += BtnSelectScreen_Click;

        // _btnWindowSelect
        _btnWindowSelect.AutoSize = true;
        _btnWindowSelect.Margin = new Padding(3, 3, 3, 3);
        _btnWindowSelect.Text = "ウィンドウ選択";
        _btnWindowSelect.UseVisualStyleBackColor = true;
        _btnWindowSelect.Click += BtnWindowSelect_Click;

        // _btnAreaSelect
        _btnAreaSelect.AutoSize = true;
        _btnAreaSelect.Margin = new Padding(3, 3, 3, 3);
        _btnAreaSelect.Text = "範囲選択";
        _btnAreaSelect.UseVisualStyleBackColor = true;
        _btnAreaSelect.Click += BtnAreaSelect_Click;

        // _btnAutoCrop
        _btnAutoCrop.AutoSize = true;
        _btnAutoCrop.Margin = new Padding(3, 3, 3, 3);
        _btnAutoCrop.Text = "ウィンドウ自動切出し";
        _btnAutoCrop.UseVisualStyleBackColor = true;
        _btnAutoCrop.Click += BtnAutoCrop_Click;

        // _btnCopy
        _btnCopy.AutoSize = true;
        _btnCopy.Margin = new Padding(3, 3, 3, 3);
        _btnCopy.Text = "クリップボードにコピー";
        _btnCopy.UseVisualStyleBackColor = true;
        _btnCopy.Click += BtnCopy_Click;

        // _btnSave
        _btnSave.AutoSize = true;
        _btnSave.Margin = new Padding(3, 3, 3, 3);
        _btnSave.Text = "保存";
        _btnSave.UseVisualStyleBackColor = true;
        _btnSave.Click += BtnSave_Click;

        // _txtFileNameTemplate
        _txtFileNameTemplate.Location = new Point(3, 3);
        _txtFileNameTemplate.Margin = new Padding(3, 3, 3, 3);
        _txtFileNameTemplate.Name = "_txtFileNameTemplate";
        _txtFileNameTemplate.Size = new Size(250, 31);
        _txtFileNameTemplate.TabIndex = 0;
        _txtFileNameTemplate.Text = "screenshot_{date}_{time}.png";
        _txtFileNameTemplate.TextChanged += TxtFileNameTemplate_TextChanged;

        // _btnSaveFolder
        _btnSaveFolder.AutoSize = true;
        _btnSaveFolder.Margin = new Padding(3, 3, 3, 3);
        _btnSaveFolder.Text = "保存先";
        _btnSaveFolder.UseVisualStyleBackColor = true;
        _btnSaveFolder.Click += BtnSaveFolder_Click;

        // _linkSaveFolder
        _linkSaveFolder.AutoSize = true;
        _linkSaveFolder.LinkBehavior = LinkBehavior.HoverUnderline;
        _linkSaveFolder.Location = new Point(3, 3);
        _linkSaveFolder.Margin = new Padding(3, 3, 3, 3);
        _linkSaveFolder.Name = "_linkSaveFolder";
        _linkSaveFolder.Size = new Size(100, 25);
        _linkSaveFolder.TabIndex = 1;
        _linkSaveFolder.TabStop = true;
        _linkSaveFolder.Text = "SaveEvidence";
        _linkSaveFolder.LinkClicked += LinkSaveFolder_LinkClicked;

        // _picPreview
        _picPreview.BackColor = SystemColors.ControlDark;
        _picPreview.BorderStyle = BorderStyle.FixedSingle;
        _picPreview.Dock = DockStyle.Fill;
        _picPreview.Location = new Point(0, 83);
        _picPreview.Name = "_picPreview";
        _picPreview.Size = new Size(1000, 520);
        _picPreview.SizeMode = PictureBoxSizeMode.Zoom;
        _picPreview.TabIndex = 2;
        _picPreview.TabStop = false;

        // _statusStrip
        _statusStrip.Items.AddRange(new ToolStripItem[] { _lblStatus });
        _statusStrip.Location = new Point(0, 603);
        _statusStrip.Name = "_statusStrip";
        _statusStrip.Size = new Size(1000, 32);
        _statusStrip.TabIndex = 3;

        // _lblStatus
        _lblStatus.Name = "_lblStatus";
        _lblStatus.Size = new Size(985, 25);
        _lblStatus.Text = "画像なし | 保存ファイル名: screenshot_{date}_{time}.png";

        // _toolTip
        _toolTip.AutoPopDelay = 5000;
        _toolTip.InitialDelay = 500;
        _toolTip.ReshowDelay = 500;
        _toolTip.ShowAlways = true;

        // MainForm
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1000, 635);
        Controls.Add(_picPreview);
        Controls.Add(_flowToolBar);
        Controls.Add(_menuStrip);
        Controls.Add(_statusStrip);
        MainMenuStrip = _menuStrip;
        MinimumSize = new Size(700, 500);
        Name = "MainForm";
        Text = "SaveEvidence - 画面キャプチャツール";

        _menuStrip.ResumeLayout(false);
        _menuStrip.PerformLayout();
        _flowToolBar.ResumeLayout(false);
        _flowToolBar.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_picPreview).EndInit();
        _statusStrip.ResumeLayout(false);
        _statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
