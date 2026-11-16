using System.Globalization;
using Avalonia;
using Avalonia.Media;
using EsUtil.Feature.Annotation;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>注釈の描画計画を Avalonia の描画 API へ写す。</summary>
/// <remarks>
/// <b>計算を持たない</b>: 角丸・塗り・枠線・しっぽの多角形・行の位置は計画が解決済みである
/// （4 つの UI 版で見た目がずれないよう、判断は Core / Presenter が単一所有する）。<br/>
/// <b>文字の実寸だけは UI が測る</b>（<see cref="CreateText"/>）。 折り返しは計画側が行う。
/// </remarks>
internal static class AnnotationPainter
{
    /// <summary>下線・取り消し線の太さの比率（文字の大きさに対する比）。</summary>
    private const double DecorationThicknessRatio = 0.06;

    /// <summary>文字列を描画できる形へ変換する。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>整形済みの文字列（元画像ピクセルの大きさ）。</returns>
    internal static FormattedText CreateText(string text, AnnotationTextStyle style)
        => new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(
                ResolveFontFamily(style.FontFamily),
                style.IsItalic ? FontStyle.Italic : FontStyle.Normal,
                style.IsBold ? FontWeight.Bold : FontWeight.Normal),
            style.FontSize,
            ToBrush(style.Color));

    /// <summary>注釈 1 件を描く（元画像ピクセルの座標系で呼ぶ）。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="shape">描画計画。</param>
    internal static void DrawShape(DrawingContext context, AnnotationShapePlan shape)
    {
        var brush = shape.HasFill ? ToBrush(shape.Fill) : null;
        var pen = new Pen(ToBrush(shape.Border), shape.BorderThickness);

        if (shape.HasTail)
        {
            DrawTail(context, shape, brush, pen);
        }

        context.DrawRectangle(
            brush,
            pen,
            new Rect(shape.Bounds.X, shape.Bounds.Y, shape.Bounds.Width, shape.Bounds.Height),
            shape.CornerRadius,
            shape.CornerRadius);

        foreach (var line in shape.Lines)
        {
            foreach (var segment in line.Segments)
            {
                var formatted = CreateText(segment.Text, segment.Style);
                var origin = new Point(
                    segment.Bounds.X,
                    segment.Bounds.Y + ((segment.Bounds.Height - formatted.Height) / 2));
                context.DrawText(formatted, origin);
                DrawDecorations(context, segment, formatted);
            }
        }
    }

    /// <summary>色をブラシへ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns>単色のブラシ。</returns>
    internal static IBrush ToBrush(DrawingColor color)
        => new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));

    /// <summary>しっぽ（三角形）を描く。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="shape">描画計画。</param>
    /// <param name="brush">塗り。</param>
    /// <param name="pen">枠線。</param>
    /// <remarks>
    /// 本体の後に描くと本体の枠線が三角形の上へ重なるため、<b>本体より先に描く</b>。
    /// 付け根の辺は本体の内側へ隠れる（本体の塗りが後から上書きする）。
    /// </remarks>
    private static void DrawTail(DrawingContext context, AnnotationShapePlan shape, IBrush? brush, IPen pen)
    {
        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            stream.BeginFigure(new Point(shape.Tail[0].X, shape.Tail[0].Y), isFilled: true);
            for (var i = 1; i < shape.Tail.Count; i++)
            {
                stream.LineTo(new Point(shape.Tail[i].X, shape.Tail[i].Y));
            }

            stream.EndFigure(isClosed: true);
        }

        context.DrawGeometry(brush, pen, geometry);
    }

    /// <summary>下線・取り消し線を描く。</summary>
    /// <param name="context">描画先。</param>
    /// <param name="segment">区間。</param>
    /// <param name="formatted">整形済みの文字列（文字の実寸を取るため）。</param>
    private static void DrawDecorations(DrawingContext context, AnnotationTextSegmentPlan segment, FormattedText formatted)
    {
        if (!segment.Style.IsUnderline && !segment.Style.IsStrike)
        {
            return;
        }

        var thickness = Math.Max(1.0, segment.Style.FontSize * DecorationThicknessRatio);
        var pen = new Pen(ToBrush(segment.Style.Color), thickness);
        var left = segment.Bounds.X;
        var right = segment.Bounds.X + formatted.Width;

        if (segment.Style.IsUnderline)
        {
            var y = segment.Bounds.Y + ((segment.Bounds.Height - formatted.Height) / 2) + formatted.Height - (thickness * 2);
            context.DrawLine(pen, new Point(left, y), new Point(right, y));
        }

        if (segment.Style.IsStrike)
        {
            var y = segment.Bounds.Y + (segment.Bounds.Height / 2);
            context.DrawLine(pen, new Point(left, y), new Point(right, y));
        }
    }

    /// <summary>使うフォント ファミリを解決する（無い場合は優先順の次のフォント）。</summary>
    /// <param name="fontFamily">指定されたフォント ファミリ名。</param>
    /// <returns>実際に使うフォント ファミリ。</returns>
    /// <remarks>
    /// 既定は <c>Yu Gothic UI</c> であり、無い環境では <c>Segoe UI</c> を使う
    /// （優先順は <see cref="AnnotationDefaults.PreferredFontFamilies"/> が単一所有する）。
    /// </remarks>
    private static FontFamily ResolveFontFamily(string fontFamily)
    {
        var installed = FontManager.Current.SystemFonts;
        if (installed.Any(font => string.Equals(font.Name, fontFamily, StringComparison.OrdinalIgnoreCase)))
        {
            return new FontFamily(fontFamily);
        }

        foreach (var fallback in AnnotationDefaults.PreferredFontFamilies)
        {
            if (installed.Any(font => string.Equals(font.Name, fallback, StringComparison.OrdinalIgnoreCase)))
            {
                return new FontFamily(fallback);
            }
        }

        return FontFamily.Default;
    }
}
