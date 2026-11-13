using System.Runtime.InteropServices;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;

/// <summary>撮影の間だけシェル タスク バー（エクスプローラー）を隠す（<see cref="IDisposable"/> で戻す）。</summary>
/// <remarks>
/// <b>何のために要るか</b>: 撮影中は仮想スクリーン全面を覆う最前面のオーバーレイを表示する。<br/>
/// タスク バーも最前面のため、オーバーレイが前面へ出てからシェルがタスク バーを前面へ戻すまでの<br/>
/// 数十ミリ秒、タスク バーが暗転マスクで覆われる。 さらに Windows 11 のタスク バーは半透明（アクリル）で、<br/>
/// 背後を覆うオーバーレイの色が透けるため、前面関係を直しても帯の色が変わり続ける。<br/>
/// 実測 2026-11-13: タスク バーの帯の輝度が 220 → 174 → 220 と 2〜8 フレーム変化した<br/>
/// （AvaloniaUI 版 2 回 / MewUI 版 1〜2 回 / WinForms 版 1 回）。 これが「タスク バーの明滅」の正体である。<br/>
/// 撮影の間だけタスク バーを隠すと、オーバーレイの静止画に写っているタスク バーがそのまま見えるため、<br/>
/// 見た目を変えずに明滅だけを消せる。<br/>
/// <br/>
/// <b>隠す順序</b>: <b>画面の撮影が済んだ後</b>、かつ<b>オーバーレイを見せる前</b>に呼ぶ。<br/>
/// 撮影より先に隠すと撮影画像からタスク バーが消え、オーバーレイを見せた後に隠すと、<br/>
/// 隠した瞬間に帯の見た目が変わる（= 明滅として見える）。<br/>
/// <br/>
/// <b>ネイティブ呼び出しを本クラスが単一所有する理由</b>: 4 つの UI 版が同じ手順を書き分けると、<br/>
/// 版ごとにタイミングがずれて同じ症状が再発する（実際に 3 つの版で再発した）。 呼び出し位置だけを<br/>
/// 各 UI 版が持ち、手順はここへ集約する。<br/>
/// <br/>
/// <b>使用例</b>: <c>using var taskbar = CaptureTaskbarScope.Hide();</c> の後、撮影を終えて<br/>
/// オーバーレイを閉じると、スコープの破棄（または <see cref="Dispose"/>）でタスク バーが戻る。
/// </remarks>
public sealed partial class CaptureTaskbarScope : IDisposable
{
    /// <summary>ウィンドウを隠す。</summary>
    private const int SwHide = 0;

    /// <summary>アクティブ化せずに表示する。</summary>
    private const int SwShowNoActivate = 4;

    /// <summary>プライマリ モニターのタスク バーのウィンドウ クラス名。</summary>
    private const string PrimaryTrayClass = "Shell_TrayWnd";

    /// <summary>セカンダリ モニターのタスク バーのウィンドウ クラス名。</summary>
    private const string SecondaryTrayClass = "Shell_SecondaryTrayWnd";

    /// <summary>実際に隠したタスク バーのハンドル（隠せなかったものは含めない）。</summary>
    private readonly List<nint> _hidden = [];

    /// <summary>復帰済みかどうか（多重呼び出しを防ぐ）。</summary>
    private bool _disposed;

    /// <summary>タスク バーを隠す。</summary>
    private CaptureTaskbarScope()
    {
        foreach (var handle in FindTaskbars())
        {
            // ShowWindow の戻り値は「呼び出し前に表示されていたか」。 隠したものだけを戻す
            if (ShowWindow(handle, SwHide))
            {
                _hidden.Add(handle);
            }
        }
    }

    /// <summary>すべてのタスク バーを隠す（タスク バーの無い環境では何もしない）。</summary>
    /// <returns>撮影の完了時に <see cref="Dispose"/> してタスク バーを戻すスコープ。</returns>
    /// <remarks>
    /// 撮影が例外で終わっても戻すため、必ず <c>using</c> で使うこと。<br/>
    /// <b>画面の撮影が済んだ後</b>、かつ<b>オーバーレイを見せる前</b>に呼ぶこと（理由は型のコメントを参照）。
    /// </remarks>
    public static CaptureTaskbarScope Hide() => new();

    /// <summary>隠したタスク バーを表示へ戻す（アクティブ化はしない）。</summary>
    /// <remarks>2 回目以降の呼び出しは何もしない（確定・キャンセルのどちらの経路でも呼べる）。</remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var handle in _hidden)
        {
            _ = ShowWindow(handle, SwShowNoActivate);
        }

        _hidden.Clear();
    }

    /// <summary>すべてのタスク バーのハンドルを列挙する。</summary>
    /// <returns>プライマリ モニターとセカンダリ モニターのタスク バーのハンドル。</returns>
    private static List<nint> FindTaskbars()
    {
        var handles = new List<nint>(capacity: 2);

        var primary = FindWindowW(PrimaryTrayClass, null);
        if (primary != nint.Zero)
        {
            handles.Add(primary);
        }

        // セカンダリ モニターのタスク バーは複数あるため、見つかる限りたどる
        var secondary = nint.Zero;
        while (true)
        {
            secondary = FindWindowExW(nint.Zero, secondary, SecondaryTrayClass, null);
            if (secondary == nint.Zero)
            {
                return handles;
            }

            handles.Add(secondary);
        }
    }

    /// <summary>ウィンドウ クラス名からトップレベル ウィンドウを探す。</summary>
    /// <param name="className">ウィンドウ クラス名。</param>
    /// <param name="windowName">ウィンドウ名（null はすべて）。</param>
    /// <returns>見つかったウィンドウのハンドル（無い場合は 0）。</returns>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindowW(string className, string? windowName);

    /// <summary>指定したウィンドウの次に一致するトップレベル ウィンドウを探す。</summary>
    /// <param name="parent">親ウィンドウ（トップレベルのみを探す場合は 0）。</param>
    /// <param name="childAfter">このウィンドウの次から探す（先頭から探す場合は 0）。</param>
    /// <param name="className">ウィンドウ クラス名。</param>
    /// <param name="windowName">ウィンドウ名（null はすべて）。</param>
    /// <returns>見つかったウィンドウのハンドル（無い場合は 0）。</returns>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindowExW(nint parent, nint childAfter, string className, string? windowName);

    /// <summary>ウィンドウの表示状態を変える。</summary>
    /// <param name="handle">対象のウィンドウ ハンドル。</param>
    /// <param name="command">表示方法（<c>SW_HIDE</c> / <c>SW_SHOWNOACTIVATE</c>）。</param>
    /// <returns>呼び出し前に表示されていた場合は true。</returns>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "ShowWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint handle, int command);
}
