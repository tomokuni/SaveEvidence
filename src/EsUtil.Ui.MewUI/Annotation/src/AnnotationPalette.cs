using EsUtil.Ui.MewUI.ColorSelector;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈の色選択で使う代表色（40 色）と、色の選択をその構成にする処理。</summary>
/// <remarks>
/// <b>並びは 4 つの UI 版で同じ</b>である（AvaloniaUI 版の <c>AnnotationPalette</c> と同じ色名・同じ値。 色名の一覧と
/// ■ の格子が同じ出所になる）。<br/>
/// 色は保存値（<c>#AARRGGBB</c>）で持つ（入力欄・保存・描画が同じ文字列を扱う）。
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
    /// <param name="columns">■ の格子の列数。</param>
    /// <returns>タブの並び。</returns>
    public static IReadOnlyList<ColorSelectorCustomTab> CreateTabs(int columns = DefaultPaletteColumns)
        => [CreatePaletteTab(columns), CreateListTab()];

    /// <summary>色の選択を「代表色パレット・代表色」の構成にする（書式バー・右クリック向け）。</summary>
    /// <param name="selector">構成する色の選択。</param>
    /// <param name="columns">■ の格子の列数。</param>
    /// <param name="showPicker">カラーピッカーのタブも出すかどうか。</param>
    /// <remarks>テーマの色・色名のタブは隠す（注釈の色は代表色から選ぶ。 設定画面は <see cref="ConfigureAll"/>）。</remarks>
    public static void Configure(
        ColorSelectorControl selector, int columns = DefaultPaletteColumns, bool showPicker = false)
    {
        ArgumentNullException.ThrowIfNull(selector);

        selector.ShowCustomTab = true;
        selector.CustomTabs = CreateTabs(columns);
        selector.ShowThemeColorTab = false;
        selector.ShowNamedColorTab = false;
        selector.ShowPickerTab = showPicker;
    }

    /// <summary>色の選択を「代表色パレット・代表色 + 標準のタブ」の構成にする（設定画面向け）。</summary>
    /// <param name="selector">構成する色の選択。</param>
    /// <param name="columns">■ の格子の列数。</param>
    public static void ConfigureAll(ColorSelectorControl selector, int columns = DefaultPaletteColumns)
    {
        ArgumentNullException.ThrowIfNull(selector);

        selector.ShowCustomTab = true;
        selector.CustomTabs = CreateTabs(columns);
        selector.ShowThemeColorTab = true;
        selector.ShowNamedColorTab = true;
        selector.ShowPickerTab = true;
    }
}
