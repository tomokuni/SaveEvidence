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
    /// <summary>代表色 1 の並び（色名と保存値 <c>#AARRGGBB</c>）。</summary>
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

    /// <summary>代表色 2 の並び（色名と保存値 <c>#AARRGGBB</c>）。</summary>
    /// <remarks>
    /// <b>代表色 1 と重ならない色</b>にする（同じ色が 2 つのタブにあると、どちらを選んだか分からない）。
    /// 和名を付け、暗い色 → 中間色 → 淡い色の順に並べる。
    /// </remarks>
    public static IReadOnlyList<ColorSelectorCustomColor> AccentColors { get; } =
    [
        new ColorSelectorCustomColor("墨", "#FF1F2937"),
        new ColorSelectorCustomColor("藍鉄", "#FF312E81"),
        new ColorSelectorCustomColor("茜", "#FFB91C1C"),
        new ColorSelectorCustomColor("琥珀", "#FFB45309"),
        new ColorSelectorCustomColor("鶯", "#FF4D7C0F"),
        new ColorSelectorCustomColor("常磐", "#FF047857"),
        new ColorSelectorCustomColor("浅葱", "#FF06B6D4"),
        new ColorSelectorCustomColor("群青", "#FF1D4ED8"),
        new ColorSelectorCustomColor("菫", "#FF6D28D9"),
        new ColorSelectorCustomColor("躑躅", "#FFBE123C"),
        new ColorSelectorCustomColor("銀", "#FF9CA3AF"),
        new ColorSelectorCustomColor("桜", "#FFF9A8D4"),
        new ColorSelectorCustomColor("珊瑚", "#FFF87171"),
        new ColorSelectorCustomColor("山吹", "#FFFBBF24"),
        new ColorSelectorCustomColor("若草", "#FFA3E635"),
        new ColorSelectorCustomColor("藤", "#FFC4B5FD"),
    ];

    /// <summary>代表色の組を取得する。</summary>
    /// <param name="kind">組（代表色 1 / 代表色 2）。</param>
    /// <returns>その組の 16 色。</returns>
    public static IReadOnlyList<ColorSelectorCustomColor> GetColors(AnnotationPaletteKind kind)
        => kind == AnnotationPaletteKind.Accent ? AccentColors : RepresentativeColors;

    /// <summary>代表色の組の題名を取得する。</summary>
    /// <param name="kind">組（代表色 1 / 代表色 2）。</param>
    /// <returns>タブに出す題名。</returns>
    public static string GetTitle(AnnotationPaletteKind kind)
        => kind == AnnotationPaletteKind.Accent ? "代表色２" : "代表色１";

    /// <summary>代表色を並べるタブの定義を作る。</summary>
    /// <param name="kind">組（代表色 1 / 代表色 2）。</param>
    /// <param name="layout">並べ方（色名付きの一覧 / 色の四角だけを 6 列）。</param>
    /// <returns>代表色のタブ。</returns>
    public static ColorSelectorCustomTab CreateTab(
        AnnotationPaletteKind kind, ColorSelectorTabLayout layout = ColorSelectorTabLayout.List)
        => new(GetTitle(kind), GetColors(kind), layout);

    /// <summary>代表色 1 と代表色 2 のタブの定義を作る（設定画面のように両方を並べる用途）。</summary>
    /// <param name="layout">並べ方（色名付きの一覧 / 色の四角だけを 6 列）。</param>
    /// <returns>2 つのタブ。</returns>
    public static IReadOnlyList<ColorSelectorCustomTab> CreateAllTabs(
        ColorSelectorTabLayout layout = ColorSelectorTabLayout.List)
        => [CreateTab(AnnotationPaletteKind.Representative, layout), CreateTab(AnnotationPaletteKind.Accent, layout)];

    /// <summary>色の選択を注釈向けの構成にする（代表色のタブを出す）。</summary>
    /// <param name="selector">構成する色の選択。</param>
    /// <param name="kind">代表色の組（代表色 1 / 代表色 2）。</param>
    /// <param name="layout">代表色の並べ方（色名付きの一覧 / 色の四角だけを 6 列）。</param>
    /// <param name="showPicker">カラーピッカーのタブを出すかどうか。</param>
    /// <remarks>
    /// <b>テーマカラーと色名のタブは出さない</b>: 注釈の色は「枠線・内側・文字」の 3 種類しかなく、
    /// 色の全一覧を開くと目的の色へ届くまでに手数がかかる。<br/>
    /// <b>タブが 1 つなら題名は出さない</b>: 切り替える相手が無いため、一覧を狭くできる。<br/>
    /// 16 進や色名を直接打ち込む入力欄は残す（代表色に無い色を指定できる）。
    /// </remarks>
    internal static void Configure(
        ColorSelectorControl selector,
        AnnotationPaletteKind kind = AnnotationPaletteKind.Representative,
        ColorSelectorTabLayout layout = ColorSelectorTabLayout.List,
        bool showPicker = false)
    {
        selector.ShowPickerTab = showPicker;
        selector.ShowThemeColorTab = false;
        selector.ShowNamedColorTab = false;
        selector.ShowTabTitles = false;
        selector.CustomTabs = [CreateTab(kind, layout)];
    }

    /// <summary>色の選択を「代表色 1・代表色 2 + 標準のタブ」の構成にする（設定画面向け）。</summary>
    /// <param name="selector">構成する色の選択。</param>
    /// <param name="layout">代表色の並べ方（色名付きの一覧 / 色の四角だけを 6 列）。</param>
    /// <remarks>
    /// <b>標準のタブも出す</b>: 設定画面では任意の色を選べるほうがよい（テーマの色・色名・カラーピッカー）。
    /// タブが 3 つ以上あるため題名も出す（どれが代表色か分かるようにする）。
    /// </remarks>
    public static void ConfigureAll(ColorSelectorControl selector, ColorSelectorTabLayout layout)
    {
        selector.ShowPickerTab = true;
        selector.ShowThemeColorTab = true;
        selector.ShowNamedColorTab = true;
        selector.ShowTabTitles = true;
        selector.CustomTabs = CreateAllTabs(layout);
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
