using System.Collections.Concurrent;
using System.Globalization;
using app.Models;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace app.Converters;

/// <summary>色名文字列（例: "Red"）を IBrush に変換するコンバーター。</summary>
/// <remarks>
/// LoupeFrameColor（string）→ Border.BorderBrush（IBrush）のバインドに使用する。<br/>
/// Color.TryParse に対応する任意の文字列を受け付ける。解釈できない場合は Transparent を返す。<br/>
/// </remarks>
public sealed class StringToBrushConverter : IValueConverter
{
    private const int CacheCapacity = 64;

    private static readonly ConcurrentDictionary<string, IBrush> s_brushCache = new();

    /// <summary>色名 → IBrush</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string name || string.IsNullOrEmpty(name) || !Color.TryParse(name, out _))
        {
            return Brushes.Transparent;
        }

        return CapacityLimitedCache.GetOrAdd(
            s_brushCache, CacheCapacity, name, static n => new SolidColorBrush(Color.Parse(n)));
    }

    /// <summary>逆変換は未サポート。</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
