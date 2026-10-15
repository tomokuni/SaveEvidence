using System.ComponentModel;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>一覧に並べるサムネイル 1 件（画像とファイル名）。</summary>
/// <remarks>
/// <b>画像の縦横比を保つ</b>: 表示枠の中で縦横比を保って縮小し、<b>下揃え</b>で配置する。<br/>
/// 項目の高さを表示モードごとに揃えているため、行内でファイル名の位置が揃う。<br/>
/// <b>画素の複製を保持する</b>: サムネイルは表示用に縮小したビットマップを 1 枚だけ保持し、<br/>
/// 差し替え時に直前の複製を破棄する（一覧を組み直すたびに増やさない）。
/// </remarks>
internal sealed class ThumbnailItem : Control
{
    /// <summary>ファイル名の行の高さ（DIP）。</summary>
    private const int CaptionHeight = 20;

    /// <summary>表示枠の左右の余白（DIP）。</summary>
    private const int HorizontalPadding = 6;

    /// <summary>タイルの高さを決めるアイコンの一辺（DIP）。</summary>
    private readonly int _iconSize;

    /// <summary>表示するサムネイル（本項目が所有する）。</summary>
    private Bitmap? _bitmap;

    /// <summary>カーソルが重なっているかどうか。</summary>
    private bool _isHovered;

    /// <summary>サムネイル 1 件を生成する。</summary>
    /// <param name="caption">表示する名前（ファイル名・履歴の説明）。</param>
    /// <param name="iconSize">サムネイルの表示枠の一辺（DIP）。</param>
    /// <param name="identity">対象を識別する文字列（ファイルパス等。 外部ツールでの表示に使う）。</param>
    public ThumbnailItem(string caption, int iconSize, string identity)
    {
        _iconSize = Math.Max(32, iconSize);
        Caption = caption;
        Identity = identity;
        Size = new Size(_iconSize + (HorizontalPadding * 2), _iconSize + CaptionHeight);
        Margin = new Padding(4, 4, 4, 4);
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor,
            true);
        UiFactory.AttachToolTip(this, caption);
    }

    /// <summary>表示する名前を取得する。</summary>
    public string Caption { get; }

    /// <summary>対象を識別する文字列を取得する。</summary>
    /// <value>ファイルの一覧ではファイルパス、履歴の一覧では空文字。</value>
    public string Identity { get; }

    /// <summary>現在の画像（履歴の最新）かどうかを取得または設定する。</summary>
    /// <value>true の場合は選択の枠を太く描く（常に強調する）。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsCurrent { get; set; }

    /// <summary>選択中かどうかを取得または設定する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsSelected { get; set; }

    /// <summary>表示するサムネイルを設定する（直前の複製を破棄する）。</summary>
    /// <param name="bitmap">設定するビットマップ。 null の場合は画像を表示しない。 所有権は本項目へ移る。</param>
    public void SetImage(Bitmap? bitmap)
    {
        var previous = _bitmap;
        _bitmap = bitmap;
        previous?.Dispose();
        Invalidate();
    }

    /// <summary>項目を描画する。</summary>
    /// <param name="e">描画のイベント引数。</param>
    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var bounds = new Rectangle(0, 0, Width, Height);
        using (var background = new SolidBrush(_isHovered ? AppStyles.ItemHover : AppStyles.ListBackground))
        {
            e.Graphics.FillRectangle(background, bounds);
        }

        if (_bitmap is not null)
        {
            // 縦横比を保って表示枠へ収め、下揃えで配置する（行内でファイル名の位置が揃う）
            var box = new Rectangle(
                HorizontalPadding, 0, Math.Max(1, Width - (HorizontalPadding * 2)), _iconSize);
            var scale = Math.Min((double)box.Width / _bitmap.Width, (double)box.Height / _bitmap.Height);
            var width = Math.Max(1, (int)Math.Round(_bitmap.Width * scale));
            var height = Math.Max(1, (int)Math.Round(_bitmap.Height * scale));
            var destination = new Rectangle(
                box.Left + ((box.Width - width) / 2), box.Bottom - height, width, height);

            var state = e.Graphics.Save();
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            try
            {
                e.Graphics.DrawImage(_bitmap, destination);
            }
            finally
            {
                e.Graphics.Restore(state);
            }
        }

        var captionBounds = new Rectangle(0, _iconSize, Width, CaptionHeight);
        TextRenderer.DrawText(
            e.Graphics,
            Caption,
            AppStyles.UiFont,
            captionBounds,
            _isHovered || IsSelected ? AppStyles.Accent : AppStyles.Foreground,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix);

        var width2 = IsCurrent ? (int)AppStyles.SelectedBorderWidth : (int)AppStyles.BorderWidth;
        var borderColor = IsCurrent || _isHovered || IsSelected ? AppStyles.Accent : AppStyles.Border;
        using var pen = new Pen(borderColor, width2);
        e.Graphics.DrawRectangle(pen, new Rectangle(0, 0, Width - 1, Height - 1));
    }

    /// <summary>カーソルが重なったときに背景を変える。</summary>
    /// <param name="e">イベント引数。</param>
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    /// <summary>カーソルが外れたときに背景を戻す。</summary>
    /// <param name="e">イベント引数。</param>
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    /// <summary>保持している資源を解放する。</summary>
    /// <param name="disposing">マネージドの資源も解放する場合は true。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bitmap?.Dispose();
            _bitmap = null;
        }

        base.Dispose(disposing);
    }
}
