#nullable disable

namespace app.Views;

partial class FolderViewForm
{
    private System.ComponentModel.IContainer components = null;
    private ListView _listView;
    private ComboBox _cmbView;
    private Button _btnSort;
    private Button _btnClose;
    private StatusStrip _statusStrip;
    private ToolStripStatusLabel _lblStatus;
    private FlowLayoutPanel _flowToolBar;
    private LinkLabel _linkFolderName;
    private ToolTip _toolTip;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components is not null))
        {
            components.Dispose();
            _toolTip?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        _flowToolBar = new FlowLayoutPanel();
        _listView = new ListView();
        _cmbView = new ComboBox();
        _btnSort = new Button();
        _btnClose = new Button();
        _statusStrip = new StatusStrip();
        _lblStatus = new ToolStripStatusLabel();

        _statusStrip.SuspendLayout();
        _flowToolBar.SuspendLayout();
        SuspendLayout();

        // _flowToolBar
        _flowToolBar.Controls.Add(_cmbView);
        _flowToolBar.Controls.Add(_btnSort);
        _flowToolBar.Controls.Add(_btnClose);
        _flowToolBar.Dock = DockStyle.Top;
        _flowToolBar.Location = new Point(0, 0);
        _flowToolBar.Name = "_flowToolBar";
        _flowToolBar.Padding = new Padding(5, 5, 5, 5);
        _flowToolBar.Size = new Size(900, 42);
        _flowToolBar.TabIndex = 0;

        // _listView
        _listView.Dock = DockStyle.Fill;
        _listView.Location = new Point(0, 42);
        _listView.Name = "_listView";
        _listView.Size = new Size(900, 488);
        _listView.TabIndex = 2;
        _listView.View = View.LargeIcon;
        _listView.UseCompatibleStateImageBehavior = false;
        _listView.FullRowSelect = true;
        _listView.ItemActivate += ListView_ItemActivate;

        // _cmbView
        _cmbView.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbView.DropDownHeight = 200;
        _cmbView.Location = new Point(8, 8);
        _cmbView.Margin = new Padding(3, 3, 10, 3);
        _cmbView.Name = "_cmbView";
        _cmbView.Size = new Size(160, 28);
        _cmbView.TabIndex = 0;
        _cmbView.SelectedIndexChanged += CmbView_SelectedIndexChanged;

        // _btnSort
        _btnSort.AutoSize = true;
        _btnSort.FlatStyle = FlatStyle.System;
        _btnSort.Location = new Point(181, 6);
        _btnSort.Margin = new Padding(3, 3, 10, 3);
        _btnSort.Name = "_btnSort";
        _btnSort.Size = new Size(90, 30);
        _btnSort.TabIndex = 1;
        _btnSort.Text = "名前 ↑";
        _btnSort.UseVisualStyleBackColor = true;
        _btnSort.Click += BtnSort_Click;

        // _btnClose
        _btnClose.AutoSize = true;
        _btnClose.FlatStyle = FlatStyle.System;
        _btnClose.Location = new Point(284, 6);
        _btnClose.Margin = new Padding(3, 3, 10, 3);
        _btnClose.Name = "_btnClose";
        _btnClose.Size = new Size(100, 30);
        _btnClose.TabIndex = 3;
        _btnClose.Text = "閉じる(&C)";
        _btnClose.UseVisualStyleBackColor = true;
        _btnClose.Click += BtnClose_Click;

        // _statusStrip
        _statusStrip.Dock = DockStyle.Bottom;
        _statusStrip.Items.AddRange(new ToolStripItem[] { _lblStatus });
        _statusStrip.Location = new Point(0, 530);
        _statusStrip.Name = "_statusStrip";
        _statusStrip.Size = new Size(900, 26);
        _statusStrip.TabIndex = 4;

        // _lblStatus
        _lblStatus.Name = "_lblStatus";
        _lblStatus.Size = new Size(885, 24);
        _lblStatus.Spring = true;
        _lblStatus.TextAlign = ContentAlignment.MiddleLeft;

        // FolderViewForm
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(900, 580);
        Controls.Add(_listView);
        Controls.Add(_flowToolBar);
        Controls.Add(_statusStrip);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(500, 350);
        Name = "FolderViewForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "保存先フォルダの内容";

        _flowToolBar.ResumeLayout(false);
        _flowToolBar.PerformLayout();
        _statusStrip.ResumeLayout(false);
        _statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
