using Avalonia;
using Avalonia.Media;
using EsUtil.SaveEvidence.App.Core.Services;

namespace EsUtil.SaveEvidence.App.AvaloniaUI.Controls;

/// <summary>画像の 4 辺・4 隅へ影を描く描画層（Avalonia 版）。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（描画手順をコントロールから分離する）。<br/>
/// 幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が単一所有するため、本クラスは<br/>
/// 「画素単位の帯を単色で塗る」手順だけを持つ（4 UI 版で同じ結果になる）。<br/>
/// <b>4 隅も帯で塗る</b>（隅の濃さは画像の隅の頂点からの斜めの距離で決まる。 放射グラデーションは描画側の<br/>
/// 半径の解釈が揺れ、隅だけ濃い点や薄い筋として見えた）。<br/>
/// <b><c>BoxShadow</c> を使わない理由</b>: 一覧のサムネイル（<see cref="ThumbnailList"/>）は <c>BoxShadow</c> を<br/>
/// 使うが、キャプチャページの画像は<b>他の UI 版と同じ合成</b>にする必要がある（<c>BoxShadow</c> の<br/>
/// ぼかしは画像の端で濃い輪を作り、画像との間に線として見える）。
/// </remarks>
internal static class ShadowRenderer
{
    /// <summary>画像の 4 辺・4 隅へ影を描く（画像は塗らない）。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="image">画像の表示矩形（DIP）。</param>
    /// <param name="scaling">DIP → 物理ピクセルの倍率。</param>
    public static void Render(DrawingContext context, Rect image, double scaling)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (image.Width <= 0 || image.Height <= 0 || scaling <= 0)
        {
            return;
        }

        foreach (var band in ShadowPlan.BuildDeviceBands(image.X, image.Y, image.Width, image.Height, scaling))
        {
            context.DrawRectangle(
                new SolidColorBrush(Color.FromUInt32(band.Argb)),
                null,
                ToDip(band.X, band.Y, band.Width, band.Height, scaling));
        }
    }

    /// <summary>物理ピクセルの矩形を DIP へ変換する。</summary>
    /// <param name="x">左端（ピクセル）。</param>
    /// <param name="y">上端（ピクセル）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="scaling">DIP → 物理ピクセルの倍率。</param>
    /// <returns>DIP の矩形（格子に一致する）。</returns>
    /// <remarks>
    /// 端数を持たないよう<b>わずかに内側へ寄せる</b>（<paramref name="x"/> ÷ 倍率は二進小数で表せず、
    /// 掛け戻すと 1 画素の手前へ落ちて端の画素が塗られない。 実測: 影の内側に 1 画素の白い線）。
    /// </remarks>
    private static Rect ToDip(int x, int y, int width, int height, double scaling)
    {
        const double Bias = 0.001;
        return new(
            (x + Bias) / scaling,
            (y + Bias) / scaling,
            (width - (Bias * 2)) / scaling,
            (height - (Bias * 2)) / scaling);
    }
}