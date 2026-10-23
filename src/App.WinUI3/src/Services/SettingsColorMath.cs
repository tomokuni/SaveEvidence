using System.Globalization;
using EsUtil.Platform.Core.Imaging;
using Windows.UI;

namespace EsUtil.SaveEvidence.App.WinUI3.Services;

/// <summary>設定に保存する色文字列（<c>#AARRGGBB</c>）と WinUI 3 の色の相互変換。</summary>
/// <remarks>
/// 設定（<c>Settings</c>）は色と不透明度を統合した <c>#AARRGGBB</c> 形式の文字列で保持する。<br/>
/// <b>形式と解釈は <see cref="DrawingColorExtensions.TryParseHexText"/> が単一所有する</b>
/// （以前は 3 つの UI 版が同じ解析をそれぞれ持っていた）。 本クラスは自 UI の色型との相互変換だけを担う。<br/>
/// <b>失敗時</b>: 解析できない文字列は既定色へフォールバックする（設定の破損で描画が落ちないようにする）。<br/>
/// <b>撮影スタック向けの色</b>はコアの写像（<c>CaptureSettingsMapper</c>）が変換するため、本クラスは持たない。
/// </remarks>
internal static class SettingsColorMath
{
    /// <summary>設定の色文字列を <c>0xAARRGGBB</c> の数値へ変換する。</summary>
    /// <param name="argb">設定の色文字列（<c>#AARRGGBB</c> / <c>#RRGGBB</c>。 前後の空白と <c>#</c> は省略可能）。 null 可。</param>
    /// <param name="fallback">解析できない場合に使用する値（<c>0xAARRGGBB</c>）。</param>
    /// <returns>変換した値。 解析できない場合は <paramref name="fallback"/>。</returns>
    public static uint ToArgb(string? argb, uint fallback)
        => DrawingColorExtensions.TryParseHexText(argb, out var color) ? (uint)color.ToArgb() : fallback;

    /// <summary>設定の色文字列を <see cref="Color"/> へ変換する。</summary>
    /// <param name="argb">設定の色文字列。 null 可。</param>
    /// <param name="fallback">解析できない場合に使用する色。</param>
    /// <returns>変換した色。 解析できない場合は <paramref name="fallback"/>。</returns>
    public static Color ToColor(string? argb, Color fallback)
        => DrawingColorExtensions.TryParseHexText(argb, out var color) ? ToColor((uint)color.ToArgb()) : fallback;

    /// <summary><c>0xAARRGGBB</c> の数値を <see cref="Color"/> へ変換する。</summary>
    /// <param name="argb">変換する値（<c>0xAARRGGBB</c>）。</param>
    /// <returns>変換した色。</returns>
    public static Color ToColor(uint argb)
        => new()
        {
            A = (byte)(argb >> 24),
            R = (byte)(argb >> 16),
            G = (byte)(argb >> 8),
            B = (byte)argb,
        };

    /// <summary><see cref="Color"/> を <c>0xAARRGGBB</c> の数値へ変換する。</summary>
    /// <param name="color">変換する色。</param>
    /// <returns>変換した値（<c>0xAARRGGBB</c>）。</returns>
    public static uint ToArgb(Color color)
        => ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;

    /// <summary><c>0xAARRGGBB</c> の数値を設定の色文字列（<c>#AARRGGBB</c>）へ変換する。</summary>
    /// <param name="argb">変換する値（<c>0xAARRGGBB</c>）。</param>
    /// <returns>設定へ保存する色文字列（大文字の 16 進・先頭に <c>#</c>）。</returns>
    public static string ToArgbString(uint argb)
        => string.Create(CultureInfo.InvariantCulture, $"#{argb:X8}");
}