using System.Globalization;
using MewUIColor = Aprillz.MewUI.Color;
using DrawingColor = System.Drawing.Color;

namespace SaveEvidence.MewUI.Services;

/// <summary>設定に保存する色文字列（<c>#AARRGGBB</c>）と MewUI / System.Drawing の色の相互変換。</summary>
/// <remarks>
/// 設定（<c>Settings</c>）は色と不透明度を統合した <c>#AARRGGBB</c> 形式の文字列で保持する。<br/>
/// Avalonia 版は <c>ColorString</c>（色名の解決を含む）を使うが、本 UI は数値の 16 進表現のみを扱う。<br/>
/// <b>形式</b>: <c>#AARRGGBB</c>（8 桁）または <c>#RRGGBB</c>（6 桁・アルファは 255）。<c>#</c> は省略できる。<br/>
/// <b>失敗時</b>: 解析できない文字列は既定色へフォールバックする（設定の破損で描画が落ちないようにする）。<br/>
/// <b>純粋関数</b>: UI ホストを必要としないため、境界値（桁数・大文字小文字・不正文字）をヘッドレスで検証できる。
/// </remarks>
internal static class SettingsColorMath
{
    /// <summary>8 桁（<c>AARRGGBB</c>）の 16 進文字数を表す。</summary>
    private const int ArgbLength = 8;

    /// <summary>6 桁（<c>RRGGBB</c>）の 16 進文字数を表す。</summary>
    private const int RgbLength = 6;

    /// <summary>設定の色文字列を MewUI の色へ変換する。</summary>
    /// <param name="argb">設定の色文字列（<c>#AARRGGBB</c> / <c>#RRGGBB</c>。前後の空白と <c>#</c> は省略可能）。null 可。</param>
    /// <param name="fallback">解析できない場合に使用する色。</param>
    /// <returns>変換した色。解析できない場合は <paramref name="fallback"/>。</returns>
    public static MewUIColor ToMewUIColor(string? argb, MewUIColor fallback)
        => TryParse(argb, out var value) ? MewUIColor.FromArgb(value) : fallback;

    /// <summary>設定の色文字列を System.Drawing の色へ変換する。</summary>
    /// <param name="argb">設定の色文字列（<c>#AARRGGBB</c> / <c>#RRGGBB</c>。前後の空白と <c>#</c> は省略可能）。null 可。</param>
    /// <param name="fallback">解析できない場合に使用する色。</param>
    /// <returns>変換した色。解析できない場合は <paramref name="fallback"/>。</returns>
    public static DrawingColor ToDrawingColor(string? argb, DrawingColor fallback)
    {
        if (!TryParse(argb, out var value))
        {
            return fallback;
        }

        return DrawingColor.FromArgb(
            (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    /// <summary>MewUI の色を設定の色文字列（<c>#AARRGGBB</c>）へ変換する。</summary>
    /// <param name="color">変換する色。</param>
    /// <returns>設定へ保存する色文字列（大文字の 16 進・先頭に <c>#</c>）。</returns>
    public static string ToArgbString(MewUIColor color)
        => $"#{color.ToArgb():X8}";

    /// <summary>System.Drawing の色を設定の色文字列（<c>#AARRGGBB</c>）へ変換する。</summary>
    /// <param name="color">変換する色。</param>
    /// <returns>設定へ保存する色文字列（大文字の 16 進・先頭に <c>#</c>）。</returns>
    public static string ToArgbString(DrawingColor color)
        => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>設定の色文字列を解析する。</summary>
    /// <param name="argb">設定の色文字列。null 可。</param>
    /// <param name="value">解析結果（<c>0xAARRGGBB</c>）。解析できない場合は 0。</param>
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
        var text = span;
        if (text.Length == ArgbLength)
        {
            return uint.TryParse(text, NumberStyles.HexNumber, null, out value);
        }

        if (text.Length == RgbLength && uint.TryParse(text, NumberStyles.HexNumber, null, out var rgb))
        {
            value = 0xFF000000 | rgb;
            return true;
        }

        return false;
    }
}
