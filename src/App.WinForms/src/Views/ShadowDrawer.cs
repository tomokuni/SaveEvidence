using System.Drawing.Drawing2D;
using EsUtil.SaveEvidence.App.Core.Services;

namespace EsUtil.SaveEvidence.App.WinForms.Views;

/// <summary>画像の 4 辺・4 隅へ影を描く描画層（WinForms / GDI+ 版）。</summary>
/// <remarks>
/// <b>適用パターン</b>: 描画層（描画手順をコントロールから分離する）。<br/>
/// 幅・濃さの変化は <see cref="ShadowPlan"/>（App.Core）が単一所有するため、本クラスは<br/>
/// 計画を GDI+ の塗りへ写すだけである（一覧のサムネイルとキャプチャページの画像が共用する）。<br/>
/// <br/>
/// <b>4 辺は 1 画素の帯を単色で塗る</b>（<see cref="ShadowPlan.BuildDeviceBands"/>）: GDI+ の<br/>
/// <c>LinearGradientBrush</c> は軸の外側を繰り返し、<b>最も濃い色の 1 画素を影の外側へ描く</b>（実測）。<br/>
/// 画素単位の帯を塗れば、その現象も端の半透明も起こらない。<br/>
/// <b>4 隅は放射グラデーション</b>（<c>PathGradientBrush</c>）で塗る（<see cref="ShadowPlan.BuildCornerFills"/>）。
/// </remarks>
internal static class ShadowDrawer
{
    /// <summary>画像の 4 辺と 4 隅を塗る領域（物理ピクセル）を求める。</summary>
    /// <param name="frame">影を落とす矩形（画像。 DIP）。</param>
    /// <param name="scaling">描画単位への倍率（DIP → ピクセル）。</param>
    /// <returns>4 辺の帯と 4 隅の矩形。</returns>
    public static IReadOnlyList<Rectangle> BuildRects(RectangleF frame, double scaling)
    {
        var rects = new List<Rectangle>();
        foreach (var band in ShadowPlan.BuildDeviceBands(frame.X, frame.Y, frame.Width, frame.Height, scaling))
        {
            rects.Add(new Rectangle(band.X, band.Y, band.Width, band.Height));
        }

        foreach (var fill in ShadowPlan.BuildCornerFills(frame.X, frame.Y, frame.Width, frame.Height))
        {
            rects.Add(new Rectangle(
                (int)Math.Round(fill.X * scaling),
                (int)Math.Round(fill.Y * scaling),
                (int)Math.Round(fill.Size * scaling),
                (int)Math.Round(fill.Size * scaling)));
        }

        return rects;
    }

    /// <summary>影を塗る図形（表示領域の切り抜きに使う）を組み立てる。</summary>
    /// <param name="frame">影を落とす矩形（画像。 DIP）。</param>
    /// <param name="scaling">描画単位への倍率（DIP → ピクセル）。</param>
    /// <returns>影の帯の図形（<see cref="Region"/> が複製するため、呼び出し側が破棄する）。</returns>
    /// <remarks>重なった矩形を打ち消さないよう和集合にする（既定の Alternate は境界の画素が抜ける）。</remarks>
    public static GraphicsPath BuildRegionPath(RectangleF frame, double scaling)
    {
        var path = new GraphicsPath { FillMode = FillMode.Winding };
        foreach (var rect in BuildRects(frame, scaling))
        {
            path.AddRectangle(rect);
        }

        return path;
    }

    /// <summary>画像の 4 辺・4 隅へ影を描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="frame">影を落とす矩形（画像。 DIP）。</param>
    /// <param name="scaling">描画単位への倍率（DIP → ピクセル）。 1 の場合はそのまま描く。</param>
    public static void Draw(Graphics graphics, RectangleF frame, double scaling)
    {
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

            foreach (var fill in ShadowPlan.BuildCornerFills(frame.X, frame.Y, frame.Width, frame.Height))
            {
                FillCorner(graphics, fill, scaling);
            }
        }
        finally
        {
            graphics.SmoothingMode = smoothing;
            graphics.PixelOffsetMode = offsetMode;
        }
    }
    /// <summary>影の 1 隅分の放射グラデーションを描く。</summary>
    /// <param name="graphics">描画先。</param>
    /// <param name="fill">隅の計画（位置・大きさ・中心）。</param>
    /// <param name="scaling">描画単位への倍率（DIP → ピクセル）。</param>
    /// <remarks>
    /// GDI+ の <see cref="PathGradientBrush"/> は<b>中心から図形の輪郭へ</b>補間するため、半径 = 一辺の円を使えば<br/>
    /// 矩形の外側の距離に応じた濃さの変化（放射グラデーション）になる。 円の外側は塗らないよう矩形で切り抜く。
    /// </remarks>
    private static void FillCorner(Graphics graphics, ShadowCornerFill fill, double scaling)
    {
        var rect = new Rectangle(
            (int)Math.Round(fill.X * scaling),
            (int)Math.Round(fill.Y * scaling),
            (int)Math.Round(fill.Size * scaling),
            (int)Math.Round(fill.Size * scaling));
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var center = new PointF(
            rect.Left + ((float)(fill.CenterX * rect.Width)),
            rect.Top + ((float)(fill.CenterY * rect.Height)));

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
            InterpolationColors = ToCenterColorBlend(fill.Stops),
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