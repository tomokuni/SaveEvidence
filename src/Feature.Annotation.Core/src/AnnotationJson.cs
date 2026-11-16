using System.Text.Json;
using EsUtil.Feature.Annotation.Core;

namespace EsUtil.Feature.Annotation;

/// <summary>注釈の JSON（<c>&lt;name&gt;.annotations.json</c>）の読み書き（単一所有）。</summary>
/// <remarks>
/// <b>書式は本クラスが単一所有する</b>: 4 つの UI 版・アプリが同じ書式で保存する
/// （UI ごとに書くと、片方で保存した注釈が他方で読めない）。<br/>
/// <b>ソース生成 + 明示の変換器を使う</b>（反射を使わない。 Native AOT / トリミングでも動く）。<br/>
/// <b>未知の値は落とさない</b>: 解釈できない色は透明、欠落した項目は既定値として読む（1 件の破損で
/// ファイル全体を失わない）。
/// </remarks>
public static class AnnotationJson
{
    /// <summary>注釈ファイルの拡張子（元画像のファイル名へ付ける）を取得する。</summary>
    public const string FileSuffix = ".annotations.json";

    /// <summary>このアプリが読み書きする JSON のオプション（インデントあり・変換器は固定）。</summary>
    private static readonly JsonSerializerOptions s_options = CreateOptions();

    /// <summary>注釈を JSON 文字列へ変換する。</summary>
    /// <param name="document">対象の文書。</param>
    /// <returns>JSON 文字列（UTF-8 の BOM 無しでファイルへ書ける）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> が null の場合。</exception>
    public static string Serialize(AnnotationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, s_options.GetTypeInfo<AnnotationDocument>());
    }

    /// <summary>JSON 文字列から注釈を読み込む。</summary>
    /// <param name="json">JSON 文字列。</param>
    /// <returns>読み込んだ文書。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> が null の場合。</exception>
    /// <exception cref="JsonException">JSON として解釈できない場合。</exception>
    public static AnnotationDocument Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize(json, s_options.GetTypeInfo<AnnotationDocument>())
            ?? new AnnotationDocument();
    }

    /// <summary>元画像のパスから注釈ファイルのパスを求める。</summary>
    /// <param name="imagePath">元画像のパス（例: <c>…\screenshot.png</c>）。</param>
    /// <returns>注釈ファイルのパス（例: <c>…\screenshot.png.annotations.json</c>）。</returns>
    /// <remarks>拡張子を置き換えず後ろへ付ける（元画像の形式が何であっても衝突しない）。</remarks>
    /// <exception cref="ArgumentException"><paramref name="imagePath"/> が空白の場合。</exception>
    public static string GetDocumentPath(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        return imagePath + FileSuffix;
    }

    /// <summary>合成画像のパスを求める。</summary>
    /// <param name="imagePath">元画像のパス（例: <c>…\screenshot.png</c>）。</param>
    /// <returns>合成画像のパス（例: <c>…\screenshot.annotated.png</c>）。</returns>
    /// <exception cref="ArgumentException"><paramref name="imagePath"/> が空白の場合。</exception>
    public static string GetComposedPath(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        var directory = Path.GetDirectoryName(imagePath);
        var stem = Path.GetFileNameWithoutExtension(imagePath);
        var extension = Path.GetExtension(imagePath);
        var name = stem + ".annotated" + extension;

        return string.IsNullOrEmpty(directory) ? name : Path.Combine(directory, name);
    }

    /// <summary>JSON のオプションを生成する。</summary>
    /// <returns>読み書きに使うオプション（読み取り専用）。</returns>
    /// <remarks>
    /// <b>変換器はインスタンスで渡す</b>: 属性で型を指定する方式は、変換器の生成に反射を使うため
    /// Native AOT で警告になる（実測ではなく設計上の理由。 設定ファイルと同じ方針）。
    /// </remarks>
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            TypeInfoResolver = AnnotationJsonContext.Default,
        };

        options.Converters.Add(new EsRectJsonConverter());
        options.Converters.Add(new EsPointJsonConverter());
        options.Converters.Add(new DrawingColorJsonConverter());
        options.MakeReadOnly();
        return options;
    }
}
