namespace EsUtil.Feature.Annotation;

/// <summary>書式バーの項目の並び順と寸法（単一所有）。</summary>
/// <remarks>
/// <b>装飾を先・値を後</b>: 太字・斜体・下線・取り消し線（押すだけの操作）を先に置き、
/// 色・大きさ・フォント名（選ぶ操作）を後に置く。 操作の種類がまとまり、視線の移動が少なくなる。
/// <b>項目の高さは揃える</b>: 高さが違うと縦位置が不揃いになり、押す位置もずれる（実測 2026-11-21）。
/// </remarks>
public static class AnnotationFormatBarLayout
{
    /// <summary>項目の並び順を取得する。</summary>
    /// <value>太字・斜体・下線・取り消し線・色・フォントの大きさ・フォント名 の順。</value>
    public static IReadOnlyList<AnnotationFormatBarItem> Order { get; } =
    [
        AnnotationFormatBarItem.Bold,
        AnnotationFormatBarItem.Italic,
        AnnotationFormatBarItem.Underline,
        AnnotationFormatBarItem.Strike,
        AnnotationFormatBarItem.Color,
        AnnotationFormatBarItem.Size,
        AnnotationFormatBarItem.Font,
    ];

    /// <summary>項目の高さ（DIP）を取得する。</summary>
    /// <value>26 DIP。</value>
    /// <remarks>
    /// <b>すべての項目で同じ高さにする</b>: ボタン・入力欄・一覧で高さが違うと、書式バーの縦位置が
    /// 揃わず押しにくい。 UI はこの値を各項目の高さに与え、縦方向の中央へ寄せる。
    /// </remarks>
    public const double ItemHeight = 26;
}

