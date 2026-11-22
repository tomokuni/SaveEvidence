using System.Runtime.InteropServices;

namespace EsUtil.Ui.MewUI.Annotation;

/// <summary>IME（<c>imm32</c>）の呼び出し（変換候補の位置をキャレットへ合わせる）。</summary>
/// <remarks>
/// <b>MewUI に IME の位置を伝える API が無い</b>ため、Win32 の <c>ImmSetCandidateWindow</c> を直接呼びます。
/// 呼び出しは本クラスへ閉じ、注釈のコントロールは「いつ呼ぶか」だけを持ちます。
/// </remarks>
internal static class NativeIme
{
    /// <summary>候補の位置を指定するスタイル（<c>CFS_CANDIDATEPOS</c>）。</summary>
    internal const int CfsCandidatePos = 0x0040;

    /// <summary>画面の点。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        /// <summary>X 座標（画面ピクセル）。</summary>
        internal int X;

        /// <summary>Y 座標（画面ピクセル）。</summary>
        internal int Y;
    }

    /// <summary>画面の矩形。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        /// <summary>左。</summary>
        internal int Left;

        /// <summary>上。</summary>
        internal int Top;

        /// <summary>右。</summary>
        internal int Right;

        /// <summary>下。</summary>
        internal int Bottom;
    }

    /// <summary>候補の表示位置（<c>CANDIDATEFORM</c>）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct CandidateForm
    {
        /// <summary>対象の候補の番号（0 は変換中の文字列）。</summary>
        internal int Index;

        /// <summary>指定のしかた（<see cref="CfsCandidatePos"/>）。</summary>
        internal int Style;

        /// <summary>候補の基準点（画面ピクセル）。</summary>
        internal Point CurrentPos;

        /// <summary>候補を避ける領域（画面ピクセル）。</summary>
        internal Rect Area;
    }

    /// <summary>IME の文脈を取得する。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <returns>文脈。 取得できない場合は <see cref="IntPtr.Zero"/>。</returns>
    [DllImport("imm32.dll", EntryPoint = "ImmGetContext")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr ImmGetContext(IntPtr window);

    /// <summary>IME の文脈を解放する。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <param name="context">文脈。</param>
    /// <returns>解放できた場合は <see langword="true"/>。</returns>
    [DllImport("imm32.dll", EntryPoint = "ImmReleaseContext")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ImmReleaseContext(IntPtr window, IntPtr context);

    /// <summary>変換候補の表示位置を設定する。</summary>
    /// <param name="context">文脈。</param>
    /// <param name="form">表示位置。</param>
    /// <returns>設定できた場合は <see langword="true"/>。</returns>
    [DllImport("imm32.dll", EntryPoint = "ImmSetCandidateWindow")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ImmSetCandidateWindow(IntPtr context, ref CandidateForm form);
}
