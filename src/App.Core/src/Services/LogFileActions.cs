using System.Diagnostics;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>設定ページから開くログ ファイルの手順（当日分を既定のアプリで開く・無ければフォルダを開く）を単一所有する。</summary>
/// <remarks>
/// <b>4 つの UI 版が同じ手順を使う</b>ため、本クラスが手順を単一所有する（UI 版ごとに書くと、
/// 「当日分が無いときに何を開くか」が版ごとにずれる）。<br/>
/// <b>失敗は例外で表し、記録は呼び出し側が行う</b>（<c>.github/例外処理規約.md</c> §2・§3。 本クラスはロガーを持たない）。<br/>
/// 対象のファイル名は <see cref="AppIdentity.GetDailyLogFilePath"/> が単一所有する。
/// </remarks>
public static class LogFileActions
{
    /// <summary>当日のログ ファイルを既定のアプリで開く（無ければログ フォルダを開く）。</summary>
    /// <param name="assemblyName">ログを書いているアセンブリ名（各 UI 版の <c>AppLog</c> が渡す）。</param>
    /// <exception cref="InvalidOperationException">プロセスを起動できなかった場合。</exception>
    /// <exception cref="DirectoryNotFoundException">ログ ファイルもログ フォルダも無かった場合。</exception>
    /// <remarks>
    /// <b>当日分が無いときにフォルダを開く理由</b>: ボタンが無反応に見えるのを避ける
    /// （日次ローテーションの直後や、書き込みに失敗している環境でも置き場所へ辿れる）。<br/>
    /// <b>表示は既定の関連付けへ任せる</b>: ログはテキストであり、<c>.log</c> の関連付け（メモ帳等）が
    /// 利用者の環境の見方に合う（アプリ内に読み込み専用のビューアを持たない）。
    /// </remarks>
    public static void ShowCurrentLogFile(string assemblyName)
    {
        var logPath = AppIdentity.GetDailyLogFilePath(assemblyName, DateTime.Now);
        if (File.Exists(logPath))
        {
            OpenWithDefaultApp(logPath);
            return;
        }

        if (!Directory.Exists(AppIdentity.LogsFolder))
        {
            throw new DirectoryNotFoundException($"ログ フォルダが見つかりませんでした: {AppIdentity.LogsFolder}");
        }

        FileSystemActions.OpenInExplorer(AppIdentity.LogsFolder);
    }

    /// <summary>ファイルを既定の関連付けのアプリで開く。</summary>
    /// <param name="filePath">開くファイルのパス。</param>
    /// <exception cref="InvalidOperationException">プロセスを起動できなかった場合。</exception>
    private static void OpenWithDefaultApp(string filePath)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = filePath,
            // 既定の関連付けで開く（関連付けの無い拡張子は Windows がアプリの選択を促す）
            UseShellExecute = true,
        }) ?? throw new InvalidOperationException($"ログ ファイルを開けませんでした: {filePath}");
    }
}
