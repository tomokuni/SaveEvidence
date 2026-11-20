namespace EsUtil.Feature.Annotation;

/// <summary>書式バーの項目の並び順（単一所有）。</summary>
/// <remarks>
/// <b>装飾を先・値を後</b>: 太字・斜体・下線・取り消し線（押すだけの操作）を先に置き、
/// フォント名・大きさ・色（選ぶ操作）を後に置く。 操作の種類がまとまり、視線の移動が少なくなる。
/// </remarks>
public static class AnnotationFormatBarLayout
{
    /// <summary>項目の並び順を取得する。</summary>
    /// <value>太字・斜体・下線・取り消し線・フォント名・フォントの大きさ・色 の順。</value>
    public static IReadOnlyList<AnnotationFormatBarItem> Order { get; } =
    [
        AnnotationFormatBarItem.Bold,
        AnnotationFormatBarItem.Italic,
        AnnotationFormatBarItem.Underline,
        AnnotationFormatBarItem.Strike,
        AnnotationFormatBarItem.Font,
        AnnotationFormatBarItem.Size,
        AnnotationFormatBarItem.Color,
    ];
}

