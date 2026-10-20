using Aprillz.MewUI;
using Aprillz.MewUI.Rendering;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.App.Core.Services;

namespace EsUtil.SaveEvidence.App.MewUI.Views;

/// <summary>画像の 4 辺・4 隅へ影を描く描画層（MewUI 標準描画版）。</summary>
/// <remarks>
/// 幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が単一所有する。 本クラスは計画の帯を
/// MewUI の矩形へ写すだけで、判断・計算を持たない<br/>
/// （キャプチャページの画像と、一覧のサムネイルが共用する）。<br/>
/// <b>座標系</b>: 呼び出し側（ズームビューの描画フック）は DIP 座標で描くため、帯も DIP で組み立てる。
/// 帯の太さは 1 DIP であり、物理ピクセルへは描画先が拡大する。
/// </remarks>
internal static class ShadowRenderer
{
    /// <summary>画像の 4 辺・4 隅へ影を描く（画像の表示矩形を受け取る）。</summary>
    /// <param name="context">描画先のコンテキスト（DIP 座標で描画できる状態であること）。</param>
    /// <param name="imageRect">画像の表示矩形（DIP）。</param>
    /// <param name="scaling">DIP → 物理ピクセルの倍率（描画フックの契約を満たすために受け取る）。</param>
    public static void Render(IGraphicsContext context, EsRect imageRect, double scaling)
        => Render(context, imageRect.X, imageRect.Y, imageRect.Width, imageRect.Height, scaling);

    /// <summary>画像の 4 辺・4 隅へ影を描く（DIP の画像矩形を受け取る）。</summary>
    /// <param name="context">描画先のコンテキスト。</param>
    /// <param name="x">画像の左端（DIP）。</param>
    /// <param name="y">画像の上端（DIP）。</param>
    /// <param name="width">画像の幅（DIP）。</param>
    /// <param name="height">画像の高さ（DIP）。</param>
    /// <param name="scaling">DIP → 物理ピクセルの倍率（描画フックの契約を満たすために受け取る）。</param>
    /// <remarks>
    /// 帯は <see cref="ShadowPlan.BuildDeviceBands"/> へ倍率 1 を渡して DIP で組み立てる（描画先が拡大する）。
    /// </remarks>
    public static void Render(IGraphicsContext context, double x, double y, double width, double height, double scaling)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = scaling;

        foreach (var band in ShadowPlan.BuildDeviceBands(x, y, width, height, 1.0))
        {
            context.FillRectangle(
                new Rect(band.X, band.Y, band.Width, band.Height),
                Color.FromArgb(band.Argb));
        }
    }
}