using System.Drawing.Drawing2D;
using EsUtil.SaveEvidence.App.Core.Services;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>画像の 4 辺・4 隅へ影を描く描画層（WinForms / GDI+ 版）。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（描画手順をコントロールから分離する）。<br/>
/// 幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が単一所有するため、本クラスは<br/>
/// 計画を GDI+ の塗りへ写すだけである（一覧のサムネイルとキャプチャページの画像が共用する）。<br/>
/// <br/>
/// <b>1 画素の帯を単色で塗るだけ</b>である（<see cref="ShadowPlan.BuildDeviceBands"/>）: GDI+ の<br/>
/// <c>LinearGradientBrush</c> は軸の外側を繰り返し、<b>最も濃い色の 1 画素を影の外側へ描く</b>（実測）。<br/>
/// 画素単位の帯を塗れば、その現象も端の半透明も起こらない。<br/>
/// <b>4 隅も帯で塗る</b>（横の帯と縦の帯が隅で重なって合成される）。
/// </remarks>
internal static class ShadowDrawer
{
    /// <summary>画像の 4 辺・4 隅へ影を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="frame">影を落とす矩形（画像。 DIP）。</param>
    /// <param name="scaling">描画単位への倍率（DIP → ピクセル）。 1 の場合はそのまま描く。</param>
    public static void Draw(Graphics graphics, RectangleF frame, double scaling)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        var bands = ShadowPlan.BuildDeviceBands(frame.X, frame.Y, frame.Width, frame.Height, scaling);
        if (bands.Count == 0)
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
            using var brush = new SolidBrush(Color.Black);
            foreach (var band in bands)
            {
                brush.Color = Color.FromArgb((int)(band.Argb >> 24), Color.Black);
                graphics.FillRectangle(brush, band.X, band.Y, band.Width, band.Height);
            }
        }
        finally
        {
            graphics.SmoothingMode = smoothing;
            graphics.PixelOffsetMode = offsetMode;
        }
    }
}