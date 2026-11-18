using System.Drawing.Text;
using EsUtil.Feature.Annotation;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>本文の書式で選べるフォントの一覧（WinForms 版）。</summary>
/// <remarks>
/// <b>優先フォントを先頭に置く</b>: <see cref="AnnotationDefaults.PreferredFontFamilies"/> を先頭へ並べ、
/// その後にこの環境へインストール済みのフォントを続ける（Avalonia UI 版と同じ規則）。<br/>
/// 一覧は起動時に 1 度だけ作る（フォントの列挙は重い）。
/// </remarks>
public static class AnnotationFonts
{
    /// <summary>選べるフォント名の一覧を取得する。</summary>
    public static IReadOnlyList<string> All { get; } = Build();

    /// <summary>フォント名の一覧を作る。</summary>
    /// <returns>優先フォントを先頭に置いた一覧。</returns>
    private static IReadOnlyList<string> Build()
    {
        using var installed = new InstalledFontCollection();
        var names = installed.Families
            .Select(family => family.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCulture)
            .ToList();

        foreach (var preferred in AnnotationDefaults.PreferredFontFamilies.Reverse())
        {
            var index = names.FindIndex(name => string.Equals(name, preferred, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                names.RemoveAt(index);
            }

            names.Insert(0, preferred);
        }

        return names;
    }
}