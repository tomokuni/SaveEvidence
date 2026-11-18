using EsUtil.Feature.Annotation;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>本文の書式で選べるフォントの一覧（WinForms 版）。</summary>
/// <remarks>
/// <b>優先フォントを先頭に置く</b>: <see cref="AnnotationDefaults.PreferredFontFamilies"/> を先頭へ並べ、
/// その後に代表的な日本語・欧文フォントを続ける（一覧の並びを UI 版で揃える）。
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
