using EsUtil.Feature.Annotation;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>本文の書式で選べるフォントの一覧（MewUI 版）。</summary>
/// <remarks>
/// <b>優先フォントを先頭に置く</b>: <see cref="AnnotationDefaults.PreferredFontFamilies"/>（<c>Yu Gothic UI</c> →
/// <c>Segoe UI</c>）を先頭へ並べ、その後に代表的な日本語・欧文フォントを続ける
/// （一覧の並びを 4 つの UI 版で揃えるため、並べ方は本クラスが単一所有する）。<br/>
/// <b>列挙はしない</b>: MewUI はインストール済みフォントの列挙を公開していないため、代表的な一覧を固定で持つ
/// （無いフォントを選んでも描画側が代替フォントへ落とす）。
/// </remarks>
public static class AnnotationFonts
{
    /// <summary>選べるフォント名の一覧を取得する。</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        .. AnnotationDefaults.PreferredFontFamilies,
        "メイリオ",
        "Meiryo UI",
        "游ゴシック",
        "游明朝",
        "MS UI Gothic",
        "MS Gothic",
        "BIZ UDPGothic",
        "Noto Sans JP",
        "Arial",
        "Calibri",
        "Consolas",
    ];
}
