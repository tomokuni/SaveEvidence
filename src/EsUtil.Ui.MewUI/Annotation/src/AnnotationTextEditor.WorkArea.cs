using System.Runtime.InteropServices;
using Aprillz.MewUI;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>注釈のパネルを画面の作業領域の内側へ収める処理。</summary>
/// <remarks>
/// <b>ウィンドウの外へ出せるようにした副作用を打ち消す</b>: パネルは別ウィンドウなので
/// ウィンドウの外へ出せますが、画面の外へ出ると見えなくなります（実測 2026-11-23）。
/// モニターの作業領域（タスク バーを除いた範囲）で位置を挟みます。
/// </remarks>
public sealed partial class AnnotationTextEditor
{
    /// <summary>指定した位置をモニターの作業領域の内側へ収める。</summary>
    /// <param name="reference">基準にするウィンドウ（モニターを決める）。</param>
    /// <param name="x">左上の X（画面座標）。</param>
    /// <param name="y">左上の Y（画面座標）。</param>
    /// <param name="width">置くウィンドウの幅。</param>
    /// <param name="height">置くウィンドウの高さ。</param>
    /// <returns>収めた位置。</returns>
    private static (int X, int Y) ClampToWorkArea(Window reference, int x, int y, double width, double height)
    {
        if (!OperatingSystem.IsWindows() || reference.Handle == IntPtr.Zero)
        {
            return (x, y);
        }

        var monitor = Win32Monitor.MonitorFromWindow(reference.Handle, 2);
        if (monitor == IntPtr.Zero)
        {
            return (x, y);
        }

        var info = default(Win32Monitor.MonitorInfo);
        info.Size = Marshal.SizeOf<Win32Monitor.MonitorInfo>();
        if (!Win32Monitor.GetMonitorInfo(monitor, ref info))
        {
            return (x, y);
        }

        var maxX = Math.Max(info.Work.Left, info.Work.Right - (int)width);
        var maxY = Math.Max(info.Work.Top, info.Work.Bottom - (int)height);
        return (Math.Clamp(x, info.Work.Left, maxX), Math.Clamp(y, info.Work.Top, maxY));
    }

    /// <summary>モニターの作業領域を問い合わせる Win32 の窓口。</summary>
    private static class Win32Monitor
    {
        /// <summary>矩形（画面座標）。</summary>
        internal struct Rect
        {
            /// <summary>左端。</summary>
            internal int Left;

            /// <summary>上端。</summary>
            internal int Top;

            /// <summary>右端。</summary>
            internal int Right;

            /// <summary>下端。</summary>
            internal int Bottom;
        }

        /// <summary>モニターの情報。</summary>
        internal struct MonitorInfo
        {
            /// <summary>構造体の大きさ（呼ぶ前に設定する）。</summary>
            internal int Size;

            /// <summary>モニター全体の矩形。</summary>
            internal Rect Monitor;

            /// <summary>作業領域の矩形（タスク バーを除く）。</summary>
            internal Rect Work;

            /// <summary>主モニターかどうかの印。</summary>
            internal int Flags;
        }

        /// <summary>ウィンドウが載っているモニターを求める。</summary>
        /// <param name="handle">ウィンドウのハンドル。</param>
        /// <param name="flags">既定（2 = 最も近いモニター）。</param>
        /// <returns>モニターのハンドル。</returns>
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll")]
        internal static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        /// <summary>モニターの情報を求める。</summary>
        /// <param name="monitor">モニターのハンドル。</param>
        /// <param name="info">受け取る情報。</param>
        /// <returns>成功した場合は <see langword="true"/>。</returns>
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("user32.dll")]
        internal static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    }
}
