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

    /// <summary>装飾の切り替え（B / I / U / S）の文字サイズ（DIP）。</summary>
    /// <remarks>
    /// <b>編集 UI の文字より一回り大きくする</b>: 記号 1 文字だけのボタンは、行の他の文字と同じ大きさだと
    /// 線（下線・取り消し線）と字面が小さく、押せる場所としても見えにくい（実測 2026-11-22）。
    /// UI の既定の文字サイズ（12 DIP）より 1 段大きい 14 DIP にします。
    /// </remarks>
    public const double ToggleFontSize = 14;

    /// <summary>装飾の切り替え（B / I / U / S）のフォント名。</summary>
    /// <remarks>
    /// <b>「I」を見分けられるフォントにする</b>: UI の既定フォント（<c>Yu Gothic UI</c> / <c>Segoe UI</c>）の
    /// 斜体の「I」は単なる斜線に見え、太字の「B」とも形が近く、押す意味が伝わりません。
    /// <c>Consolas</c> は Windows に標準で入っており（環境が変わっても解決できる）、
    /// <b>「I」の上下にセリフ</b>が付くため、記号として読めます（実測 2026-11-22）。
    /// 斜体の「I」が読めるかを優先し、欧文の記号用フォントとして選びます。
    /// </remarks>
    public const string ToggleFontFamily = "Consolas";
}
