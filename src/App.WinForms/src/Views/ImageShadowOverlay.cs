using System.Drawing.Drawing2D;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Crop;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>キャプチャページの画像の 4 辺・4 隅へ影を描く重ね合わせ（WinForms 実装）。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（画像の外側へ影を描くだけの重ね合わせ）。<br/>
/// 影の幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が単一所有するため、本クラスは<br/>
/// 「計画の 4 辺・4 隅の矩形を、計画の停止点のグラデーションで塗る」手順だけを持つ（4 UI 版で同じ結果になる）。<br/>
/// <br/>
/// <b>透明な重ね合わせを <see cref="Region"/> で作る理由</b>: WinForms の子コントロールは自分の領域を必ず塗る<br/>
/// （背景を透明にしても親の背景で塗り潰され、下の兄弟（画像）は見えない）。 表示領域を影の帯だけに絞れば、<br/>
/// 帯の外側は<b>まったく塗らない</b>ため、下の画像がそのまま見える。<br/>
/// <br/>
/// <b>座標系</b>: 表示領域はピクセル、描画は DIP（<see cref="Control.DeviceDpi"/> の倍率を掛ける）。<br/>
/// 影の幅は他の UI 版と同じ DIP で決まる。<br/>
/// <br/>
/// <b>追従</b>: ズーム・パン・ウィンドウの大きさの変更で画像の表示矩形が変わるため、<br/>
/// <see cref="CropViewControl.ViewportChanged"/> を購読して領域と影を更新する。
/// </remarks>
internal sealed class ImageShadowOverlay : Control
{
    /// <summary>表示領域を描画範囲より広く取る量（ピクセル）。 影は画像側へ 1 ピクセル広がる。</summary>
    private const int OverlapPixels = 1;

    /// <summary>影を落とす画像の表示矩形を提供するコントロール。</summary>
    private CropViewControl? _crop;

    /// <summary>直前に影を合わせた画像の矩形（DIP）。 同じ矩形の再設定で描き直さない。</summary>
    private RectangleF _image = RectangleF.Empty;

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

        var rect = _crop.ImageRectViewport();
        var image = new RectangleF((float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height);
        if (image == _image)
        {
            return;
        }
        _image = image;
        Region?.Dispose();
        Region = image.Width > 0 && image.Height > 0
            ? new Region(BuildRegionPath(image, DeviceDpi / 96.0))
            : null;
        Invalidate();
    }

    /// <summary>影の帯（4 辺 + 4 隅）を表す図形を組み立てる。</summary>
    /// <param name="image">画像の矩形（DIP）。</param>
    /// <param name="scaling">DIP → ピクセルの倍率。</param>
    /// <returns>表示領域の図形（<see cref="Region"/> が複製するため、呼び出し側が破棄する）。</returns>
    /// <remarks>
    /// 外側へ丸める（床と天井）のは、<b>表示領域を必ず描画範囲より広くする</b>ためである。<br/>
    /// 内側へ丸めると、帯と隅の境界に 1 ピクセルの塗り残しができ、背景が筋状に見える。
    /// </remarks>
    private static GraphicsPath BuildRegionPath(RectangleF image, double scaling)
    {
        // 既定の Alternate は重なった矩形を打ち消す（境界の 1 ピクセルが抜ける）ため、和集合にする
        var path = new GraphicsPath { FillMode = FillMode.Winding };
        foreach (var fill in ShadowPlan.BuildEdgeRects(image.X, image.Y, image.Width, image.Height))
        {
            path.AddRectangle(ToPixels(fill.X, fill.Y, fill.Width, fill.Height, scaling));
        }

        foreach (var fill in ShadowPlan.BuildCornerFills(image.X, image.Y, image.Width, image.Height))
        {
            path.AddRectangle(ToPixels(fill.X, fill.Y, fill.Size, fill.Size, scaling));
        }

        return path;
    }

    /// <summary>DIP の矩形をピクセル矩形へ変換する（外側へ広げる）。</summary>
    /// <param name="x">左端（DIP）。</param>
    /// <param name="y">上端（DIP）。</param>
    /// <param name="width">幅（DIP）。</param>
    /// <param name="height">高さ（DIP）。</param>
    /// <param name="scaling">DIP → ピクセルの倍率。</param>
    /// <returns>ピクセル矩形。</returns>
    /// <remarks>
    /// 端数を外側へ丸め、さらに <see cref="OverlapPixels"/> だけ広げる。 表示領域が描画範囲より狭いと、<br/>
    /// 帯と隅の境界や画像との境目に 1 ピクセルの塗り残しができ、背景が筋状に見える。
    /// </remarks>
    private static Rectangle ToPixels(double x, double y, double width, double height, double scaling)
        => Rectangle.FromLTRB(
            (int)Math.Floor(x * scaling) - OverlapPixels,
            (int)Math.Floor(y * scaling) - OverlapPixels,
            (int)Math.Ceiling((x + width) * scaling) + OverlapPixels,
            (int)Math.Ceiling((y + height) * scaling) + OverlapPixels);

    /// <summary>影を描画する（領域で帯だけに絞られている）。</summary>
    /// <param name="e">描画のイベント引数。</param>
    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPaint(e);

        if (_crop is null || _image.Width <= 0 || _image.Height <= 0)
        {
            return;
        }

        // 画像の表示矩形（DIP）をそのまま拡大して描く（丸めると画像との間に筋が出る）
        ShadowDrawer.Draw(e.Graphics, _image, DeviceDpi / 96.0);
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