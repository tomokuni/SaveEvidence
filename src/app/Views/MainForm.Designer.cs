#nullable disable

namespace app.Views;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    // メニュー
    private MenuStrip _menuStrip;
    private ToolStripMenuItem _menuFile;
    private ToolStripMenuItem _menuFileSave;
    private ToolStripMenuItem _menuFileSaveFolder;
    private ToolStripMenuItem _menuFileDisplaySettings;
    private ToolStripMenuItem _menuEdit;
    private ToolStripMenuItem _menuEditUndo;
    private ToolStripMenuItem _menuEditCrop;
    private ToolStripMenuItem _menuEditAutoCrop;
    private ToolStripMenuItem _menuEditCopy;
    private ToolStripMenuItem _menuEditPaste;
    private ToolStripMenuItem _menuEditZoomIn;
    private ToolStripMenuItem _menuEditZoomOut;
    private ToolStripMenuItem _menuEditAlignLeft;
    private ToolStripMenuItem _menuEditAlignCenter;
    private ToolStripMenuItem _menuEditShowLoupe;
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
    private ToolStripStatusLabel _lblZoom;
    private ToolStripStatusLabel _lblAlign;
    private ToolStripStatusLabel _lblLoupe;
    private ToolStripStatusLabel _lblDummy;
    private ToolStripStatusLabel _lblImageSize;
    private ToolStripStatusLabel _lblSavedStatus;
    private ToolStripStatusLabel _lblSpacer;

    // ToolTip
    private ToolTip _toolTip;

    // コンテキストメニュー
    private ContextMenuStrip _contextMenuPreview;
    private ToolStripMenuItem _ctxCrop;
    private ToolStripMenuItem _ctxAutoCrop;
    private ToolStripSeparator _ctxSep0;
    private ToolStripMenuItem _ctxZoomIn;
    private ToolStripMenuItem _ctxZoomOut;
    private ToolStripSeparator _ctxSep1;
    private ToolStripMenuItem _ctxAlignLeft;
    private ToolStripMenuItem _ctxAlignCenter;
    private ToolStripSeparator _ctxSep2;
    private ToolStripMenuItem _ctxShowLoupe;
    private ContextMenuStrip _contextMenuLink;
    private ToolStripMenuItem _ctxLinkSaveFolder;
    private ToolStripMenuItem _ctxLinkFolderView;
    private ToolStripMenuItem _ctxLinkOpenExplorer;

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _menuStrip = new MenuStrip();
        _menuFile = new ToolStripMenuItem();
        _menuFileSave = new ToolStripMenuItem();
        _menuFileSaveFolder = new ToolStripMenuItem();
        _menuFileDisplaySettings = new ToolStripMenuItem();
        _menuEdit = new ToolStripMenuItem();
        _menuEditUndo = new ToolStripMenuItem();
        _menuEditCrop = new ToolStripMenuItem();
        _menuEditAutoCrop = new ToolStripMenuItem();
        _menuEditCopy = new ToolStripMenuItem();
        _menuEditPaste = new ToolStripMenuItem();
        _menuEditZoomIn = new ToolStripMenuItem();
        _menuEditZoomOut = new ToolStripMenuItem();
        _menuEditAlignLeft = new ToolStripMenuItem();
        _menuEditAlignCenter = new ToolStripMenuItem();
        _menuEditShowLoupe = new ToolStripMenuItem();
        _menuSettings = new ToolStripMenuItem();
        _menuSaveFolder = new ToolStripMenuItem();
        _menuHotKeySettings = new ToolStripMenuItem();
        _menuDisplaySettings = new ToolStripMenuItem();
        _flowToolBar = new FlowLayoutPanel();
        _btnSelectScreen = new Button();
        _btnWindowSelect = new Button();
        _btnAreaSelect = new Button();
        _cmbZoom = new ComboBox();
        _btnSave = new Button();
        _txtFileNameTemplate = new TextBox();
        _linkSaveFolder = new LinkLabel();
        _contextMenuLink = new ContextMenuStrip(components);
        _ctxLinkSaveFolder = new ToolStripMenuItem();
        _ctxLinkFolderView = new ToolStripMenuItem();
        _ctxLinkOpenExplorer = new ToolStripMenuItem();
        _btnAutoCrop = new Button();
        _btnCropMode = new Button();
        _btnCropApply = new Button();
        _btnCopy = new Button();
        _btnSaveFolder = new Button();
        _picPreview = new PictureBox();
        _picLoupe = new PictureBox();
        _statusStrip = new StatusStrip();
        _lblZoom = new ToolStripStatusLabel();
        _lblImageSize = new ToolStripStatusLabel();
        _lblSavedStatus = new ToolStripStatusLabel();
        _lblAlign = new ToolStripStatusLabel();
        _lblLoupe = new ToolStripStatusLabel();
        _lblSpacer = new ToolStripStatusLabel();
        _lblDummy = new ToolStripStatusLabel();
        _toolTip = new ToolTip(components);
        _contextMenuPreview = new ContextMenuStrip(components);
        _ctxCrop = new ToolStripMenuItem();
        _ctxAutoCrop = new ToolStripMenuItem();
        _ctxSep0 = new ToolStripSeparator();
        _ctxZoomIn = new ToolStripMenuItem();
        _ctxZoomOut = new ToolStripMenuItem();
        _ctxSep1 = new ToolStripSeparator();
        _ctxAlignLeft = new ToolStripMenuItem();
        _ctxAlignCenter = new ToolStripMenuItem();
        _ctxSep2 = new ToolStripSeparator();
        _ctxShowLoupe = new ToolStripMenuItem();
        _pnlPreview = new Panel();
        _vScroll = new VScrollBar();
        _hScroll = new HScrollBar();
        _menuStrip.SuspendLayout();
        _flowToolBar.SuspendLayout();
        _contextMenuLink.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_picPreview).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_picLoupe).BeginInit();
        _statusStrip.SuspendLayout();
        _contextMenuPreview.SuspendLayout();
        _pnlPreview.SuspendLayout();
        SuspendLayout();
        // 
        // _menuStrip
        // 
        _menuStrip.ImageScalingSize = new Size(20, 20);
        _menuStrip.Items.AddRange(new ToolStripItem[] { _menuFile, _menuEdit });
        _menuStrip.Location = new Point(0, 0);
        _menuStrip.Name = "_menuStrip";
        _menuStrip.Size = new Size(1000, 28);
        _menuStrip.TabIndex = 0;
        _menuStrip.Text = "MenuStrip";
        // 
        // _menuFile
        // 
        _menuFile.DropDownItems.AddRange(new ToolStripItem[] { _menuFileSave, _menuFileSaveFolder, _menuFileDisplaySettings });
        _menuFile.Name = "_menuFile";
        _menuFile.Size = new Size(82, 24);
        _menuFile.Text = "ファイル(&F)";
        // 
        // _menuFileSave
        // 
        _menuFileSave.Name = "_menuFileSave";
        _menuFileSave.ShortcutKeys = Keys.Control | Keys.S;
        _menuFileSave.Size = new Size(205, 26);
        _menuFileSave.Text = "保存(&S)";
        _menuFileSave.Click += MenuFileSave_Click;
        // 
        // _menuFileSaveFolder
        // 
        _menuFileSaveFolder.Name = "_menuFileSaveFolder";
        _menuFileSaveFolder.Size = new Size(205, 26);
        _menuFileSaveFolder.Text = "保存先設定(&F)...";
        _menuFileSaveFolder.Click += MenuFileSaveFolder_Click;
        // 
        // _menuFileDisplaySettings
        // 
        _menuFileDisplaySettings.Name = "_menuFileDisplaySettings";
        _menuFileDisplaySettings.Size = new Size(205, 26);
        _menuFileDisplaySettings.Text = "描画プロパティ(&D)...";
        _menuFileDisplaySettings.Click += MenuFileDisplaySettings_Click;
        // 
        // _menuEdit
        // 
        _menuEdit.DropDownItems.AddRange(new ToolStripItem[] { _menuEditUndo, _menuEditCrop, _menuEditAutoCrop, _menuEditCopy, _menuEditPaste, _menuEditZoomIn, _menuEditZoomOut, _menuEditAlignLeft, _menuEditAlignCenter, _menuEditShowLoupe });
        _menuEdit.Name = "_menuEdit";
        _menuEdit.Size = new Size(71, 24);
        _menuEdit.Text = "編集(&E)";
        // 
        // _menuEditUndo
        // 
        _menuEditUndo.Name = "_menuEditUndo";
        _menuEditUndo.ShortcutKeys = Keys.Control | Keys.Z;
        _menuEditUndo.Size = new Size(312, 26);
        _menuEditUndo.Text = "元に戻す(&U)";
        _menuEditUndo.Click += MenuEditUndo_Click;
        // 
        // _menuEditCrop
        // 
        _menuEditCrop.Name = "_menuEditCrop";
        _menuEditCrop.ShortcutKeys = Keys.Control | Keys.X;
        _menuEditCrop.Size = new Size(312, 26);
        _menuEditCrop.Text = "選択範囲を切り出し";
        _menuEditCrop.Click += MenuEditCrop_Click;
        // 
        // _menuEditAutoCrop
        // 
        _menuEditAutoCrop.Name = "_menuEditAutoCrop";
        _menuEditAutoCrop.Size = new Size(312, 26);
        _menuEditAutoCrop.Text = "自動切り出し(&A)";
        _menuEditAutoCrop.Click += MenuEditAutoCrop_Click;
        // 
        // _menuEditCopy
        // 
        _menuEditCopy.Name = "_menuEditCopy";
        _menuEditCopy.ShortcutKeys = Keys.Control | Keys.C;
        _menuEditCopy.Size = new Size(312, 26);
        _menuEditCopy.Text = "クリップボードにコピー(&C)";
        _menuEditCopy.Click += MenuEditCopy_Click;
        // 
        // _menuEditPaste
        // 
        _menuEditPaste.Name = "_menuEditPaste";
        _menuEditPaste.ShortcutKeys = Keys.Control | Keys.V;
        _menuEditPaste.Size = new Size(312, 26);
        _menuEditPaste.Text = "クリップボードから貼り付け(&P)";
        _menuEditPaste.Click += MenuEditPaste_Click;
        // 
        // _menuEditZoomIn
        // 
        _menuEditZoomIn.Name = "_menuEditZoomIn";
        _menuEditZoomIn.ShortcutKeys = Keys.Control | Keys.Oemplus;
        _menuEditZoomIn.Size = new Size(312, 26);
        _menuEditZoomIn.Text = "拡大";
        _menuEditZoomIn.Click += MenuEditZoomIn_Click;
        // 
        // _menuEditZoomOut
        // 
        _menuEditZoomOut.Name = "_menuEditZoomOut";
        _menuEditZoomOut.ShortcutKeys = Keys.Control | Keys.OemMinus;
        _menuEditZoomOut.Size = new Size(312, 26);
        _menuEditZoomOut.Text = "縮小";
        _menuEditZoomOut.Click += MenuEditZoomOut_Click;
        // 
        // _menuEditAlignLeft
        // 
        _menuEditAlignLeft.Name = "_menuEditAlignLeft";
        _menuEditAlignLeft.Size = new Size(312, 26);
        _menuEditAlignLeft.Text = "左上寄せ";
        _menuEditAlignLeft.Click += MenuEditAlignLeft_Click;
        // 
        // _menuEditAlignCenter
        // 
        _menuEditAlignCenter.Name = "_menuEditAlignCenter";
        _menuEditAlignCenter.Size = new Size(312, 26);
        _menuEditAlignCenter.Text = "中央寄せ";
        _menuEditAlignCenter.Click += MenuEditAlignCenter_Click;
        // 
        // _menuEditShowLoupe
        // 
        _menuEditShowLoupe.CheckOnClick = true;
        _menuEditShowLoupe.Name = "_menuEditShowLoupe";
        _menuEditShowLoupe.Size = new Size(312, 26);
        _menuEditShowLoupe.Text = "拡大鏡表示";
        _menuEditShowLoupe.Click += MenuEditShowLoupe_Click;
        // 
        // _menuSettings
        // 
        _menuSettings.Name = "_menuSettings";
        _menuSettings.Size = new Size(32, 19);
        // 
        // _menuSaveFolder
        // 
        _menuSaveFolder.Name = "_menuSaveFolder";
        _menuSaveFolder.Size = new Size(32, 19);
        // 
        // _menuHotKeySettings
        // 
        _menuHotKeySettings.Name = "_menuHotKeySettings";
        _menuHotKeySettings.Size = new Size(32, 19);
        // 
        // _menuDisplaySettings
        // 
        _menuDisplaySettings.Name = "_menuDisplaySettings";
        _menuDisplaySettings.Size = new Size(32, 19);
        // 
        // _flowToolBar
        // 
        _flowToolBar.AutoSize = true;
        _flowToolBar.Controls.Add(_btnSelectScreen);
        _flowToolBar.Controls.Add(_btnWindowSelect);
        _flowToolBar.Controls.Add(_btnAreaSelect);
        _flowToolBar.Controls.Add(_cmbZoom);
        _flowToolBar.Controls.Add(_btnSave);
        _flowToolBar.Controls.Add(_txtFileNameTemplate);
        _flowToolBar.Controls.Add(_linkSaveFolder);
        _flowToolBar.Dock = DockStyle.Top;
        _flowToolBar.Location = new Point(0, 28);
        _flowToolBar.Name = "_flowToolBar";
        _flowToolBar.Padding = new Padding(5);
        _flowToolBar.Size = new Size(1000, 57);
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
        // _cmbZoom
        // 
        _cmbZoom.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbZoom.Location = new Point(251, 8);
        _cmbZoom.Margin = new Padding(3, 3, 10, 3);
        _cmbZoom.Name = "_cmbZoom";
        _cmbZoom.Size = new Size(80, 28);
        _cmbZoom.TabIndex = 0;
        _cmbZoom.SelectedIndexChanged += CmbZoom_SelectedIndexChanged;
        // 
        // _btnSave
        // 
        _btnSave.AutoSize = true;
        _btnSave.Location = new Point(344, 8);
        _btnSave.Name = "_btnSave";
        _btnSave.Size = new Size(75, 30);
        _btnSave.TabIndex = 7;
        _btnSave.Text = "保存";
        _btnSave.UseVisualStyleBackColor = true;
        _btnSave.Click += BtnSave_Click;
        // 
        // _txtFileNameTemplate
        // 
        _txtFileNameTemplate.Location = new Point(425, 8);
        _txtFileNameTemplate.Name = "_txtFileNameTemplate";
        _txtFileNameTemplate.Size = new Size(250, 27);
        _txtFileNameTemplate.TabIndex = 0;
        _txtFileNameTemplate.Text = "screenshot_{date}_{time}.png";
        _txtFileNameTemplate.TextChanged += TxtFileNameTemplate_TextChanged;
        // 
        // _linkSaveFolder
        // 
        _linkSaveFolder.AutoSize = true;
        _linkSaveFolder.ContextMenuStrip = _contextMenuLink;
        _linkSaveFolder.LinkBehavior = LinkBehavior.HoverUnderline;
        _linkSaveFolder.Location = new Point(681, 8);
        _linkSaveFolder.Margin = new Padding(3);
        _linkSaveFolder.Name = "_linkSaveFolder";
        _linkSaveFolder.Size = new Size(99, 20);
        _linkSaveFolder.TabIndex = 1;
        _linkSaveFolder.TabStop = true;
        _linkSaveFolder.Text = "SaveEvidence";
        _linkSaveFolder.LinkClicked += LinkSaveFolder_LinkClicked;
        // 
        // _contextMenuLink
        // 
        _contextMenuLink.ImageScalingSize = new Size(20, 20);
        _contextMenuLink.Items.AddRange(new ToolStripItem[] { _ctxLinkSaveFolder, _ctxLinkFolderView, _ctxLinkOpenExplorer });
        _contextMenuLink.Name = "_contextMenuLink";
        _contextMenuLink.Size = new Size(244, 76);
        // 
        // _ctxLinkSaveFolder
        // 
        _ctxLinkSaveFolder.Name = "_ctxLinkSaveFolder";
        _ctxLinkSaveFolder.Size = new Size(243, 24);
        _ctxLinkSaveFolder.Text = "保存先設定";
        _ctxLinkSaveFolder.Click += MenuFileSaveFolder_Click;
        // 
        // _ctxLinkFolderView
        // 
        _ctxLinkFolderView.Name = "_ctxLinkFolderView";
        _ctxLinkFolderView.Size = new Size(243, 24);
        _ctxLinkFolderView.Text = "フォルダビューの表示";
        _ctxLinkFolderView.Click += CtxLinkFolderView_Click;
        // 
        // _ctxLinkOpenExplorer
        // 
        _ctxLinkOpenExplorer.Name = "_ctxLinkOpenExplorer";
        _ctxLinkOpenExplorer.Size = new Size(243, 24);
        _ctxLinkOpenExplorer.Text = "エクスプローラでフォルダを開く";
        _ctxLinkOpenExplorer.Click += CtxLinkOpenExplorer_Click;
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
        _picLoupe.Location = new Point(793, 321);
        _picLoupe.Name = "_picLoupe";
        _picLoupe.Size = new Size(170, 170);
        _picLoupe.TabIndex = 1;
        _picLoupe.TabStop = false;
        _picLoupe.Visible = false;
        // 
        // _statusStrip
        // 
        _statusStrip.ImageScalingSize = new Size(20, 20);
        _statusStrip.Items.AddRange(new ToolStripItem[] { _lblZoom, _lblImageSize, _lblSavedStatus, _lblSpacer, _lblAlign, _lblLoupe, _lblDummy });
        _statusStrip.Location = new Point(0, 611);
        _statusStrip.Name = "_statusStrip";
        _statusStrip.Size = new Size(1000, 24);
        _statusStrip.TabIndex = 3;
        // 
        // _lblZoom
        // 
        _lblZoom.BorderSides = ToolStripStatusLabelBorderSides.Right;
        _lblZoom.Name = "_lblZoom";
        _lblZoom.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _lblImageSize
        // 
        _lblImageSize.BorderSides = ToolStripStatusLabelBorderSides.Right;
        _lblImageSize.Name = "_lblImageSize";
        _lblImageSize.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _lblSavedStatus
        // 
        _lblSavedStatus.BorderSides = ToolStripStatusLabelBorderSides.None;
        _lblSavedStatus.Name = "_lblSavedStatus";
        _lblSavedStatus.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _lblSpacer
        // 
        _lblSpacer.Name = "_lblSpacer";
        _lblSpacer.Spring = true;
        // 
        // _lblAlign
        // 
        _lblAlign.BorderSides = ToolStripStatusLabelBorderSides.Left;
        _lblAlign.Name = "_lblAlign";
        _lblAlign.TextAlign = ContentAlignment.MiddleRight;
        // 
        // _lblLoupe
        // 
        _lblLoupe.BorderSides = ToolStripStatusLabelBorderSides.Left;
        _lblLoupe.Name = "_lblLoupe";
        _lblLoupe.AutoSize = true;
        _lblLoupe.TextAlign = ContentAlignment.MiddleRight;
        // 
        // _lblDummy
        // 
        _lblDummy.Name = "_lblDummy";
        _lblDummy.AutoSize = false;
        _lblDummy.Size = new Size(1, 18);
        // 
        // _toolTip
        // 
        _toolTip.AutoPopDelay = 5000;
        _toolTip.InitialDelay = 500;
        _toolTip.ReshowDelay = 500;
        _toolTip.ShowAlways = true;
        // 
        // _contextMenuPreview
        // 
        _contextMenuPreview.ImageScalingSize = new Size(20, 20);
        _contextMenuPreview.Items.AddRange(new ToolStripItem[] { _ctxCrop, _ctxAutoCrop, _ctxSep0, _ctxZoomIn, _ctxZoomOut, _ctxSep1, _ctxAlignLeft, _ctxAlignCenter, _ctxSep2, _ctxShowLoupe });
        _contextMenuPreview.Name = "_contextMenuPreview";
        _contextMenuPreview.Size = new Size(251, 190);
        // 
        // _ctxCrop
        // 
        _ctxCrop.Name = "_ctxCrop";
        _ctxCrop.ShortcutKeys = Keys.Control | Keys.X;
        _ctxCrop.Size = new Size(250, 24);
        _ctxCrop.Text = "選択範囲を切り出し";
        _ctxCrop.Click += MenuEditCrop_Click;
        // 
        // _ctxAutoCrop
        // 
        _ctxAutoCrop.Name = "_ctxAutoCrop";
        _ctxAutoCrop.Size = new Size(250, 24);
        _ctxAutoCrop.Text = "自動切り出し";
        _ctxAutoCrop.Click += MenuEditAutoCrop_Click;
        // 
        // _ctxSep0
        // 
        _ctxSep0.Name = "_ctxSep0";
        _ctxSep0.Size = new Size(247, 6);
        // 
        // _ctxZoomIn
        // 
        _ctxZoomIn.Name = "_ctxZoomIn";
        _ctxZoomIn.ShortcutKeys = Keys.Control | Keys.Oemplus;
        _ctxZoomIn.Size = new Size(250, 24);
        _ctxZoomIn.Text = "拡大";
        _ctxZoomIn.Click += MenuEditZoomIn_Click;
        // 
        // _ctxZoomOut
        // 
        _ctxZoomOut.Name = "_ctxZoomOut";
        _ctxZoomOut.ShortcutKeys = Keys.Control | Keys.OemMinus;
        _ctxZoomOut.Size = new Size(250, 24);
        _ctxZoomOut.Text = "縮小";
        _ctxZoomOut.Click += MenuEditZoomOut_Click;
        // 
        // _ctxSep1
        // 
        _ctxSep1.Name = "_ctxSep1";
        _ctxSep1.Size = new Size(247, 6);
        // 
        // _ctxAlignLeft
        // 
        _ctxAlignLeft.Name = "_ctxAlignLeft";
        _ctxAlignLeft.Size = new Size(250, 24);
        _ctxAlignLeft.Text = "左上寄せ";
        _ctxAlignLeft.Click += MenuEditAlignLeft_Click;
        // 
        // _ctxAlignCenter
        // 
        _ctxAlignCenter.Name = "_ctxAlignCenter";
        _ctxAlignCenter.Size = new Size(250, 24);
        _ctxAlignCenter.Text = "中央寄せ";
        _ctxAlignCenter.Click += MenuEditAlignCenter_Click;
        // 
        // _ctxSep2
        // 
        _ctxSep2.Name = "_ctxSep2";
        _ctxSep2.Size = new Size(247, 6);
        // 
        // _ctxShowLoupe
        // 
        _ctxShowLoupe.CheckOnClick = true;
        _ctxShowLoupe.Name = "_ctxShowLoupe";
        _ctxShowLoupe.Size = new Size(250, 24);
        _ctxShowLoupe.Text = "拡大鏡表示";
        _ctxShowLoupe.Click += MenuEditShowLoupe_Click;
        // 
        // _pnlPreview
        // 
        _pnlPreview.BackColor = SystemColors.ControlDark;
        _pnlPreview.BorderStyle = BorderStyle.FixedSingle;
        _pnlPreview.ContextMenuStrip = _contextMenuPreview;
        _pnlPreview.Controls.Add(_picPreview);
        _pnlPreview.Controls.Add(_picLoupe);
        _pnlPreview.Dock = DockStyle.Fill;
        _pnlPreview.Location = new Point(0, 85);
        _pnlPreview.Name = "_pnlPreview";
        _pnlPreview.Size = new Size(979, 505);
        _pnlPreview.TabIndex = 2;
        // 
        // _vScroll
        // 
        _vScroll.Dock = DockStyle.Right;
        _vScroll.Location = new Point(979, 85);
        _vScroll.Name = "_vScroll";
        _vScroll.Size = new Size(21, 505);
        _vScroll.TabIndex = 3;
        _vScroll.Scroll += VScroll_Scroll;
        // 
        // _hScroll
        // 
        _hScroll.Dock = DockStyle.Bottom;
        _hScroll.Location = new Point(0, 590);
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
        _contextMenuLink.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_picPreview).EndInit();
        ((System.ComponentModel.ISupportInitialize)_picLoupe).EndInit();
        _statusStrip.ResumeLayout(false);
        _statusStrip.PerformLayout();
        _contextMenuPreview.ResumeLayout(false);
        _pnlPreview.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }
}
