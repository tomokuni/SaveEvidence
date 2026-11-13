using System.Diagnostics;
using EsUtil.SaveEvidence.App.Core.Native;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>フォルダ ビューから行う OS の操作（エクスプローラーの起動・ごみ箱への移動）を単一所有する。</summary>
/// <remarks>
/// 4 つの UI 版が同じ手順（シェル起動の引数・ごみ箱の指定）を使うため、本クラスが手順を単一所有する<br/>
/// （UI ごとに <see cref="Process"/> の起動引数を書くと、片方だけ設定がずれる）。<br/>
/// <b>失敗は例外で表し、記録は呼び出し側が行う</b>（`.github/例外処理規約.md` §2・§3）。<br/>
/// 本クラスはロガーを持たないため、記録は各 UI 版のロガーが行う。
/// </remarks>
public static class FileSystemActions
{
    /// <summary>フォルダをエクスプローラーで開く。</summary>
    /// <param name="folderPath">開くフォルダのパス。</param>
    /// <exception cref="InvalidOperationException">プロセスを起動できなかった場合。</exception>
    /// <remarks>
    /// 既定の関連付け（<c>UseShellExecute</c>）に任せず <c>explorer.exe</c> を明示する<br/>
    /// （関連付けが変更されていてもフォルダはエクスプローラーで開く）。
    /// </remarks>
    public static void OpenInExplorer(string folderPath)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            // 引数は 1 つにまとめる（パスに空白が含まれても分割されないようにする）
            Arguments = $"\"{folderPath}\"",
            UseShellExecute = true,
        }) ?? throw new InvalidOperationException("エクスプローラーを起動できませんでした。");
    }

    /// <summary>ファイルをごみ箱へ移動する（完全削除しない）。</summary>
    /// <param name="filePath">移動するファイルのパス。</param>
    /// <exception cref="IOException">移動できなかった場合。</exception>
    /// <remarks>
    /// <b>ごみ箱へ移動する理由</b>: 一覧で選んだだけのファイルを完全削除すると元へ戻せない。<br/>
    /// 利用者がごみ箱を空にするまで復元できる状態にしておく。
    /// </remarks>
    public static void DeleteToRecycleBin(string filePath)
    {
        if (!WinRecycleBin.TryDelete(filePath))
        {
            throw new IOException($"ファイルをごみ箱へ移動できませんでした: {filePath}");
        }
    }
}
