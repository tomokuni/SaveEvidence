using System.Runtime.InteropServices;
using EsUtil.Platform.Core;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>タスク バーでのグループ分け（アプリ ユーザー モデル ID）をプロセスへ設定する。</summary>
/// <remarks>
/// <b>なぜ必要か</b>: Windows はタスク バーのボタンをアプリ ユーザー モデル ID（AUMID）でグループ化する。<br/>
/// 実行ファイルが 4 つある本アプリは、AUMID を明示しないと<b>実行ファイルごとに別のアプリ</b>として<br/>
/// タスク バーの別の位置へ表示される（実測）。 4 つの UI 版が同じ AUMID を設定すると 1 か所にまとまる。<br/>
/// <br/>
/// <b>設定する時機</b>: ウィンドウを作る<b>前</b>（プロセス単位の設定のため、最初のウィンドウ作成より前に行う）。<br/>
/// <b>失敗時</b>: 記録せずに続行する（タスク バーのグループ分けが既定のままになるだけで、機能は損なわれない）。
/// </remarks>
public static class TaskbarIdentity
{
    /// <summary>このアプリの AUMID（<see cref="AppIdentity.AppUserModelId"/>）をプロセスへ設定する。</summary>
    /// <remarks>起動時に 1 回だけ呼ぶ。 2 回目以降の呼び出しは同じ値を設定するだけである。</remarks>
    public static void Apply() => Apply(AppIdentity.AppUserModelId);

    /// <summary>指定の AUMID をプロセスへ設定する。</summary>
    /// <param name="appUserModelId">アプリ ユーザー モデル ID（逆ドメイン形式）。</param>
    /// <returns>設定できた場合は true。</returns>
    /// <remarks>失敗（Windows 以外・API 不在・値の不備）でも例外にしない（起動を妨げない）。</remarks>
    public static bool Apply(string appUserModelId)
    {
        try
        {
            return SetCurrentProcessExplicitAppUserModelID(appUserModelId) == 0;
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // API 不在・値の不備でも起動を妨げない（タスク バーのグループ分けが既定のままになるだけ）
            return false;
        }
    }

    /// <summary>プロセスの AUMID を設定する（Win32）。</summary>
    /// <param name="appUserModelId">アプリ ユーザー モデル ID。</param>
    /// <returns>成功した場合は 0（<c>S_OK</c>）。</returns>
    /// <remarks>
    /// 安全でないコードの許可を必要としない <c>DllImport</c> を使い、読み込み先を System32 に固定する<br/>
    /// （探索順の脆弱性を作らない）。
    /// </remarks>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetCurrentProcessExplicitAppUserModelID")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appUserModelId);
}
