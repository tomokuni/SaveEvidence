using Aprillz.MewUI;
using Aprillz.MewUI.Text;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>本文の実寸の測定と行のベースラインの解決（描画 API に依存する部分だけを担う）。</summary>
/// <remarks>
/// <b>折り返しはコアが単一所有する</b>: 本クラスは「1 つの書式の文字列の実寸」と「行のベースライン」だけを返す
/// （折り返しの判断は <c>AnnotationTextWrap</c>、行の配置は <c>AnnotationPresenter</c> が行う）。<br/>
/// <b>行の高さは斜体で変えない</b>: 斜体は字を傾けるだけであり、行の高さ・ベースラインを変えると 1 行ごとに
/// 間隔が変わる（AvaloniaUI 版と同じ規則）。 幅は実際の送り幅で測る（折り返しの判定に使う）。
/// </remarks>
internal static class AnnotationTextMetrics
{
    /// <summary>文字の実寸を測る DPI（元画像ピクセルの座標系で測るため 96 に固定する）。</summary>
    private const uint MeasureDpi = 96;

    /// <summary>行の高さとベースラインを測るための文字列。</summary>
    private const string LineMetricsText = "Ag";

    /// <summary>文字列と書式から実寸を測る。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <returns>実寸（元画像ピクセル）。 高さは斜体を外した書式で測る。</returns>
    internal static EsSize Measure(string text, AnnotationTextStyle style)
    {
        var layout = CreateLayout(text, style, ignoreItalic: false);
        var line = ResolveLineMetrics(style);
        return new EsSize(layout.MeasuredSize.Width, line.Height);
    }

    /// <summary>行の高さとベースラインを測る。</summary>
    /// <param name="style">書式。</param>
    /// <returns>行の実寸（高さとベースライン）。</returns>
    /// <remarks>
    /// <b>斜体を外して測る</b>: 斜体にしても行の高さ・ベースラインは変えない。 4 つの UI 版で同じ
    /// 結果になるよう、この規則は本メソッドが単一所有する。
    /// </remarks>
    internal static TextLineMetrics MeasureLineMetrics(AnnotationTextStyle style)
    {
        var (height, baseline) = ResolveLineMetrics(style);
        return new TextLineMetrics(height, baseline);
    }

    /// <summary>行の高さとベースライン（上端から）を求める。</summary>
    /// <param name="style">書式。</param>
    /// <returns>行の高さとベースライン。</returns>
    /// <remarks>斜体を外した書式で測る（斜体にしても行の高さ・ベースラインを変えない）。</remarks>
    internal static (double Height, double Baseline) ResolveLineMetrics(AnnotationTextStyle style)
    {
        var layout = CreateLayout(LineMetricsText, style, ignoreItalic: true);
        var lines = layout.Lines;
        var height = layout.MeasuredSize.Height;
        var baseline = lines.Count > 0 ? lines[0].Baseline : height;
        return (height, baseline);
    }

    /// <summary>行の高さいっぱいへ中央寄せしたときのベースラインを求める。</summary>
    /// <param name="lineTop">行の上端（元画像ピクセル）。</param>
    /// <param name="lineHeight">行の高さ。</param>
    /// <param name="referenceHeight">基準の書式の実寸の高さ。</param>
    /// <param name="referenceBaseline">基準の書式のベースライン（上端から）。</param>
    /// <returns>ベースラインの Y 座標。</returns>
    /// <remarks>
    /// 区間ごとの実寸で縦位置を決めると、斜体にした区間だけが上下へずれる。 行のベースラインを 1 本に決め、
    /// 各 run は「ベースライン − 自分のベースライン」を上端にして描く。
    /// </remarks>
    internal static double ResolveBaseline(
        double lineTop, double lineHeight, double referenceHeight, double referenceBaseline)
        => lineTop + ((lineHeight - referenceHeight) / 2) + referenceBaseline;

    /// <summary>文字列を描画できる形（レイアウト）へ変換する。</summary>
    /// <param name="text">文字列。</param>
    /// <param name="style">書式。</param>
    /// <param name="ignoreItalic">斜体を外して作る場合は <see langword="true"/>（行の実寸を測るとき）。</param>
    /// <returns>レイアウト。</returns>
    internal static ITextLayout CreateLayout(string text, AnnotationTextStyle style, bool ignoreItalic)
        => Application.DefaultGraphicsFactory.TextEngine.CreateLayout(new TextLayoutRequest
        {
            Text = text.AsMemory(),
            Dpi = MeasureDpi,
            DefaultStyle = new TextRunStyle
            {
                FontFamily = style.FontFamily,
                FontSize = style.FontSize,
                Weight = style.IsBold ? FontWeight.Bold : FontWeight.Normal,
                Italic = style.IsItalic && !ignoreItalic,
            },
        });

    /// <summary>色を MewUI の色へ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns>MewUI の色。</returns>
    internal static Color ToMewColor(DrawingColor color)
        => new(color.A, color.R, color.G, color.B);
}
