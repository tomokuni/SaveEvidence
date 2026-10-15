using System.Globalization;
using Windows.UI;

namespace EsUtil.SaveEvidence.App.WinUI3.Services;

/// <summary>設定に保存する色文字列（<c>#AARRGGBB</c>）と WinUI 3 の色の相互変換。</summary>
/// <remarks>
/// 設定（<c>Settings</c>）は色と不透明度を統合した <c>#AARRGGBB</c> 形式の文字列で保持する。<br/>
/// <b>形式</b>: <c>#AARRGGBB</c>（8 桁）または <c>#RRGGBB</c>（6 桁・アルファは 255）。 <c>#</c> は省略できる。<br/>
/// <b>失敗時</b>: 解析できない文字列は既定色へフォールバックする（設定の破損で描画が落ちないようにする）。<br/>
/// <b>純粋関数</b>: ウィンドウを必要としないため、桁数（6 / 8）・大文字小文字・不正文字をヘッドレスで検証できる。<br/>
/// <b>撮影スタック向けの色</b>はコアの写像（<c>CaptureSettingsMapper</c>）が変換するため、本クラスは持たない。
/// </remarks>
internal static class SettingsColorMath
{
    /// <summary>8 桁（<c>AARRGGBB</c>）の 16 進文字数を表す。</summary>
    private const int ArgbLength = 8;

    /// <summary>6 桁（<c>RRGGBB</c>）の 16 進文字数を表す。</summary>
    private const int RgbLength = 6;

    /// <summary>設定の色文字列を <c>0xAARRGGBB</c> の数値へ変換する。</summary>
    /// <param name="argb">設定の色文字列（<c>#AARRGGBB</c> / <c>#RRGGBB</c>。 前後の空白と <c>#</c> は省略可能）。 null 可。</param>
    /// <param name="fallback">解析できない場合に使用する値（<c>0xAARRGGBB</c>）。</param>
    /// <returns>変換した値。 解析できない場合は <paramref name="fallback"/>。</returns>
    public static uint ToArgb(string? argb, uint fallback)
        => TryParse(argb, out var value) ? value : fallback;

    /// <summary>設定の色文字列を <see cref="Color"/> へ変換する。</summary>
    /// <param name="argb">設定の色文字列。 null 可。</param>
    /// <param name="fallback">解析できない場合に使用する色。</param>
    /// <returns>変換した色。 解析できない場合は <paramref name="fallback"/>。</returns>
    public static Color ToColor(string? argb, Color fallback)
        => TryParse(argb, out var value) ? ToColor(value) : fallback;

    /// <summary><c>0xAARRGGBB</c> の数値を <see cref="Color"/> へ変換する。</summary>
    /// <param name="argb">変換する値（<c>0xAARRGGBB</c>）。</param>
    /// <returns>変換した色。</returns>
    public static Color ToColor(uint argb)
    {
        var color = new Color
        {
            A = (byte)(argb >> 24),
            R = (byte)(argb >> 16),
            G = (byte)(argb >> 8),
            B = (byte)argb,
        };

        return color;
    }

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

    /// <summary>設定の色文字列を解析する。</summary>
    /// <param name="argb">設定の色文字列。 null 可。</param>
    /// <param name="value">解析結果（<c>0xAARRGGBB</c>）。 解析できない場合は 0。</param>
    /// <returns>解析できた場合は true。</returns>
    /// <remarks>
    /// 前後の空白を除去し、先頭の <c>#</c> があれば外して 6 桁または 8 桁の 16 進数として解釈する。<br/>
    /// それ以外の桁数・16 進数でない文字を含む場合は false を返す。
    /// </remarks>
    private static bool TryParse(string? argb, out uint value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(argb))
        {
            return false;
        }

        var span = argb.AsSpan().Trim();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span[1..];
        }

        // 8 桁は ARGB、6 桁は RGB のみとみなしアルファを不透明（255）として補う
        if (span.Length == ArgbLength)
        {
            return uint.TryParse(span, NumberStyles.HexNumber, null, out value);
        }

        if (span.Length == RgbLength && uint.TryParse(span, NumberStyles.HexNumber, null, out var rgb))
        {
            value = 0xFF000000 | rgb;
            return true;
        }

        return false;
    }
}
