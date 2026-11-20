using System.Runtime.InteropServices;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>マウス ポインターの画面座標を取得する（フライアウトの初期位置に使う）。</summary>
/// <remarks>
/// <b>ポインター位置を使う</b>: 右クリックした位置は、コントロールの座標（DIP）から画面座標（物理ピクセル）へ
/// 変換するより、OS のポインター位置をそのまま使うほうが DPI の倍率に依存せず正しい。<br/>
/// <b>座標系</b>: <see cref="GetCursorPos"/> は物理ピクセルの画面座標を返す（<c>AppWindow.Move</c> と同じ座標系）。
/// </remarks>
internal static class NativeCursor
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
