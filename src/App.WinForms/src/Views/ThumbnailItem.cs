using System.ComponentModel;
using System.Drawing.Drawing2D;
using EsUtil.SaveEvidence.App.Core.Services;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>一覧に並べるサムネイル 1 件（画像と名前）。</summary>
/// <remarks>
/// <b>画像の縦横比を保つ</b>: 幅を表示モードの一辺に固定し、高さを元画像の縦横比から決める<br/>
/// （AvaloniaUI 版が <c>Image</c> の幅だけを与えて縮小するのと同じ結果になる）。<br/>
/// <b>行内で下端が揃う</b>: 名前は画像のすぐ下へ置き、一覧が行の最大高さで下端揃えする<br/>
/// （<see cref="ThumbnailListView"/>）。<br/>
/// <b>影は画像に付ける</b>: 要素全体ではなく<b>画像の枠</b>の背後へぼかした影を描く（AvaloniaUI 版の<br/>
/// <c>BoxShadow</c> と同じ見え方）。 影が外へはみ出して隣と重ならないよう、影の分だけ内側に余白を取る。<br/>
/// <b>画素の複製を保持する</b>: サムネイルは表示用に縮小したビットマップを 1 枚だけ保持し、<br/>
/// 差し替え時に直前の複製を破棄する（一覧を組み直すたびに増やさない）。
/// </remarks>
internal sealed class ThumbnailItem : Control
{
    /// <summary>名前の行の高さ（DIP）。</summary>
    private const int CaptionHeight = (int)ThumbnailTileMetrics.LabelHeight;

    /// <summary>影がはみ出す分の左右の余白（DIP）。 影の計画（<see cref="ShadowPlan.Insets"/>）が単一所有する。</summary>
    private static readonly int s_shadowPadding = (int)Math.Ceiling(ShadowPlan.Insets.Left);

    /// <summary>画像の上側の余白（DIP）。 影は下へ落ちるため、上側はその分だけ狭い。</summary>
    private static readonly int s_shadowTop = (int)Math.Ceiling(ShadowPlan.Insets.Top);

    /// <summary>画像の下側の余白（DIP）。 影は下へ落ちるため、下側はその分だけ広い。</summary>
    private static readonly int s_shadowBottom = (int)Math.Ceiling(ShadowPlan.Insets.Bottom);

    /// <summary>タイルの幅を決めるアイコンの一辺（DIP）。</summary>
    private int _iconSize;

    /// <summary>表示するサムネイル（本項目が所有する）。</summary>
    private Bitmap? _bitmap;

    /// <summary>画像の表示高さ（DIP）。 画像が無い間は一辺と同じ（正方形）。</summary>
    private int _imageHeight;

    /// <summary>カーソルが重なっているかどうか。</summary>
    private bool _isHovered;

    /// <summary>サムネイル 1 件を生成する。</summary>
    /// <param name="caption">表示する名前（ファイル名・履歴の説明）。</param>
    /// <param name="iconSize">サムネイルの表示幅（DIP）。</param>
    /// <param name="identity">対象を識別する文字列（ファイルパス等。 外部ツールでの表示に使う）。</param>
    public ThumbnailItem(string caption, int iconSize, string identity)
    {
        _iconSize = Math.Max(32, iconSize);
        // 枠の外側の間隔（4 UI 版で共通の値。 影の余白と合わせて隣の項目との間隔になる）
        Margin = new Padding((int)ThumbnailTileMetrics.ItemMargin);
        _imageHeight = _iconSize;
        Caption = caption;
        Identity = identity;
        ApplySize();
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
    /// <value>true の場合は名前を太字で描く（枠・影は変えない）。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsCurrent { get; set; }

    /// <summary>選択中かどうかを取得または設定する。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsSelected { get; set; }

    /// <summary>画像の縦横比から決まるタイルの高さ（DIP）を取得する。</summary>
    /// <remarks>一覧の行の高さと下端揃えはこの値を基準にする。</remarks>
    public int TileHeight => s_shadowTop + _imageHeight + s_shadowBottom + CaptionHeight;

    /// <summary>表示するサムネイルを設定する（直前の複製を破棄する）。</summary>
    /// <param name="bitmap">設定するビットマップ。 null の場合は画像を表示しない。 所有権は本項目へ移る。</param>
    public void SetImage(Bitmap? bitmap)
    {
        var previous = _bitmap;
        _bitmap = bitmap;
        previous?.Dispose();

        // 画像の縦横比に合わせて高さを変える（行の高さと下端揃えが追従する）
        _imageHeight = bitmap is null
            ? _iconSize
            : Math.Max(1, (int)Math.Round(ThumbnailTileMetrics.ImageHeight(_iconSize, bitmap.Width, bitmap.Height)));
        ApplySize();
        Invalidate();
    }

    /// <summary>アイコンの一辺（表示モード）を変更する。</summary>
    /// <param name="iconSize">新しい一辺（DIP）。</param>
    /// <remarks>幅と高さを新しい一辺で計算し直す（保持している画像の縦横比はそのまま使う）。</remarks>
    public void SetIconSize(int iconSize)
    {
        var size = Math.Max(32, iconSize);
        if (_iconSize == size)
        {
            return;
        }

        _iconSize = size;
        _imageHeight = _bitmap is null
            ? _iconSize
            : Math.Max(1, (int)Math.Round(ThumbnailTileMetrics.ImageHeight(_iconSize, _bitmap.Width, _bitmap.Height)));
        ApplySize();
        Invalidate();
    }

    /// <summary>指定のアイコンサイズに対するタイルの幅を求める。</summary>
    /// <param name="iconSize">アイコンの一辺（DIP）。</param>
    /// <returns>タイルの幅（DIP。 影の余白を含む）。</returns>
    public static int WidthFor(int iconSize) => Math.Max(32, iconSize) + (s_shadowPadding * 2);

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

        var frame = ImageBounds();

        // 影は画像の枠の背後へ描く（要素全体には付けない）。
        // マウスが重なっている間は影を消す（AvaloniaUI 版の :pointerover と同じ）
        if (!_isHovered && !IsSelected)
        {
            // アイテムの描画は DIP と同じ単位（倍率 1）で行う
            ShadowDrawer.Draw(e.Graphics, frame, scaling: 1);
        }

        if (_bitmap is not null)
        {
            var state = e.Graphics.Save();
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.SmoothingMode = SmoothingMode.None;
            try
            {
                e.Graphics.DrawImage(_bitmap, frame);
            }
            finally
            {
                e.Graphics.Restore(state);
            }
        }

        var captionBounds = new Rectangle(0, frame.Bottom, Width, CaptionHeight);
        TextRenderer.DrawText(
            e.Graphics,
            Caption,
            IsCurrent ? AppStyles.BoldUiFont : AppStyles.UiFont,
            captionBounds,
            _isHovered || IsSelected ? AppStyles.Accent : AppStyles.Foreground,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix);
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

    /// <summary>画像の表示矩形を求める。</summary>
    /// <returns>画像を描く矩形（影の余白の内側）。</returns>
    private Rectangle ImageBounds()
        => new(s_shadowPadding, s_shadowTop, _iconSize, _imageHeight);

    /// <summary>縦横比から決まる大きさを項目へ反映する。</summary>
    private void ApplySize()
    {
        var size = new Size(_iconSize + (s_shadowPadding * 2), s_shadowTop + _imageHeight + s_shadowBottom + CaptionHeight);
        if (Size != size)
        {
            Size = size;
        }
    }
}
