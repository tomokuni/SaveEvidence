using EsUtil.Ui.AvaloniaUI.ColorSelector;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>注釈の色選択で使う代表色（色名付きの 16 色）。</summary>
/// <remarks>
/// <b>代表色だけを出す</b>: 注釈の色は「枠線・内側・文字」の 3 種類しかなく、テーマの色や色名の全一覧を開くと
/// 目的の色へ届くまでに手数がかかる。 よく使う 16 色を 1 つのタブにまとめ、色名付きで並べる。<br/>
/// <b>タブの題名は出さない</b>: タブが 1 つだけなので、題名を省いて一覧を狭くする。<br/>
/// <b>定義は 1 か所</b>: 書式バー・右クリックのフライアウト・設定ペインが同じ並びを使う（画面ごとに色が違わない）。
/// </remarks>
public static class AnnotationPalette
{
    /// <summary>代表色の並び（色名と保存値 <c>#AARRGGBB</c>）。</summary>
    /// <remarks>無彩色 → 暖色 → 寒色の順に並べる（同じ系統の色を続けて選びやすくする）。</remarks>
    public static IReadOnlyList<ColorSelectorCustomColor> RepresentativeColors { get; } =
    [
        new ColorSelectorCustomColor("黒", "#FF000000"),
        new ColorSelectorCustomColor("濃い灰色", "#FF404040"),
        new ColorSelectorCustomColor("灰色", "#FF808080"),
        new ColorSelectorCustomColor("薄い灰色", "#FFC0C0C0"),
        new ColorSelectorCustomColor("白", "#FFFFFFFF"),
        new ColorSelectorCustomColor("赤", "#FFDC2626"),
        new ColorSelectorCustomColor("橙", "#FFEA580C"),
        new ColorSelectorCustomColor("黄", "#FFEAB308"),
        new ColorSelectorCustomColor("黄緑", "#FF65A30D"),
        new ColorSelectorCustomColor("緑", "#FF16A34A"),
        new ColorSelectorCustomColor("青緑", "#FF0D9488"),
        new ColorSelectorCustomColor("水色", "#FF0284C7"),
        new ColorSelectorCustomColor("青", "#FF2563EB"),
        new ColorSelectorCustomColor("紫", "#FF7C3AED"),
        new ColorSelectorCustomColor("桃", "#FFDB2777"),
        new ColorSelectorCustomColor("茶", "#FF92400E"),
    ];

    /// <summary>代表色を並べるタブの定義。</summary>
    internal static IReadOnlyList<ColorSelectorCustomTab> Tabs { get; } =
        [new ColorSelectorCustomTab("代表色", RepresentativeColors)];

    /// <summary>色の選択を注釈向けの構成にする（代表色のタブだけを出す）。</summary>
    /// <param name="selector">構成する色の選択。</param>
    /// <remarks>
    /// <b>ColorPicker と既存のタブは出さない</b>: 入力欄と代表色で足りるため、幅を取るタブを省く。<br/>
    /// 16 進や色名を直接打ち込む入力欄は残す（代表色に無い色を指定できる）。
    /// </remarks>
    internal static void Configure(ColorSelectorControl selector)
    {
        selector.ShowPickerTab = false;
        selector.ShowThemeColorTab = false;
        selector.ShowNamedColorTab = false;
        selector.ShowTabTitles = false;
        selector.CustomTabs = Tabs;
    }

    /// <summary>代表色を <c>#AARRGGBB</c> の文字列として取得する。</summary>
    /// <param name="name">色名。</param>
    /// <returns>保存値の文字列。 見つからない場合は <see langword="null"/>。</returns>
    /// <remarks>色の既定値を作るときに使う（色名と値を 1 か所で持つ）。</remarks>
    public static string? GetHex(string name)
        => RepresentativeColors.FirstOrDefault(color => string.Equals(color.Name, name, StringComparison.Ordinal))?.Value;

    /// <summary>代表色を色として取得する。</summary>
    /// <param name="name">色名。</param>
    /// <param name="color">色。</param>
    /// <returns>見つかった場合は <see langword="true"/>。</returns>
    public static bool TryGetColor(string name, out DrawingColor color)
    {
        var hex = GetHex(name);
        if (hex is not null
            && EsUtil.Platform.Core.Imaging.DrawingColorExtensions.TryParseHexText(hex, out var parsed))
        {
            color = parsed;
            return true;
        }

        color = default;
        return false;
    }
}
