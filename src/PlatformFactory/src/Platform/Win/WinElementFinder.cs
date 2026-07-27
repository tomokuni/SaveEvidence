using Avalonia;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace Estable.AvaloniaUI.Platform.Win;

/// <summary>Win32 API を利用した要素（ウィンドウ・スクリーン）検出ユーティリティを内包する。</summary>
/// <remarks>
/// P/Invoke 呼び出しは <c>WinNative</c> に委譲する。<br/>
/// <see cref="IElementFinder"/> の実装であり、テスト時のモックに備える。<br/>
/// 自己完結型: 自ウィンドウの除外（自プロセスのウィンドウ）とスクリーン境界
/// （Win32 の EnumDisplayMonitors）は外部からの設定なしに解決する。<br/>
/// 生成は <see cref="PlatformFactory.CreateElementFinder"/> が唯一の経路。<br/>
/// 継承不可。<br/>
/// </remarks>
internal sealed class WinElementFinder : IElementFinder
{
    /// <summary>カーソル位置取得用の共有プラットフォーム実装（Win32）。</summary>
    /// <remarks>カーソル位置の取得（<see cref="GetCursorScreenPoint"/>）に使用する。</remarks>
    private readonly ICursorService _cursorService = new WinCursorService();

    private IReadOnlyList<PixelRect>? _screenBounds;

    /// <summary>全ディスプレイモニターの矩形（画面物理座標）を列挙する（自己完結）。</summary>
    /// <returns>各モニターの矩形リスト。</returns>
    /// <remarks>
    /// Win32 の <c>EnumDisplayMonitors</c> で全モニターを列挙する（Avalonia の TopLevel 非依存）。<br/>
    /// 列挙に失敗した場合は空リストを返す（フォールバック）。
    /// </remarks>
    private IReadOnlyList<PixelRect> EnumerateMonitorBounds()
        => _screenBounds ??= EnumerateMonitorBoundsCore();

    private static IReadOnlyList<PixelRect> EnumerateMonitorBoundsCore()
    {
        var bounds = new List<PixelRect>();
        WinNative.EnumDisplayMonitors(
            IntPtr.Zero, IntPtr.Zero, (_, _, lprcMonitor, _) =>
            {
                if (lprcMonitor != IntPtr.Zero)
                {
                    var rect = Marshal.PtrToStructure<WinNative.RECT>(lprcMonitor);
                    bounds.Add(new PixelRect(rect.Left, rect.Top, rect.Width, rect.Height));
                }
                return true;
            }, IntPtr.Zero);
        return bounds;
    }

    // ====================================================================
    // カーソル位置取得（パラメーターなし API の入力）
    // ====================================================================

    /// <summary>現在のカーソル位置を画面物理座標で取得する。</summary>
    /// <returns>現在のカーソル位置（画面物理座標）。</returns>
    /// <remarks>共有プラットフォームの <see cref="WinCursorService.GetCursorScreenPoint"/> に委譲する（Win32 の <c>GetCursorPos</c> で取得）。</remarks>
    public PixelPoint GetCursorScreenPoint()
        => _cursorService.GetCursorScreenPoint();

    /// <summary>カーソル位置の最前面ウィンドウを検出する（カーソル位置は内部で取得）。</summary>
    /// <returns>ウィンドウの検出結果。未検出時は <see cref="ElementFinderResults.IsDetected"/> = false。</returns>
    /// <remarks><see cref="GetCursorScreenPoint"/> でカーソル位置を取得し、<see cref="FindWindowUnderCursor(Avalonia.PixelPoint)"/> に委譲する。</remarks>
    public ElementFinderResults FindWindowUnderCursor()
        => FindWindowUnderCursor(GetCursorScreenPoint());

    /// <summary>カーソル位置が含まれるスクリーンを検出する（カーソル位置は内部で取得）。</summary>
    /// <returns>スクリーンの検出結果。未検出時は <see cref="ElementFinderResults.IsDetected"/> = false。</returns>
    /// <remarks><see cref="GetCursorScreenPoint"/> でカーソル位置を取得し、<see cref="FindScreenUnderCursor(Avalonia.PixelPoint)"/> に委譲する。</remarks>
    public ElementFinderResults FindScreenUnderCursor()
        => FindScreenUnderCursor(GetCursorScreenPoint());

    /// <summary>カーソル位置の最前面ウィンドウを検出する。</summary>
    /// <param name="screenPoint">判定する位置（画面物理座標）。</param>
    /// <returns>ウィンドウの検出結果（矩形・タイトル・識別子）。未検出時は <see cref="ElementFinderResults.IsDetected"/> = false。</returns>
    /// <remarks>
    /// <see cref="ListWindows"/>（全ウィンドウ一覧）を列挙し、<paramref name="screenPoint"/> を含む最初の<br/>
    /// ウィンドウを採用する（EnumWindows の列挙順 = 元の FindWindowUnderCursor と同じ最前面順）。<br/>
    /// 除外条件（自プロセス・不可視・無効・最小化・0x0・タスクバー/デスクトップ）は ListWindows が適用済み。
    /// </remarks>
    public ElementFinderResults FindWindowUnderCursor(PixelPoint screenPoint)
        => ListWindows().FirstOrDefault(r => r.Bounds.Contains(screenPoint));

    /// <summary>指定位置が含まれるスクリーンを検出する。</summary>
    /// <param name="screenPoint">判定する位置（画面物理座標）。</param>
    /// <returns>スクリーンの検出結果（矩形・「Screen {インデックス}」）。未検出時は <see cref="ElementFinderResults.IsDetected"/> = false。</returns>
    /// <remarks>
    /// <see cref="ListScreens"/>（全スクリーン一覧）を列挙し、<paramref name="screenPoint"/> を含む最初の<br/>
    /// スクリーンを採用する。スクリーン境界は Win32 の <c>EnumDisplayMonitors</c> から自己解決する。
    /// </remarks>
    public ElementFinderResults FindScreenUnderCursor(PixelPoint screenPoint)
        => ListScreens().FirstOrDefault(r => r.Bounds.Contains(screenPoint));

    /// <summary>全スクリーンの一覧を返す（仮想スクリーンの1枚画像に対するスナップショット管理用）。</summary>
    /// <returns>全スクリーンの検出結果一覧（矩形・「Screen {インデックス}」）。列挙できない場合は空リスト。</returns>
    /// <remarks>
    /// 全スクリーンを1枚の画像（仮想スクリーン）として取得した際に、その画像内のスクリーン境界を<br/>
    /// 座標系（画像ローカル）で特定するための一覧を返す。<br/>
    /// モニター境界は Win32 の <c>EnumDisplayMonitors</c> から自己解決する（外部設定不要）。
    /// </remarks>
    public IReadOnlyList<ElementFinderResults> ListScreens()
    {
        var bounds = EnumerateMonitorBounds();
        var results = new List<ElementFinderResults>(bounds.Count);
        for (int i = 0; i < bounds.Count; i++)
        {
            results.Add(new ElementFinderResults(bounds[i], true, Title: $"Screen {i}"));
        }
        return results;
    }

    /// <summary>全ウィンドウの一覧を返す（仮想スクリーンの1枚画像に対するスナップショット管理用）。</summary>
    /// <returns>全ウィンドウの検出結果一覧（矩形・タイトル・識別子）。列挙できない場合は空リスト。</returns>
    /// <remarks>
    /// 全スクリーンを1枚の画像（仮想スクリーン）として取得した際に、その画像内のウィンドウ境界を<br/>
    /// 座標系（画像ローカル）で特定するための一覧を返す。<br/>
    /// <see cref="FindWindowUnderCursor(PixelPoint)"/> と同じ除外条件（自プロセス・不可視・無効・最小化・<br/>
    /// 0x0サイズ・タスクバー/デスクトップ）を適用する。
    /// </remarks>
    public IReadOnlyList<ElementFinderResults> ListWindows()
    {
        var results = new List<ElementFinderResults>();
        var selfPid = WinNative.GetCurrentProcessId();

        // EnumWindows: 全トップレベルウィンドウを列挙し、除外条件を適用して一覧化する
        // コールバック内では安全なAPIのみを呼び出す（IsWindowVisible, IsWindowEnabled, IsIconic, GetWindowRect, GetClassName）
        WinNative.EnumWindows((hWnd, _) =>
        {
            // 自プロセスのウィンドウ（オーバーレイ等）を除外（外部設定不要で自己完結）
            WinNative.GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == selfPid) return true;
            if (!WinNative.IsWindowVisible(hWnd)) return true;
            if (!WinNative.IsWindowEnabled(hWnd)) return true;
            if (WinNative.IsIconic(hWnd)) return true;
            if (IsExcludedClass(hWnd)) return true;

            if (!WinNative.GetWindowRect(hWnd, out var rect)) return true;
            if (rect.Width <= 0 || rect.Height <= 0) return true;

            var handle = new PlatformHandle(hWnd, "HWND");
            if (!TryGetWindowVisibleRect(handle, out var visibleRect)) return true;
            var title = GetWindowTitle(handle);
            results.Add(new ElementFinderResults(visibleRect, true, Title: title, Handle: handle));
            return true;
        }, IntPtr.Zero);

        return results;
    }

    /// <summary>タスクバー / デスクトップウィンドウ（仕様 2.4.2 の除外対象）かどうかを判定する。</summary>
    /// <remarks>
    /// クラス名で判定する: Shell_TrayWnd（タスクバー）、Progman / WorkerW（デスクトップ）。<br/>
    /// デスクトップ全体のキャプチャは ScreenSelect を使用するため、これらは選択対象外とする。<br/>
    /// </remarks>
    private static bool IsExcludedClass(IntPtr hWnd)
    {
        var className = new char[256];
        var len = WinNative.GetClassName(hWnd, className, className.Length);
        if (len <= 0) return false;
        return className.AsSpan(0, len).SequenceEqual("Shell_TrayWnd")
            || className.AsSpan(0, len).SequenceEqual("Progman")
            || className.AsSpan(0, len).SequenceEqual("WorkerW");
    }

    /// <summary>ウィンドウの可視矩形領域を取得する（DWM 影除外）。</summary>
    /// <remarks>
    /// DWM の DWMWA_EXTENDED_FRAME_BOUNDS で影を除外した矩形を取得し、失敗時は GetWindowRect にフォールバックする。<br/>
    /// インターフェイスからは公開せず、<see cref="FindWindowUnderCursor"/> の内部実装として使用する。
    /// </remarks>
    internal bool TryGetWindowVisibleRect(IPlatformHandle handle, out PixelRect rect)
    {
        var hWnd = handle.Handle;
        if (hWnd == IntPtr.Zero)
        {
            rect = default;
            return false;
        }

        // DWM 影除外: DWMWA_EXTENDED_FRAME_BOUNDS で可視領域を取得する
        WinNative.RECT visibleRect = default;
        var hr = WinNative.DwmGetWindowAttribute(hWnd, WinNative.DWMWA_EXTENDED_FRAME_BOUNDS,
            ref visibleRect, Marshal.SizeOf<WinNative.RECT>());
        if (hr >= 0 && visibleRect.Left < visibleRect.Right && visibleRect.Top < visibleRect.Bottom)
        {
            rect = new PixelRect(visibleRect.Left, visibleRect.Top, visibleRect.Width, visibleRect.Height);
            return true;
        }

        // フォールバック: GetWindowRect
        if (!WinNative.GetWindowRect(hWnd, out var wndRect))
        {
            rect = default;
            return false;
        }
        rect = new PixelRect(wndRect.Left, wndRect.Top, wndRect.Width, wndRect.Height);
        return true;
    }

    /// <summary>ウィンドウのタイトルを取得する。</summary>
    /// <param name="handle">タイトルを取得するウィンドウのハンドル。</param>
    /// <returns>ウィンドウタイトル。取得できない場合は空文字。</returns>
    /// <remarks>インターフェイスからは公開せず、<see cref="FindWindowUnderCursor"/> の内部実装として使用する。</remarks>
    internal string GetWindowTitle(IPlatformHandle handle)
    {
        var hWnd = handle.Handle;
        var len = WinNative.GetWindowTextLengthW(hWnd);
        if (len <= 0) return "";
        var titleChars = new char[len + 1];
        WinNative.GetWindowText(hWnd, titleChars, titleChars.Length);
        return new string(titleChars, 0, len);
    }
}
