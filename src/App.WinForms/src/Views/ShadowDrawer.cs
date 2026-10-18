using System.Drawing.Drawing2D;
using EsUtil.SaveEvidence.App.Core.Services;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>画像の 4 辺・4 隅へ影を描く描画層（WinForms / GDI+ 版）。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（描画手順をコントロールから分離する）。<br/>
/// 幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が単一所有するため、本クラスは<br/>
/// 「計画の 4 辺を線形グラデーションで、4 隅を放射グラデーションで塗る」手順だけを持つ<br/>
/// （4 UI 版で同じ結果になる。 一覧のサムネイルとキャプチャページの画像が共用する）。<br/>
/// <br/>
/// <b>整数の画素へ揃える理由</b>: 画像は GDI+ が端数を丸めた位置へ描く。 影の矩形を端数のまま<br/>
/// 塗ると、GDI+ は塗りを切り捨てるため画像との間に背景が 1 ピクセル残り「白い線」に見える（実測）。<br/>
/// 画像の端と同じ丸め（四捨五入）で矩形を組み立て、アンチエイリアスを切って隙間なく接させる。
/// </remarks>
internal static class ShadowDrawer
{
    /// <summary>画像の 4 辺と 4 隅の矩形（ピクセル）を求める。</summary>
    /// <param name="frame">影を落とす矩形（画像）。</param>
    /// <param name="scaling">描画単位への倍率（DIP → ピクセル）。 1 の場合はそのまま。</param>
    /// <returns>上辺・右辺・下辺・左辺・左上・右上・右下・左下の矩形（この順）。</returns>
    public static IReadOnlyList<Rectangle> BuildRects(RectangleF frame, double scaling)
    {
        if (frame.Width <= 0 || frame.Height <= 0 || scaling <= 0)
        {
            return [];
        }

        var extent = (int)Math.Round(ShadowPlan.BlurRadius * scaling);
        if (extent <= 0)
        {
            return [];
        }

        // 画像の端は GDI+ の描画と同じ四捨五入で合わせる（ずれると画素が 1 つ余る / 掛かる）
        var left = (int)Math.Round(frame.X * scaling);
        var top = (int)Math.Round(frame.Y * scaling);
        var right = (int)Math.Round((frame.X + frame.Width) * scaling);
        var bottom = (int)Math.Round((frame.Y + frame.Height) * scaling);
        var width = Math.Max(0, right - left);
        var height = Math.Max(0, bottom - top);

        return
        [
            new Rectangle(left, top - extent, width, extent),
            new Rectangle(right, top, extent, height),
            new Rectangle(left, bottom, width, extent),
            new Rectangle(left - extent, top, extent, height),
            new Rectangle(left - extent, top - extent, extent, extent),
            new Rectangle(right, top - extent, extent, extent),
            new Rectangle(right, bottom, extent, extent),
            new Rectangle(left - extent, bottom, extent, extent),
        ];
    }

    /// <summary>影を塗る図形（表示領域の切り抜きに使う）を組み立てる。</summary>
    /// <param name="frame">影を落とす矩形（画像）。</param>
    /// <param name="scaling">描画単位への倍率（DIP → ピクセル）。</param>
    /// <returns>影の帯の図形（<see cref="Region"/> が複製するため、呼び出し側が破棄する）。</returns>
    public static GraphicsPath BuildRegionPath(RectangleF frame, double scaling)
    {
        // 重なった矩形を打ち消さないよう和集合にする（既定の Alternate は境界の画素が抜ける）
        var path = new GraphicsPath { FillMode = FillMode.Winding };
        foreach (var rect in BuildRects(frame, scaling))
        {
            path.AddRectangle(rect);
        }

        return path;
    }

    /// <summary>画像の 4 辺・4 隅へ影を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="frame">影を落とす矩形（画像）。</param>
    /// <param name="scaling">描画単位への倍率（DIP → ピクセル）。 1 の場合はそのまま描く。</param>
    public static void Draw(Graphics graphics, RectangleF frame, double scaling)
    {
        var rects = BuildRects(frame, scaling);
        if (rects.Count < 8)
        {
            return;
        }

        // 端の画素を半透明にしない（半端な位置へ描くと、画像との間に線として見える）
        var smoothing = graphics.SmoothingMode;
        var offsetMode = graphics.PixelOffsetMode;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.PixelOffsetMode = PixelOffsetMode.None;
        try
        {
            FillEdge(graphics, rects[0], ShadowPlan.Edge(ShadowSide.Top));
            FillEdge(graphics, rects[1], ShadowPlan.Edge(ShadowSide.Right));
            FillEdge(graphics, rects[2], ShadowPlan.Edge(ShadowSide.Bottom));
            FillEdge(graphics, rects[3], ShadowPlan.Edge(ShadowSide.Left));

            FillCorner(graphics, rects[4], 1, 1);
            FillCorner(graphics, rects[5], 0, 1);
            FillCorner(graphics, rects[6], 0, 0);
            FillCorner(graphics, rects[7], 1, 0);
        }
        finally
        {
            graphics.SmoothingMode = smoothing;
            graphics.PixelOffsetMode = offsetMode;
        }
    }

    /// <summary>影の 1 辺分のグラデーションを描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="rect">描く矩形（画像の外側）。</param>
    /// <param name="edge">辺の計画（濃さの停止点と向き）。</param>
    private static void FillEdge(Graphics graphics, Rectangle rect, ShadowEdgePlan edge)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        // 始点を外側にする（位置 0 が外側になる）
        var start = edge.IsVertical
            ? new PointF(rect.Left, edge.IsOuterAtStart ? rect.Top : rect.Bottom)
            : new PointF(edge.IsOuterAtStart ? rect.Left : rect.Right, rect.Top);
        var end = edge.IsVertical
            ? new PointF(rect.Left, edge.IsOuterAtStart ? rect.Bottom : rect.Top)
            : new PointF(edge.IsOuterAtStart ? rect.Right : rect.Left, rect.Top);

        using var brush = new LinearGradientBrush(start, end, Color.Black, Color.Black)
        {
            InterpolationColors = ToColorBlend(edge.Stops),
        };
        graphics.FillRectangle(brush, rect);
    }

    /// <summary>影の 1 隅分の放射グラデーションを描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="rect">隅の矩形（画像の外側）。</param>
    /// <param name="centerX">中心の X（0〜1。 隅の矩形に対する相対位置）。</param>
    /// <param name="centerY">中心の Y（0〜1）。</param>
    /// <remarks>
    /// GDI+ の <see cref="PathGradientBrush"/> は<b>中心から図形の輪郭へ</b>補間するため、半径 = 一辺の円を使えば<br/>
    /// 矩形の外側の距離に応じた濃さの変化（放射グラデーション）になる。 円の外側は塗らないよう矩形で切り抜く。
    /// </remarks>
    private static void FillCorner(Graphics graphics, Rectangle rect, int centerX, int centerY)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var center = new PointF(
            rect.Left + (centerX * rect.Width),
            rect.Top + (centerY * rect.Height));

        using var path = new GraphicsPath();
        path.AddEllipse(new RectangleF(
            center.X - rect.Width,
            center.Y - rect.Height,
            rect.Width * 2,
            rect.Height * 2));

        using var brush = new PathGradientBrush(path)
        {
            CenterPoint = center,

            // PathGradientBrush の色の変化は位置 0 が輪郭・1 が中心である（線形グラデーションとは逆）
            InterpolationColors = ToCenterColorBlend(ShadowPlan.CornerStops),
        };

        var state = graphics.Save();
        try
        {
            graphics.SetClip(rect);
            graphics.FillRectangle(brush, rect);
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