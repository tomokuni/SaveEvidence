using EsUtil.Feature.Annotation;

namespace EsUtil.SaveEvidence.App.Core.Models;

/// <summary>画像に付随する注釈ファイル（<c>*.annotations.json</c> / <c>*.annotated.*</c>）の判定。</summary>
/// <remarks>
/// <b>保存フォルダの一覧から除く</b>: <c>*.annotated.*</c> は画像拡張子を持つため、そのままでは
/// 元画像と並んで一覧に出る（同じ絵が 2 つ見え、選択すると注釈付きの絵を元画像として開いてしまう）。<br/>
/// <b>名前の規則は EsUtil.Feature.Annotation.Core が単一所有する</b>（AnnotationJson）。 本クラスは判定だけを持ち、
/// パスの組み立ては行わない（規則の二重管理を避ける）。
/// </remarks>
public static class AnnotationCompanions
{
    /// <summary>注釈の文書ファイルの接尾辞。</summary>
    public const string DocumentSuffix = AnnotationJson.FileSuffix;

    /// <summary>合成画像の名前に挟まる目印。</summary>
    public const string ComposedInfix = AnnotationJson.ComposedInfix;

    /// <summary>指定したパスが注釈の付随ファイルかどうかを判定する。</summary>
    /// <param name="path">判定するパス。</param>
    /// <returns>付随ファイルの場合は <see langword="true"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> が null の場合。</exception>
    public static bool IsCompanion(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var name = Path.GetFileName(path);
        return name.EndsWith(DocumentSuffix, StringComparison.OrdinalIgnoreCase)
            || name.Contains(ComposedInfix, StringComparison.OrdinalIgnoreCase);
    }
}
