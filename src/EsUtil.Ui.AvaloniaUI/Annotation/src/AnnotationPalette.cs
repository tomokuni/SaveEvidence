using EsUtil.Ui.AvaloniaUI.ColorSelector;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.AvaloniaUI.Annotation;

/// <summary>注釈の色選択で使う代表色（色名付きの 40 色）。</summary>
/// <remarks>
/// <b>色名付きの 40 色を 1 か所で持つ</b>: 注釈の色は「枠線・内側・文字」の 3 種類しかなく、テーマの色や
/// 色名の全一覧を開くと目的の色へ届くまでに手数がかかる。 よく使う 40 色を 1 つにまとめる。<br/>
/// <b>同じ並びを 2 通りで出す</b>: ■ だけを並べた「代表色パレット」（速く選べる）と、色名付きの「代表色」
/// （探しやすい）を出す。 並びが同じため、一覧で見つけた色を格子でも同じ位置で選べる。<br/>
/// <b>定義は 1 か所</b>: 書式バー・右クリックのフライアウト・設定ペインが同じ並びを使う（画面ごとに色が違わない）。
/// </remarks>
public static class AnnotationPalette
{
    /// <summary>代表色パレットの既定の列数（40 色なら 8 列 × 5 行に収まり、スクロールせずに選べる）。</summary>
    public const int DefaultPaletteColumns = 8;

    /// <summary>代表色パレットの題名。</summary>
    public const string PaletteTabTitle = "代表色パレット";

    /// <summary>代表色（色名の一覧）の題名。</summary>
    public const string ListTabTitle = "代表色";

    /// <summary>代表色の並び（色名と保存値 <c>#AARRGGBB</c>）。</summary>
    /// <remarks>
    /// 無彩色 → 赤系 → 橙・黄系 → 緑系 → 青系 → 紫・桃系の順に並べる（同じ系統の色を続けて選びやすくする）。
    /// 値は 40 色すべて異なる。
    /// </remarks>
    public static IReadOnlyList<ColorSelectorCustomColor> RepresentativeColors { get; } =
    [
        // 無彩色
        new ColorSelectorCustomColor("黒", "#FF000000"),
        new ColorSelectorCustomColor("濃い灰色", "#FF404040"),
        new ColorSelectorCustomColor("灰色", "#FF808080"),
        new ColorSelectorCustomColor("薄い灰色", "#FFC0C0C0"),
        new ColorSelectorCustomColor("白", "#FFFFFFFF"),
        new ColorSelectorCustomColor("墨", "#FF1F2937"),
        new ColorSelectorCustomColor("銀", "#FF9CA3AF"),

        // 赤系
        new ColorSelectorCustomColor("赤", "#FFDC2626"),
        new ColorSelectorCustomColor("茜", "#FFB91C1C"),
        new ColorSelectorCustomColor("紅", "#FFE11D48"),
        new ColorSelectorCustomColor("躑躅", "#FFBE123C"),
        new ColorSelectorCustomColor("珊瑚", "#FFF87171"),

        // 橙・黄系
        new ColorSelectorCustomColor("朱", "#FFEA580C"),
        new ColorSelectorCustomColor("橙", "#FFF97316"),
        new ColorSelectorCustomColor("琥珀", "#FFB45309"),
        new ColorSelectorCustomColor("杏", "#FFFDBA74"),
        new ColorSelectorCustomColor("黄", "#FFEAB308"),
        new ColorSelectorCustomColor("山吹", "#FFFBBF24"),
        new ColorSelectorCustomColor("檸檬", "#FFFDE047"),
        new ColorSelectorCustomColor("黄土", "#FFCA8A04"),
        new ColorSelectorCustomColor("茶", "#FF92400E"),

        // 緑系
        new ColorSelectorCustomColor("黄緑", "#FF65A30D"),
        new ColorSelectorCustomColor("若草", "#FF84CC16"),
        new ColorSelectorCustomColor("鶯", "#FF4D7C0F"),
        new ColorSelectorCustomColor("苔", "#FF3F6212"),
        new ColorSelectorCustomColor("緑", "#FF16A34A"),
        new ColorSelectorCustomColor("常磐", "#FF047857"),
        new ColorSelectorCustomColor("青緑", "#FF0D9488"),

        // 青系
        new ColorSelectorCustomColor("水色", "#FF0EA5E9"),
        new ColorSelectorCustomColor("空色", "#FF38BDF8"),
        new ColorSelectorCustomColor("青磁", "#FF22D3EE"),
        new ColorSelectorCustomColor("浅葱", "#FF06B6D4"),
        new ColorSelectorCustomColor("青", "#FF2563EB"),
        new ColorSelectorCustomColor("瑠璃", "#FF3B82F6"),
        new ColorSelectorCustomColor("群青", "#FF1D4ED8"),
        new ColorSelectorCustomColor("藍", "#FF312E81"),

        // 紫・桃系
        new ColorSelectorCustomColor("紫", "#FF7C3AED"),
        new ColorSelectorCustomColor("菫", "#FFA78BFA"),
        new ColorSelectorCustomColor("藤", "#FFC4B5FD"),
        new ColorSelectorCustomColor("桃", "#FFDB2777"),
        new ColorSelectorCustomColor("桜", "#FFF9A8D4"),
    ];

    /// <summary>代表色パレットのタブ（■ だけを指定した列数で並べる）を作る。</summary>
    /// <param name="columns">■ の列数（1 以上。 既定は <see cref="DefaultPaletteColumns"/>）。</param>
    /// <returns>■ だけの格子のタブ。</returns>
    public static ColorSelectorCustomTab CreatePaletteTab(int columns = DefaultPaletteColumns)
        => new(PaletteTabTitle, RepresentativeColors, Math.Max(1, columns));

    /// <summary>代表色（色名の一覧）のタブを作る。</summary>
    /// <returns>色名付きの一覧のタブ。</returns>
    public static ColorSelectorCustomTab CreateListTab() => new(ListTabTitle, RepresentativeColors);

    /// <summary>代表色のタブ（■ の格子と色名の一覧）を作る。</summary>
    /// <param name="columns">■ の列数（1 以上。 既定は <see cref="DefaultPaletteColumns"/>）。</param>
    /// <returns>2 つのタブ（パレット → 一覧 の順）。</returns>
    public static IReadOnlyList<ColorSelectorCustomTab> CreateTabs(int columns = DefaultPaletteColumns)
        => [CreatePaletteTab(columns), CreateListTab()];

    /// <summary>色の選択を注釈向けの構成にする（代表色のタブだけを出す）。</summary>
    /// <param name="selector">構成する色の選択。</param>
    /// <param name="columns">■ の列数（1 以上。 既定は <see cref="DefaultPaletteColumns"/>）。</param>
    /// <param name="showPicker">カラーピッカーのタブを出すかどうか。</param>
    /// <remarks>
    /// <b>テーマカラーと色名のタブは出さない</b>: 注釈の色は「枠線・内側・文字」の 3 種類しかなく、
    /// 色の全一覧を開くと目的の色へ届くまでに手数がかかる。<br/>
    /// 16 進や色名を直接打ち込む入力欄は残す（代表色に無い色を指定できる）。
    /// </remarks>
    internal static void Configure(
        ColorSelectorControl selector, int columns = DefaultPaletteColumns, bool showPicker = false)
    {
        selector.ShowPickerTab = showPicker;
        selector.ShowThemeColorTab = false;
        selector.ShowNamedColorTab = false;
        selector.ShowTabTitles = true;
        selector.CustomTabs = CreateTabs(columns);
    }

    /// <summary>色の選択を「代表色パレット・代表色 + 標準のタブ」の構成にする（設定画面向け）。</summary>
    /// <param name="selector">構成する色の選択。</param>
    /// <param name="columns">■ の列数（1 以上。 既定は <see cref="DefaultPaletteColumns"/>）。</param>
    /// <remarks>
    /// <b>標準のタブも出す</b>: 設定画面では任意の色を選べるほうがよい（テーマの色・色名・カラーピッカー）。
    /// </remarks>
    public static void ConfigureAll(ColorSelectorControl selector, int columns = DefaultPaletteColumns)
    {
        selector.ShowPickerTab = true;
        selector.ShowThemeColorTab = true;
        selector.ShowNamedColorTab = true;
        selector.ShowTabTitles = true;
        selector.CustomTabs = CreateTabs(columns);
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
