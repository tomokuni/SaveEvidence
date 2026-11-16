using DrawingColor = System.Drawing.Color;
using System.Collections.Immutable;
using EsUtil.Feature.Annotation.Core;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Annotation;

/// <summary>注釈の描画計画を解決する提示層（Core と同一アセンブリ）。</summary>
/// <remarks>
/// <b>幾何と折り返しを解決し、UI へ計画を渡す</b>: 文字の実寸だけは描画 API に依存するため
/// <c>measureText</c> として UI から受け取る（ルーペの <c>measureText</c> と同じ形）。<br/>
/// <b>選択枠は解決しない</b>: 選択中の枠とハンドルは Marquee の提示層が解決する（枠の見た目を
/// 4 つの UI 版で揃えるため、同じ仕組みを使う）。<br/>
/// <b>毎フレームの解決は軽い</b>（比例計算と折り返しのみ。 画像を持たず、描画 API を呼ばない）。
/// </remarks>
public sealed class AnnotationPresenter
{
    /// <summary>行間の倍率の既定値（文字の実寸の高さに対する行の高さ）。</summary>
    /// <remarks>日本語の本文が詰まって見えないよう、実寸より少し広く取る。</remarks>
    public const double LineHeightFactorDefault = 1.25;

    /// <summary>行間の倍率を取得または設定する。</summary>
    /// <value>文字の実寸の高さに対する行の高さの比。 既定は <see cref="LineHeightFactorDefault"/>。</value>
    public double LineHeightFactor { get; set; } = LineHeightFactorDefault;

    /// <summary>描画計画を解決する。</summary>
    /// <param name="document">対象の文書。</param>
    /// <param name="measureText">文字列と書式から実寸（元画像ピクセル）を測る関数。</param>
    /// <param name="selectedId">選択中の注釈の識別子（無い場合は <see langword="null"/>）。</param>
    /// <returns>1 フレーム分の描画計画。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> または <paramref name="measureText"/> が null の場合。</exception>
    public AnnotationDrawPlan ResolveDrawPlan(
        AnnotationDocument document,
        Func<string, AnnotationTextStyle, EsSize> measureText,
        string? selectedId = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(measureText);

        var shapes = ImmutableArray.CreateBuilder<AnnotationShapePlan>(document.Items.Length);
        foreach (var item in document.Items)
        {
            shapes.Add(item is BubbleAnnotation bubble
                ? ResolveBubble(bubble, measureText, LineHeightFactor)
                : ResolveFrame(item));
        }

        return new AnnotationDrawPlan(
            new EsSize(document.ImageWidth, document.ImageHeight),
            selectedId,
            shapes.ToImmutable());
    }

    /// <summary>枠線の注釈の計画を解決する。</summary>
    /// <param name="item">対象の注釈。</param>
    /// <returns>描画計画。</returns>
    private static AnnotationShapePlan ResolveFrame(AnnotationItem item)
    {
        var appearance = item.Appearance.Normalized();
        return new AnnotationShapePlan(
            item.Id,
            AnnotationKind.Frame,
            item.Bounds,
            appearance.CornerRadius,
            appearance.Fill,
            appearance.Border,
            appearance.BorderThickness,
            [],
            []);
    }

    /// <summary>吹き出しの注釈の計画を解決する（折り返しと行の配置を含む）。</summary>
    /// <param name="bubble">対象の吹き出し。</param>
    /// <param name="measureText">実寸を測る関数。</param>
    /// <param name="lineHeightFactor">行間の倍率。</param>
    /// <returns>描画計画。</returns>
    private static AnnotationShapePlan ResolveBubble(
        BubbleAnnotation bubble, Func<string, AnnotationTextStyle, EsSize> measureText, double lineHeightFactor)
    {
        var appearance = bubble.Appearance.Normalized();
        var body = bubble.ResolveBodyRect();
        var wrapped = AnnotationTextWrap.Wrap(bubble.Text, body.Width, measureText);

        var lines = LayoutLines(wrapped, body, measureText, lineHeightFactor);
        var tail = bubble.Tail.Normalized();
        var (from, to) = tail.ResolveBase(bubble.Bounds);
        var polygon = new List<EsPoint>(3) { tail.Tip, from, to };

        return new AnnotationShapePlan(
            bubble.Id,
            AnnotationKind.Bubble,
            bubble.Bounds,
            appearance.CornerRadius,
            appearance.Fill,
            appearance.Border,
            appearance.BorderThickness,
            polygon,
            lines);
    }

    /// <summary>折り返した行を本文の矩形へ配置する。</summary>
    /// <param name="wrapped">折り返した行。</param>
    /// <param name="body">本文の矩形。</param>
    /// <param name="measureText">実寸を測る関数。</param>
    /// <param name="lineHeightFactor">行間の倍率。</param>
    /// <returns>行の描画計画（左揃え・縦は中央揃え）。</returns>
    private static ImmutableArray<AnnotationTextLinePlan> LayoutLines(
        ImmutableArray<ImmutableArray<AnnotationTextWrap.TextSegment>> wrapped,
        EsRect body,
        Func<string, AnnotationTextStyle, EsSize> measureText,
        double lineHeightFactor)
    {
        if (wrapped.Length == 0)
        {
            return [];
        }

        // 行の高さは行内で最も大きい文字に合わせる（書式が混ざっても行の間隔が揃う）
        var heights = new double[wrapped.Length];
        var totalHeight = 0.0;
        for (var i = 0; i < wrapped.Length; i++)
        {
            var height = 0.0;
            foreach (var segment in wrapped[i])
            {
                height = Math.Max(height, measureText("Ag", segment.Style).Height * lineHeightFactor);
            }

            heights[i] = height;
            totalHeight += height;
        }

        // 本文の矩形より縦に大きい場合は上へ詰める（はみ出しは描画側のクリップに任せる）
        var offsetY = Math.Max(0.0, (body.Height - totalHeight) / 2);
        var lines = ImmutableArray.CreateBuilder<AnnotationTextLinePlan>(wrapped.Length);
        var y = body.Y + offsetY;
        for (var i = 0; i < wrapped.Length; i++)
        {
            var segments = ImmutableArray.CreateBuilder<AnnotationTextSegmentPlan>(wrapped[i].Length);
            var x = body.X;
            foreach (var segment in wrapped[i])
            {
                segments.Add(new AnnotationTextSegmentPlan(
                    segment.Text, segment.Style, new EsRect(x, y, segment.Width, heights[i])));
                x += segment.Width;
            }

            lines.Add(new AnnotationTextLinePlan(new EsRect(body.X, y, body.Width, heights[i]), segments.ToImmutable()));
            y += heights[i];
        }

        return lines.ToImmutable();
    }

    /// <summary>外観の既定値（枠線）を取得する。</summary>
    /// <returns>枠線の既定の外観。</returns>
    /// <remarks>設定ページの初期値と新規作成時の値が同じになるよう、既定値は <see cref="AnnotationDefaults"/> が単一所有する。</remarks>
    public static AnnotationAppearance DefaultFrameAppearance => AnnotationDefaults.FrameAppearance;

    /// <summary>外観の既定値（吹き出し）を取得する。</summary>
    /// <returns>吹き出しの既定の外観。</returns>
    public static AnnotationAppearance DefaultBubbleAppearance => AnnotationDefaults.BubbleAppearance;

    /// <summary>文字の既定の書式を取得する。</summary>
    /// <returns>既定の書式。</returns>
    public static AnnotationTextStyle DefaultTextStyle => AnnotationDefaults.TextStyle;

    /// <summary>透明な色を取得する（内側を塗らない指定に使う）。</summary>
    /// <returns>透明。</returns>
    public static DrawingColor Transparent => DrawingColor.Transparent;
}
