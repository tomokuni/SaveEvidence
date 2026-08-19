using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace app.Converters;

/// <summary>PixelRect を Thickness に変換するコンバーター（ウィンドウ復元時のマージン用）。</summary>
/// <remarks>
/// マージンは左上座標（X / Y）のみを適用し、右下は 0 とする。
/// </remarks>
public sealed class BoundsToMarginConverter : IValueConverter
{
    /// <summary>PixelRectからThicknessに変換する（ウィンドウ復元時のマージン用）。</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PixelRect rect)
            return new Thickness(rect.X, rect.Y, 0, 0);
        return new Thickness(0);
    }

    /// <summary>逆変換は未サポート。</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
