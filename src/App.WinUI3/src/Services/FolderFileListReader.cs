using EsUtil.SaveEvidence.App.Core.Models;

namespace EsUtil.SaveEvidence.App.WinUI3.Services;

/// <summary>保存先フォルダの画像ファイル一覧を読み込む。</summary>
/// <remarks>
/// <b>役割</b>: フォルダビューが表示する画像ファイルの列挙をビューから分離する<br/>
/// （列挙の規則をウィンドウなしで検証できるようにする）。<br/>
/// <b>対象</b>: 画像拡張子（<see cref="ImageFileExtensions.IsImageFile"/>）を持つファイルだけを返す。<br/>
/// <b>並び順</b>: 名前の昇順または降順。 大文字小文字は区別しない（文化に依存しない比較を使う）。<br/>
/// <b>失敗時</b>: フォルダが無い・アクセスできない場合は空の一覧を返す（例外にしない）。
/// </remarks>
internal static class FolderFileListReader
{
    /// <summary>指定フォルダの画像ファイルを列挙する。</summary>
    /// <param name="folderPath">対象のフォルダ パス。 空の場合は空の一覧を返す。</param>
    /// <param name="isSortAscending">true の場合は名前の昇順、false の場合は降順。</param>
    /// <returns>画像ファイルの絶対パスの一覧。</returns>
    public static IReadOnlyList<string> Read(string? folderPath, bool isSortAscending)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return [];
        }

        var files = Directory
            .EnumerateFiles(folderPath)
            .Where(ImageFileExtensions.IsImageFile)
            .ToList();

        files.Sort(StringComparer.OrdinalIgnoreCase);
        if (!isSortAscending)
        {
            files.Reverse();
        }

        return files;
    }
}
