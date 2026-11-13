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
/// <br/>
/// <b>隠すと明滅が消える理由</b>: 覆う相手がいなくなるため、シェルが前面へ戻す動きも起きない。<br/>
/// オーバーレイの静止画に写っているタスク バーがそのまま見えるので、見た目も変わらない<br/>
/// （設定で撮影画像に写さないことも選べる）。<br/>
/// <br/>
/// <b>待つ必要がある理由</b>: <c>ShowWindow(SW_HIDE)</c> の反映は DWM の合成 1〜2 フレーム後になる。<br/>
/// 隠した直後にオーバーレイを表示すると、その 1〜2 フレームだけタスク バーが暗転マスクで覆われ、<br/>
/// 明滅として見える（実測 2026-11-13）。 そのため <see cref="HideSettleDelay"/> だけ待ってから表示する。<br/>
/// <br/>
/// <b>ネイティブ呼び出しを本クラスが単一所有する理由</b>: 4 つの UI 版が同じ手順を書き分けると、<br/>
/// 版ごとにタイミングがずれて同じ症状が再発する（実際に 3 つの版で再発した）。 呼び出し位置だけを<br/>
/// 各 UI 版が持ち、手順はここへ集約する。
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

    /// <summary>隠したタスク バーが画面から消えるまで待つ時間。</summary>
    /// <remarks>
    /// <c>ShowWindow(SW_HIDE)</c> の反映は DWM の合成 1〜2 フレーム後になる（60 Hz で 17〜34 ms）。<br/>
    /// 実測 2026-11-13: 隠した直後にオーバーレイを表示すると、1〜3 フレームだけタスク バーが<br/>
    /// 暗転マスクで覆われて明滅して見えた。 余裕をみて 3 フレーム分（50 ms）待つ。
    /// </remarks>
    public static readonly TimeSpan HideSettleDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>すべてのタスク バーを隠す（タスク バーの無い環境では何もしない）。</summary>
    /// <returns>撮影の完了時に <see cref="Dispose"/> してタスク バーを戻すスコープ。</returns>
    /// <remarks>
    /// 撮影が例外で終わっても戻すため、必ず <c>using</c> で使うこと。<br/>
    /// オーバーレイを表示する前に <see cref="HideSettleDelay"/> だけ待つこと（待たないと明滅が残る）。
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