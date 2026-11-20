using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>画面の作業領域（タスク バーを除いた領域）を取得する。</summary>
/// <remarks>
/// <b>作業領域を使う理由</b>: パネルはアプリのウィンドウの内側へ収めないが、画面の外（タスク バーの下）へ
/// 出すと掴めなくなる。 タスク バーを除いた領域へ収める。<br/>
/// <b>座標の単位は物理ピクセル</b>（<c>SPI_GETWORKAREA</c> は物理ピクセルを返す）。
/// </remarks>
internal static class MewUiScreen
{
    /// <summary>作業領域を取得する問い合わせ。</summary>
    private const uint SpiGetWorkArea = 0x0030;

    /// <summary>作業領域を取得する。</summary>
    /// <param name="workArea">受け取る作業領域（物理ピクセル）。</param>
    /// <returns>取得できた場合は <see langword="true"/>。</returns>
    internal static bool TryGetWorkArea(out EsRect workArea)
    {
        if (SystemParametersInfoW(SpiGetWorkArea, 0, out var rect, 0))
        {
            workArea = new EsRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            return true;
        }

        workArea = default;
        return false;
    }

    /// <summary>矩形（OS）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect32
    {
        /// <summary>左端。</summary>
        public int Left;

        /// <summary>上端。</summary>
        public int Top;

        /// <summary>右端。</summary>
        public int Right;

        /// <summary>下端。</summary>
        public int Bottom;
    }

    /// <summary>システム パラメーターを取得する（OS）。</summary>
    /// <param name="action">問い合わせ。</param>
    /// <param name="param">問い合わせの引数。</param>
    /// <param name="value">受け取る値。</param>
    /// <param name="windowIni">更新の対象。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoW(uint action, uint param, out Rect32 value, uint windowIni);
}
