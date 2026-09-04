using System.Runtime.InteropServices;
using Avalonia.Input;

namespace Estable.AvaloniaUI.Platform.Win;

/// <summary>プラットフォーム機能に必要な Win32 API の P/Invoke 宣言・構造体・デリゲート・定数を集約する。</summary>
/// <remarks>
/// カーソル操作（<c>GetCursorPos</c> / <c>SetCursorPos</c> / <c>ShowCursor</c>）、<br/>
/// カーソル上書き抑止（WndProc サブクラス化: <c>SetWindowLongPtrW</c> / <c>CallWindowProcW</c>）、<br/>
/// 要素（ウィンドウ・スクリーン）検出（<c>EnumWindows</c> / <c>EnumDisplayMonitors</c> 等）、<br/>
/// 画面/ウィンドウキャプチャ（<c>PrintWindow</c> / <c>GetSystemMetrics</c> 等）、<br/>
/// ネイティブウィンドウ操作（<c>SetWindowPos</c> / <c>ShowWindow</c> / <c>PostMessage</c> 等）、<br/>
/// キャプチャ除外（<c>SetWindowDisplayAffinity</c>）に必要な Win32 API を集約する（Windows 依存の最小実装・DRY）。
/// </remarks>
internal static partial class WinNative
{
    /// <summary><see cref="RECT"/> 構造体のマーシャリングサイズ（バイト）。</summary>
    internal static readonly int RectSize = Marshal.SizeOf<RECT>();

    // ============================================================
    // 構造体
    // ============================================================

    /// <summary>Win32 RECT 構造体（矩形）。</summary>
    /// <param name="left">左端。</param>
    /// <param name="top">上端。</param>
    /// <param name="right">右端。</param>
    /// <param name="bottom">下端。</param>
    /// <remarks>P/Invoke のマーシャリング用にレイアウトを定義する。</remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct RECT(int left, int top, int right, int bottom)
    {
        /// <summary>左端。</summary>
        public int Left { get; } = left;

        /// <summary>上端。</summary>
        public int Top { get; } = top;

        /// <summary>右端。</summary>
        public int Right { get; } = right;

        /// <summary>下端。</summary>
        public int Bottom { get; } = bottom;

        /// <summary>幅（Right - Left）。</summary>
        public int Width => Right - Left;

        /// <summary>高さ（Bottom - Top）。</summary>
        public int Height => Bottom - Top;
    }

    /// <summary>Win32 POINT 構造体（スクリーン座標）。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <remarks>P/Invoke のマーシャリング用にレイアウトを定義する。</remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct POINT(int x, int y)
    {
        /// <summary>X 座標。</summary>
        public int X { get; } = x;

        /// <summary>Y 座標。</summary>
        public int Y { get; } = y;
    }

    /// <summary>Win32 CURSORINFO 構造体（カーソルの表示状態・位置）。</summary>
    /// <remarks>
    /// <see cref="GetCursorInfo"/> の出力先。表示状態（<see cref="flags"/> の <see cref="CURSOR_SHOWING"/>）を<br/>
    /// 取得して、Win32 の表示カウンタと <see cref="WinCursorService"/> の状態追跡を同期するために使用する。<br/>
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct CURSORINFO
    {
        /// <summary>構造体のサイズ（cbSize）。呼び出し前に設定する。</summary>
        public int cbSize;

        /// <summary>カーソルの表示状態（<see cref="CURSOR_SHOWING"/> とのビット演算で判定）。</summary>
        public int flags;

        /// <summary>カーソルのハンドル。</summary>
        public IntPtr hCursor;

        /// <summary>カーソルの画面物理座標。</summary>
        public POINT ptScreenPos;
    }

    // ============================================================
    // デリゲート
    // ============================================================

    /// <summary>WndProc サブクラス化用のウィンドウプロシージャ。</summary>
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>EnumWindows のコールバック。true を返すと列挙を継続する。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="lParam">アプリケーション定義の値。</param>
    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>EnumDisplayMonitors のコールバック。true を返すと列挙を継続する。</summary>
    /// <param name="hMonitor">モニターハンドル。</param>
    /// <param name="hdcMonitor">モニターのデバイスコンテキスト。</param>
    /// <param name="lprcMonitor">モニターの矩形（画面物理座標）へのポインター。</param>
    /// <param name="dwData">アプリケーション定義の値。</param>
    internal delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    // ============================================================
    // 定数
    // ============================================================

    // --- カーソル ---
    /// <summary>WM_SETCURSOR: OS がカーソル形状を上書きしようとしたときに送られるメッセージ。</summary>
    internal const uint WM_SETCURSOR = 0x0020;

    /// <summary>GWLP_WNDPROC: ウィンドウプロシージャを設定する <c>SetWindowLongPtr</c> のインデックス。</summary>
    internal const int GWLP_WNDPROC = -4;

    /// <summary>HTCLIENT: クライアント領域内のヒットテスト結果。</summary>
    internal const int HTCLIENT = 1;

    /// <summary>CURSORINFO.flags: カーソルが表示されていることを示すフラグ。</summary>
    internal const int CURSOR_SHOWING = 0x00000001;

    // --- DWM 影除外 ---
    /// <summary>DWM 拡張フレーム境界（影を除外した可視矩形）の属性 ID。</summary>
    internal const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    // --- PrintWindow フラグ ---
    /// <summary>PrintWindow: クライアント領域のみを描画するフラグ。</summary>
    internal const uint PW_CLIENTONLY = 0x00000001;

    /// <summary>PrintWindow: フルコンテンツを描画するフラグ。</summary>
    internal const uint PW_RENDERFULLCONTENT = 0x00000002;

    // --- 仮想スクリーン（全モニタのバウンディングボックス） ---
    /// <summary>仮想スクリーン左端のメトリクス。</summary>
    internal const int SM_XVIRTUALSCREEN = 76;

    /// <summary>仮想スクリーン上端のメトリクス。</summary>
    internal const int SM_YVIRTUALSCREEN = 77;

    /// <summary>仮想スクリーン幅のメトリクス。</summary>
    internal const int SM_CXVIRTUALSCREEN = 78;

    /// <summary>仮想スクリーン高さのメトリクス。</summary>
    internal const int SM_CYVIRTUALSCREEN = 79;

    // --- ウィンドウスタイル（Alt+Tab 非表示用） ---
    /// <summary>拡張ウィンドウスタイルを取得/設定するオフセット。</summary>
    internal const int GWL_EXSTYLE = -20;

    /// <summary>タスクスイッチャー非表示の拡張スタイル。</summary>
    internal const int WS_EX_TOOLWINDOW = 0x00000080;

    // --- ウォッチドッグ（UI ハング時の救出用） ---
    /// <summary>ウィンドウクローズメッセージ。</summary>
    internal const uint WM_CLOSE = 0x0010;

    /// <summary>ウィンドウ非表示コマンド。</summary>
    internal const int SW_HIDE = 0;

    /// <summary>最前面解除用の特別なハンドル値。</summary>
    internal static readonly IntPtr HWND_NOTOPMOST = new(-2);

    /// <summary>サイズ変更なしの SetWindowPos フラグ。</summary>
    internal const uint SWP_NOSIZE = 0x0001;

    /// <summary>移動なしの SetWindowPos フラグ。</summary>
    internal const uint SWP_NOMOVE = 0x0002;

    /// <summary>アクティブ化なしの SetWindowPos フラグ。</summary>
    internal const uint SWP_NOACTIVATE = 0x0010;

    // --- キャプチャ除外（SetWindowDisplayAffinity） ---
    /// <summary>キャプチャ除外なし（通常状態）。</summary>
    internal const int WDA_NONE = 0x00000000;

    /// <summary>ウィンドウを全キャプチャAPI（GDI / DXGI）から除外する（Windows 10 2004+）。</summary>
    internal const int WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    // ============================================================
    // user32.dll
    // ============================================================

    /// <summary>現在のカーソル位置を取得する（user32.GetCursorPos）。</summary>
    /// <param name="lpPoint">カーソル位置の出力先。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    [LibraryImport("user32.dll", EntryPoint = "GetCursorPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out POINT lpPoint);

    /// <summary>カーソルを指定位置に移動する（user32.SetCursorPos）。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    [LibraryImport("user32.dll", EntryPoint = "SetCursorPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetCursorPos(int x, int y);

    /// <summary>マウスポインターの表示/非表示を切り替える（user32.ShowCursor）。</summary>
    /// <param name="bShow">表示する場合は <see langword="true"/>。</param>
    /// <returns>表示カウンタ。</returns>
    [LibraryImport("user32.dll", EntryPoint = "ShowCursor")]
    internal static partial int ShowCursor([MarshalAs(UnmanagedType.Bool)] bool bShow);

    /// <summary>カーソルの表示状態・位置を取得する（user32.GetCursorInfo）。</summary>
    /// <param name="pci">カーソル情報の出力先。cbSize を事前に設定する。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// 表示状態（<see cref="CURSORINFO.flags"/> の <see cref="CURSOR_SHOWING"/>）を取得して、<br/>
    /// Win32 の表示カウンタと <see cref="WinCursorService"/> の状態追跡を同期するために使用する。<br/>
    /// </remarks>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorInfo(ref CURSORINFO pci);

    /// <summary>指定したウィンドウの情報を取得/設定する（user32.SetWindowLongPtrW）。WndProc サブクラス化とスタイル設定に使用する。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="nIndex">設定する情報のインデックス（<see cref="GWLP_WNDPROC"/> / <see cref="GWL_EXSTYLE"/> 等）。</param>
    /// <param name="dwNewLong">設定する値。</param>
    /// <returns>設定前の値。</returns>
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary>指定したウィンドウの情報を取得する（user32.GetWindowLongPtrW）。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="nIndex">取得する情報のインデックス（<see cref="GWL_EXSTYLE"/> 等）。</param>
    /// <returns>取得した値。</returns>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static partial IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    /// <summary>元の WndProc にメッセージを委譲する（user32.CallWindowProcW）。</summary>
    /// <param name="lpPrevWndFunc">元の WndProc アドレス。</param>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="msg">ウィンドウメッセージ。</param>
    /// <param name="wParam">WPARAM。</param>
    /// <param name="lParam">LPARAM。</param>
    /// <returns>ウィンドウプロシージャの処理結果。</returns>
    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW", SetLastError = true)]
    internal static partial IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>全トップレベルウィンドウを列挙する（user32.EnumWindows）。</summary>
    /// <param name="lpEnumFunc">列挙コールバック。</param>
    /// <param name="lParam">アプリケーション定義の値。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    /// <summary>ウィンドウの外接矩形を取得する（user32.GetWindowRect）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="lpRect">矩形の出力先。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>ウィンドウが最大化されているかを判定する（user32.IsZoomed）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsZoomed(IntPtr hWnd);

    /// <summary>ウィンドウの可視状態を判定する（user32.IsWindowVisible）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(IntPtr hWnd);

    /// <summary>ウィンドウが入力を受け付け可能かを判定する（user32.IsWindowEnabled）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowEnabled(IntPtr hWnd);

    /// <summary>ウィンドウが最小化されているかを判定する（user32.IsIconic）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(IntPtr hWnd);

    /// <summary>ウィンドウのクラス名を取得する（user32.GetClassNameW）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="lpClassName">クラス名の出力先バッファ。</param>
    /// <param name="nMaxCount">バッファサイズ。</param>
    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int GetClassName(IntPtr hWnd, char[] lpClassName, int nMaxCount);

    /// <summary>ウィンドウタイトルの文字列長を取得する（user32.GetWindowTextLengthW）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    internal static partial int GetWindowTextLengthW(IntPtr hWnd);

    /// <summary>ウィンドウタイトルを取得する（user32.GetWindowTextW）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="lpString">タイトルの出力先バッファ。</param>
    /// <param name="nMaxCount">バッファサイズ。</param>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount);

    /// <summary>ウィンドウを所有するプロセス ID を取得する（user32.GetWindowThreadProcessId）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="processId">プロセス ID の出力先。</param>
    /// <returns>ウィンドウを作成したスレッド ID。</returns>
    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>全ディスプレイモニターを列挙する（user32.EnumDisplayMonitors）。</summary>
    /// <param name="hdc">デバイスコンテキスト（NULL = 全モニター）。</param>
    /// <param name="lprcClip">クリップ矩形（NULL = クリップなし）。</param>
    /// <param name="lpfnEnum">列挙コールバック。</param>
    /// <param name="dwData">アプリケーション定義の値。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    /// <summary>ウィンドウをデバイスコンテキストへ直接描画する（user32.PrintWindow）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="hdcBlt">描画先デバイスコンテキスト。</param>
    /// <param name="nFlags">描画フラグ（<see cref="PW_CLIENTONLY"/> / <see cref="PW_RENDERFULLCONTENT"/>）。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    /// <summary>指定されたシステムメトリクス（SM_ 定数）を取得する（user32.GetSystemMetrics）。</summary>
    /// <param name="nIndex">取得するシステムメトリクスのインデックス。</param>
    /// <returns>システムメトリクスの値。</returns>
    [LibraryImport("user32.dll")]
    internal static partial int GetSystemMetrics(int nIndex);

    /// <summary>ウィンドウの位置・サイズ・Z オーダーを変更する（user32.SetWindowPos）。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="hWndInsertAfter">Z オーダー位置（<see cref="HWND_NOTOPMOST"/> 等）。</param>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <param name="cx">幅。</param>
    /// <param name="cy">高さ。</param>
    /// <param name="uFlags">フラグ（<see cref="SWP_NOMOVE"/> 等）。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    /// <summary>ウィンドウの表示状態を変更する（user32.ShowWindow）。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="nCmdShow">表示コマンド（<see cref="SW_HIDE"/> 等）。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    /// <summary>ウィンドウへメッセージを送信する（user32.PostMessage）。</summary>
    /// <param name="hWnd">対象ウィンドウのハンドル。</param>
    /// <param name="msg">メッセージ（<see cref="WM_CLOSE"/> 等）。</param>
    /// <param name="wParam">WPARAM。</param>
    /// <param name="lParam">LPARAM。</param>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>ウィンドウのキャプチャ除外設定を変更する（user32.SetWindowDisplayAffinity）。</summary>
    /// <param name="hWnd">対象ウィンドウハンドル。</param>
    /// <param name="dwAffinity">除外設定（<see cref="WDA_NONE"/> / <see cref="WDA_EXCLUDEFROMCAPTURE"/>）。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <see cref="WDA_EXCLUDEFROMCAPTURE"/> を設定すると、そのウィンドウは画面キャプチャ（CopyFromScreen / DXGI 等）に
    /// 一切写らなくなる（フレームタイミングに依存しない決定的な除外）。<br/>
    /// キャプチャ前に設定し、キャプチャ後に <see cref="WDA_NONE"/> へ戻す。<br/>
    /// Windows 10 2004（build 19041）以降で利用可能。それ以前では失敗（false）を返す。<br/>
    /// </remarks>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowDisplayAffinity(IntPtr hWnd, int dwAffinity);

    // ============================================================
    // kernel32.dll
    // ============================================================

    /// <summary>現在のプロセス ID を取得する（kernel32.GetCurrentProcessId）。</summary>
    [LibraryImport("kernel32.dll")]
    internal static partial uint GetCurrentProcessId();

    // ============================================================
    // dwmapi.dll
    // ============================================================

    /// <summary>DWM のウィンドウ属性を取得する（dwmapi.DwmGetWindowAttribute）。</summary>
    /// <param name="hWnd">ウィンドウハンドル。</param>
    /// <param name="dwAttribute">取得する属性。</param>
    /// <param name="pvAttribute">属性値の出力先。</param>
    /// <param name="cbAttribute">属性値のサイズ。</param>
    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmGetWindowAttribute(IntPtr hWnd, int dwAttribute, ref RECT pvAttribute, int cbAttribute);

    /// <summary>DWM（デスクトップ合成マネージャー）が現在のフレームの合成を完了するまでブロックする（dwmapi.DwmFlush）。</summary>
    /// <returns>成功した場合は 0（S_OK）。</returns>
    /// <remarks>
    /// ウィンドウの表示・非表示・サイズ変更後に呼ぶと、その変更が画面へ反映されたことを保証できる。<br/>
    /// キャプチャ前に使用して、非表示状態の残像が写り込むのを防ぐ（仕様 6.2.1 HideBeforeCaptureDelay の補強）。<br/>
    /// </remarks>
    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmFlush();

    // ============================================================
    // ヘルパー
    // ============================================================

    /// <summary>仮想スクリーン（全モニタを包含するバウンディングボックス）の境界を取得する。</summary>
    /// <returns>仮想スクリーンの境界（画面物理座標）。プライマリモニタが (0,0) にない場合は負値になり得る。</returns>
    /// <remarks>
    /// Win32 の仮想スクリーン全体を 1 回のキャプチャ（CopyFromScreen / BitBlt）で取得するための基準境界。<br/>
    /// 複数モニタ環境でも全モニタを包含する矩形を返す。<br/>
    /// </remarks>
    internal static (int X, int Y, int Width, int Height) GetVirtualScreenBounds()
        => (GetSystemMetrics(SM_XVIRTUALSCREEN),
            GetSystemMetrics(SM_YVIRTUALSCREEN),
            GetSystemMetrics(SM_CXVIRTUALSCREEN),
            GetSystemMetrics(SM_CYVIRTUALSCREEN));

    /// <summary>ウィンドウの可視矩形領域を取得する（最大化はワークエリア・通常は DWM 影除外）。</summary>
    /// <param name="hWnd">対象ウィンドウハンドル。</param>
    /// <param name="rect">取得した可視矩形。</param>
    /// <returns>取得できた場合は true。</returns>
    /// <remarks>
    /// 最大化ウィンドウには影がなく、<c>DWMWA_EXTENDED_FRAME_BOUNDS</c> は左右に 8px の内部マージンを返す<br/>
    /// （Windows 10 の既知の挙動）ため、<c>GetWindowRect</c>（ワークエリア = タスクバーを除く最大化領域）を使用する。<br/>
    /// 通常ウィンドウは <c>DWMWA_EXTENDED_FRAME_BOUNDS</c> で影を除外した可視領域を取得し、<br/>
    /// 失敗時は <c>GetWindowRect</c> にフォールバックする。<br/>
    /// <see cref="WinElementFinder"/> / <see cref="WinCaptureService"/> で共用する（単一所有）。
    /// </remarks>
    internal static bool TryGetVisibleRect(IntPtr hWnd, out RECT rect)
    {
        rect = default;
        if (hWnd == IntPtr.Zero) return false;

        // 最大化ウィンドウ: 影がないため GetWindowRect（ワークエリア）を使用する
        // （DWMWA_EXTENDED_FRAME_BOUNDS は最大化時に 8px の内部マージンを返す既知の挙動の回避）
        if (IsZoomed(hWnd))
        {
            return GetWindowRect(hWnd, out rect);
        }

        // 通常ウィンドウ: DWM 影除外（DWMWA_EXTENDED_FRAME_BOUNDS）で可視領域を取得する
        RECT visibleRect = default;
        var hr = DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS,
            ref visibleRect, RectSize);
        if (hr >= 0 && visibleRect.Left < visibleRect.Right && visibleRect.Top < visibleRect.Bottom)
        {
            rect = visibleRect;
            return true;
        }

        // フォールバック: GetWindowRect
        return GetWindowRect(hWnd, out rect);
    }

    // ============================================================
    // 定数（ホットキー）
    // ============================================================

    /// <summary>WM_HOTKEY: グローバルホットキーが押下されたときに送られるメッセージ。</summary>
    internal const uint WM_HOTKEY = 0x0312;

    // ============================================================
    // 定数（カーソル ID: LoadCursor 用）
    // ============================================================

    /// <summary>サイズ変更カーソル（左上-右下方向）。</summary>
    internal const int IDC_SIZENWSE = 32642;

    /// <summary>サイズ変更カーソル（右上-左下方向）。</summary>
    internal const int IDC_SIZENESW = 32643;

    /// <summary>水平サイズ変更カーソル。</summary>
    internal const int IDC_SIZEWE = 32644;

    /// <summary>垂直サイズ変更カーソル。</summary>
    internal const int IDC_SIZENS = 32645;

    /// <summary>四方サイズ変更カーソル。</summary>
    internal const int IDC_SIZEALL = 32646;

    /// <summary>十字カーソル。</summary>
    internal const int IDC_CROSS = 32515;

    // ============================================================
    // user32.dll — ホットキー
    // ============================================================

    /// <summary>グローバルホットキーを登録する（user32.RegisterHotKey）。</summary>
    /// <param name="hWnd">ホットキー通知を受け取るウィンドウのハンドル。</param>
    /// <param name="id">ホットキーの識別子。</param>
    /// <param name="fsModifiers">修飾キーのフラグ（MOD_ALT / MOD_CONTROL / MOD_SHIFT / MOD_WIN）。</param>
    /// <param name="vk">仮想キーコード。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, uint vk);

    /// <summary>グローバルホットキーを解除する（user32.UnregisterHotKey）。</summary>
    /// <param name="hWnd">ホットキーを登録したウィンドウのハンドル。</param>
    /// <param name="id">解除するホットキーの識別子。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    // ============================================================
    // user32.dll — カーソル
    // ============================================================

    /// <summary>システムカーソルを読み込む（user32.LoadCursor）。</summary>
    /// <param name="hInstance">カスタムカーソルを含むモジュールのハンドル。システムカーソルでは IntPtr.Zero。</param>
    /// <param name="lpCursorName">カーソル ID（IDC_ 定数）またはカスタムカーソルの名前。</param>
    /// <returns>カーソルのハンドル。</returns>
    [DllImport("user32.dll")]
    internal static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    /// <summary>現在のカーソル形状を設定する（user32.SetCursor）。</summary>
    /// <param name="hCursor">設定するカーソルのハンドル。</param>
    /// <returns>直前のカーソルのハンドル。</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetCursor(IntPtr hCursor);

    /// <summary>Avalonia の <see cref="Key"/> を Win32 仮想キーコードへ変換する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <returns>対応する Win32 仮想キーコード。不明なキーはキャスト値。</returns>
    /// <remarks>RegisterHotKey の vk パラメータに渡すために使用する。</remarks>
    internal static uint KeyToWin32VirtualKey(Key key) => key switch
    {
        Key.LeftShift => 0xA0,
        Key.RightShift => 0xA1,
        Key.LeftCtrl => 0xA2,
        Key.RightCtrl => 0xA3,
        Key.LeftAlt => 0xA4,
        Key.RightAlt => 0xA5,
        Key.LWin => 0x5B,
        Key.RWin => 0x5C,
        Key.Back => 0x08,
        Key.Tab => 0x09,
        Key.Clear => 0x0C,
        Key.Return => 0x0D,
        Key.Pause => 0x13,
        Key.Capital => 0x14,
        Key.Escape => 0x1B,
        Key.Space => 0x20,
        Key.PageUp => 0x21,
        Key.PageDown => 0x22,
        Key.End => 0x23,
        Key.Home => 0x24,
        Key.Left => 0x25,
        Key.Up => 0x26,
        Key.Right => 0x27,
        Key.Down => 0x28,
        Key.Select => 0x29,
        Key.Print => 0x2A,
        Key.Execute => 0x2B,
        Key.Snapshot => 0x2C,
        Key.Insert => 0x2D,
        Key.Delete => 0x2E,
        Key.Help => 0x2F,
        Key.D0 => 0x30, Key.D1 => 0x31, Key.D2 => 0x32, Key.D3 => 0x33,
        Key.D4 => 0x34, Key.D5 => 0x35, Key.D6 => 0x36, Key.D7 => 0x37,
        Key.D8 => 0x38, Key.D9 => 0x39,
        Key.A => 0x41, Key.B => 0x42, Key.C => 0x43, Key.D => 0x44,
        Key.E => 0x45, Key.F => 0x46, Key.G => 0x47, Key.H => 0x48,
        Key.I => 0x49, Key.J => 0x4A, Key.K => 0x4B, Key.L => 0x4C,
        Key.M => 0x4D, Key.N => 0x4E, Key.O => 0x4F, Key.P => 0x50,
        Key.Q => 0x51, Key.R => 0x52, Key.S => 0x53, Key.T => 0x54,
        Key.U => 0x55, Key.V => 0x56, Key.W => 0x57, Key.X => 0x58,
        Key.Y => 0x59, Key.Z => 0x5A,
        Key.NumPad0 => 0x60, Key.NumPad1 => 0x61, Key.NumPad2 => 0x62, Key.NumPad3 => 0x63,
        Key.NumPad4 => 0x64, Key.NumPad5 => 0x65, Key.NumPad6 => 0x66, Key.NumPad7 => 0x67,
        Key.NumPad8 => 0x68, Key.NumPad9 => 0x69,
        Key.Multiply => 0x6A, Key.Add => 0x6B, Key.Separator => 0x6C,
        Key.Subtract => 0x6D, Key.Decimal => 0x6E, Key.Divide => 0x6F,
        Key.F1 => 0x70, Key.F2 => 0x71, Key.F3 => 0x72, Key.F4 => 0x73,
        Key.F5 => 0x74, Key.F6 => 0x75, Key.F7 => 0x76, Key.F8 => 0x77,
        Key.F9 => 0x78, Key.F10 => 0x79, Key.F11 => 0x7A, Key.F12 => 0x7B,
        Key.F13 => 0x7C, Key.F14 => 0x7D, Key.F15 => 0x7E, Key.F16 => 0x7F,
        Key.F17 => 0x80, Key.F18 => 0x81, Key.F19 => 0x82, Key.F20 => 0x83,
        Key.F21 => 0x84, Key.F22 => 0x85, Key.F23 => 0x86, Key.F24 => 0x87,
        Key.NumLock => 0x90, Key.Scroll => 0x91,
        Key.OemSemicolon => 0xBA,
        Key.OemPlus => 0xBB, Key.OemComma => 0xBC,
        Key.OemMinus => 0xBD, Key.OemPeriod => 0xBE,
        Key.OemQuestion => 0xBF, Key.OemTilde => 0xC0,
        Key.OemOpenBrackets => 0xDB, Key.OemPipe => 0xDC,
        Key.OemCloseBrackets => 0xDD,
        _ => (uint)key
    };
}
