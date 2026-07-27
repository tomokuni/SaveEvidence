using System.Globalization;
using Avalonia.Media;
using Xunit;

namespace Estable.AvaloniaUI.ColorPicker.Tests;

/// <summary><see cref="StringToBrushConverter"/> の単体テスト。</summary>
/// <remarks>
/// 変換は <see cref="ColorString.TryParse"/>（純粋演算）と <see cref="SolidColorBrush"/> 生成のみのため、
/// Avalonia ヘッドレス実行（Dispatcher / Application）を必要としない。<br/>
/// 並列実行を妨げないよう <c>[Fact]</c> で検証する。
/// </remarks>
public class StringToBrushConverterTests
{
    private static readonly StringToBrushConverter s_converter = new();

    [Fact]
    public void Convert_色名はSolidColorBrushを返す()
    {
        var result = s_converter.Convert("Red", typeof(IBrush), null, CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Colors.Red, brush.Color);
    }

    [Fact]
    public void Convert_16進コードはSolidColorBrushを返す()
    {
        var result = s_converter.Convert("#00FF00", typeof(IBrush), null, CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Colors.Lime, brush.Color);
    }

    [Fact]
    public void Convert_無効な値はTransparentを返す()
    {
        var result = s_converter.Convert("NotAColor", typeof(IBrush), null, CultureInfo.InvariantCulture);

        Assert.Equal(Brushes.Transparent, result);
    }

    [Fact]
    public void Convert_nullはTransparentを返す()
    {
        var result = s_converter.Convert(null, typeof(IBrush), null, CultureInfo.InvariantCulture);

        Assert.Equal(Brushes.Transparent, result);
    }

    [Fact]
    public void Convert_空文字はTransparentを返す()
    {
        var result = s_converter.Convert("", typeof(IBrush), null, CultureInfo.InvariantCulture);

        Assert.Equal(Brushes.Transparent, result);
    }

    [Fact]
    public void ConvertBack_NotSupportedExceptionを投げる()
    {
        Assert.Throws<NotSupportedException>(() =>
            s_converter.ConvertBack("Red", typeof(IBrush), null, CultureInfo.InvariantCulture));
    }
}
