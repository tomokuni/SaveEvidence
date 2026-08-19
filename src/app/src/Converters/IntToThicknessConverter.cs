using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace app.Converters;

/// <summary>
/// int 値を Avalonia.Thickness に変換するコンバーター（全辺同じ値）。
/// </summary>
/// <remarks>
/// LoupeFrameWidth（int）→ Border.BorderThickness（Thickness）のバインドに使用する。<br/>
/// </remarks>
public sealed class IntToThicknessConverter : IValueConverter
{
    /// <summary>int 値を Thickness に変換する。</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i ? new Thickness(i) : new Thickness(0);

    /// <summary>逆変換は未サポート。</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
