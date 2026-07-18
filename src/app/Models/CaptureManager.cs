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
    /// アクティブウィンドウをキャプチャする
    /// </summary>
    public static Bitmap? CaptureActiveWindow()
    {
        var hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
        {
            return null;
        }

        var rect = GetWindowRect(hWnd);
        if (rect == Rectangle.Empty)
        {
            return null;
        }

        return CaptureArea(rect);
    }

    /// <summary>
    /// マウスカーソル下のウィンドウハンドルを取得する
    /// </summary>
    public static IntPtr GetWindowUnderCursor()
    {
        if (!GetCursorPosNative(out POINT point))
        {
            return IntPtr.Zero;
        }

        return WindowFromPoint(point);
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
    private static partial IntPtr GetForegroundWindow();

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
