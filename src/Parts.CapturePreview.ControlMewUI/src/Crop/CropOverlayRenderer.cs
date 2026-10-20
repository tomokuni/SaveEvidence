using Aprillz.MewUI;
using Aprillz.MewUI.Rendering;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.SaveEvidence.Platform.MewUI;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Crop;

/// <summary>オーバーレイ矩形の描画入力（<see cref="OverlayRectFrame"/>）を MewUI の描画コンテキストへ写す。</summary>
/// <remarks>
/// コアが確定した描画入力（配置・太さ・破線の判定と長さ・色）を写すだけの層であり、
/// 式・判断・クランプを持たない（描画手順として「枠を内側へ寄せる」「背景→前景の順に描く」だけを持つ）。<br/>
/// 実線は前景色の枠のみ、破線は「背景色の実線 → 前景色の破線」の 2 回描画で市松風の視認性を出す
/// （Avalonia 版が背景色の下層矩形と前景色の破線矩形を重ねる方式と等価）。<br/>
/// <b>破線の描き方</b>: MewUI の描画 API は線種を持たないため、辺ごとに線分へ分解して描く
/// （長さはコアの <see cref="OverlayRectFrame.DashLength"/> / <see cref="OverlayRectFrame.GapLength"/> に従う）。<br/>
/// 座標変換（対象座標 → ビューポート座標）と描画入力の組み立ては呼び出し側が済ませている前提とする。
/// </remarks>
internal static class CropOverlayRenderer
{
    /// <summary>描画入力を描画コンテキストへ描画する。</summary>
    /// <param name="context">描画先のコンテキスト（DIP 座標で描画できる状態であること）。</param>
    /// <param name="frame">描画入力（コアが確定した配置・太さ・破線・色）。</param>
    /// <remarks>
    /// 描画できる大きさでない場合（幅または高さが 0 以下）は何も描画しない（早期リターン）。
    /// 破線の判定とダッシュ長はコア（<see cref="OverlayRectFrame"/>）を単一所有元とする。
    /// </remarks>
    public static void Render(IGraphicsContext context, OverlayRectFrame frame)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!frame.HasDrawableSize)
        {
            return;
        }

        // 枠線は矩形の内側に描く（Avalonia の Rectangle.Stroke と同じ見た目にする）
        var bounds = frame.Bounds;
        var half = frame.HalfThickness;
        var inset = new EsRect(
            bounds.X + half, bounds.Y + half, bounds.Width - frame.Thickness, bounds.Height - frame.Thickness);

        if (frame.DrawsBackground)
        {
            // 背景色の実線を下層に描く（破線の隙間から見える市松風の下地）
            StrokeRect(context, inset, frame.Background.ToMewUIColor(), frame.Thickness);
        }

        if (frame.IsDashed)
        {
            StrokeDashedRect(context, inset, frame);
            return;
        }

        StrokeRect(context, inset, frame.Foreground.ToMewUIColor(), frame.Thickness);
    }

    /// <summary>矩形の 4 辺を実線で描く。</summary>
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="rect">枠の中心線が通る矩形。</param>
    /// <param name="color">線の色。</param>
    /// <param name="thickness">線の太さ。</param>
    private static void StrokeRect(IGraphicsContext context, EsRect rect, Color color, double thickness)
    {
        var left = rect.X;
        var top = rect.Y;
        var right = rect.X + rect.Width;
        var bottom = rect.Y + rect.Height;

        context.DrawLine(new Point(left, top), new Point(right, top), color, thickness);
        context.DrawLine(new Point(right, top), new Point(right, bottom), color, thickness);
        context.DrawLine(new Point(right, bottom), new Point(left, bottom), color, thickness);
        context.DrawLine(new Point(left, bottom), new Point(left, top), color, thickness);
    }

    /// <summary>矩形の 4 辺を破線で描く。</summary>
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="rect">枠の中心線が通る矩形。</param>
    /// <param name="frame">描画入力（色・太さ・ダッシュ長・間隔）。</param>
    /// <remarks>辺ごとに破線を引き直す（辺の境目で位相を合わせない。 Avalonia 版と同じ扱い）。</remarks>
    private static void StrokeDashedRect(IGraphicsContext context, EsRect rect, in OverlayRectFrame frame)
    {
        var color = frame.Foreground.ToMewUIColor();
        var left = rect.X;
        var top = rect.Y;
        var right = rect.X + rect.Width;
        var bottom = rect.Y + rect.Height;

        StrokeDashedLine(context, left, top, right, top, color, frame);
        StrokeDashedLine(context, right, top, right, bottom, color, frame);
        StrokeDashedLine(context, right, bottom, left, bottom, color, frame);
        StrokeDashedLine(context, left, bottom, left, top, color, frame);
    }

    /// <summary>1 本の線分を破線で描く。</summary>
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="x0">始点の X 座標。</param>
    /// <param name="y0">始点の Y 座標。</param>
    /// <param name="x1">終点の X 座標。</param>
    /// <param name="y1">終点の Y 座標。</param>
    /// <param name="color">線の色。</param>
    /// <param name="frame">描画入力（太さ・ダッシュ長・間隔）。</param>
    /// <remarks>
    /// ダッシュ長・間隔が 0 以下の場合は実線として描く（0 除算で停止しないようにする）。
    /// 端数は切り上げず、線分の終端を超えるダッシュは終端で切る。
    /// </remarks>
    private static void StrokeDashedLine(
        IGraphicsContext context, double x0, double y0, double x1, double y1, Color color, in OverlayRectFrame frame)
    {
        var dash = frame.DashLength;
        var gap = frame.GapLength;
        if (dash <= 0 || gap <= 0)
        {
            context.DrawLine(new Point(x0, y0), new Point(x1, y1), color, frame.Thickness);
            return;
        }

        var dx = x1 - x0;
        var dy = y1 - y0;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length <= 0)
        {
            return;
        }

        var stepX = dx / length;
        var stepY = dy / length;

        for (var start = 0.0; start < length; start += dash + gap)
        {
            var end = Math.Min(start + dash, length);
            context.DrawLine(
                new Point(x0 + (stepX * start), y0 + (stepY * start)),
                new Point(x0 + (stepX * end), y0 + (stepY * end)),
                color,
                frame.Thickness);
        }
    }
}
