using EsUtil.SaveEvidence.App.Core.Services;
using SkiaSharp;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>画像の 4 辺へ影を描く描画層（MewUI / Skia 版）。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（描画手順をコントロールから分離する）。<br/>
/// 幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が単一所有するため、本クラスは<br/>
/// 「計画の 4 辺の矩形を、計画の停止点のグラデーションで塗る」手順だけを持つ<br/>
/// （4 UI 版で同じ結果になる）。<br/>
/// <b>座標系</b>: 呼び出し側が DIP で描ける状態（<c>canvas.Scale</c> 済み）で呼ぶ。
/// </remarks>
internal static class ShadowRenderer
{
    /// <summary>画像の 4 辺へ影を描く（画像は塗らない）。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標）。</param>
    /// <param name="imageRect">画像の矩形（DIP）。</param>
    public static void Render(SKCanvas canvas, SKRect imageRect)
        => Render(canvas, imageRect.Left, imageRect.Top, imageRect.Width, imageRect.Height);

    /// <summary>画像の 4 辺へ影を描く（画像は塗らない）。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標）。</param>
    /// <param name="x">画像の左端（DIP）。</param>
    /// <param name="y">画像の上端（DIP）。</param>
    /// <param name="width">画像の幅（DIP）。</param>
    /// <param name="height">画像の高さ（DIP）。</param>
    public static void Render(SKCanvas canvas, double x, double y, double width, double height)
    {
        foreach (var fill in ShadowPlan.BuildEdgeRects(x, y, width, height))
        {
            var stops = fill.Edge.Stops;
            var colors = new SKColor[stops.Count];
            var positions = new float[stops.Count];
            for (var i = 0; i < stops.Count; i++)
            {
                // SKColor の引数は (赤, 緑, 青, 透過度) の順である
                colors[i] = new SKColor(
                    (byte)(stops[i].Argb >> 16),
                    (byte)(stops[i].Argb >> 8),
                    (byte)stops[i].Argb,
                    (byte)(stops[i].Argb >> 24));
                positions[i] = (float)stops[i].Offset;
            }

            var (start, end) = GradientAxis(fill);
            using var shader = SKShader.CreateLinearGradient(start, end, colors, positions, SKShaderTileMode.Clamp);
            using var paint = new SKPaint { Shader = shader, IsAntialias = false };
            canvas.DrawRect(
                new SKRect((float)fill.X, (float)fill.Y, (float)(fill.X + fill.Width), (float)(fill.Y + fill.Height)),
                paint);
        }
    }

    /// <summary>影のグラデーションの始点と終点を求める（位置 0 が外側）。</summary>
    /// <param name="fill">塗る矩形と辺の計画。</param>
    /// <returns>始点と終点（DIP）。</returns>
    private static (SKPoint Start, SKPoint End) GradientAxis(ShadowEdgeFill fill)
    {
        var left = (float)fill.X;
        var top = (float)fill.Y;
        var right = (float)(fill.X + fill.Width);
        var bottom = (float)(fill.Y + fill.Height);

        if (fill.Edge.IsVertical)
        {
            return fill.Edge.IsOuterAtStart
                ? (new SKPoint(left, top), new SKPoint(left, bottom))
                : (new SKPoint(left, bottom), new SKPoint(left, top));
        }

        return fill.Edge.IsOuterAtStart
            ? (new SKPoint(left, top), new SKPoint(right, top))
            : (new SKPoint(right, top), new SKPoint(left, top));
    }
}
