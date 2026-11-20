using System.Runtime.InteropServices;

namespace EsUtil.Feature.Annotation;

/// <summary>フローティング パネルのウィンドウから OS の飾りを外す。</summary>
/// <remarks>
/// <b>なぜコアに置くか</b>: ウィンドウの枠は OS が描くため、UI ごとに対処すると「版によって辺枠がある」
/// という差になる（実測 2026-11-20: AvaloniaUI 版と WinUI 3 版に、パネル自身の枠とは別の OS の枠が
/// 残っていた）。 4 つの UI 版が同じ手順を呼べるよう、ここが単一所有する。<br/>
/// <b>枠なしの指定だけでは消えない</b>: 枠なし（<c>WindowDecorations.None</c> /
/// <c>SetBorderAndTitleBar(false, false)</c>）でも DWM は 1 ピクセルの枠と角丸を描く。
/// DWM の属性で枠を「描かない色」にし、角丸を切る。<br/>
/// <b>失敗しても害はない</b>: 対応していない OS では属性が無視されるだけである。
/// </remarks>
public static class AnnotationPanelWindow
{
    /// <summary>枠の色を指定する DWM の属性。</summary>
    private const int DwmwaBorderColor = 34;

    /// <summary>角の丸め方を指定する DWM の属性。</summary>
    private const int DwmwaWindowCornerPreference = 33;

    /// <summary>枠を描かないことを示す色。</summary>
    private const uint DwmColorNone = 0xFFFFFFFE;

    /// <summary>角を丸めない。</summary>
    private const uint DwmWindowCornerDoNotRound = 1;

    /// <summary>拡張スタイルの位置。</summary>
    private const int GwlExStyle = -20;

    /// <summary>タスク バーへ出さない拡張スタイル。</summary>
    private const long WsExToolWindow = 0x00000080L;

    /// <summary>タスク バーへ出す拡張スタイル。</summary>
    private const long WsExAppWindow = 0x00040000L;

    /// <summary>最前面の位置。</summary>
    private static readonly nint s_hwndTopmost = new(-1);

    /// <summary>位置を変えない。</summary>
    private const uint SwpNoMove = 0x0002;

    /// <summary>大きさを変えない。</summary>
    private const uint SwpNoSize = 0x0001;

    /// <summary>アクティブにしない。</summary>
    private const uint SwpNoActivate = 0x0010;

    /// <summary>枠を描き直す。</summary>
    private const uint SwpFrameChanged = 0x0020;

    /// <summary>ウィンドウの枠と角丸を消し、タスク バーと Alt+Tab の一覧から外す。</summary>
    /// <param name="windowHandle">対象のウィンドウ ハンドル。 0 の場合は何もしない。</param>
    /// <remarks>
    /// <b>タスク バーへ出さない</b>: パネルはアプリの一部であり、独立したウィンドウとして
    /// タスク バーへ並ぶと利用者が別のアプリと誤認する。<c>WS_EX_TOOLWINDOW</c> を付けると
    /// タスク バーと Alt+Tab の一覧の両方から外れる（<c>IsShownInSwitchers</c> は WinUI 3 で
    /// ウィンドウがアクティブにならなくなるため使わない）。
    /// </remarks>
    public static void RemoveSystemFrame(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            return;
        }

        var border = DwmColorNone;
        _ = DwmSetWindowAttribute(windowHandle, DwmwaBorderColor, ref border, sizeof(uint));

        var corner = DwmWindowCornerDoNotRound;
        _ = DwmSetWindowAttribute(windowHandle, DwmwaWindowCornerPreference, ref corner, sizeof(uint));

        HideFromTaskbar(windowHandle);
    }

    /// <summary>ウィンドウを最前面へ出す（アプリのウィンドウより前に置く）。</summary>
    /// <param name="windowHandle">対象のウィンドウ ハンドル。 0 の場合は何もしない。</param>
    /// <remarks>
    /// <b>アプリより前に出す</b>: 枠なしウィンドウは生成直後にアプリのウィンドウの後ろへ回ることがある
    /// （実測 2026-11-20）。 フォーカスは奪わない（<c>SWP_NOACTIVATE</c>）。
    /// </remarks>
    public static void BringToFront(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            return;
        }

        _ = SetWindowPos(
            windowHandle, s_hwndTopmost, 0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpFrameChanged);
    }

    /// <summary>ウィンドウをタスク バーと Alt+Tab の一覧から外す。</summary>
    /// <param name="windowHandle">対象のウィンドウ ハンドル。</param>
    private static void HideFromTaskbar(nint windowHandle)
    {
        var style = GetWindowLongPtrW(windowHandle, GwlExStyle);
        if (style == nint.Zero)
        {
            return;
        }

        var updated = (style.ToInt64() | WsExToolWindow) & ~WsExAppWindow;
        _ = SetWindowLongPtrW(windowHandle, GwlExStyle, new nint(updated));
    }

    /// <summary>DWM のウィンドウ属性を設定する（OS）。</summary>
    /// <param name="windowHandle">対象のウィンドウ ハンドル。</param>
    /// <param name="attribute">属性。</param>
    /// <param name="value">値（4 バイト）。</param>
    /// <param name="size">値のバイト数。</param>
    /// <returns>成功した場合は 0。</returns>
    [DllImport("dwmapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle, int attribute, ref uint value, int size);

    /// <summary>ウィンドウの拡張スタイルを取得する（OS）。</summary>
    /// <param name="windowHandle">対象のウィンドウ ハンドル。</param>
    /// <param name="index">取得するスタイル（<c>GWL_EXSTYLE</c>）。</param>
    /// <returns>拡張スタイル。 取得できなかった場合は 0。</returns>
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetWindowLongPtrW(nint windowHandle, int index);

    /// <summary>ウィンドウの拡張スタイルを設定する（OS）。</summary>
    /// <param name="windowHandle">対象のウィンドウ ハンドル。</param>
    /// <param name="index">設定するスタイル（<c>GWL_EXSTYLE</c>）。</param>
    /// <param name="value">設定する値。</param>
    /// <returns>変更前の値。 失敗した場合は 0。</returns>
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint SetWindowLongPtrW(nint windowHandle, int index, nint value);

    /// <summary>ウィンドウの位置と重なり順を変える（OS）。</summary>
    /// <param name="windowHandle">対象のウィンドウ ハンドル。</param>
    /// <param name="insertAfter">重なり順の基準。</param>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <param name="flags">動作の指定。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    [DllImport("user32.dll", EntryPoint = "SetWindowPos", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint windowHandle, nint insertAfter, int x, int y, int width, int height, uint flags);
}
