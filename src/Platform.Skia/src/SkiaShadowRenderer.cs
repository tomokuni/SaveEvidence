using SkiaSharp;

namespace EsUtil.SaveEvidence.Platform.Skia;

/// <summary>画像の 4 辺・4 隅へ影を描く（Skia 版。 描画手順を単一所有する）。</summary>
/// <remarks>
/// <b>1 画素の帯を単色で塗るだけ</b>である: グラデーション ブラシの端・半径の扱いは描画 API ごとに<br/>
/// 異なるため、画素単位の帯（<see cref="SkiaShadowBand"/>）を単色で塗ればどの環境でも同じ結果になる。<br/>
/// <b>4 隅も帯で塗る</b>（横の帯と縦の帯が隅で重なって合成される。 濃さの計画は <c>ShadowPlan</c> が単一所有）。<br/>
/// <b>アンチエイリアスは無効</b>にする（端の画素を半透明にすると、画像との間に線として見える）。
/// </remarks>
public static class SkiaShadowRenderer
{
    /// <summary>影の帯を描く（画像は塗らない）。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル座標。 拡大を適用しないこと）。</param>
    /// <param name="bands">帯（1 画素幅の矩形と色。 物理ピクセル）。</param>
    public static void Draw(SKCanvas canvas, ReadOnlySpan<SkiaShadowBand> bands)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        foreach (var band in bands)
        {
            paint.Color = ToColor(band.Argb);
            canvas.DrawRect(new SKRect(band.X, band.Y, band.X + band.Width, band.Y + band.Height), paint);
        }
    }

    /// <summary>色を Skia の色へ変換する。</summary>
    /// <param name="argb">色（<c>0xAARRGGBB</c>）。</param>
    /// <returns>Skia の色（引数の順は 赤・緑・青・透過度）。</returns>
    private static SKColor ToColor(uint argb)
        => new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));
}