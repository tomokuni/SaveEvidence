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

    /// <summary>ウィンドウの枠と角丸を消す。</summary>
    /// <param name="windowHandle">対象のウィンドウ ハンドル。 0 の場合は何もしない。</param>
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
}
