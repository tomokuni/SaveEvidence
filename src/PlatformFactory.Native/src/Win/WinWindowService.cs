using Estable.Platform;
using Estable.Platform.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Estable.Platform.Win;

/// <summary>Win32 API を用いたネイティブウィンドウ操作を提供する。</summary>
/// <remarks>
/// オーバーレイウィンドウに必要なネイティブウィンドウ操作（Alt+Tab 非表示・UI ハング時の退避）を提供する。<br/>
/// P/Invoke 呼び出しは <c>WinNative</c> に委譲する。<br/>
/// <see cref="IWindowService"/> の実装であり、テスト時のモックに備える。<br/>
/// ウィンドウの特定は <see cref="EsPlatformHandle"/>（UI 非依存の識別子）で行い、<br/>
/// 本クラスは Avalonia オブジェクト（TopLevel 等）に一切触れない（スレッド非依存・ハング時退避に適合）。<br/>
/// 生成は <see cref="PlatformFactory.CreateWindowService"/> が唯一の経路。<br/>
/// 継承不可。<br/>
/// </remarks>
internal sealed class WinWindowService : IWindowService
{
    /// <summary>ロガー（null の場合は出力なし）。</summary>
    private readonly ILogger _log;

    /// <summary><see cref="WinWindowService"/> の新しいインスタンスを初期化します。</summary>
    /// <param name="logger">ロガー。null の場合は出力なし。</param>
    public WinWindowService(ILogger? logger = null)
    {
        _log = logger ?? NullLogger.Instance;
    }

    /// <summary>ウィンドウをタスクバー / Alt+Tab に表示しないよう設定する（選択オーバーレイ用、仕様 2.2）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <remarks>
    /// WS_EX_TOOLWINDOW を拡張スタイルに追加し、Alt+Tab の切り替え候補から除外する。<br/>
    /// 無効なハンドルの場合は何もしない。<br/>
    /// </remarks>
    public void HideFromTaskSwitcher(EsPlatformHandle handle)
    {
        var nativeHwnd = handle.Handle;
        if (nativeHwnd == IntPtr.Zero) return;

        _log.LogWindowHiddenFromTaskSwitcher(nativeHwnd.ToString("X"));
        var exStyle = WinNative.GetWindowLongPtr(nativeHwnd, WinNative.GWL_EXSTYLE).ToInt64();
        WinNative.SetWindowLongPtr(nativeHwnd, WinNative.GWL_EXSTYLE,
            new IntPtr(exStyle | WinNative.WS_EX_TOOLWINDOW));
    }

    /// <summary>ウィンドウをキャプチャから除外する/除外を解除する（仕様 6.2.1）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <param name="exclude">キャプチャ除外する場合は <see langword="true"/>。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// WDA_EXCLUDEFROMCAPTURE により、そのウィンドウは画面キャプチャ（CopyFromScreen / DXGI 等）に
    /// 一切写らなくなる（フレームタイミングに依存しない決定的な除外）。<br/>
    /// 除外設定時のみ DWM の再合成待ち（<c>DwmFlush</c>）を行い、設定直後のキャプチャを防ぐ。<br/>
    /// Windows 10 2004（build 19041）以降で利用可能。それ以前では失敗（false）を返す。<br/>
    /// </remarks>
    public bool SetCaptureExclusion(EsPlatformHandle handle, bool exclude)
    {
        var nativeHwnd = handle.Handle;
        if (nativeHwnd == IntPtr.Zero) return false;

        var affinity = exclude ? WinNative.WDA_EXCLUDEFROMCAPTURE : WinNative.WDA_NONE;
        var success = WinNative.SetWindowDisplayAffinity(nativeHwnd, affinity);
        _log.LogCaptureExclusionChanged(nativeHwnd.ToString("X"), exclude, success);
        if (success && exclude)
        {
            // 除外設定が DWM に反映されるまで 1 フレーム待つ（設定直後のキャプチャを防ぐ）
            WinNative.DwmFlush();
        }
        return success;
    }

    /// <summary>ウィンドウを最前面解除し、非表示にして閉じる（UI ハング時の退避、仕様 3.6）。</summary>
    /// <param name="handle">対象ウィンドウの識別子。</param>
    /// <remarks>
    /// UI スレッドがハングした場合にバックグラウンドスレッドから呼び出され、<br/>
    /// タスクマネージャー等でプロセスを終了できるようにする。<br/>
    /// 1. Topmost を解除（タスクマネージャー等が前面に出せるようにする）<br/>
    /// 2. ウィンドウを非表示にする（全画面オーバーレイが邪魔しないようにする）<br/>
    /// 3. WM_CLOSE を送信（UI スレッドが復帰した場合に自動キャンセル処理が走る）<br/>
    /// Win32 API はスレッド非依存のため、バックグラウンドスレッドから呼び出し可能。<br/>
    /// 本メソッドは <see cref="EsPlatformHandle"/>（数値）のみを使用し、UI オブジェクトに触れない。<br/>
    /// </remarks>
    public void EvacuateWindow(EsPlatformHandle handle)
    {
        var nativeHwnd = handle.Handle;
        if (nativeHwnd == IntPtr.Zero) return;

        _log.LogWindowEvacuated(nativeHwnd.ToString("X"));

        // 1. Topmost を解除
        WinNative.SetWindowPos(nativeHwnd, WinNative.HWND_NOTOPMOST, 0, 0, 0, 0,
            WinNative.SWP_NOMOVE | WinNative.SWP_NOSIZE | WinNative.SWP_NOACTIVATE);

        // 2. ウィンドウを非表示にする
        WinNative.ShowWindow(nativeHwnd, WinNative.SW_HIDE);

        // 3. WM_CLOSE を送信
        WinNative.PostMessage(nativeHwnd, WinNative.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }
}
