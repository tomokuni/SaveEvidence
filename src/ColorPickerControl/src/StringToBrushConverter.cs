using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Estable.AvaloniaUI.ColorPicker;

/// <summary>色名文字列（例: "Red"）を <see cref="IBrush"/> に変換するコンバーター。</summary>
/// <remarks>
/// カラーComboBox のテキストエリアに該当色の四角を表示するために使用する。<br/>
/// 色と不透明度を統合した形式（例: "Red", "Red+50%", "#336699+75%"）を受け付ける。<br/>
/// 解釈できない値（null・空文字・未知の文字列）の場合は <see cref="Brushes.Transparent"/> を返す。<br/>
/// <br/>
/// **公開 API 一覧**:<br/>
/// - メソッド: <see cref="Convert(object?, Type, object?, CultureInfo)"/> / <see cref="ConvertBack(object?, Type, object?, CultureInfo)"/><br/>
/// <br/>
/// **利用例（XAML）**:<br/>
/// リソース定義: <c>&lt;cp:StringToBrushConverter x:Key="StringToBrushConverter"/&gt;</c><br/>
/// バインド: <c>Background="{Binding SelectedColor, Converter={StaticResource StringToBrushConverter}}"</c><br/>
/// </remarks>
public sealed class StringToBrushConverter : IValueConverter
{
    /// <summary>色名 → IBrush に変換する。</summary>
    /// <param name="value">変換元の値。色と不透明度を統合した文字列（例: "Red", "Red+50%", "#336699+75%"）。</param>
    /// <param name="targetType">変換先の型。</param>
    /// <param name="parameter">変換パラメーター（未使用）。</param>
    /// <param name="culture">カルチャ情報（未使用）。</param>
    /// <returns>変換結果の <see cref="IBrush"/>。解釈できない場合は <see cref="Brushes.Transparent"/>。</returns>
    /// <remarks>
    /// <see cref="ColorString.TryParse(string, out Color, out double)"/> で解析可能な文字列のみ
    /// <see cref="SolidColorBrush"/> を生成する（不透明度はアルファへ合成される）。<br/>
    /// </remarks>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string name && !string.IsNullOrEmpty(name))
        {
            // 色 + 不透明度（+n%）の形式から Color を解析（不透明度はアルファへ合成）
            if (ColorString.TryParse(name, out var color, out _))
            {
                return new SolidColorBrush(color);
            }
        }

        return Brushes.Transparent;
    }

    /// <summary>逆変換は未サポート（常に <see cref="NotSupportedException"/> を送出する）。</summary>
    /// <param name="value">変換元の値。</param>
    /// <param name="targetType">変換先の型。</param>
    /// <param name="parameter">変換パラメーター。</param>
    /// <param name="culture">カルチャ情報。</param>
    /// <returns>常に <see cref="NotSupportedException"/>。</returns>
    /// <exception cref="NotSupportedException">逆変換は未サポートのため常に送出される。</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

