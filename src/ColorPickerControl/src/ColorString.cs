using System.Globalization;
using System.Reflection;
using System.Text;
using Avalonia.Media;

namespace Estable.AvaloniaUI.ColorPicker;

/// <summary>色文字列（色名 or 16進 + 不透明度%）の解析・整形・正規化を提供するユーティリティ。</summary>
/// <remarks>
/// 色と不透明度を統合した文字列のうち、**保存値**と**画面表示**の 2 形式を定義する:<br/>
/// - 保存値（<see cref="ToArgbHex"/> の出力）: <c>#AARRGGBB</c>（アルファ先頭 8 桁。例: "#80FF0000"）<br/>
/// - 画面表示（<see cref="ToDisplayText"/> / <see cref="Format"/> の出力）: <c>{色}+{不透明度%}</c><br/>
///   - 色名が <see cref="Colors"/> に定義されている色: 色名で表す（例: "Red"）<br/>
///   - 色名が未定義の色: <c>#RRGGBB</c> で表す（例: "#336699"）<br/>
///   - 不透明度が 100% の場合: <c>{色}</c>（例: "Red", "#336699"）<br/>
///   - 不透明度が 100% 以外の場合: <c>{色}+{不透明度%}</c>（例: "Red+50%", "#336699+75%"）<br/>
/// 入力は両形式（色名 / #RRGGBB / #AARRGGBB / +% 形式）を <see cref="TryParse"/> で受け付ける。<br/>
/// 本形式は <see cref="ColorPickerControl.SelectedColor"/> の文字列仕様であり、<br/>
/// ホストアプリ（設定の保存・反映）も本ユーティリティで解析できるよう public とする。
/// </remarks>
public static class ColorString
{
    /// <summary>色名（英名）の辞書（<see cref="Colors"/> の全色名 → 色）。</summary>
    /// <remarks><see cref="Colors"/> の公開静的プロパティをリフレクションで列挙する（140 色）。</remarks>
    private static readonly IReadOnlyDictionary<string, Color> s_colorNames;

    /// <summary>静的コンストラクタ。色名一覧を構築する。</summary>
    /// <remarks>
    /// <see cref="Colors"/> の公開静的プロパティのうち <see cref="Color"/> 型のみを列挙する
    /// （<see cref="ColorEntry"/> と同一のフィルタ方式。`is` 判定の防御分岐を作らない）。
    /// </remarks>
    static ColorString()
    {
        s_colorNames = typeof(Colors)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(Color))
            .ToDictionary(p => p.Name, p => (Color)p.GetValue(null)!, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>指定された色名が <see cref="Colors"/> に定義されているか判定する。</summary>
    /// <param name="name">色名（例: "Red"）。大文字小文字は無視。</param>
    /// <returns>定義されている場合は <see langword="true"/>。</returns>
    public static bool IsDefinedColorName(string name)
        => s_colorNames.ContainsKey(name);

    /// <summary>色文字列（色名 or 16進 + 不透明度% の形式）を解析して色と不透明度を取得する。</summary>
    /// <param name="text">解析対象の文字列（例: "Red", "#336699", "Red+50%", "#336699+75%"）。</param>
    /// <param name="color">解析結果の色（アルファは <paramref name="opacity"/> に応じて 0〜255 に合成される）。</param>
    /// <param name="opacity">解析結果の不透明度（0.0〜1.0）。</param>
    /// <returns>解析に成功した場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// 不透明度部分（<c>+{n}%</c>）が省略された場合は不透明度 1.0 として扱う。<br/>
    /// 色は <see cref="Color.TryParse(string, out Color)"/> で解釈する（色名・16進・アルファ付き16進）。<br/>
    /// <c>+{n}%</c> が指定された場合、色のアルファを不透明度へ合成する（例: "Red+50%" → <c>#80FF0000</c>）。
    /// </remarks>
    public static bool TryParse(string? text, out Color color, out double opacity)
    {
        color = default;
        opacity = 1.0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        // "+{n}%" の不透明度部分を分離する
        var plusIndex = text.LastIndexOf('+');
        if (plusIndex > 0 && text.EndsWith('%'))
        {
            var colorPart = text[..plusIndex];
            var opacityPart = text[(plusIndex + 1)..^1]; // "%" を除く
            if (int.TryParse(opacityPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pct)
                && pct is >= 0 and <= 100
                && Color.TryParse(colorPart, out var parsed))
            {
                opacity = pct / 100.0;
                // 不透明度をアルファへ合成する（例: "Red+50%" → #80FF0000）
                // pct * 2.55 は浮動小数誤差（例: 50 * 2.55 = 127.4999...）を生むため
                // 整数倍してから除算し、50% → 128（#80）が正確に得られるようにする
                color = Color.FromArgb((byte)Math.Round(pct * 255.0 / 100.0), parsed.R, parsed.G, parsed.B);
                return true;
            }
            return false;
        }

        // 不透明度なし（100%）→ 色のみ
        return Color.TryParse(text, out color);
    }

    /// <summary>色と不透明度を「{色}+{不透明度%}」形式の文字列へ整形する。</summary>
    /// <param name="color">色（RGB。アルファは無視される）。</param>
    /// <param name="opacity">不透明度（0.0〜1.0）。1.0 の場合は不透明度部分を省略する。</param>
    /// <returns>整形結果の文字列（例: "Red", "Red+50%", "#336699+75%"）。</returns>
    /// <remarks>
    /// 色名が <see cref="Colors"/> に定義されている場合は色名で、未定義の場合は <c>#RRGGBB</c> で表す。<br/>
    /// 不透明度は 0〜100% の整数に丸め、1.0（100%）の場合は省略する。
    /// </remarks>
    public static string Format(Color color, double opacity)
    {
        var colorPart = FindColorName(color) ?? ToHex(color);
        var pct = (int)Math.Round(Math.Clamp(opacity, 0.0, 1.0) * 100);
        return pct >= 100 ? colorPart : $"{colorPart}+{pct}%";
    }

    /// <summary>保存値（色名 / #RRGGBB / #AARRGGBB / +% 形式）を画面表示用テキスト（{色}+{不透明度%}）へ変換する。</summary>
    /// <param name="text">保存値（例: "#80FF0000", "Red", "Red+50%", "#336699+75%"）。</param>
    /// <returns>表示用テキスト（例: "Red+50%", "Red", "#336699+75%"）。解析できない場合は入力をそのまま返す。</returns>
    /// <remarks>
    /// ComboBox などの画面表示に使用する。不透明度は <c>+{n}%</c> 形式で表し、1.0（100%）の場合は省略する。<br/>
    /// <c>+{n}%</c> 形式を含まない入力（#AARRGGBB 等）はアルファ成分を不透明度として解釈する。
    /// </remarks>
    public static string ToDisplayText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;
        if (!TryParse(text, out var color, out var opacity)) return text;

        // 不透明度が明示指定されていない場合（+{n}% なし）は、アルファ成分を不透明度として扱う
        if (!text.Contains('+'))
        {
            opacity = color.A / 255.0;
        }
        return Format(color, opacity);
    }

    /// <summary>任意の色文字列（色名 / #RRGGBB / #AARRGGBB / +% 形式）を <c>#AARRGGBB</c>（アルファ先頭16進）へ正規化する。</summary>
    /// <param name="text">入力文字列（例: "Red", "Red+50%", "#80FF0000"）。</param>
    /// <returns><c>#AARRGGBB</c> 形式の保存値（例: "#80FF0000"）。解析できない場合は入力をそのまま返す。</returns>
    /// <remarks>
    /// Settings などの保存値は Avalonia 標準の <c>#AARRGGBB</c>（アルファ先頭）とする。<br/>
    /// 色名・#RRGGBB はアルファ 255（不透明）へ、<c>+{n}%</c> は指定された不透明度へ正規化する。
    /// </remarks>
    public static string ToArgbHex(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;
        if (!TryParse(text, out var color, out _)) return text;
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    /// <summary>色を <c>#AARRGGBB</c>（アルファ先頭・常時アルファ含む）形式へ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns><c>#AARRGGBB</c> 形式の文字列（例: "#80FF0000"）。</returns>
    /// <remarks>
    /// 保存値（<see cref="ColorPickerControl.SelectedColor"/> / Settings）の形式は Avalonia 標準の
    /// <c>#AARRGGBB</c> とする（<see cref="Color.Parse"/> と整合。不透明度はアルファ成分として統合される）。<br/>
    /// カラーピッカー（ColorView）の OK 確定時など、色オブジェクトを直接保存値へ変換する場合に使用する。
    /// </remarks>
    public static string ToArgbHex(Color color)
        => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>指定された色に対応する色名（<see cref="Colors"/>）を検索する。</summary>
    /// <param name="color">色。</param>
    /// <returns>一致する色名。未定義の場合は null。</returns>
    /// <remarks>
    /// アルファも含めて完全一致する色名を最優先する。<br/>
    /// 完全一致が無い場合は RGB のみで一致する色名を返す（同 RGB で複数ある場合は
    /// 不透明（A=255）の色名を優先。例: White と Transparent は同 RGB のため、
    /// 半透明の白は "White" と表示される）。<br/>
    /// </remarks>
    private static string? FindColorName(Color color)
    {
        // アルファも含めて完全一致する色名を優先する
        foreach (var (name, c) in s_colorNames)
        {
            if (c == color) return name;
        }

        // RGB のみで一致する色名を探す（不透明（A=255）の色名を優先）
        string? fallback = null;
        foreach (var (name, c) in s_colorNames)
        {
            if (c.R == color.R && c.G == color.G && c.B == color.B)
            {
                if (c.A == 255) return name;
                fallback ??= name;
            }
        }
        return fallback;
    }

    /// <summary>色を <c>#RRGGBB</c>（アルファなし16進）へ変換する。</summary>
    /// <param name="color">色。</param>
    /// <returns><c>#RRGGBB</c> 形式の文字列。</returns>
    private static string ToHex(Color color)
    {
        var sb = new StringBuilder(7);
        sb.Append('#');
        sb.Append(color.R.ToString("X2", CultureInfo.InvariantCulture));
        sb.Append(color.G.ToString("X2", CultureInfo.InvariantCulture));
        sb.Append(color.B.ToString("X2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}
