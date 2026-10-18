using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.Platform.Skia;
using SkiaSharp;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>画像の 4 辺・4 隅へ影を描く描画層（Skia 版。 計画を描画の入力へ写す）。</summary>
/// <remarks>
/// 幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が、描画手順は
/// <see cref="SkiaShadowRenderer"/>（Platform.Skia）が単一所有する。 本クラスは両者の変換だけを持つ<br/>
/// （キャプチャページの画像と、一覧のサムネイルが共用する）。
/// </remarks>
internal static class ShadowRenderer
{
    /// <summary>画像の 4 辺・4 隅へ影を描く（画像の表示矩形と倍率を受け取る）。</summary>
    /// <param name="canvas">描画先のキャンバス（物理ピクセル座標。 拡大を適用しないこと）。</param>
    /// <param name="imageRect">画像の表示矩形（DIP）。</param>
    /// <param name="scaling">DIP → 物理ピクセルの倍率。</param>
    public static void Render(SKCanvas canvas, SKRect imageRect, double scaling)
        => Render(canvas, imageRect.Left, imageRect.Top, imageRect.Width, imageRect.Height, scaling);

    /// <summary>画像の 4 辺・4 隅へ影を描く（DIP の画像矩形と倍率を受け取る）。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="x">画像の左端（DIP）。</param>
    /// <param name="y">画像の上端（DIP）。</param>
    /// <param name="width">画像の幅（DIP）。</param>
    /// <param name="height">画像の高さ（DIP）。</param>
    /// <param name="scaling">DIP → 物理ピクセルの倍率。</param>
    public static void Render(SKCanvas canvas, double x, double y, double width, double height, double scaling)
    {
        var bands = ShadowPlan.BuildDeviceBands(x, y, width, height, scaling);
        if (bands.Count == 0)
        {
            return;
        }

        SkiaShadowRenderer.Draw(canvas, ToSkia(bands));
    }

    /// <summary>帯を Skia の入力へ変換する。</summary>
    /// <param name="bands">帯（物理ピクセル）。</param>
    /// <returns>Skia の入力。</returns>
    private static SkiaShadowBand[] ToSkia(IReadOnlyList<ShadowBand> bands)
    {
        var result = new SkiaShadowBand[bands.Count];
        for (var i = 0; i < bands.Count; i++)
        {
            var band = bands[i];
            result[i] = new SkiaShadowBand(band.X, band.Y, band.Width, band.Height, band.Argb);
        }

        return result;
    }
}