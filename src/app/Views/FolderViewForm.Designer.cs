#nullable disable

namespace app.Views;

partial class FolderViewForm
{
    private System.ComponentModel.IContainer components = null;
    private ListView _listView;
    private StatusStrip _statusStrip;
    private ToolStripStatusLabel _lblStatus;
    private ToolStripStatusLabel _lblFolderPath;
    private ToolStripStatusLabel _lblViewMode;
    private ToolStripStatusLabel _lblSortStatus;
    private ToolStripStatusLabel _lblDummy;
    private ToolTip _toolTip;
    private ContextMenuStrip _contextMenuView;
    private ContextMenuStrip _contextMenuSort;
    private ToolStripMenuItem _ctxViewExtraLarge;
    private ToolStripMenuItem _ctxViewLarge;
    private ToolStripMenuItem _ctxViewMedium;
    private ToolStripMenuItem _ctxViewList;
    private ToolStripMenuItem _ctxViewDetails;
    private ToolStripMenuItem _ctxSortAscending;
    private ToolStripMenuItem _ctxSortDescending;

    private MenuStrip _menuStrip;
    private ToolStripMenuItem _menuFile;
    private ToolStripMenuItem _menuFileOpenExplorer;
    private ToolStripMenuItem _menuFileCopyFolderPath;
    private ToolStripMenuItem _menuView;
    private ToolStripMenuItem _menuViewExtraLarge;
    private ToolStripMenuItem _menuViewLarge;
    private ToolStripMenuItem _menuViewMedium;
    private ToolStripMenuItem _menuViewList;
    private ToolStripMenuItem _menuViewDetails;
    private ToolStripMenuItem _menuSortAscending;
    private ToolStripMenuItem _menuSortDescending;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components is not null))
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _listView = new ListView();
        _toolTip = new ToolTip(components);
        _statusStrip = new StatusStrip();
        _lblStatus = new ToolStripStatusLabel();
        _lblFolderPath = new ToolStripStatusLabel();
        _lblViewMode = new ToolStripStatusLabel();
        _lblSortStatus = new ToolStripStatusLabel();
        _lblDummy = new ToolStripStatusLabel();
        _contextMenuView = new ContextMenuStrip(components);
        _ctxViewExtraLarge = new ToolStripMenuItem();
        _ctxViewLarge = new ToolStripMenuItem();
        _ctxViewMedium = new ToolStripMenuItem();
        _ctxViewList = new ToolStripMenuItem();
        _ctxViewDetails = new ToolStripMenuItem();
        _contextMenuSort = new ContextMenuStrip(components);
        _ctxSortAscending = new ToolStripMenuItem();
        _ctxSortDescending = new ToolStripMenuItem();
        _menuStrip = new MenuStrip();
        _menuFile = new ToolStripMenuItem();
        _menuFileOpenExplorer = new ToolStripMenuItem();
        _menuFileCopyFolderPath = new ToolStripMenuItem();
        _menuView = new ToolStripMenuItem();
        _menuViewExtraLarge = new ToolStripMenuItem();
        _menuViewLarge = new ToolStripMenuItem();
        _menuViewMedium = new ToolStripMenuItem();
        _menuViewList = new ToolStripMenuItem();
        _menuViewDetails = new ToolStripMenuItem();
        toolStripSeparator2 = new ToolStripSeparator();
        _menuSortAscending = new ToolStripMenuItem();
        _menuSortDescending = new ToolStripMenuItem();
        _statusStrip.SuspendLayout();
        _contextMenuView.SuspendLayout();
        _contextMenuSort.SuspendLayout();
        _menuStrip.SuspendLayout();
        SuspendLayout();
        // 
        // _listView
        // 
        _listView.Dock = DockStyle.Fill;
        _listView.FullRowSelect = true;
        _listView.Location = new Point(0, 0);
        _listView.Name = "_listView";
        _listView.Size = new Size(900, 550);
        _listView.TabIndex = 2;
        _listView.UseCompatibleStateImageBehavior = false;
        _listView.ItemActivate += ListView_ItemActivate;
        // 
        // _statusStrip
        // 
        _statusStrip.ImageScalingSize = new Size(20, 20);
        _statusStrip.Items.AddRange(new ToolStripItem[] { _lblStatus, _lblFolderPath, _lblViewMode, _lblSortStatus, _lblDummy });
        _statusStrip.Location = new Point(0, 550);
        _statusStrip.Name = "_statusStrip";
        _statusStrip.ShowItemToolTips = true;
        _statusStrip.Size = new Size(900, 30);
        _statusStrip.TabIndex = 4;
        // 
        // _lblStatus
        // 
        _lblStatus.BorderSides = ToolStripStatusLabelBorderSides.Right;
        _lblStatus.Name = "_lblStatus";
        _lblStatus.Size = new Size(53, 24);
        _lblStatus.Text = "Status";
        _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _lblFolderPath
        // 
        _lblFolderPath.IsLink = true;
        _lblFolderPath.Name = "_lblFolderPath";
        _lblFolderPath.Size = new Size(628, 24);
        _lblFolderPath.Spring = true;
        _lblFolderPath.Text = "FolderPath";
        _lblFolderPath.TextAlign = ContentAlignment.MiddleLeft;
        _lblFolderPath.Click += LblFolderPath_Click;
        // 
        // _lblViewMode
        // 
        _lblViewMode.BorderSides = ToolStripStatusLabelBorderSides.Left;
        _lblViewMode.BorderStyle = Border3DStyle.SunkenOuter;
        _lblViewMode.Name = "_lblViewMode";
        _lblViewMode.Size = new Size(84, 24);
        _lblViewMode.Text = "ViewMode";
        _lblViewMode.MouseDown += LblViewMode_MouseDown;
        // 
        // _lblSortStatus
        // 
        _lblSortStatus.BorderSides = ToolStripStatusLabelBorderSides.Left;
        _lblSortStatus.BorderStyle = Border3DStyle.SunkenOuter;
        _lblSortStatus.Name = "_lblSortStatus";
        _lblSortStatus.Size = new Size(80, 24);
        _lblSortStatus.Text = "SortStatus";
        _lblSortStatus.MouseDown += LblSortStatus_MouseDown;
        // 
        // _lblDummy
        // 
        _lblDummy.AutoSize = false;
        _lblDummy.Name = "_lblDummy";
        _lblDummy.Size = new Size(1, 24);
        // 
        // _contextMenuView
        // 
        _contextMenuView.ImageScalingSize = new Size(20, 20);
        _contextMenuView.Items.AddRange(new ToolStripItem[] { _ctxViewExtraLarge, _ctxViewLarge, _ctxViewMedium, _ctxViewList, _ctxViewDetails });
        _contextMenuView.Name = "_contextMenuView";
        _contextMenuView.Size = new Size(109, 124);
        // 
        // _ctxViewExtraLarge
        // 
        _ctxViewExtraLarge.Name = "_ctxViewExtraLarge";
        _ctxViewExtraLarge.Size = new Size(108, 24);
        _ctxViewExtraLarge.Text = "特大";
        _ctxViewExtraLarge.Click += CtxViewExtraLarge_Click;
        // 
        // _ctxViewLarge
        // 
        _ctxViewLarge.Name = "_ctxViewLarge";
        _ctxViewLarge.Size = new Size(108, 24);
        _ctxViewLarge.Text = "大";
        _ctxViewLarge.Click += CtxViewLarge_Click;
        // 
        // _ctxViewMedium
        // 
        _ctxViewMedium.Name = "_ctxViewMedium";
        _ctxViewMedium.Size = new Size(108, 24);
        _ctxViewMedium.Text = "中";
        _ctxViewMedium.Click += CtxViewMedium_Click;
        // 
        // _ctxViewList
        // 
        _ctxViewList.Name = "_ctxViewList";
        _ctxViewList.Size = new Size(108, 24);
        _ctxViewList.Text = "一覧";
        _ctxViewList.Click += CtxViewList_Click;
        // 
        // _ctxViewDetails
        // 
        _ctxViewDetails.Name = "_ctxViewDetails";
        _ctxViewDetails.Size = new Size(108, 24);
        _ctxViewDetails.Text = "詳細";
        _ctxViewDetails.Click += CtxViewDetails_Click;
        // 
        // _contextMenuSort
        // 
        _contextMenuSort.ImageScalingSize = new Size(20, 20);
        _contextMenuSort.Items.AddRange(new ToolStripItem[] { _ctxSortAscending, _ctxSortDescending });
        _contextMenuSort.Name = "_contextMenuSort";
        _contextMenuSort.Size = new Size(180, 52);
        // 
        // _ctxSortAscending
        // 
        _ctxSortAscending.Name = "_ctxSortAscending";
        _ctxSortAscending.Size = new Size(179, 24);
        _ctxSortAscending.Text = "名前の昇順（▲）";
        _ctxSortAscending.Click += CtxSortAscending_Click;
        // 
        // _ctxSortDescending
        // 
        _ctxSortDescending.Name = "_ctxSortDescending";
        _ctxSortDescending.Size = new Size(179, 24);
        _ctxSortDescending.Text = "名前の降順（▼）";
        _ctxSortDescending.Click += CtxSortDescending_Click;
        // 
        // _menuStrip
        // 
        _menuStrip.ImageScalingSize = new Size(20, 20);
        _menuStrip.Items.AddRange(new ToolStripItem[] { _menuFile, _menuView });
        _menuStrip.Location = new Point(0, 0);
        _menuStrip.Name = "_menuStrip";
        _menuStrip.Size = new Size(900, 28);
        _menuStrip.TabIndex = 5;
        _menuStrip.Text = "メニュー";
        // 
        // _menuFile
        // 
        _menuFile.DropDownItems.AddRange(new ToolStripItem[] { _menuFileOpenExplorer, _menuFileCopyFolderPath });
        _menuFile.Name = "_menuFile";
        _menuFile.Size = new Size(86, 24);
        _menuFile.Text = "ファイル (&F)";
        // 
        // _menuFileOpenExplorer
        // 
        _menuFileOpenExplorer.Name = "_menuFileOpenExplorer";
        _menuFileOpenExplorer.Size = new Size(257, 26);
        _menuFileOpenExplorer.Text = "エクスプローラでフォルダを開く";
        _menuFileOpenExplorer.Click += MenuFileOpenExplorer_Click;
        // 
        // _menuFileCopyFolderPath
        // 
        _menuFileCopyFolderPath.Name = "_menuFileCopyFolderPath";
        _menuFileCopyFolderPath.Size = new Size(257, 26);
        _menuFileCopyFolderPath.Text = "パスをコピー";
        _menuFileCopyFolderPath.Click += MenuFileCopyFolderPath_Click;
        // 
        // _menuView
        // 
        _menuView.DropDownItems.AddRange(new ToolStripItem[] { _menuViewExtraLarge, _menuViewLarge, _menuViewMedium, _menuViewList, _menuViewDetails, toolStripSeparator2, _menuSortAscending, _menuSortDescending });
        _menuView.Name = "_menuView";
        _menuView.Size = new Size(76, 24);
        _menuView.Text = "表示 (&V)";
        // 
        // _menuViewExtraLarge
        // 
        _menuViewExtraLarge.Name = "_menuViewExtraLarge";
        _menuViewExtraLarge.Size = new Size(230, 26);
        _menuViewExtraLarge.Text = "画像サイズ: 特大";
        _menuViewExtraLarge.Click += MenuViewExtraLarge_Click;
        // 
        // _menuViewLarge
        // 
        _menuViewLarge.Name = "_menuViewLarge";
        _menuViewLarge.Size = new Size(230, 26);
        _menuViewLarge.Text = "画像サイズ: 大";
        _menuViewLarge.Click += MenuViewLarge_Click;
        // 
        // _menuViewMedium
        // 
        _menuViewMedium.Name = "_menuViewMedium";
        _menuViewMedium.Size = new Size(230, 26);
        _menuViewMedium.Text = "画像サイズ: 中";
        _menuViewMedium.Click += MenuViewMedium_Click;
        // 
        // _menuViewList
        // 
        _menuViewList.Name = "_menuViewList";
        _menuViewList.Size = new Size(230, 26);
        _menuViewList.Text = "画像サイズ: 一覧";
        _menuViewList.Click += MenuViewList_Click;
        // 
        // _menuViewDetails
        // 
        _menuViewDetails.Name = "_menuViewDetails";
        _menuViewDetails.Size = new Size(230, 26);
        _menuViewDetails.Text = "画像サイズ: 詳細";
        _menuViewDetails.Click += MenuViewDetails_Click;
        // 
        // toolStripSeparator2
        // 
        toolStripSeparator2.Name = "toolStripSeparator2";
        toolStripSeparator2.Size = new Size(227, 6);
        // 
        // _menuSortAscending
        // 
        _menuSortAscending.Name = "_menuSortAscending";
        _menuSortAscending.Size = new Size(230, 26);
        _menuSortAscending.Text = "並替: 名前の昇順 (▲)";
        _menuSortAscending.Click += MenuSortAscending_Click;
        // 
        // _menuSortDescending
        // 
        _menuSortDescending.Name = "_menuSortDescending";
        _menuSortDescending.Size = new Size(230, 26);
        _menuSortDescending.Text = "並替: 名前の降順 (▼)";
        _menuSortDescending.Click += MenuSortDescending_Click;
        // 
        // FolderViewForm
        // 
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(900, 580);
        Controls.Add(_menuStrip);
        Controls.Add(_listView);
        Controls.Add(_statusStrip);
        MinimumSize = new Size(500, 350);
        Name = "FolderViewForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "保存先フォルダビュー";
        _statusStrip.ResumeLayout(false);
        _statusStrip.PerformLayout();
        _contextMenuView.ResumeLayout(false);
        _contextMenuSort.ResumeLayout(false);
        _menuStrip.ResumeLayout(false);
        _menuStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
    private ToolStripSeparator toolStripSeparator2;
}
