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
    private ToolStripMenuItem _menuDisplaySettings;

    // ツールバー
    private FlowLayoutPanel _flowToolBar;
    private Button _btnSelectScreen;
    private Button _btnWindowSelect;
    private Button _btnAreaSelect;
    private Button _btnAutoCrop;
    private Button _btnCropMode;
    private Button _btnCropApply;
    private Button _btnCopy;
    private Button _btnSave;
    private TextBox _txtFileNameTemplate;
    private Button _btnSaveFolder;
    private LinkLabel _linkSaveFolder;

    // 拡大率
    private ComboBox _cmbZoom;

    // プレビュー（Panel でラップ）
    private Panel _pnlPreview;
    private PictureBox _picPreview;
    private PictureBox _picLoupe;
    private VScrollBar _vScroll;
    private HScrollBar _hScroll;

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
        _menuDisplaySettings = new ToolStripMenuItem();
        _flowToolBar = new FlowLayoutPanel();
        _btnSelectScreen = new Button();
        _btnWindowSelect = new Button();
        _btnAreaSelect = new Button();
        _btnAutoCrop = new Button();
        _btnCropMode = new Button();
        _btnCropApply = new Button();
        _btnCopy = new Button();
        _btnSave = new Button();
        _txtFileNameTemplate = new TextBox();
        _btnSaveFolder = new Button();
        _linkSaveFolder = new LinkLabel();
        _cmbZoom = new ComboBox();
        _picPreview = new PictureBox();
        _picLoupe = new PictureBox();
        _statusStrip = new StatusStrip();
        _lblStatus = new ToolStripStatusLabel();
        _toolTip = new ToolTip(components);
        _pnlPreview = new Panel();
        _vScroll = new VScrollBar();
        _hScroll = new HScrollBar();
        _menuStrip.SuspendLayout();
        _flowToolBar.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_picPreview).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_picLoupe).BeginInit();
        _statusStrip.SuspendLayout();
        _pnlPreview.SuspendLayout();
        SuspendLayout();
        // 
        // _menuStrip
        // 
        _menuStrip.ImageScalingSize = new Size(20, 20);
        _menuStrip.Items.AddRange(new ToolStripItem[] { _menuSettings });
        _menuStrip.Location = new Point(0, 0);
        _menuStrip.Name = "_menuStrip";
        _menuStrip.Size = new Size(1000, 28);
        _menuStrip.TabIndex = 0;
        _menuStrip.Text = "MenuStrip";
        // 
        // _menuSettings
        // 
        _menuSettings.DropDownItems.AddRange(new ToolStripItem[] { _menuSaveFolder, _menuHotKeySettings, _menuDisplaySettings });
        _menuSettings.Name = "_menuSettings";
        _menuSettings.Size = new Size(71, 24);
        _menuSettings.Text = "設定(&S)";
        // 
        // _menuSaveFolder
        // 
        _menuSaveFolder.Name = "_menuSaveFolder";
        _menuSaveFolder.Size = new Size(231, 26);
        _menuSaveFolder.Text = "保存先のフォルダ(&F)...";
        _menuSaveFolder.Click += MenuSaveFolder_Click;
        // 
        // _menuHotKeySettings
        // 
        _menuHotKeySettings.Name = "_menuHotKeySettings";
        _menuHotKeySettings.Size = new Size(231, 26);
        _menuHotKeySettings.Text = "グローバルホットキー(&H)...";
        _menuHotKeySettings.Click += MenuHotKeySettings_Click;
        // 
        // _menuDisplaySettings
        // 
        _menuDisplaySettings.Name = "_menuDisplaySettings";
        _menuDisplaySettings.Size = new Size(231, 26);
        _menuDisplaySettings.Text = "表示設定(&D)...";
        _menuDisplaySettings.Click += MenuDisplaySettings_Click;
        // 
        // _flowToolBar
        // 
        _flowToolBar.AutoSize = true;
        _flowToolBar.Controls.Add(_btnSelectScreen);
        _flowToolBar.Controls.Add(_btnWindowSelect);
        _flowToolBar.Controls.Add(_btnAreaSelect);
        _flowToolBar.Controls.Add(_btnAutoCrop);
        _flowToolBar.Controls.Add(_btnCropMode);
        _flowToolBar.Controls.Add(_btnCropApply);
        _flowToolBar.Controls.Add(_btnCopy);
        _flowToolBar.Controls.Add(_btnSave);
        _flowToolBar.Controls.Add(_txtFileNameTemplate);
        _flowToolBar.Controls.Add(_btnSaveFolder);
        _flowToolBar.Controls.Add(_linkSaveFolder);
        _flowToolBar.Controls.Add(_cmbZoom);
        _flowToolBar.Dock = DockStyle.Top;
        _flowToolBar.Location = new Point(0, 28);
        _flowToolBar.Name = "_flowToolBar";
        _flowToolBar.Padding = new Padding(5);
        _flowToolBar.Size = new Size(1000, 93);
        _flowToolBar.TabIndex = 1;
        // 
        // _btnSelectScreen
        // 
        _btnSelectScreen.AutoSize = true;
        _btnSelectScreen.FlatStyle = FlatStyle.System;
        _btnSelectScreen.Font = new Font("Segoe UI Symbol", 14F);
        _btnSelectScreen.Location = new Point(8, 8);
        _btnSelectScreen.Name = "_btnSelectScreen";
        _btnSelectScreen.Size = new Size(75, 41);
        _btnSelectScreen.TabIndex = 0;
        _btnSelectScreen.Text = "⊞";
        _toolTip.SetToolTip(_btnSelectScreen, "スクリーン選択");
        _btnSelectScreen.UseVisualStyleBackColor = true;
        _btnSelectScreen.Click += BtnSelectScreen_Click;
        // 
        // _btnWindowSelect
        // 
        _btnWindowSelect.AutoSize = true;
        _btnWindowSelect.FlatStyle = FlatStyle.System;
        _btnWindowSelect.Font = new Font("Segoe UI Symbol", 14F);
        _btnWindowSelect.Location = new Point(89, 8);
        _btnWindowSelect.Name = "_btnWindowSelect";
        _btnWindowSelect.Size = new Size(75, 41);
        _btnWindowSelect.TabIndex = 1;
        _btnWindowSelect.Text = "⊟";
        _toolTip.SetToolTip(_btnWindowSelect, "ウィンドウ選択");
        _btnWindowSelect.UseVisualStyleBackColor = true;
        _btnWindowSelect.Click += BtnWindowSelect_Click;
        // 
        // _btnAreaSelect
        // 
        _btnAreaSelect.AutoSize = true;
        _btnAreaSelect.FlatStyle = FlatStyle.System;
        _btnAreaSelect.Font = new Font("Segoe UI Symbol", 14F);
        _btnAreaSelect.Location = new Point(170, 8);
        _btnAreaSelect.Name = "_btnAreaSelect";
        _btnAreaSelect.Size = new Size(75, 41);
        _btnAreaSelect.TabIndex = 2;
        _btnAreaSelect.Text = "⊡";
        _toolTip.SetToolTip(_btnAreaSelect, "範囲選択");
        _btnAreaSelect.UseVisualStyleBackColor = true;
        _btnAreaSelect.Click += BtnAreaSelect_Click;
        // 
        // _btnAutoCrop
        // 
        _btnAutoCrop.AutoSize = true;
        _btnAutoCrop.Location = new Point(251, 8);
        _btnAutoCrop.Name = "_btnAutoCrop";
        _btnAutoCrop.Size = new Size(89, 30);
        _btnAutoCrop.TabIndex = 3;
        _btnAutoCrop.Text = "自動切出し";
        _btnAutoCrop.UseVisualStyleBackColor = true;
        _btnAutoCrop.Click += BtnAutoCrop_Click;
        // 
        // _btnCropMode
        // 
        _btnCropMode.AutoSize = true;
        _btnCropMode.Location = new Point(346, 8);
        _btnCropMode.Name = "_btnCropMode";
        _btnCropMode.Size = new Size(89, 30);
        _btnCropMode.TabIndex = 4;
        _btnCropMode.Text = "切出し範囲";
        _btnCropMode.UseVisualStyleBackColor = true;
        _btnCropMode.Click += BtnCropMode_Click;
        // 
        // _btnCropApply
        // 
        _btnCropApply.AutoSize = true;
        _btnCropApply.Location = new Point(441, 8);
        _btnCropApply.Name = "_btnCropApply";
        _btnCropApply.Size = new Size(89, 30);
        _btnCropApply.TabIndex = 5;
        _btnCropApply.Text = "切出し確定";
        _btnCropApply.UseVisualStyleBackColor = true;
        _btnCropApply.Click += BtnCropApply_Click;
        // 
        // _btnCopy
        // 
        _btnCopy.AutoSize = true;
        _btnCopy.Location = new Point(536, 8);
        _btnCopy.Name = "_btnCopy";
        _btnCopy.Size = new Size(138, 30);
        _btnCopy.TabIndex = 6;
        _btnCopy.Text = "クリップボードにコピー";
        _btnCopy.UseVisualStyleBackColor = true;
        _btnCopy.Click += BtnCopy_Click;
        // 
        // _btnSave
        // 
        _btnSave.AutoSize = true;
        _btnSave.Location = new Point(680, 8);
        _btnSave.Name = "_btnSave";
        _btnSave.Size = new Size(75, 30);
        _btnSave.TabIndex = 7;
        _btnSave.Text = "保存";
        _btnSave.UseVisualStyleBackColor = true;
        _btnSave.Click += BtnSave_Click;
        // 
        // _txtFileNameTemplate
        // 
        _txtFileNameTemplate.Location = new Point(8, 55);
        _txtFileNameTemplate.Name = "_txtFileNameTemplate";
        _txtFileNameTemplate.Size = new Size(250, 27);
        _txtFileNameTemplate.TabIndex = 0;
        _txtFileNameTemplate.Text = "screenshot_{date}_{time}.png";
        _txtFileNameTemplate.TextChanged += TxtFileNameTemplate_TextChanged;
        // 
        // _btnSaveFolder
        // 
        _btnSaveFolder.AutoSize = true;
        _btnSaveFolder.Location = new Point(264, 55);
        _btnSaveFolder.Name = "_btnSaveFolder";
        _btnSaveFolder.Size = new Size(75, 30);
        _btnSaveFolder.TabIndex = 8;
        _btnSaveFolder.Text = "保存先";
        _btnSaveFolder.UseVisualStyleBackColor = true;
        _btnSaveFolder.Click += BtnSaveFolder_Click;
        // 
        // _linkSaveFolder
        // 
        _linkSaveFolder.AutoSize = true;
        _linkSaveFolder.LinkBehavior = LinkBehavior.HoverUnderline;
        _linkSaveFolder.Location = new Point(345, 55);
        _linkSaveFolder.Margin = new Padding(3);
        _linkSaveFolder.Name = "_linkSaveFolder";
        _linkSaveFolder.Size = new Size(99, 20);
        _linkSaveFolder.TabIndex = 1;
        _linkSaveFolder.TabStop = true;
        _linkSaveFolder.Text = "SaveEvidence";
        _linkSaveFolder.LinkClicked += LinkSaveFolder_LinkClicked;
        // 
        // _cmbZoom
        // 
        _cmbZoom.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbZoom.Location = new Point(450, 55);
        _cmbZoom.Margin = new Padding(3, 3, 10, 3);
        _cmbZoom.Name = "_cmbZoom";
        _cmbZoom.Size = new Size(80, 28);
        _cmbZoom.TabIndex = 0;
        _cmbZoom.SelectedIndexChanged += CmbZoom_SelectedIndexChanged;
        // 
        // _picPreview
        // 
        _picPreview.BackColor = SystemColors.ControlDark;
        _picPreview.Location = new Point(0, 0);
        _picPreview.Name = "_picPreview";
        _picPreview.Size = new Size(100, 100);
        _picPreview.SizeMode = PictureBoxSizeMode.Zoom;
        _picPreview.TabIndex = 0;
        _picPreview.TabStop = false;
        // 
        // _picLoupe
        // 
        _picLoupe.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _picLoupe.BackColor = Color.Transparent;
        _picLoupe.Enabled = false;
        _picLoupe.Location = new Point(793, 283);
        _picLoupe.Name = "_picLoupe";
        _picLoupe.Size = new Size(170, 170);
        _picLoupe.TabIndex = 1;
        _picLoupe.TabStop = false;
        _picLoupe.Visible = false;
        // 
        // _statusStrip
        // 
        _statusStrip.ImageScalingSize = new Size(20, 20);
        _statusStrip.Items.AddRange(new ToolStripItem[] { _lblStatus });
        _statusStrip.Location = new Point(0, 609);
        _statusStrip.Name = "_statusStrip";
        _statusStrip.Size = new Size(1000, 26);
        _statusStrip.TabIndex = 3;
        // 
        // _lblStatus
        // 
        _lblStatus.Name = "_lblStatus";
        _lblStatus.Size = new Size(359, 20);
        _lblStatus.Text = "画像なし | 保存ファイル名: screenshot_{date}_{time}.png";
        // 
        // _toolTip
        // 
        _toolTip.AutoPopDelay = 5000;
        _toolTip.InitialDelay = 500;
        _toolTip.ReshowDelay = 500;
        _toolTip.ShowAlways = true;
        // 
        // _pnlPreview
        // 
        _pnlPreview.BackColor = SystemColors.ControlDark;
        _pnlPreview.BorderStyle = BorderStyle.FixedSingle;
        _pnlPreview.Controls.Add(_picPreview);
        _pnlPreview.Controls.Add(_picLoupe);
        _pnlPreview.Dock = DockStyle.Fill;
        _pnlPreview.Location = new Point(0, 121);
        _pnlPreview.Name = "_pnlPreview";
        _pnlPreview.Size = new Size(979, 467);
        _pnlPreview.TabIndex = 2;
        // 
        // _vScroll
        // 
        _vScroll.Dock = DockStyle.Right;
        _vScroll.Location = new Point(979, 121);
        _vScroll.Name = "_vScroll";
        _vScroll.Size = new Size(21, 467);
        _vScroll.TabIndex = 3;
        _vScroll.Scroll += VScroll_Scroll;
        // 
        // _hScroll
        // 
        _hScroll.Dock = DockStyle.Bottom;
        _hScroll.Location = new Point(0, 588);
        _hScroll.Name = "_hScroll";
        _hScroll.Size = new Size(1000, 21);
        _hScroll.TabIndex = 4;
        _hScroll.Scroll += HScroll_Scroll;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1000, 635);
        Controls.Add(_pnlPreview);
        Controls.Add(_vScroll);
        Controls.Add(_hScroll);
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
        ((System.ComponentModel.ISupportInitialize)_picLoupe).EndInit();
        _statusStrip.ResumeLayout(false);
        _statusStrip.PerformLayout();
        _pnlPreview.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }
}
