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
    /// <param name="canvas">描画先のキャンバス（物理ピクセル座標。 拡大を適用しないこと）。</param>
    /// <param name="imageRect">画像の矩形（DIP）。</param>
    /// <param name="scaling">DIP → 物理ピクセルの倍率。</param>
    public static void Render(SKCanvas canvas, SKRect imageRect, double scaling)
        => Render(canvas, imageRect.Left, imageRect.Top, imageRect.Width, imageRect.Height, scaling);

    /// <summary>画像の 4 辺・4 隅へ影を描く（画像は塗らない）。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル座標。 拡大を適用しないこと）。</param>
    /// <param name="x">画像の左端（DIP）。</param>
    /// <param name="y">画像の上端（DIP）。</param>
    /// <param name="width">画像の幅（DIP）。</param>
    /// <param name="height">画像の高さ（DIP）。</param>
    /// <param name="scaling">DIP → 物理ピクセルの倍率。</param>
    /// <remarks>
    /// 4 辺は 1 画素の帯を単色で塗る（<see cref="ShadowPlan.BuildDeviceBands"/>。 端の扱いが描画 API に<br/>
    /// 依存しない）。 4 隅は放射グラデーションで塗る（<see cref="ShadowPlan.BuildCornerFills"/>）。
    /// </remarks>
    public static void Render(SKCanvas canvas, double x, double y, double width, double height, double scaling)
    {
        if (scaling <= 0)
        {
            return;
        }

        using (var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill })
        {
            foreach (var band in ShadowPlan.BuildDeviceBands(x, y, width, height, scaling))
            {
                var argb = band.Argb;

                // SKColor の引数は (赤, 緑, 青, 透過度) の順である
                paint.Color = new SKColor((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));
                canvas.DrawRect(
                    new SKRect(band.X, band.Y, band.X + band.Width, band.Y + band.Height),
                    paint);
            }
        }

        foreach (var fill in ShadowPlan.BuildCornerFills(x, y, width, height))
        {
            var left = fill.X * scaling;
            var top = fill.Y * scaling;
            var size = fill.Size * scaling;
            var center = new SKPoint(
                (float)(left + (fill.CenterX * size)),
                (float)(top + (fill.CenterY * size)));
            DrawWithShader(
                canvas,
                left,
                top,
                size,
                size,
                SKShader.CreateRadialGradient(
                    center, (float)size, ToColors(fill.Stops), ToPositions(fill.Stops), SKShaderTileMode.Clamp));
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