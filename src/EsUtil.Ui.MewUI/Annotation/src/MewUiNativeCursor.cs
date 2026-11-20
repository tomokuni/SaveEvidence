using System.Runtime.InteropServices;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>マウス ポインターの画面座標と、OS のウィンドウ操作を行う。</summary>
/// <remarks>
/// <b>座標の単位は物理ピクセル</b>: <see cref="GetCursorPos"/> は物理ピクセルの画面座標を返す
/// （コアのフローティング パネルは物理ピクセルで位置を扱う）。 MewUI のウィンドウの位置は DIP のため、
/// 呼び出し側が倍率で割る。
/// </remarks>
internal static class MewUiNativeCursor
{
    /// <summary>画面座標。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Point32
    {
        /// <summary>X 座標（物理ピクセル）。</summary>
        public int X;

        /// <summary>Y 座標（物理ピクセル）。</summary>
        public int Y;
    }

    /// <summary>ポインターの画面座標を取得する。</summary>
    /// <param name="x">X 座標（物理ピクセル）。</param>
    /// <param name="y">Y 座標（物理ピクセル）。</param>
    /// <returns>取得できた場合は <see langword="true"/>。</returns>
    internal static bool TryGetScreenPosition(out int x, out int y)
    {
        if (GetCursorPos(out var point))
        {
            x = point.X;
            y = point.Y;
            return true;
        }

        x = 0;
        y = 0;
        return false;
    }

    /// <summary>ポインターの画面座標を取得する（OS）。</summary>
    /// <param name="point">受け取る座標。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point32 point);
}
