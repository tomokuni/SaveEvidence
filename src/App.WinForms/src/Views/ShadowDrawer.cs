using System.Drawing.Drawing2D;
using EsUtil.SaveEvidence.App.Core.Services;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>画像の 4 辺・4 隅へ影を描く描画層（WinForms / GDI+ 版）。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（描画手順をコントロールから分離する）。<br/>
/// 幅・濃さの変化・矩形は <see cref="ShadowPlan"/>（App.Core）が単一所有するため、本クラスは<br/>
/// 「計画の 4 辺を線形グラデーションで、4 隅を放射グラデーションで塗る」手順だけを持つ<br/>
/// （4 UI 版で同じ結果になる。 一覧のサムネイルとキャプチャページの画像が共用する）。<br/>
/// <br/>
/// <b>端数を丸めない理由</b>: 画像は端数のある位置へ描かれる。 影の矩形を整数へ丸めると、<br/>
/// 画像と影の間に背景が 1 ピクセル筋状に見える（実測）。 計画の矩形をそのまま拡大し、<br/>
/// アンチエイリアスで隣り合う塗りを連続させる。<br/>
/// <br/>
/// <b>画像側へ 1 ピクセル広げる理由</b>: 画像の描画位置は GDI+ が端数を丸めるため、通知された<br/>
/// 表示矩形と最大 1 ピクセルずれる。 影を画像側へ広げておくと、そのずれが背景の筋として見えない<br/>
/// （画像の端 1 ピクセルに最も濃い影が掛かる）。
/// </remarks>
internal static class ShadowDrawer
{
    /// <summary>画像側へ広げる量（ピクセル）。</summary>
    private const int OverlapPixels = 1;

    /// <summary>画像の 4 辺・4 隅へ影を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="frame">影を落とす矩形（画像）。</param>
    /// <param name="scaling">描画単位への倍率（DIP → ピクセル）。 1 の場合はそのまま描く。</param>
    public static void Draw(Graphics graphics, RectangleF frame, double scaling)
    {
        if (frame.Width <= 0 || frame.Height <= 0 || scaling <= 0)
        {
            return;
        }

        var smoothing = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            foreach (var fill in ShadowPlan.BuildEdgeRects(frame.X, frame.Y, frame.Width, frame.Height))
            {
                var area = Scale(fill.X, fill.Y, fill.Width, fill.Height, scaling);
                FillEdge(graphics, GrowEdge(area, fill.Edge), fill.Edge);
            }

            foreach (var fill in ShadowPlan.BuildCornerFills(frame.X, frame.Y, frame.Width, frame.Height))
            {
                var area = Scale(fill.X, fill.Y, fill.Size, fill.Size, scaling);
                FillCorner(graphics, GrowCorner(area, fill), fill);
            }
        }
        finally
        {
            graphics.SmoothingMode = smoothing;
        }
    }

    /// <summary>計画の矩形を描画単位へ拡大する。</summary>
    /// <param name="x">左端。</param>
    /// <param name="y">上端。</param>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <param name="scaling">倍率。</param>
    /// <returns>描画単位の矩形。</returns>
    private static RectangleF Scale(double x, double y, double width, double height, double scaling)
        => new(
            (float)(x * scaling),
            (float)(y * scaling),
            (float)(width * scaling),
            (float)(height * scaling));

    /// <summary>辺の矩形を画像側へ広げる。</summary>
    /// <param name="rect">辺の矩形。</param>
    /// <param name="edge">辺（広げる向きを決める）。</param>
    /// <returns>画像側へ広げた矩形。</returns>
    private static RectangleF GrowEdge(RectangleF rect, ShadowEdgePlan edge)
        => edge.Side switch
        {
            ShadowSide.Top => RectangleF.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom + OverlapPixels),
            ShadowSide.Bottom => RectangleF.FromLTRB(rect.Left, rect.Top - OverlapPixels, rect.Right, rect.Bottom),
            ShadowSide.Left => RectangleF.FromLTRB(rect.Left, rect.Top, rect.Right + OverlapPixels, rect.Bottom),
            _ => RectangleF.FromLTRB(rect.Left - OverlapPixels, rect.Top, rect.Right, rect.Bottom),
        };

    /// <summary>隅の矩形を画像側（放射グラデーションの中心の向き）へ広げる。</summary>
    /// <param name="rect">隅の矩形。</param>
    /// <param name="fill">隅の計画（中心の向き）。</param>
    /// <returns>画像側へ広げた矩形。</returns>
    private static RectangleF GrowCorner(RectangleF rect, ShadowCornerFill fill)
        => RectangleF.FromLTRB(
            fill.CenterX >= 1 ? rect.Left : rect.Left - OverlapPixels,
            fill.CenterY >= 1 ? rect.Top : rect.Top - OverlapPixels,
            fill.CenterX >= 1 ? rect.Right + OverlapPixels : rect.Right,
            fill.CenterY >= 1 ? rect.Bottom + OverlapPixels : rect.Bottom);

    /// <summary>影の 1 辺分のグラデーションを描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">塗る矩形（画像側へ 1 ピクセル広げたもの。 グラデーションの軸もこの矩形で決める）。</param>
    /// <param name="edge">辺の計画（濃さの停止点と向き）。</param>
    private static void FillEdge(Graphics graphics, RectangleF area, ShadowEdgePlan edge)
    {
        if (area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        // 始点を外側にする（位置 0 が外側になる）
        var start = edge.IsVertical
            ? new PointF(area.Left, edge.IsOuterAtStart ? area.Top : area.Bottom)
            : new PointF(edge.IsOuterAtStart ? area.Left : area.Right, area.Top);
        var end = edge.IsVertical
            ? new PointF(area.Left, edge.IsOuterAtStart ? area.Bottom : area.Top)
            : new PointF(edge.IsOuterAtStart ? area.Right : area.Left, area.Top);

        using var brush = new LinearGradientBrush(start, end, Color.Black, Color.Black)
        {
            InterpolationColors = ToColorBlend(edge.Stops),
        };
        graphics.FillRectangle(brush, area);
    }

    /// <summary>影の 1 隅分の放射グラデーションを描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="area">塗る矩形（画像側へ 1 ピクセル広げたもの。 円の半径もこの矩形で決める）。</param>
    /// <param name="fill">隅の計画（放射グラデーションの中心）。</param>
    /// <remarks>
    /// GDI+ の <see cref="PathGradientBrush"/> は<b>中心から図形の輪郭へ</b>補間するため、半径 = 一辺の円を使えば<br/>
    /// 矩形の外側の距離に応じた濃さの変化（放射グラデーション）になる。 円の外側は塗らないよう矩形で切り抜く。
    /// </remarks>
    private static void FillCorner(Graphics graphics, RectangleF area, ShadowCornerFill fill)
    {
        if (area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        var center = new PointF(
            (float)(area.X + (fill.CenterX * area.Width)),
            (float)(area.Y + (fill.CenterY * area.Height)));

        using var path = new GraphicsPath();
        path.AddEllipse(new RectangleF(
            center.X - area.Width,
            center.Y - area.Height,
            area.Width * 2,
            area.Height * 2));

        using var brush = new PathGradientBrush(path)
        {
            CenterPoint = center,

            // PathGradientBrush の色の変化は位置 0 が輪郭・1 が中心である（線形グラデーションとは逆）
            InterpolationColors = ToCenterColorBlend(fill.Stops),
        };

        var state = graphics.Save();
        try
        {
            graphics.SetClip(area);
            graphics.FillRectangle(brush, area);
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    /// <summary>停止点を GDI+ の色の変化へ変換する（位置 0 が外側・中心）。</summary>
    /// <param name="stops">停止点（位置 0〜1 と色）。</param>
    /// <returns>色の変化（位置と色）。</returns>
    private static ColorBlend ToColorBlend(IReadOnlyList<ShadowStop> stops)
    {
        var positions = new float[stops.Count];
        var colors = new Color[stops.Count];
        for (var i = 0; i < stops.Count; i++)
        {
            positions[i] = (float)stops[i].Offset;
            colors[i] = Color.FromArgb((int)(stops[i].Argb >> 24), Color.Black);
        }

        return new ColorBlend { Positions = positions, Colors = colors };
    }

    /// <summary>停止点を「輪郭が位置 0」の色の変化へ変換する。</summary>
    /// <param name="stops">停止点（位置 0 が中心、1 が輪郭の順）。</param>
    /// <returns>色の変化（<see cref="PathGradientBrush"/> 用。 位置 0 が輪郭、1 が中心）。</returns>
    private static ColorBlend ToCenterColorBlend(IReadOnlyList<ShadowStop> stops)
    {
        var positions = new float[stops.Count];
        var colors = new Color[stops.Count];
        for (var i = 0; i < stops.Count; i++)
        {
            var source = stops[stops.Count - 1 - i];
            positions[i] = 1 - (float)source.Offset;
            colors[i] = Color.FromArgb((int)(source.Argb >> 24), Color.Black);
        }

        return new ColorBlend { Positions = positions, Colors = colors };
    }
}