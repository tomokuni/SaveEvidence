using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

namespace EsUtil.SaveEvidence.App.Core.Icons;

/// <summary>SVG アイコンを埋め込みリソースから読み込む（4 つの UI 版で共通）。</summary>
/// <remarks>
/// <b>アイコンの出所を 1 か所にする</b>ため、Fluent System Icons 形式の SVG（viewBox は 24x24 の<br/>
/// 単一 <c>&lt;path d="..."&gt;</c>）を本プロジェクトの <c>Icons/</c> に置き、埋め込みリソースとして読む。<br/>
/// UI 版ごとに SVG を複製すると、片方だけ直したときに見た目がずれる。<br/>
/// <br/>
/// <b>解析だけを担う</b>: パス データを各 UI のジオメトリへ変換するのは UI 版の描画層の仕事である<br/>
/// （本クラスは UI フレームワークを参照しない）。<br/>
/// <b>失敗しても止めない</b>: リソース欠落・解析失敗の場合は空の <see cref="SvgIcon"/> を返す（UI を壊さない）。
/// </remarks>
internal static partial class SvgIconLoader
{
    /// <summary>埋め込みリソースの論理名プレフィックス（App.Core.csproj の論理名と一致させる）。</summary>
    private const string ResourcePrefix = "EsUtil.SaveEvidence.App.Core.Icons.";

    /// <summary>解析結果のキャッシュ（アイコンは表示のたびに参照されるため）。</summary>
    private static readonly ConcurrentDictionary<string, SvgIcon> s_cache = new(StringComparer.Ordinal);

    /// <summary>アイコンを読み込む。</summary>
    /// <param name="fileName">SVG ファイル名（例: <c>ic_fluent_save_24_regular.svg</c>）。</param>
    /// <returns>アイコンの内容。 読み込めない場合は空のアイコン。</returns>
    public static SvgIcon Load(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return s_cache.GetOrAdd(fileName, LoadCore);
    }

    /// <summary>アイコンの一覧（埋め込みリソースのファイル名）を取得する。</summary>
    /// <returns>ファイル名の一覧（並びは不定）。</returns>
    public static IReadOnlyList<string> Enumerate()
        => [.. typeof(SvgIconLoader).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(name => name[ResourcePrefix.Length..])];

    /// <summary>アイコンを 1 件読み込む（キャッシュの外側）。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <returns>アイコンの内容。 読み込めない場合は空のアイコン。</returns>
    private static SvgIcon LoadCore(string fileName)
    {
        try
        {
            using var stream = typeof(SvgIconLoader).Assembly.GetManifestResourceStream(ResourcePrefix + fileName);
            if (stream is null)
            {
                return Empty(fileName);
            }

            using var reader = new StreamReader(stream);
            return Parse(fileName, reader.ReadToEnd());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // 埋め込みリソースの読み込み失敗は空のアイコンで代替する（表示だけが欠け、アプリは動く）
            return Empty(fileName);
        }
    }

    /// <summary>SVG のテキストを解析する。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <param name="svg">SVG のテキスト。</param>
    /// <returns>アイコンの内容。</returns>
    private static SvgIcon Parse(string fileName, string svg)
    {
        var (width, height) = ParseViewBox(svg);
        var paths = new List<string>();
        foreach (Match match in PathRegex().Matches(svg))
        {
            paths.Add(match.Groups["d"].Value);
        }

        if (paths.Count == 0)
        {
            return Empty(fileName);
        }

        return new SvgIcon(fileName, width, height, paths, ParseFill(svg));
    }

    /// <summary>viewBox（無ければ width / height）から論理的な大きさを求める。</summary>
    /// <param name="svg">SVG のテキスト。</param>
    /// <returns>幅と高さ（数値でない場合は 24）。</returns>
    private static (double Width, double Height) ParseViewBox(string svg)
    {
        if (ViewBoxRegex().Match(svg) is { Success: true } viewBox)
        {
            var parts = viewBox.Groups["value"].Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4
                && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
                && double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h)
                && w > 0 && h > 0)
            {
                return (w, h);
            }
        }

        return (24, 24);
    }

    /// <summary>塗りの色を求める。</summary>
    /// <param name="svg">SVG のテキスト。</param>
    /// <returns>色（<c>0xAARRGGBB</c>）。 見つからない場合は不透明の黒。</returns>
    private static uint ParseFill(string svg)
    {
        if (FillRegex().Match(svg) is not { Success: true } fill)
        {
            return 0xFF000000;
        }

        var value = fill.Groups["value"].Value.Trim();
        if (value.Length == 7 && value[0] == '#'
            && uint.TryParse(value[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return 0xFF000000 | rgb;
        }

        return 0xFF000000;
    }

    /// <summary>空のアイコンを作る。</summary>
    /// <param name="fileName">SVG ファイル名。</param>
    /// <returns>パスを持たないアイコン。</returns>
    private static SvgIcon Empty(string fileName) => new(fileName, 24, 24, [], 0xFF000000);

    /// <summary>path 要素の <c>d</c> 属性を抽出する（属性の順序に依存しない）。</summary>
    [GeneratedRegex(@"<path\b[^>]*?\bd\s*=\s*""(?<d>[^""]+)""", RegexOptions.None, 200)]
    private static partial Regex PathRegex();

    /// <summary>viewBox 属性を抽出する。</summary>
    [GeneratedRegex(@"viewBox\s*=\s*""(?<value>[^""]*)""", RegexOptions.IgnoreCase, 200)]
    private static partial Regex ViewBoxRegex();

    /// <summary>塗りの指定を抽出する（svg 要素の fill を使う。 path 側の指定は持たない前提）。</summary>
    [GeneratedRegex(@"\bfill\s*=\s*""(?<value>[^""]*)""", RegexOptions.IgnoreCase, 200)]
    private static partial Regex FillRegex();
}