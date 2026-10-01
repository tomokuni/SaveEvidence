namespace EsUtil.SaveEvidence.App.Core.Models;

/// <summary>画像ファイルの拡張子判定（フォルダビューの一覧抽出で共用する）。</summary>
/// <remarks>
/// 一覧に含める画像の拡張子は UI 実装に依存しない知識のため、本クラスが単一所有する。<br/>
/// 拡張子の比較は大文字・小文字を区別しない（<c>.PNG</c> も画像として扱う）。<br/>
/// 保存時の形式判定（<c>IImageWriter</c> の実装）とは目的が異なるため、それぞれが所有する。
/// </remarks>
internal static class ImageFileExtensions
{
    /// <summary>一覧に含める画像ファイルの拡張子（先頭のドットを含む）。</summary>
    private static readonly HashSet<string> s_all = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif", ".webp",
    };

    /// <summary>拡張子の一覧（読み取り専用）を取得する。</summary>
    /// <value>先頭のドットを含む拡張子の集合。</value>
    public static IReadOnlyCollection<string> All => s_all;

    /// <summary>指定されたパスが画像ファイルかどうかを判定する。</summary>
    /// <param name="path">判定するパス（拡張子のみを使用する）。</param>
    /// <returns>一覧に含める画像の場合は true。 拡張子が無い・対象外の場合は false。</returns>
    public static bool IsImageFile(string path)
        => s_all.Contains(Path.GetExtension(path));
}
