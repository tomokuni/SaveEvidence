using System.Drawing.Drawing2D;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.SaveEvidence.Platform.WinForms;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Crop;

/// <summary>オーバーレイ矩形の描画入力（<see cref="OverlayRectFrame"/>）を GDI+ で描画するレンダラー。</summary>
/// <remarks>
/// コアが確定した描画入力（配置・太さ・破線の判定と長さ・色）を <see cref="Graphics"/> へ写すだけの層であり、<br/>
/// 式・判断・クランプを持たない（描画手順として「枠を内側へ寄せる」「背景→前景の順に描く」だけを持つ）。<br/>
/// 実線は前景色の枠のみ、破線は「背景色の実線 → 前景色の破線」の 2 回描画で市松風の視認性を出す<br/>
/// （他の UI 版が背景色の下層矩形と前景色の破線矩形を重ねる方式と等価）。<br/>
/// 座標変換（対象座標 → ビューポート座標）と描画入力の組み立ては呼び出し側が済ませている前提とする。
/// </remarks>
internal static class RectOverlayGraphicsRenderer
{
    /// <summary>枠線のペン（スレッドごとに 1 つを使い回す）。</summary>
    /// <remarks>
    /// 破線の指定は描くときに付け外しする（<see cref="DashStyle.Solid"/> が実線）。<br/>
    /// GDI+ のダッシュ長は<b>ペンの幅の倍数</b>であるため、コアが確定したピクセル長を太さで割って渡す。
    /// </remarks>
    [ThreadStatic]
    private static Pen? s_pen;

    /// <summary>描画入力を GDI+ の描画面へ描画する。</summary>
    /// <param name="graphics">描画先（DIP 座標で描画できる状態であること）。 null 不可。</param>
    /// <param name="frame">描画入力（コアが確定した配置・太さ・破線・色）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="graphics"/> が null の場合。</exception>
    /// <remarks>
    /// 描画できる大きさでない場合（幅または高さが 0 以下）は何も描画しない。<br/>
    /// 破線の判定とダッシュ長はコア（<see cref="OverlayRectFrame"/>）を単一所有元とする。
    /// </remarks>
    public static void Render(Graphics graphics, OverlayRectFrame frame)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        if (!frame.HasDrawableSize)
        {
            return;
        }

        // 枠線は矩形の内側に描く（他の UI 版の Rectangle.Stroke と同じ見た目にする）
        var half = (float)frame.HalfThickness;
        var bounds = frame.Bounds.ToRectangleF();
        var inset = RectangleF.Inflate(bounds, -half, -half);
        if (inset.Width <= 0 || inset.Height <= 0)
        {
            return;
        }

        var pen = s_pen ??= new Pen(Color.Transparent);
        pen.Width = (float)frame.Thickness;
        pen.Alignment = PenAlignment.Center;
        pen.DashStyle = DashStyle.Solid;

        if (frame.DrawsBackground)
        {
            // 背景色の実線を下層に描く（破線の隙間から見える市松風の下地）
            pen.Color = frame.Background;
            graphics.DrawRectangle(pen, inset.X, inset.Y, inset.Width, inset.Height);
        }

        pen.Color = frame.Foreground;
        if (frame.IsDashed && TrySetDashPattern(pen, frame.DashLength, frame.GapLength))
        {
            graphics.DrawRectangle(pen, inset.X, inset.Y, inset.Width, inset.Height);
            pen.DashStyle = DashStyle.Solid;
            return;
        }

        graphics.DrawRectangle(pen, inset.X, inset.Y, inset.Width, inset.Height);
    }

    /// <summary>破線の長さをペンへ設定する（GDI+ の単位＝ペン幅の倍数へ換算する）。</summary>
    /// <param name="pen">設定するペン。</param>
    /// <param name="dashLength">ダッシュ長（ピクセル）。</param>
    /// <param name="gapLength">ダッシュの間隔（ピクセル）。</param>
    /// <returns>設定できた場合は true。 長さが不正（0 以下・非数）な場合は false（実線で描く）。</returns>
    /// <remarks>
    /// <see cref="Pen.DashPattern"/> はペン幅の倍数であり、0 以下や非数を受け付けない<br/>
    /// （不正な値を渡すと例外になるため、設定の可否を本メソッドが判断する）。
    /// </remarks>
    private static bool TrySetDashPattern(Pen pen, double dashLength, double gapLength)
    {
        var width = pen.Width;
        if (width <= 0 || dashLength <= 0 || gapLength <= 0)
        {
            return false;
        }

        pen.DashStyle = DashStyle.Custom;
        pen.DashPattern = [(float)(dashLength / width), (float)(gapLength / width)];
        return true;
    }
}
