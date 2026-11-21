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
    /// <value>32 DIP。</value>
    /// <remarks>
    /// <b>すべての項目で同じ高さにする</b>: ボタン・入力欄・一覧で高さが違うと、書式バーの縦位置が
    /// 揃わず押しにくい。 UI はこの値を各項目の高さに与え、縦方向の中央へ寄せる。<br/>
    /// <b>32 DIP を下回る値にしてはいけない</b>: AvaloniaUI の <c>ComboBox</c> と WinUI 3 の
    /// <c>ToggleButton</c> の最小の高さは 32 DIP である。 26 DIP を与えたところ、内側の入力欄だけが
    /// 元の高さを保ち、<b>色の入力欄の上下の枠線が切れた</b>（実測 2026-11-21）。<br/>
    /// <b>この値はパネルの高さにも使う</b>（<see cref="AnnotationPanelAppearance.FormatBar"/> の
    /// <c>FixedContentHeight</c>）。 変えるとパネルの高さと置く位置も一緒に変わる。
    /// </remarks>
    public const double ItemHeight = 32;
}

