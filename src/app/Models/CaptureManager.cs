using System.Runtime.InteropServices;

namespace app.Models;

/// <summary>
/// 画面キャプチャを管理するクラス
/// </summary>
public static partial class CaptureManager
{
    /// <summary>
    /// 全スクリーンの境界情報を取得する
    /// </summary>
    public static (int Index, Rectangle Bounds, string DeviceName)[] GetAllScreenBounds()
    {
        return [.. Screen.AllScreens.Select((s, i) => (i, s.Bounds, s.DeviceName))];
    }

    /// <summary>
    /// 指定された領域をキャプチャする
    /// </summary>
    public static Bitmap CaptureArea(Rectangle bounds)
    {
        var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var g = Graphics.FromImage(bitmap);
        g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
        return bitmap;
    }

    /// <summary>
    /// 指定されたウィンドウハンドルの領域をキャプチャする
    /// </summary>
    public static Bitmap CaptureWindow(IntPtr hWnd)
    {
        var rect = GetWindowRect(hWnd);
        return CaptureArea(rect);
    }

    /// <summary>
    /// マウスカーソル下のトップレベルウィンドウハンドルを取得する
    /// </summary>
    public static IntPtr GetWindowUnderCursor()
    {
        if (!GetCursorPosNative(out POINT point))
        {
            return IntPtr.Zero;
        }

        var hWnd = WindowFromPoint(point);
        if (hWnd == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        // GetAncestor(GA_ROOT) でトップレベルウィンドウを取得（子要素・コントロールを除外）
        return GetAncestor(hWnd, GA_ROOT);
    }

    /// <summary>
    /// ウィンドウの可視矩形領域を取得する。
    /// DwmGetWindowAttribute を使用して半透明の影部分を除外した領域を返す。
    /// 失敗した場合は通常の GetWindowRect の結果を返す。
    /// </summary>
    public static Rectangle GetWindowVisibleRect(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return Rectangle.Empty;
        }

        // DwmGetWindowAttribute で半透明影を除外した可視矩形を取得
        var visibleRect = new RECT();
        var hr = DwmGetWindowAttribute(hWnd,
            DWMWA_EXTENDED_FRAME_BOUNDS,
            ref visibleRect,
            Marshal.SizeOf<RECT>());

        if (hr >= 0 && visibleRect.Left < visibleRect.Right && visibleRect.Top < visibleRect.Bottom)
        {
            return Rectangle.FromLTRB(visibleRect.Left, visibleRect.Top, visibleRect.Right, visibleRect.Bottom);
        }

        // 失敗時は通常の GetWindowRect を使用
        return GetWindowRect(hWnd);
    }

    /// <summary>
    /// ウィンドウの矩形領域を取得する
    /// </summary>
    public static Rectangle GetWindowRect(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return Rectangle.Empty;
        }

        if (!GetWindowRectNative(hWnd, out var rect))
        {
            return Rectangle.Empty;
        }

        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowRect", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRectNative(IntPtr hWnd, out RECT lpRect);

    [LibraryImport("user32.dll", EntryPoint = "WindowFromPoint")]
    private static partial IntPtr WindowFromPoint(POINT point);

    [LibraryImport("user32.dll", EntryPoint = "GetCursorPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPosNative(out POINT lpPoint);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hWnd, int dwAttribute, ref RECT pvAttribute, int cbAttribute);

    private const uint GA_ROOT = 2;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}
