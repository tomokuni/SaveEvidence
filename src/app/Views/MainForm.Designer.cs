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
    private ToolStripMenuItem _menuFileCopyFolderPath;
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
    private ToolStripMenuItem _menuView;
    private ToolStripMenuItem _menuViewLoupeHide;
    private ToolStripMenuItem _menuViewLoupeAuto;
    private ToolStripMenuItem _menuDisplaySettings;
    private ToolStripMenuItem _menuCapture;
    private ToolStripMenuItem _menuCaptureScreen;
    private ToolStripMenuItem _menuCaptureWindow;
    private ToolStripMenuItem _menuCaptureArea;

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

    // プレビュー（Panel でラップ）
    private Panel _pnlPreview;
    private PictureBox _picPreview;
    private PictureBox _picLoupe;
    private VScrollBar _vScroll;
    private HScrollBar _hScroll;

    // ステータスバー
    private StatusStrip _statusStrip;
    private ToolStripStatusLabel _lblZoom;
    private ToolStripStatusLabel _lblFolderLink;
    private ToolStripStatusLabel _lblAlign;
    private ToolStripStatusLabel _lblLoupe;
    private ToolStripStatusLabel _lblDummy;
    private ToolStripStatusLabel _lblImageSize;
    private ToolStripStatusLabel _lblSavedStatus;

    // ToolTip
    private ToolTip _toolTip;

    // コンテキストメニュー
    private ContextMenuStrip _contextMenuPreview;
    private ToolStripMenuItem _ctxCrop;
    private ToolStripMenuItem _ctxAutoCrop;
    private ToolStripMenuItem _ctxCopy;
    private ToolStripMenuItem _ctxPaste;
    private ToolStripSeparator _ctxSep0;
    private ToolStripMenuItem _ctxPZoomIn;
    private ToolStripMenuItem _ctxPZoomOut;
    private ToolStripMenuItem _ctxPZoomAuto;
    private ToolStripMenuItem _ctxZoomIn;
    private ToolStripMenuItem _ctxZoomOut;
    // コンテキストメニュー（拡大率）
    private ContextMenuStrip _contextMenuZoom;
    private ToolStripMenuItem _ctxZoomAuto;
    private ToolStripMenuItem _ctxZoom25;
    private ToolStripMenuItem _ctxZoom33;
    private ToolStripMenuItem _ctxZoom50;
    private ToolStripMenuItem _ctxZoom67;
    private ToolStripMenuItem _ctxZoom75;
    private ToolStripMenuItem _ctxZoom80;
    private ToolStripMenuItem _ctxZoom90;
    private ToolStripMenuItem _ctxZoom100;
    private ToolStripMenuItem _ctxZoom110;
    private ToolStripMenuItem _ctxZoom125;
    private ToolStripMenuItem _ctxZoom150;
    private ToolStripMenuItem _ctxZoom175;
    private ToolStripMenuItem _ctxZoom200;
    private ToolStripMenuItem _ctxZoom250;
    private ToolStripMenuItem _ctxZoom300;
    private ToolStripMenuItem _ctxZoom400;
    private ToolStripMenuItem _ctxZoom500;
    // コンテキストメニュー（配置）
    private ContextMenuStrip _contextMenuAlign;
    private ToolStripMenuItem _ctxAlignLeft;
    private ToolStripMenuItem _ctxAlignCenter;
    // コンテキストメニュー（ルーペ）
    private ContextMenuStrip _contextMenuLoupe;
    private ToolStripMenuItem _ctxLoupeHide;
    private ToolStripMenuItem _ctxShowLoupe;
    private ToolStripMenuItem _ctxLoupeAuto;
    private ContextMenuStrip _contextMenuLink;
    private ToolStripMenuItem _ctxLinkSaveFolder;
    private ToolStripMenuItem _ctxLinkFolderView;
    private ToolStripMenuItem _ctxLinkOpenExplorer;
    private ToolStripMenuItem _ctxLinkCopyFolderPath;

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _menuStrip = new MenuStrip();
        _menuFile = new ToolStripMenuItem();
        _menuFileSave = new ToolStripMenuItem();
        toolStripSeparator6 = new ToolStripSeparator();
        _menuFileSaveFolder = new ToolStripMenuItem();
        toolStripSeparator9 = new ToolStripSeparator();
        _menuFolderView = new ToolStripMenuItem();
        _menuOpenExplorer = new ToolStripMenuItem();
        _menuFileCopyFolderPath = new ToolStripMenuItem();
        toolStripSeparator3 = new ToolStripSeparator();
        _menuDisplaySettings = new ToolStripMenuItem();
        _menuEdit = new ToolStripMenuItem();
        _menuEditUndo = new ToolStripMenuItem();
        toolStripSeparator4 = new ToolStripSeparator();
        _menuEditAutoCrop = new ToolStripMenuItem();
        _menuEditCrop = new ToolStripMenuItem();
        toolStripSeparator5 = new ToolStripSeparator();
        _menuEditCopy = new ToolStripMenuItem();
        _menuEditPaste = new ToolStripMenuItem();
        _menuCapture = new ToolStripMenuItem();
        _menuCaptureScreen = new ToolStripMenuItem();
        _menuCaptureWindow = new ToolStripMenuItem();
        _menuCaptureArea = new ToolStripMenuItem();
        _menuView = new ToolStripMenuItem();
        _menuEditZoomIn = new ToolStripMenuItem();
        _menuEditZoomOut = new ToolStripMenuItem();
        toolStripSeparator1 = new ToolStripSeparator();
        _menuEditAlignLeft = new ToolStripMenuItem();
        _menuEditAlignCenter = new ToolStripMenuItem();
        toolStripSeparator2 = new ToolStripSeparator();
        _menuViewLoupeHide = new ToolStripMenuItem();
        _menuEditShowLoupe = new ToolStripMenuItem();
        _menuViewLoupeAuto = new ToolStripMenuItem();
        _flowToolBar = new FlowLayoutPanel();
        _btnSelectScreen = new Button();
        _btnWindowSelect = new Button();
        _btnAreaSelect = new Button();
        _btnSave = new Button();
        _txtFileNameTemplate = new TextBox();
        _contextMenuLink = new ContextMenuStrip(components);
        _ctxLinkSaveFolder = new ToolStripMenuItem();
        _ctxLinkFolderView = new ToolStripMenuItem();
        _ctxLinkOpenExplorer = new ToolStripMenuItem();
        _ctxLinkCopyFolderPath = new ToolStripMenuItem();
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
        _lblFolderLink = new ToolStripStatusLabel();
        _lblAlign = new ToolStripStatusLabel();
        _lblLoupe = new ToolStripStatusLabel();
        _lblDummy = new ToolStripStatusLabel();
        _toolTip = new ToolTip(components);
        _contextMenuPreview = new ContextMenuStrip(components);
        _ctxAutoCrop = new ToolStripMenuItem();
        _ctxCrop = new ToolStripMenuItem();
        toolStripSeparator8 = new ToolStripSeparator();
        _ctxCopy = new ToolStripMenuItem();
        _ctxPaste = new ToolStripMenuItem();
        _ctxSep0 = new ToolStripSeparator();
        _ctxPZoomAuto = new ToolStripMenuItem();
        _ctxPZoomIn = new ToolStripMenuItem();
        _ctxPZoomOut = new ToolStripMenuItem();
        _ctxZoomIn = new ToolStripMenuItem();
        _ctxZoomOut = new ToolStripMenuItem();
        _contextMenuZoom = new ContextMenuStrip(components);
        toolStripSeparator7 = new ToolStripSeparator();
        _ctxZoomAuto = new ToolStripMenuItem();
        _ctxZoom25 = new ToolStripMenuItem();
        _ctxZoom33 = new ToolStripMenuItem();
        _ctxZoom50 = new ToolStripMenuItem();
        _ctxZoom67 = new ToolStripMenuItem();
        _ctxZoom75 = new ToolStripMenuItem();
        _ctxZoom80 = new ToolStripMenuItem();
        _ctxZoom90 = new ToolStripMenuItem();
        _ctxZoom100 = new ToolStripMenuItem();
        _ctxZoom110 = new ToolStripMenuItem();
        _ctxZoom125 = new ToolStripMenuItem();
        _ctxZoom150 = new ToolStripMenuItem();
        _ctxZoom175 = new ToolStripMenuItem();
        _ctxZoom200 = new ToolStripMenuItem();
        _ctxZoom250 = new ToolStripMenuItem();
        _ctxZoom300 = new ToolStripMenuItem();
        _ctxZoom400 = new ToolStripMenuItem();
        _ctxZoom500 = new ToolStripMenuItem();
        _contextMenuAlign = new ContextMenuStrip(components);
        _ctxAlignLeft = new ToolStripMenuItem();
        _ctxAlignCenter = new ToolStripMenuItem();
        _contextMenuLoupe = new ContextMenuStrip(components);
        _ctxLoupeHide = new ToolStripMenuItem();
        _ctxShowLoupe = new ToolStripMenuItem();
        _ctxLoupeAuto = new ToolStripMenuItem();
        _pnlPreview = new Panel();
        _vScroll = new VScrollBar();
        _hScroll = new HScrollBar();
        toolStripSeparator10 = new ToolStripSeparator();
        _menuStrip.SuspendLayout();
        _flowToolBar.SuspendLayout();
        _contextMenuLink.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_picPreview).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_picLoupe).BeginInit();
        _statusStrip.SuspendLayout();
        _contextMenuPreview.SuspendLayout();
        _contextMenuZoom.SuspendLayout();
        _contextMenuAlign.SuspendLayout();
        _contextMenuLoupe.SuspendLayout();
        _pnlPreview.SuspendLayout();
        SuspendLayout();
        // 
        // _menuStrip
        // 
        _menuStrip.ImageScalingSize = new Size(20, 20);
        _menuStrip.Items.AddRange(new ToolStripItem[] { _menuFile, _menuEdit, _menuCapture, _menuView });
        _menuStrip.Location = new Point(0, 0);
        _menuStrip.Name = "_menuStrip";
        _menuStrip.Size = new Size(1000, 28);
        _menuStrip.TabIndex = 0;
        _menuStrip.Text = "MenuStrip";
        // 
        // _menuFile
        // 
        _menuFile.DropDownItems.AddRange(new ToolStripItem[] { _menuFileSave, toolStripSeparator6, _menuFileSaveFolder, toolStripSeparator9, _menuFolderView, _menuOpenExplorer, _menuFileCopyFolderPath, toolStripSeparator3, _menuDisplaySettings });
        _menuFile.Name = "_menuFile";
        _menuFile.Size = new Size(82, 24);
        _menuFile.Text = "ファイル(&F)";
        // 
        // _menuFileSave
        // 
        _menuFileSave.Name = "_menuFileSave";
        _menuFileSave.ShortcutKeys = Keys.Control | Keys.S;
        _menuFileSave.Size = new Size(257, 26);
        _menuFileSave.Text = "保存(&S)";
        _menuFileSave.Click += MenuFileSave_Click;
        // 
        // toolStripSeparator6
        // 
        toolStripSeparator6.Name = "toolStripSeparator6";
        toolStripSeparator6.Size = new Size(254, 6);
        // 
        // _menuFileSaveFolder
        // 
        _menuFileSaveFolder.Name = "_menuFileSaveFolder";
        _menuFileSaveFolder.Size = new Size(257, 26);
        _menuFileSaveFolder.Text = "保存先設定(&F)...";
        _menuFileSaveFolder.Click += MenuFileSaveFolder_Click;
        // 
        // toolStripSeparator9
        // 
        toolStripSeparator9.Name = "toolStripSeparator9";
        toolStripSeparator9.Size = new Size(254, 6);
        // 
        // _menuFolderView
        // 
        _menuFolderView.Name = "_menuFolderView";
        _menuFolderView.Size = new Size(257, 26);
        _menuFolderView.Text = "保存先フォルダビューの表示";
        _menuFolderView.Click += MenuFolderView_Click;
        // 
        // _menuOpenExplorer
        // 
        _menuOpenExplorer.Name = "_menuOpenExplorer";
        _menuOpenExplorer.Size = new Size(257, 26);
        _menuOpenExplorer.Text = "エクスプローラでフォルダを開く";
        _menuOpenExplorer.Click += MenuOpenExplorer_Click;
        // 
        // _menuFileCopyFolderPath
        // 
        _menuFileCopyFolderPath.Name = "_menuFileCopyFolderPath";
        _menuFileCopyFolderPath.Size = new Size(257, 26);
        _menuFileCopyFolderPath.Text = "パスをコピー(&C)";
        _menuFileCopyFolderPath.Click += MenuFileCopyFolderPath_Click;
        // 
        // toolStripSeparator3
        // 
        toolStripSeparator3.Name = "toolStripSeparator3";
        toolStripSeparator3.Size = new Size(254, 6);
        // 
        // _menuDisplaySettings
        // 
        _menuDisplaySettings.Name = "_menuDisplaySettings";
        _menuDisplaySettings.Size = new Size(257, 26);
        _menuDisplaySettings.Text = "動作設定(&D)...";
        _menuDisplaySettings.Click += MenuDisplaySettings_Click;
        // 
        // _menuEdit
        // 
        _menuEdit.DropDownItems.AddRange(new ToolStripItem[] { _menuEditUndo, toolStripSeparator4, _menuEditAutoCrop, _menuEditCrop, toolStripSeparator5, _menuEditCopy, _menuEditPaste });
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
        // toolStripSeparator4
        // 
        toolStripSeparator4.Name = "toolStripSeparator4";
        toolStripSeparator4.Size = new Size(309, 6);
        // 
        // _menuEditAutoCrop
        // 
        _menuEditAutoCrop.Name = "_menuEditAutoCrop";
        _menuEditAutoCrop.Size = new Size(312, 26);
        _menuEditAutoCrop.Text = "自動切り出し(&A)";
        _menuEditAutoCrop.Click += MenuEditAutoCrop_Click;
        // 
        // _menuEditCrop
        // 
        _menuEditCrop.Name = "_menuEditCrop";
        _menuEditCrop.ShortcutKeys = Keys.Control | Keys.X;
        _menuEditCrop.Size = new Size(312, 26);
        _menuEditCrop.Text = "選択範囲を切り出し";
        _menuEditCrop.Click += MenuEditCrop_Click;
        // 
        // toolStripSeparator5
        // 
        toolStripSeparator5.Name = "toolStripSeparator5";
        toolStripSeparator5.Size = new Size(309, 6);
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
        // _menuCapture
        // 
        _menuCapture.DropDownItems.AddRange(new ToolStripItem[] { _menuCaptureScreen, _menuCaptureWindow, _menuCaptureArea });
        _menuCapture.Name = "_menuCapture";
        _menuCapture.Size = new Size(94, 24);
        _menuCapture.Text = "キャプチャ(&C)";
        // 
        // _menuCaptureScreen
        // 
        _menuCaptureScreen.Name = "_menuCaptureScreen";
        _menuCaptureScreen.ShortcutKeys = Keys.Control | Keys.Shift | Keys.Q;
        _menuCaptureScreen.Size = new Size(304, 26);
        _menuCaptureScreen.Text = "スクリーン選択(&S)...";
        _menuCaptureScreen.Click += BtnSelectScreen_Click;
        // 
        // _menuCaptureWindow
        // 
        _menuCaptureWindow.Name = "_menuCaptureWindow";
        _menuCaptureWindow.ShortcutKeys = Keys.Control | Keys.Shift | Keys.W;
        _menuCaptureWindow.Size = new Size(304, 26);
        _menuCaptureWindow.Text = "ウィンドウ選択(&W)...";
        _menuCaptureWindow.Click += BtnWindowSelect_Click;
        // 
        // _menuCaptureArea
        // 
        _menuCaptureArea.Name = "_menuCaptureArea";
        _menuCaptureArea.ShortcutKeys = Keys.Control | Keys.Shift | Keys.E;
        _menuCaptureArea.Size = new Size(304, 26);
        _menuCaptureArea.Text = "領域選択(&A)...";
        _menuCaptureArea.Click += BtnAreaSelect_Click;
        // 
        // _menuView
        // 
        _menuView.DropDownItems.AddRange(new ToolStripItem[] { _menuEditZoomIn, _menuEditZoomOut, toolStripSeparator1, _menuEditAlignLeft, _menuEditAlignCenter, toolStripSeparator2, _menuViewLoupeHide, _menuEditShowLoupe, _menuViewLoupeAuto });
        _menuView.Name = "_menuView";
        _menuView.Size = new Size(72, 24);
        _menuView.Text = "表示(&V)";
        // 
        // _menuEditZoomIn
        // 
        _menuEditZoomIn.Name = "_menuEditZoomIn";
        _menuEditZoomIn.ShortcutKeys = Keys.Control | Keys.Oemplus;
        _menuEditZoomIn.Size = new Size(238, 26);
        _menuEditZoomIn.Text = "拡大";
        _menuEditZoomIn.Click += MenuEditZoomIn_Click;
        // 
        // _menuEditZoomOut
        // 
        _menuEditZoomOut.Name = "_menuEditZoomOut";
        _menuEditZoomOut.ShortcutKeys = Keys.Control | Keys.OemMinus;
        _menuEditZoomOut.Size = new Size(238, 26);
        _menuEditZoomOut.Text = "縮小";
        _menuEditZoomOut.Click += MenuEditZoomOut_Click;
        // 
        // toolStripSeparator1
        // 
        toolStripSeparator1.Name = "toolStripSeparator1";
        toolStripSeparator1.Size = new Size(235, 6);
        // 
        // _menuEditAlignLeft
        // 
        _menuEditAlignLeft.Name = "_menuEditAlignLeft";
        _menuEditAlignLeft.Size = new Size(238, 26);
        _menuEditAlignLeft.Text = "イメージ位置: 左上寄せ";
        _menuEditAlignLeft.Click += MenuEditAlignLeft_Click;
        // 
        // _menuEditAlignCenter
        // 
        _menuEditAlignCenter.Name = "_menuEditAlignCenter";
        _menuEditAlignCenter.Size = new Size(238, 26);
        _menuEditAlignCenter.Text = "イメージ位置: 中央寄せ";
        _menuEditAlignCenter.Click += MenuEditAlignCenter_Click;
        // 
        // toolStripSeparator2
        // 
        toolStripSeparator2.Name = "toolStripSeparator2";
        toolStripSeparator2.Size = new Size(235, 6);
        // 
        // _menuViewLoupeHide
        // 
        _menuViewLoupeHide.Name = "_menuViewLoupeHide";
        _menuViewLoupeHide.Size = new Size(238, 26);
        _menuViewLoupeHide.Text = "ルーペ: 非表示";
        _menuViewLoupeHide.Click += MenuViewLoupeHide_Click;
        // 
        // _menuEditShowLoupe
        // 
        _menuEditShowLoupe.Name = "_menuEditShowLoupe";
        _menuEditShowLoupe.Size = new Size(238, 26);
        _menuEditShowLoupe.Text = "ルーペ: 常時表示";
        _menuEditShowLoupe.Click += MenuEditShowLoupe_Click;
        // 
        // _menuViewLoupeAuto
        // 
        _menuViewLoupeAuto.Name = "_menuViewLoupeAuto";
        _menuViewLoupeAuto.Size = new Size(238, 26);
        _menuViewLoupeAuto.Text = "ルーペ: 範囲選択時表示";
        _menuViewLoupeAuto.Click += MenuViewLoupeAuto_Click;
        // 
        // _flowToolBar
        // 
        _flowToolBar.AutoSize = true;
        _flowToolBar.Controls.Add(_btnSelectScreen);
        _flowToolBar.Controls.Add(_btnWindowSelect);
        _flowToolBar.Controls.Add(_btnAreaSelect);
        _flowToolBar.Controls.Add(_btnSave);
        _flowToolBar.Controls.Add(_txtFileNameTemplate);
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
        // _btnSave
        // 
        _btnSave.AutoSize = true;
        _btnSave.Location = new Point(251, 8);
        _btnSave.Name = "_btnSave";
        _btnSave.Size = new Size(75, 30);
        _btnSave.TabIndex = 7;
        _btnSave.Text = "保存";
        _btnSave.UseVisualStyleBackColor = true;
        _btnSave.Click += BtnSave_Click;
        // 
        // _txtFileNameTemplate
        // 
        _txtFileNameTemplate.Location = new Point(332, 8);
        _txtFileNameTemplate.Name = "_txtFileNameTemplate";
        _txtFileNameTemplate.Size = new Size(250, 27);
        _txtFileNameTemplate.TabIndex = 0;
        _txtFileNameTemplate.Text = "screenshot_{date}_{time}.png";
        _txtFileNameTemplate.TextChanged += TxtFileNameTemplate_TextChanged;
        // 
        // _contextMenuLink
        // 
        _contextMenuLink.ImageScalingSize = new Size(20, 20);
        _contextMenuLink.Items.AddRange(new ToolStripItem[] { _ctxLinkSaveFolder, toolStripSeparator10, _ctxLinkFolderView, _ctxLinkOpenExplorer, _ctxLinkCopyFolderPath });
        _contextMenuLink.Name = "_contextMenuLink";
        _contextMenuLink.Size = new Size(244, 106);
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
        _ctxLinkFolderView.Text = "保存先フォルダビューの表示";
        _ctxLinkFolderView.Click += CtxLinkFolderView_Click;
        // 
        // _ctxLinkOpenExplorer
        // 
        _ctxLinkOpenExplorer.Name = "_ctxLinkOpenExplorer";
        _ctxLinkOpenExplorer.Size = new Size(243, 24);
        _ctxLinkOpenExplorer.Text = "エクスプローラでフォルダを開く";
        _ctxLinkOpenExplorer.Click += CtxLinkOpenExplorer_Click;
        // 
        // _ctxLinkCopyFolderPath
        // 
        _ctxLinkCopyFolderPath.Name = "_ctxLinkCopyFolderPath";
        _ctxLinkCopyFolderPath.Size = new Size(243, 24);
        _ctxLinkCopyFolderPath.Text = "パスをコピー";
        _ctxLinkCopyFolderPath.Click += MenuFileCopyFolderPath_Click;
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
        _picLoupe.Location = new Point(793, 315);
        _picLoupe.Name = "_picLoupe";
        _picLoupe.Size = new Size(170, 170);
        _picLoupe.TabIndex = 1;
        _picLoupe.TabStop = false;
        _picLoupe.Visible = false;
        // 
        // _statusStrip
        // 
        _statusStrip.ImageScalingSize = new Size(20, 20);
        _statusStrip.Items.AddRange(new ToolStripItem[] { _lblZoom, _lblImageSize, _lblSavedStatus, _lblFolderLink, _lblAlign, _lblLoupe, _lblDummy });
        _statusStrip.Location = new Point(0, 605);
        _statusStrip.Name = "_statusStrip";
        _statusStrip.ShowItemToolTips = true;
        _statusStrip.Size = new Size(1000, 30);
        _statusStrip.TabIndex = 3;
        // 
        // _lblZoom
        // 
        _lblZoom.BorderSides = ToolStripStatusLabelBorderSides.Right;
        _lblZoom.Name = "_lblZoom";
        _lblZoom.Size = new Size(53, 24);
        _lblZoom.Text = "Zoom";
        _lblZoom.TextAlign = ContentAlignment.MiddleLeft;
        _lblZoom.MouseDown += LblZoom_MouseDown;
        // 
        // _lblImageSize
        // 
        _lblImageSize.BorderSides = ToolStripStatusLabelBorderSides.Right;
        _lblImageSize.Name = "_lblImageSize";
        _lblImageSize.Size = new Size(82, 24);
        _lblImageSize.Text = "ImageSize";
        _lblImageSize.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _lblSavedStatus
        // 
        _lblSavedStatus.BorderSides = ToolStripStatusLabelBorderSides.Right;
        _lblSavedStatus.Name = "_lblSavedStatus";
        _lblSavedStatus.Size = new Size(93, 24);
        _lblSavedStatus.Text = "SavedStatus";
        _lblSavedStatus.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _lblFolderLink
        // 
        _lblFolderLink.IsLink = true;
        _lblFolderLink.LinkBehavior = LinkBehavior.HoverUnderline;
        _lblFolderLink.Name = "_lblFolderLink";
        _lblFolderLink.Size = new Size(654, 24);
        _lblFolderLink.Spring = true;
        _lblFolderLink.Text = "FolderLink";
        _lblFolderLink.TextAlign = ContentAlignment.MiddleLeft;
        _lblFolderLink.MouseDown += LblFolderLink_MouseDown;
        // 
        // _lblAlign
        // 
        _lblAlign.BorderSides = ToolStripStatusLabelBorderSides.Left;
        _lblAlign.Name = "_lblAlign";
        _lblAlign.Size = new Size(48, 24);
        _lblAlign.Text = "Align";
        _lblAlign.TextAlign = ContentAlignment.MiddleRight;
        _lblAlign.MouseDown += LblAlign_MouseDown;
        // 
        // _lblLoupe
        // 
        _lblLoupe.BorderSides = ToolStripStatusLabelBorderSides.Left;
        _lblLoupe.Name = "_lblLoupe";
        _lblLoupe.Size = new Size(54, 24);
        _lblLoupe.Text = "Loupe";
        _lblLoupe.TextAlign = ContentAlignment.MiddleRight;
        _lblLoupe.MouseDown += LblLoupe_MouseDown;
        // 
        // _lblDummy
        // 
        _lblDummy.AutoSize = false;
        _lblDummy.Name = "_lblDummy";
        _lblDummy.Size = new Size(1, 24);
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
        _contextMenuPreview.Items.AddRange(new ToolStripItem[] { _ctxAutoCrop, _ctxCrop, toolStripSeparator8, _ctxCopy, _ctxPaste, _ctxSep0, _ctxPZoomAuto, _ctxPZoomIn, _ctxPZoomOut });
        _contextMenuPreview.Name = "_contextMenuPreview";
        _contextMenuPreview.Size = new Size(281, 184);
        // 
        // _ctxAutoCrop
        // 
        _ctxAutoCrop.Name = "_ctxAutoCrop";
        _ctxAutoCrop.Size = new Size(280, 24);
        _ctxAutoCrop.Text = "自動切り出し";
        _ctxAutoCrop.Click += MenuEditAutoCrop_Click;
        // 
        // _ctxCrop
        // 
        _ctxCrop.Name = "_ctxCrop";
        _ctxCrop.ShortcutKeys = Keys.Control | Keys.X;
        _ctxCrop.Size = new Size(280, 24);
        _ctxCrop.Text = "選択範囲を切り出し";
        _ctxCrop.Click += MenuEditCrop_Click;
        // 
        // toolStripSeparator8
        // 
        toolStripSeparator8.Name = "toolStripSeparator8";
        toolStripSeparator8.Size = new Size(277, 6);
        // 
        // _ctxCopy
        // 
        _ctxCopy.Name = "_ctxCopy";
        _ctxCopy.ShortcutKeys = Keys.Control | Keys.C;
        _ctxCopy.Size = new Size(280, 24);
        _ctxCopy.Text = "クリップボードにコピー";
        _ctxCopy.Click += MenuEditCopy_Click;
        // 
        // _ctxPaste
        // 
        _ctxPaste.Name = "_ctxPaste";
        _ctxPaste.ShortcutKeys = Keys.Control | Keys.V;
        _ctxPaste.Size = new Size(280, 24);
        _ctxPaste.Text = "クリップボードから貼り付け";
        _ctxPaste.Click += MenuEditPaste_Click;
        // 
        // _ctxSep0
        // 
        _ctxSep0.Name = "_ctxSep0";
        _ctxSep0.Size = new Size(277, 6);
        // 
        // _ctxPZoomAuto
        // 
        _ctxPZoomAuto.Name = "_ctxPZoomAuto";
        _ctxPZoomAuto.Size = new Size(280, 24);
        _ctxPZoomAuto.Text = "自動";
        _ctxPZoomAuto.Click += CtxZoomAuto_Click;
        // 
        // _ctxPZoomIn
        // 
        _ctxPZoomIn.Name = "_ctxPZoomIn";
        _ctxPZoomIn.ShortcutKeys = Keys.Control | Keys.Oemplus;
        _ctxPZoomIn.Size = new Size(280, 24);
        _ctxPZoomIn.Text = "拡大";
        _ctxPZoomIn.Click += MenuEditZoomIn_Click;
        // 
        // _ctxPZoomOut
        // 
        _ctxPZoomOut.Name = "_ctxPZoomOut";
        _ctxPZoomOut.ShortcutKeys = Keys.Control | Keys.OemMinus;
        _ctxPZoomOut.Size = new Size(280, 24);
        _ctxPZoomOut.Text = "縮小";
        _ctxPZoomOut.Click += MenuEditZoomOut_Click;
        // 
        // _ctxZoomIn
        // 
        _ctxZoomIn.Name = "_ctxZoomIn";
        _ctxZoomIn.ShortcutKeys = Keys.Control | Keys.Oemplus;
        _ctxZoomIn.Size = new Size(221, 24);
        _ctxZoomIn.Text = "拡大";
        _ctxZoomIn.Click += MenuEditZoomIn_Click;
        // 
        // _ctxZoomOut
        // 
        _ctxZoomOut.Name = "_ctxZoomOut";
        _ctxZoomOut.ShortcutKeys = Keys.Control | Keys.OemMinus;
        _ctxZoomOut.Size = new Size(221, 24);
        _ctxZoomOut.Text = "縮小";
        _ctxZoomOut.Click += MenuEditZoomOut_Click;
        // 
        // _contextMenuZoom
        // 
        _contextMenuZoom.ImageScalingSize = new Size(20, 20);
        _contextMenuZoom.Items.AddRange(new ToolStripItem[] { _ctxZoomIn, _ctxZoomOut, toolStripSeparator7, _ctxZoomAuto, _ctxZoom25, _ctxZoom33, _ctxZoom50, _ctxZoom67, _ctxZoom75, _ctxZoom80, _ctxZoom90, _ctxZoom100, _ctxZoom110, _ctxZoom125, _ctxZoom150, _ctxZoom175, _ctxZoom200, _ctxZoom250, _ctxZoom300, _ctxZoom400, _ctxZoom500 });
        _contextMenuZoom.Name = "_contextMenuZoom";
        _contextMenuZoom.Size = new Size(222, 490);
        // 
        // toolStripSeparator7
        // 
        toolStripSeparator7.Name = "toolStripSeparator7";
        toolStripSeparator7.Size = new Size(218, 6);
        // 
        // _ctxZoomAuto
        // 
        _ctxZoomAuto.Name = "_ctxZoomAuto";
        _ctxZoomAuto.Size = new Size(221, 24);
        _ctxZoomAuto.Text = "自動";
        _ctxZoomAuto.Click += CtxZoomAuto_Click;
        // 
        // _ctxZoom25
        // 
        _ctxZoom25.Name = "_ctxZoom25";
        _ctxZoom25.Size = new Size(221, 24);
        _ctxZoom25.Text = "25%";
        _ctxZoom25.Click += CtxZoom25_Click;
        // 
        // _ctxZoom33
        // 
        _ctxZoom33.Name = "_ctxZoom33";
        _ctxZoom33.Size = new Size(221, 24);
        _ctxZoom33.Text = "33%";
        _ctxZoom33.Click += CtxZoom33_Click;
        // 
        // _ctxZoom50
        // 
        _ctxZoom50.Name = "_ctxZoom50";
        _ctxZoom50.Size = new Size(221, 24);
        _ctxZoom50.Text = "50%";
        _ctxZoom50.Click += CtxZoom50_Click;
        // 
        // _ctxZoom67
        // 
        _ctxZoom67.Name = "_ctxZoom67";
        _ctxZoom67.Size = new Size(221, 24);
        _ctxZoom67.Text = "67%";
        _ctxZoom67.Click += CtxZoom67_Click;
        // 
        // _ctxZoom75
        // 
        _ctxZoom75.Name = "_ctxZoom75";
        _ctxZoom75.Size = new Size(221, 24);
        _ctxZoom75.Text = "75%";
        _ctxZoom75.Click += CtxZoom75_Click;
        // 
        // _ctxZoom80
        // 
        _ctxZoom80.Name = "_ctxZoom80";
        _ctxZoom80.Size = new Size(221, 24);
        _ctxZoom80.Text = "80%";
        _ctxZoom80.Click += CtxZoom80_Click;
        // 
        // _ctxZoom90
        // 
        _ctxZoom90.Name = "_ctxZoom90";
        _ctxZoom90.Size = new Size(221, 24);
        _ctxZoom90.Text = "90%";
        _ctxZoom90.Click += CtxZoom90_Click;
        // 
        // _ctxZoom100
        // 
        _ctxZoom100.Name = "_ctxZoom100";
        _ctxZoom100.Size = new Size(221, 24);
        _ctxZoom100.Text = "100%";
        _ctxZoom100.Click += CtxZoom100_Click;
        // 
        // _ctxZoom110
        // 
        _ctxZoom110.Name = "_ctxZoom110";
        _ctxZoom110.Size = new Size(221, 24);
        _ctxZoom110.Text = "110%";
        _ctxZoom110.Click += CtxZoom110_Click;
        // 
        // _ctxZoom125
        // 
        _ctxZoom125.Name = "_ctxZoom125";
        _ctxZoom125.Size = new Size(221, 24);
        _ctxZoom125.Text = "125%";
        _ctxZoom125.Click += CtxZoom125_Click;
        // 
        // _ctxZoom150
        // 
        _ctxZoom150.Name = "_ctxZoom150";
        _ctxZoom150.Size = new Size(221, 24);
        _ctxZoom150.Text = "150%";
        _ctxZoom150.Click += CtxZoom150_Click;
        // 
        // _ctxZoom175
        // 
        _ctxZoom175.Name = "_ctxZoom175";
        _ctxZoom175.Size = new Size(221, 24);
        _ctxZoom175.Text = "175%";
        _ctxZoom175.Click += CtxZoom175_Click;
        // 
        // _ctxZoom200
        // 
        _ctxZoom200.Name = "_ctxZoom200";
        _ctxZoom200.Size = new Size(221, 24);
        _ctxZoom200.Text = "200%";
        _ctxZoom200.Click += CtxZoom200_Click;
        // 
        // _ctxZoom250
        // 
        _ctxZoom250.Name = "_ctxZoom250";
        _ctxZoom250.Size = new Size(221, 24);
        _ctxZoom250.Text = "250%";
        _ctxZoom250.Click += CtxZoom250_Click;
        // 
        // _ctxZoom300
        // 
        _ctxZoom300.Name = "_ctxZoom300";
        _ctxZoom300.Size = new Size(221, 24);
        _ctxZoom300.Text = "300%";
        _ctxZoom300.Click += CtxZoom300_Click;
        // 
        // _ctxZoom400
        // 
        _ctxZoom400.Name = "_ctxZoom400";
        _ctxZoom400.Size = new Size(221, 24);
        _ctxZoom400.Text = "400%";
        _ctxZoom400.Click += CtxZoom400_Click;
        // 
        // _ctxZoom500
        // 
        _ctxZoom500.Name = "_ctxZoom500";
        _ctxZoom500.Size = new Size(221, 24);
        _ctxZoom500.Text = "500%";
        _ctxZoom500.Click += CtxZoom500_Click;
        // 
        // _contextMenuAlign
        // 
        _contextMenuAlign.ImageScalingSize = new Size(20, 20);
        _contextMenuAlign.Items.AddRange(new ToolStripItem[] { _ctxAlignLeft, _ctxAlignCenter });
        _contextMenuAlign.Name = "_contextMenuAlign";
        _contextMenuAlign.Size = new Size(137, 52);
        // 
        // _ctxAlignLeft
        // 
        _ctxAlignLeft.Name = "_ctxAlignLeft";
        _ctxAlignLeft.Size = new Size(136, 24);
        _ctxAlignLeft.Text = "左上寄せ";
        _ctxAlignLeft.Click += MenuEditAlignLeft_Click;
        // 
        // _ctxAlignCenter
        // 
        _ctxAlignCenter.Name = "_ctxAlignCenter";
        _ctxAlignCenter.Size = new Size(136, 24);
        _ctxAlignCenter.Text = "中央寄せ";
        _ctxAlignCenter.Click += MenuEditAlignCenter_Click;
        // 
        // _contextMenuLoupe
        // 
        _contextMenuLoupe.ImageScalingSize = new Size(20, 20);
        _contextMenuLoupe.Items.AddRange(new ToolStripItem[] { _ctxLoupeHide, _ctxShowLoupe, _ctxLoupeAuto });
        _contextMenuLoupe.Name = "_contextMenuLoupe";
        _contextMenuLoupe.Size = new Size(184, 76);
        // 
        // _ctxLoupeHide
        // 
        _ctxLoupeHide.Name = "_ctxLoupeHide";
        _ctxLoupeHide.Size = new Size(183, 24);
        _ctxLoupeHide.Text = "非表示";
        _ctxLoupeHide.Click += MenuViewLoupeHide_Click;
        // 
        // _ctxShowLoupe
        // 
        _ctxShowLoupe.Name = "_ctxShowLoupe";
        _ctxShowLoupe.Size = new Size(183, 24);
        _ctxShowLoupe.Text = "常時表示";
        _ctxShowLoupe.Click += MenuEditShowLoupe_Click;
        // 
        // _ctxLoupeAuto
        // 
        _ctxLoupeAuto.Name = "_ctxLoupeAuto";
        _ctxLoupeAuto.Size = new Size(183, 24);
        _ctxLoupeAuto.Text = "範囲選択時表示";
        _ctxLoupeAuto.Click += MenuViewLoupeAuto_Click;
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
        _pnlPreview.Size = new Size(979, 499);
        _pnlPreview.TabIndex = 2;
        // 
        // _vScroll
        // 
        _vScroll.Dock = DockStyle.Right;
        _vScroll.Location = new Point(979, 85);
        _vScroll.Name = "_vScroll";
        _vScroll.Size = new Size(21, 499);
        _vScroll.TabIndex = 3;
        _vScroll.Scroll += VScroll_Scroll;
        // 
        // _hScroll
        // 
        _hScroll.Dock = DockStyle.Bottom;
        _hScroll.Location = new Point(0, 584);
        _hScroll.Name = "_hScroll";
        _hScroll.Size = new Size(1000, 21);
        _hScroll.TabIndex = 4;
        _hScroll.Scroll += HScroll_Scroll;
        // 
        // toolStripSeparator10
        // 
        toolStripSeparator10.Name = "toolStripSeparator10";
        toolStripSeparator10.Size = new Size(240, 6);
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
        _contextMenuZoom.ResumeLayout(false);
        _contextMenuAlign.ResumeLayout(false);
        _contextMenuLoupe.ResumeLayout(false);
        _pnlPreview.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }

    private ToolStripMenuItem _menuFolderView;
    private ToolStripMenuItem _menuOpenExplorer;
    private ToolStripSeparator toolStripSeparator1;
    private ToolStripSeparator toolStripSeparator2;
    private ToolStripSeparator toolStripSeparator6;
    private ToolStripSeparator toolStripSeparator4;
    private ToolStripSeparator toolStripSeparator5;
    private ToolStripSeparator toolStripSeparator8;
    private ToolStripSeparator toolStripSeparator7;
    private ToolStripSeparator toolStripSeparator9;
    private ToolStripSeparator toolStripSeparator3;
    private ToolStripSeparator toolStripSeparator10;
}
