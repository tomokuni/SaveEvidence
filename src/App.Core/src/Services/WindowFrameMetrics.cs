using System.ComponentModel;
using System.Runtime.InteropServices;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>ウィンドウの外寸とクライアント領域の差（枠の太さ）を実測する。</summary>
/// <remarks>
/// <b>なぜ必要か</b>: 最小サイズ・既定サイズは<b>クライアント領域</b>の DIP で決めるが、UI フレームワークが
/// 受け付けるのは<b>外寸</b>である。 枠の太さは環境（テーマ・DPI・OS のバージョン）で変わるため定数にできず、
/// 実際のウィンドウから測る必要がある。<br/>
/// <b>UI 版ごとに書かない理由</b>: 同じ計測を各 UI 版へ複製すると、片方だけ直したときに最小サイズが食い違う
/// （依頼「ウィンドウの最小サイズが UI ごとに異なる」の原因になる）。 計測とその失敗の扱いを本クラスが単一所有する。<br/>
/// <b>失敗時</b>: 測れない場合は <see cref="Win32Exception"/> を投げる（枠を無視した寸法でウィンドウを作ると
/// 最小サイズが破られるため、呼び出し側が気付けるようにする）。<br/>
/// <b>呼ぶ時機</b>: ウィンドウのハンドルが確定した後（表示前でも測れる）。
/// </remarks>
internal static class WindowFrameMetrics
{
    /// <summary>ウィンドウの外寸とクライアント領域の差（枠の太さ）を取得する。</summary>
    /// <param name="handle">対象ウィンドウのネイティブ ハンドル。</param>
    /// <returns>枠の幅と高さ（画面物理ピクセル）。</returns>
    /// <exception cref="Win32Exception">外寸またはクライアント領域を取得できなかった場合。</exception>
    public static (int Width, int Height) Get(IntPtr handle)
    {
        if (!GetWindowRect(handle, out var windowRect))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "ウィンドウの外寸を取得できません。");
        }

        if (!GetClientRect(handle, out var clientRect))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "ウィンドウのクライアント領域を取得できません。");
        }

        return (
            (windowRect.Right - windowRect.Left) - (clientRect.Right - clientRect.Left),
            (windowRect.Bottom - windowRect.Top) - (clientRect.Bottom - clientRect.Top));
    }

    /// <summary>ウィンドウの外寸を画面物理ピクセルで取得する（Win32 の <c>GetWindowRect</c>）。</summary>
    /// <param name="hwnd">対象のウィンドウ ハンドル。</param>
    /// <param name="rect">取得した矩形。</param>
    /// <returns>成功した場合は true。</returns>
    /// <remarks>
    /// 安全でないコードの許可を必要としない <c>DllImport</c> を使い、読み込み先を System32 に固定する<br/>
    /// （探索順の脆弱性を作らない）。
    /// </remarks>
    [DllImport("user32.dll", EntryPoint = "GetWindowRect", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    /// <summary>ウィンドウのクライアント領域の大きさを取得する（Win32 の <c>GetClientRect</c>）。</summary>
    /// <param name="hwnd">対象のウィンドウ ハンドル。</param>
    /// <param name="rect">取得した矩形（左上は常に 0）。</param>
    /// <returns>成功した場合は true。</returns>
    [DllImport("user32.dll", EntryPoint = "GetClientRect", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hwnd, out NativeRect rect);

    /// <summary>Win32 の矩形（<c>RECT</c>）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        /// <summary>左端の座標。</summary>
        public int Left;

        /// <summary>上端の座標。</summary>
        public int Top;

        /// <summary>右端の座標。</summary>
        public int Right;

        /// <summary>下端の座標。</summary>
        public int Bottom;
    }
}
