using System.Runtime.InteropServices;

namespace app.Models;

/// <summary>
/// 画面キャプチャ機能を提供する静的ユーティリティクラス。
/// </summary>
/// <remarks>
/// Win32 API（user32.dll, dwmapi.dll）を使用して画面のキャプチャやウィンドウ情報の取得を行う。<br/>
/// ソースジェネレーター対応のため <c>partial</c> として宣言されている。<br/>
/// 全メソッドが静的であり、インスタンス化は不要。<br/>
/// </remarks>
public static partial class CaptureManager
{
    /// <summary>
    /// 全スクリーンの境界情報を取得する。
    /// </summary>
    /// <returns>各スクリーンのインデックス、矩形領域、デバイス名の配列。</returns>
    public static (int Index, Rectangle Bounds, string DeviceName)[] GetAllScreenBounds()
    {
        return [.. Screen.AllScreens.Select((s, i) => (i, s.Bounds, s.DeviceName))];
    }

    /// <summary>
    /// 指定された画面領域をキャプチャする。
    /// </summary>
    /// <param name="bounds">キャプチャする画面座標の矩形領域。</param>
    /// <returns>キャプチャされたビットマップ。</returns>
    public static Bitmap CaptureArea(Rectangle bounds)
    {
        var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var g = Graphics.FromImage(bitmap);
        g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
        return bitmap;
    }

    /// <summary>
    /// 指定されたウィンドウハンドルの領域をキャプチャする。
    /// </summary>
    /// <param name="hWnd">キャプチャ対象のウィンドウハンドル。</param>
    /// <param name="mode">キャプチャ方式。</param>
    /// <returns>キャプチャされたビットマップ。</returns>
    /// <remarks>
    /// <see cref="WindowCaptureMode.PrintWindow"/>: PrintWindow(PW_CLIENTONLY) により
    /// ウィンドウの描画内容を直接取得するため、1px の透明リサイズ境界や
    /// 背景の映り込みが発生しない。<br/>
    /// PrintWindow が失敗した場合は自動的に CopyFromScreen 方式にフォールバックする。<br/>
    /// <see cref="WindowCaptureMode.CopyFromScreen"/>: 従来の画面矩形コピー方式。<br/>
    /// </remarks>
    public static Bitmap CaptureWindow(IntPtr hWnd, WindowCaptureMode mode = WindowCaptureMode.PrintWindow)
    {
        var visibleRect = GetWindowVisibleRect(hWnd);
        if (visibleRect.IsEmpty) return new Bitmap(1, 1);

        if (mode == WindowCaptureMode.PrintWindow)
        {
            var fullRect = GetWindowRect(hWnd);
            if (fullRect.IsEmpty) return new Bitmap(1, 1);
            var fw = fullRect.Width;
            var fh = fullRect.Height;

            // PrintWindow 試行順:
            //   1. フラグ 0       → 通常のウィンドウ全体描画（タイトルバー含む）。多くのアプリで最適。
            //   2. PW_RENDERFULLCONTENT → WinUI/モダンアプリ向け DWM 完全描画。
            //   3. PW_CLIENTONLY → クライアント領域のみ（最終手段）。
            var fullBitmap = TryPrintWindow(hWnd, fw, fh, 0)
                          ?? TryPrintWindow(hWnd, fw, fh, PW_RENDERFULLCONTENT)
                          ?? TryPrintWindow(hWnd, fw, fh, PW_CLIENTONLY);

            if (fullBitmap is not null)
            {
                // DWMWA_EXTENDED_FRAME_BOUNDS（visibleRect）は左端を1px詰めて返すことがあるため、
                // そのオフセットでクロッピングすると内容が欠ける。PrintWindow の結果は
                // フルサイズ（GetWindowRect）のまま返し、後処理での切り出しは行わない。
                return fullBitmap;
            }
        }

        return CaptureArea(visibleRect);
    }

    /// <summary>
    /// PrintWindow を試行し、成功かつ有効な内容が取得できた場合のみビットマップを返す。
    /// </summary>
    private static Bitmap? TryPrintWindow(IntPtr hWnd, int width, int height, uint flags)
    {
        try
        {
            var bitmap = new Bitmap(width, height);
            using var g = Graphics.FromImage(bitmap);
            var hdc = g.GetHdc();
            var success = PrintWindow(hWnd, hdc, flags);
            g.ReleaseHdc(hdc);

            if (success && IsBitmapValid(bitmap)) return bitmap;
            bitmap.Dispose();
        }
        catch
        {
            // PrintWindow 失敗時は呼び出し元でフォールバック
        }
        return null;
    }

    /// <summary>
    /// PrintWindow で生成されたビットマップが有効な内容を含むか簡易判定する。
    /// </summary>
    /// <remarks>
    /// Windows 11 のモダンアプリ（WinUI/アクリル装飾等）では PrintWindow が成功を返しても
    /// 真っ黒な画像が生成される場合がある。<br/>
    /// 画像内の複数箇所をサンプリングし、全てが黒（RGB=0）の場合は無効と判断する。<br/>
    /// </remarks>
    private static bool IsBitmapValid(Bitmap bitmap)
    {
        // 画像の中央付近など5箇所をサンプリング
        var w = bitmap.Width;
        var h = bitmap.Height;
        var samples = new[]
        {
            new Point(w / 2, h / 2),             // 中央
            new Point(w / 4, h / 4),             // 左上寄り
            new Point(w * 3 / 4, h / 4),          // 右上寄り
            new Point(w / 4, h * 3 / 4),          // 左下寄り
            new Point(w * 3 / 4, h * 3 / 4),      // 右下寄り
        };

        foreach (var pt in samples)
        {
            if (pt.X >= 0 && pt.X < w && pt.Y >= 0 && pt.Y < h)
            {
                var pixel = bitmap.GetPixel(pt.X, pt.Y);
                if (pixel.R != 0 || pixel.G != 0 || pixel.B != 0)
                    return true; // 1つでも非黒ピクセルがあれば有効
            }
        }
        return false; // 全て黒 → 無効
    }

    /// <summary>
    /// マウスカーソル下のトップレベルウィンドウハンドルを取得する。
    /// </summary>
    /// <returns>トップレベルウィンドウのハンドル。取得失敗時は <see cref="IntPtr.Zero"/>。</returns>
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
    /// ウィンドウの矩形領域を取得する（GetWindowRect Win32 API ラッパー）。
    /// </summary>
    /// <param name="hWnd">ウィンドウハンドル</param>
    /// <returns>ウィンドウの矩形領域。失敗時は <see cref="Rectangle.Empty"/>。</returns>
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

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hWnd, int dwAttribute, ref RECT pvAttribute, int cbAttribute);

    private const uint GA_ROOT = 2;
    private const uint PW_CLIENTONLY = 0x00000001;
    private const uint PW_RENDERFULLCONTENT = 0x00000002;
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
