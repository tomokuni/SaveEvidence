namespace EsUtil.Feature.Annotation.Presenter;

/// <summary>注釈の編集 UI（右クリックのフライアウト・書式バー）の寸法と余白（単一所有）。</summary>
/// <remarks>
/// <b>UI ごとに値を決めない</b>: 同じ役割の項目（項目名の幅・色の入力の幅・装飾ボタンの幅）が
/// 4 つの UI 版で違うと、同じ内容でも押す位置と見た目が変わります。 値はここが単一所有し、
/// UI は自分の単位へ写すだけにします（WinForms のフォントだけはポイント指定のため換算します）。<br/>
/// <b>処理は持たない</b>: 行の組み立て（要素の生成・配置）は UI 固有なので UI に残します。
/// </remarks>
public static class AnnotationEditorLayout
{
    /// <summary>項目名の幅（DIP）。</summary>
    public const double LabelWidth = 104;

    /// <summary>色の入力の幅（DIP。 フライアウトの行で使う）。</summary>
    public const double ColorSelectorWidth = 210;

    /// <summary>行の中の項目の間隔（DIP）。</summary>
    public const double RowSpacing = 4;

    /// <summary>行と行の間隔（DIP）。</summary>
    public const double GroupSpacing = 6;

    /// <summary>装飾の切り替えボタンの幅（DIP）。</summary>
    public const double ToggleWidth = 34;

    /// <summary>装飾の切り替えボタンの最小の幅（DIP。 幅を指定しない UI 用）。</summary>
    public const double ToggleMinWidth = 28;

    /// <summary>本文の文字サイズ（DIP。 編集 UI の文字）。</summary>
    public const double FontSizeDip = 12;

    /// <summary>色の入力の幅（DIP。 書式バーの行で使う。 表記に合わせて広げる場合はこの値を下限にする）。</summary>
    public const double FormatBarColorSelectorWidth = 210;
}
