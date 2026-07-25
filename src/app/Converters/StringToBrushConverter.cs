using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace app.Converters;

/// <summary>色名文字列（例: "Red"）を <see cref="IBrush"/> に変換するコンバーター。</summary>
/// <remarks>
/// カラーComboBox のテキストエリアに該当色の四角を表示するために使用する。<br/>
/// <see cref="Avalonia.Media.Color.Parse(string)"/> に対応する任意の文字列を受け付ける。<br/>
/// 解釈できない場合は <see cref="Brushes.Transparent"/> を返す。<br/>
/// </remarks>
public sealed class StringToBrushConverter : IValueConverter
{
    /// <summary>色名 → IBrush</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string name && !string.IsNullOrEmpty(name))
        {
            // 名前または16進コードから Color をパース
            if (Color.TryParse(name, out var color))
            {
                return new SolidColorBrush(color);
            }
        }
        return Brushes.Transparent;
    }

    /// <summary>逆変換は未サポート。</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
