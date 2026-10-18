using EsUtil.SaveEvidence.App.Core.Services;
using SkiaSharp;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>画像の 4 辺・4 隅へ影を描く描画層（MewUI / Skia 版）。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（描画手順をコントロールから分離する）。<br/>
/// 幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が単一所有するため、本クラスは<br/>
/// 「計画の 4 辺を線形グラデーションで、4 隅を放射グラデーションで塗る」手順だけを持つ<br/>
/// （4 UI 版で同じ結果になる。 一覧のサムネイルとキャプチャページの画像が共用する）。<br/>
/// <b>座標系</b>: 呼び出し側が DIP で描ける状態（<c>canvas.Scale</c> 済み）で呼ぶ。
/// </remarks>
internal static class ShadowRenderer
{
    /// <summary>画像の 4 辺・4 隅へ影を描く（画像は塗らない）。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標）。</param>
    /// <param name="imageRect">画像の矩形（DIP）。</param>
    public static void Render(SKCanvas canvas, SKRect imageRect)
        => Render(canvas, imageRect.Left, imageRect.Top, imageRect.Width, imageRect.Height);

    /// <summary>画像の 4 辺・4 隅へ影を描く（画像は塗らない）。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標）。</param>
    /// <param name="x">画像の左端（DIP）。</param>
    /// <param name="y">画像の上端（DIP）。</param>
    /// <param name="width">画像の幅（DIP）。</param>
    /// <param name="height">画像の高さ（DIP）。</param>
    public static void Render(SKCanvas canvas, double x, double y, double width, double height)
    {
        foreach (var fill in ShadowPlan.BuildEdgeRects(x, y, width, height))
        {
            var (start, end) = EdgeAxis(fill);
            DrawWithShader(
                canvas,
                fill.X,
                fill.Y,
                fill.Width,
                fill.Height,
                SKShader.CreateLinearGradient(start, end, ToColors(fill.Edge.Stops), ToPositions(fill.Edge.Stops), SKShaderTileMode.Clamp));
        }

        foreach (var fill in ShadowPlan.BuildCornerFills(x, y, width, height))
        {
            var center = new SKPoint(
                (float)(fill.X + (fill.CenterX * fill.Size)),
                (float)(fill.Y + (fill.CenterY * fill.Size)));
            DrawWithShader(
                canvas,
                fill.X,
                fill.Y,
                fill.Size,
                fill.Size,
                SKShader.CreateRadialGradient(
                    center, (float)fill.Size, ToColors(fill.Stops), ToPositions(fill.Stops), SKShaderTileMode.Clamp));
        }
    }

    /// <summary>影の 1 つの領域を、グラデーションのシェーダーで塗る。</summary>
    /// <param name="canvas">描画先のキャンバス（DIP 座標）。</param>
    /// <param name="x">左端。</param>
    /// <param name="y">上端。</param>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <param name="shader">塗りに使うグラデーション（本メソッドが破棄する）。</param>
    private static void DrawWithShader(SKCanvas canvas, double x, double y, double width, double height, SKShader shader)
    {
        using (shader)
        using (var paint = new SKPaint { Shader = shader, IsAntialias = false })
        {
            canvas.DrawRect(new SKRect((float)x, (float)y, (float)(x + width), (float)(y + height)), paint);
        }
    }

    /// <summary>影のグラデーションの始点と終点を求める（位置 0 が外側）。</summary>
    /// <param name="fill">塗る矩形と辺の計画。</param>
    /// <returns>始点と終点（DIP）。</returns>
    private static (SKPoint Start, SKPoint End) EdgeAxis(ShadowEdgeFill fill)
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

    /// <summary>停止点の色を取り出す。</summary>
    /// <param name="stops">停止点。</param>
    /// <returns>色（Skia の引数順は 赤・緑・青・透過度）。</returns>
    private static SKColor[] ToColors(IReadOnlyList<ShadowStop> stops)
    {
        var colors = new SKColor[stops.Count];
        for (var i = 0; i < stops.Count; i++)
        {
            var argb = stops[i].Argb;
            colors[i] = new SKColor((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));
        }

        return colors;
    }

    /// <summary>停止点の位置を取り出す。</summary>
    /// <param name="stops">停止点。</param>
    /// <returns>位置（0〜1）。</returns>
    private static float[] ToPositions(IReadOnlyList<ShadowStop> stops)
    {
        var positions = new float[stops.Count];
        for (var i = 0; i < stops.Count; i++)
        {
            positions[i] = (float)stops[i].Offset;
        }

        return positions;
    }
}