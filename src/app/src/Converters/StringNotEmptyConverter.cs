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
