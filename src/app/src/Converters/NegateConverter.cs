using System.Globalization;
using Avalonia.Data.Converters;

namespace app.Converters;

/// <summary>
/// bool 値を反転するコンバーター。
/// </summary>
/// <remarks>
/// ToggleButton の IsChecked 反転などの用途に使用する。<br/>
/// </remarks>
public sealed class NegateConverter : IValueConverter
{
    /// <summary>bool 値を反転して返す。</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    /// <summary>逆変換も同様に反転する。</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}
