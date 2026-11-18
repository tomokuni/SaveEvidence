using EsUtil.Feature.Annotation;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>本文の書式で選べるフォントの一覧（WinUI 3 版）。</summary>
/// <remarks>
/// <b>優先フォントを先頭に置く</b>: <see cref="AnnotationDefaults.PreferredFontFamilies"/>（<c>Yu Gothic UI</c> →
/// <c>Segoe UI</c>）を先頭へ並べ、その後に代表的な日本語・欧文フォントを続ける
/// （一覧の並びを 4 つの UI 版で揃える。 並べ方は MewUI 版と同じ）。<br/>
/// <b>列挙はしない</b>: 代表的な一覧を固定で持つ（無いフォントを選んでも描画側が代替フォントへ落とす）。
/// </remarks>
public static class AnnotationFonts
{
    /// <summary>選べるフォント名の一覧（並びの実体）。</summary>
    private static readonly string[] s_all =
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

    /// <summary>選べるフォント名の一覧を取得する。</summary>
    public static IReadOnlyList<string> All => s_all;
}
