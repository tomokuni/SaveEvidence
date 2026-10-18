using System.Drawing.Drawing2D;
using EsUtil.SaveEvidence.App.Core.Services;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>画像の 4 辺へ影を描く描画層（WinForms / GDI+ 版）。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（描画手順をコントロールから分離する）。<br/>
/// 幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が単一所有するため、本クラスは<br/>
/// 「計画の 4 辺の矩形を、計画の停止点のグラデーションで塗る」手順だけを持つ<br/>
/// （4 UI 版で同じ結果になる。 一覧のサムネイルとキャプチャページの画像が共用する）。<br/>
/// <b>画像は塗らない</b>: 辺の矩形は画像の外側にだけ置くため、画像が覆われることはない。
/// </remarks>
internal static class ShadowDrawer
{
    /// <summary>画像の 4 辺へ影を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="frame">影を落とす矩形（画像）。</param>
    public static void Draw(Graphics graphics, Rectangle frame)
    {
        foreach (var fill in ShadowPlan.BuildEdgeRects(frame.X, frame.Y, frame.Width, frame.Height))
        {
            FillStrip(
                graphics,
                new RectangleF(
                    (float)fill.X,
                    (float)fill.Y,
                    (float)fill.Width,
                    (float)fill.Height),
                fill.Edge);
        }
    }

    /// <summary>影の 1 辺分のグラデーションを描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="rect">描く矩形（画像の外側）。</param>
    /// <param name="edge">辺の計画（濃さの停止点と向き）。</param>
    private static void FillStrip(Graphics graphics, RectangleF rect, ShadowEdgePlan edge)
    {
        if (rect.Width <= 0 || rect.Height <= 0 || edge.Extent <= 0)
        {
            return;
        }

        var count = edge.Stops.Count;
        var positions = new float[count];
        var colors = new Color[count];
        for (var i = 0; i < count; i++)
        {
            positions[i] = (float)edge.Stops[i].Offset;
            colors[i] = Color.FromArgb((int)(edge.Stops[i].Argb >> 24), Color.Black);
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
            InterpolationColors = new ColorBlend { Positions = positions, Colors = colors },
        };
        graphics.FillRectangle(brush, rect);
    }
}
