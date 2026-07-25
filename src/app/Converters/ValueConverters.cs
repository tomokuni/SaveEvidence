using System.Globalization;
using Avalonia.Data.Converters;

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
    /// <summary>PixelRectまたはWindowBoundsからThicknessに変換する（ウィンドウ復元時のマージン用）。</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Avalonia.PixelRect rect)
            return new Avalonia.Thickness(rect.X, rect.Y, 0, 0);
        if (value is Models.WindowBounds wb)
            return new Avalonia.Thickness(wb.X, wb.Y, 0, 0);
        return new Avalonia.Thickness(0);
    }

    /// <summary>逆変換は未サポート。</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
