using System.Globalization;
using Avalonia.Data.Converters;

namespace app.Converters;

/// <summary>
/// null または空文字の場合は true を返すコンバーター。
/// </summary>
public sealed class StringIsNullOrEmptyConverter : IValueConverter
{
    /// <summary>値がnullまたは空文字の場合にtrueを返す。</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not string s || s.Length == 0;

    /// <summary>逆変換は未サポート。</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
