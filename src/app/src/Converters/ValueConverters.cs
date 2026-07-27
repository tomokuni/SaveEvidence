using System.ComponentModel;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace app.Converters;

/// <summary>
/// ファイル名テンプレート編集のヒント表示用コンバーター。
/// </summary>
/// <remarks>
/// 編集ボックスにフォーカスがない場合にヒントテキストを表示するために使用する。<br/>
/// </remarks>
public sealed class StringNotEmptyConverter : IValueConverter
{
    /// <summary>値が空でなければtrueを返す。</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && s.Length > 0;

    /// <summary>逆変換は未サポート。</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

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
        => value is int i ? new Avalonia.Thickness(i) : new Avalonia.Thickness(0);

    /// <summary>逆変換は未サポート。</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 設定値の範囲チェック用コンバーター（最小値・最大値を比較）。
/// </summary>
public sealed class BoundsToMarginConverter : IValueConverter
{
    /// <summary>PixelRectからThicknessに変換する（ウィンドウ復元時のマージン用）。</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Avalonia.PixelRect rect)
            return new Avalonia.Thickness(rect.X, rect.Y, 0, 0);
        return new Avalonia.Thickness(0);
    }

    /// <summary>逆変換は未サポート。</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>色名文字列（例: "Red"）を IBrush に変換するコンバーター。</summary>
/// <remarks>
/// LoupeFrameColor（string）→ Border.BorderBrush（IBrush）のバインドに使用する。<br/>
/// Color.TryParse に対応する任意の文字列を受け付ける。解釈できない場合は Transparent を返す。<br/>
/// </remarks>
public sealed class StringToBrushConverter : IValueConverter
{
    /// <summary>色名 → IBrush</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string name && !string.IsNullOrEmpty(name) && Color.TryParse(name, out var color))
            return new SolidColorBrush(color);
        return Brushes.Transparent;
    }

    /// <summary>逆変換は未サポート。</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>enum 値を <see cref="System.ComponentModel.DescriptionAttribute"/> の表示名へ変換するコンバーター。</summary>
/// <remarks>
/// Description 属性が未定義の enum 値は、そのまま enum 名（ToString()）を返す。<br/>
/// 設定画面の ComboBox（テーマモード等）の表示に使用する。
/// </remarks>
public sealed class EnumToDisplayNameConverter : IValueConverter
{
    /// <summary>enum → 表示名</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return "";
        var type = value.GetType();
        if (!type.IsEnum) return value.ToString() ?? "";

        var member = type.GetMember(value.ToString()!).FirstOrDefault();
        var description = member?.GetCustomAttributes(typeof(DescriptionAttribute), false)
            .OfType<DescriptionAttribute>()
            .FirstOrDefault()?.Description;
        return description ?? value.ToString() ?? "";
    }

    /// <summary>逆変換は未サポート。</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
