using System.Drawing.Drawing2D;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Crop;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>キャプチャページの画像の 4 辺へ影を描く重ね合わせ（WinForms 実装）。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（画像の外側へ影を描くだけの重ね合わせ）。<br/>
/// 影の幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が単一所有するため、本クラスは<br/>
/// 「計画の 4 辺の矩形を、計画の停止点のグラデーションで塗る」手順だけを持つ（4 UI 版で同じ結果になる）。<br/>
/// <br/>
/// <b>透明な重ね合わせを <see cref="Region"/> で作る理由</b>: WinForms の子コントロールは自分の領域を必ず塗る<br/>
/// （背景を透明にしても親の背景で塗り潰され、下の兄弟（画像）は見えない）。 表示領域を影の帯だけに絞れば、<br/>
/// 帯の外側は<b>まったく塗らない</b>ため、下の画像がそのまま見える。<br/>
/// <br/>
/// <b>追従</b>: ズーム・パン・ウィンドウの大きさの変更で画像の表示矩形が変わるため、<br/>
/// <see cref="CropViewControl.ViewportChanged"/> を購読して領域と影を更新する。
/// </remarks>
internal sealed class ImageShadowOverlay : Control
{
    /// <summary>影を落とす画像の表示矩形を提供するコントロール。</summary>
    private CropViewControl? _crop;

    /// <summary>直前に設定した表示領域（同じ領域の再設定で再描画が連鎖しないようにする）。</summary>
    private Rectangle _region = Rectangle.Empty;

    /// <summary>重ね合わせを初期化する（マウス操作は下のコントロールへ通す）。</summary>
    public ImageShadowOverlay()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,
            true);
        Dock = DockStyle.Fill;
        Enabled = false;
        TabStop = false;
    }

    /// <summary>影を落とす対象のコントロールを設定する。</summary>
    /// <param name="crop">画像を表示するコントロール。</param>
    public void Attach(CropViewControl crop)
    {
        ArgumentNullException.ThrowIfNull(crop);

        _crop = crop;
        crop.ViewportChanged += (_, _) => RefreshShadow();
        RefreshShadow();
    }

    /// <summary>画像の表示矩形へ影の領域を合わせる。</summary>
    private void RefreshShadow()
    {
        if (_crop is null || _crop.IsDisposed)
        {
            return;
        }

        var image = ImageBounds();
        if (image == _region)
        {
            return;
        }

        _region = image;

        // 影の帯だけを自分の表示領域にする（帯の外側は塗らないため、下の画像が見える）
        using var path = new GraphicsPath();
        foreach (var fill in ShadowPlan.BuildEdgeRects(image.X, image.Y, image.Width, image.Height))
        {
            path.AddRectangle(Rectangle.Round(new RectangleF(
                (float)fill.X, (float)fill.Y, (float)fill.Width, (float)fill.Height)));
        }

        Region?.Dispose();
        Region = new Region(path);
        Invalidate();
    }

    /// <summary>画像の表示矩形（コントロールのピクセル座標）を求める。</summary>
    /// <returns>画像の矩形。</returns>
    /// <remarks>表示矩形は DIP で通知されるため、コントロールの倍率（<see cref="Control.DeviceDpi"/>）を掛ける。</remarks>
    private Rectangle ImageBounds()
    {
        var rect = _crop!.ImageRectViewport();
        var scaling = DeviceDpi / 96.0;
        return new Rectangle(
            (int)Math.Round(rect.X * scaling),
            (int)Math.Round(rect.Y * scaling),
            (int)Math.Round(rect.Width * scaling),
            (int)Math.Round(rect.Height * scaling));
    }

    /// <summary>影を描画する（領域で帯だけに絞られている）。</summary>
    /// <param name="e">描画のイベント引数。</param>
    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPaint(e);

        if (_crop is null || _region.Width <= 0 || _region.Height <= 0)
        {
            return;
        }

        ShadowDrawer.Draw(e.Graphics, _region);
    }

    /// <summary>破棄時に表示領域を解放する。</summary>
    /// <param name="disposing">マネージド リソースを破棄するかどうか。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Region?.Dispose();
            Region = null;
        }

        base.Dispose(disposing);
    }
}
