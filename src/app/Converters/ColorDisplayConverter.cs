using System.Globalization;
using app.Helpers;
using Avalonia.Data.Converters;

namespace app.Converters;

/// <summary>色名文字列を「英語名 (日本語名)」形式に変換するコンバーター。</summary>
/// <remarks>
/// <see cref="Convert"/> はモデル値（例: "Red"）を表示名（例: "Red (赤)"）に変換する。<br/>
/// <see cref="ConvertBack"/> は表示名から英語名部分を抽出してモデルに書き戻す。<br/>
/// モデルは常に <see cref="Color.TryParse(string)"/> で解釈可能な値のみを保持する。<br/>
/// </remarks>
public sealed class ColorDisplayConverter : IValueConverter
{
    /// <summary>色名 → 表示名</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string name)
        {
            ColorEntry.TryGetDisplayName(name, out var display);
            return display;
        }
        return value;
    }

    /// <summary>表示名 → 色名（英語部分のみ抽出）</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string display)
        {
            // "Red (赤)" → "Red"  /  "Red" → "Red"
            var parenIndex = display.IndexOf(" (", StringComparison.Ordinal);
            return parenIndex > 0 ? display[..parenIndex] : display;
        }
        return value;
    }
}
