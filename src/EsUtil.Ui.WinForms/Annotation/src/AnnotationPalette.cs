namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>注釈の色選択で使う代表色（40 色。 WinForms 版）。</summary>
/// <remarks>
/// <b>並びは 4 つの UI 版で同じ</b>である（AvaloniaUI 版・MewUI 版・WinUI 3 版と同じ色名・同じ値）。<br/>
/// WinForms には色選択コントロールが無いため、代表色を<b>メニューに並べる</b>（任意の色は色ダイアログで選ぶ）。
/// </remarks>
public static class AnnotationPalette
{
    /// <summary>代表色の並び（色名と色）を取得する。</summary>
    public static IReadOnlyList<(string Name, Color Value)> RepresentativeColors { get; } =
    [
        ("黒", Color.FromArgb(255, 0, 0, 0)),
        ("濃い灰色", Color.FromArgb(255, 64, 64, 64)),
        ("灰色", Color.FromArgb(255, 128, 128, 128)),
        ("薄い灰色", Color.FromArgb(255, 192, 192, 192)),
        ("白", Color.FromArgb(255, 255, 255, 255)),
        ("墨", Color.FromArgb(255, 31, 41, 55)),
        ("銀", Color.FromArgb(255, 156, 163, 175)),
        ("赤", Color.FromArgb(255, 220, 38, 38)),
        ("茜", Color.FromArgb(255, 185, 28, 28)),
        ("紅", Color.FromArgb(255, 225, 29, 72)),
        ("躑躅", Color.FromArgb(255, 190, 18, 60)),
        ("珊瑚", Color.FromArgb(255, 248, 113, 113)),
        ("朱", Color.FromArgb(255, 234, 88, 12)),
        ("橙", Color.FromArgb(255, 249, 115, 22)),
        ("琥珀", Color.FromArgb(255, 180, 83, 9)),
        ("杏", Color.FromArgb(255, 253, 186, 116)),
        ("黄", Color.FromArgb(255, 234, 179, 8)),
        ("山吹", Color.FromArgb(255, 251, 191, 36)),
        ("檸檬", Color.FromArgb(255, 253, 224, 71)),
        ("黄土", Color.FromArgb(255, 202, 138, 4)),
        ("茶", Color.FromArgb(255, 146, 64, 14)),
        ("黄緑", Color.FromArgb(255, 101, 163, 13)),
        ("若草", Color.FromArgb(255, 132, 204, 22)),
        ("鶯", Color.FromArgb(255, 77, 124, 15)),
        ("緑", Color.FromArgb(255, 22, 163, 74)),
        ("常磐", Color.FromArgb(255, 4, 120, 87)),
        ("青緑", Color.FromArgb(255, 13, 148, 136)),
        ("水色", Color.FromArgb(255, 14, 165, 233)),
        ("空色", Color.FromArgb(255, 56, 189, 248)),
        ("青磁", Color.FromArgb(255, 34, 211, 238)),
        ("浅葱", Color.FromArgb(255, 6, 182, 212)),
        ("青", Color.FromArgb(255, 37, 99, 235)),
        ("瑠璃", Color.FromArgb(255, 59, 130, 246)),
        ("群青", Color.FromArgb(255, 29, 78, 216)),
        ("藍", Color.FromArgb(255, 49, 46, 129)),
        ("紫", Color.FromArgb(255, 124, 58, 237)),
        ("菫", Color.FromArgb(255, 167, 139, 250)),
        ("藤", Color.FromArgb(255, 196, 181, 253)),
        ("桃", Color.FromArgb(255, 219, 39, 119)),
        ("桜", Color.FromArgb(255, 249, 168, 212)),
    ];
}
