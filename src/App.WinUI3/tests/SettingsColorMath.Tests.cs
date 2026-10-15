using EsUtil.SaveEvidence.App.WinUI3.Services;
using Microsoft.UI;
using Windows.UI;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinUI3.Tests;

/// <summary><see cref="SettingsColorMath"/> の単体テスト（色文字列の解析と整形）。</summary>
/// <remarks>設定は色を <c>#AARRGGBB</c> の文字列で保持する。 桁数の違い（6 / 8）・大文字小文字・不正入力を固定する。</remarks>
public class SettingsColorMathTests
{
    private const uint Fallback = 0xFF010203;

    [Theory]
    [InlineData("#FF102030", 0xFF102030)]
    [InlineData("FF102030", 0xFF102030)]
    [InlineData("#80102030", 0x80102030)]
    [InlineData("  #ff102030  ", 0xFF102030)]
    [InlineData("#102030", 0xFF102030)]
    [InlineData("#00000000", 0x00000000)]
    internal void ToArgb_有効な色文字列を解析する(string argb, uint expected)
        => Assert.Equal(expected, SettingsColorMath.ToArgb(argb, Fallback));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#123456789")]
    [InlineData("#GGGGGGGG")]
    [InlineData("transparent")]
    [InlineData("#FF10203Z")]
    internal void ToArgb_解析できない文字列は既定値を返す(string? argb)
        => Assert.Equal(Fallback, SettingsColorMath.ToArgb(argb, Fallback));

    [Theory]
    [InlineData("#FF102030", 255, 16, 32, 48)]
    [InlineData("#80102030", 128, 16, 32, 48)]
    [InlineData("#102030", 255, 16, 32, 48)]
    internal void ToColor_有効な色文字列を解析する(string argb, byte a, byte r, byte g, byte b)
    {
        var color = SettingsColorMath.ToColor(argb, Colors.Red);

        Assert.Equal(a, color.A);
        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
    }

    [Fact]
    internal void ToColor_解析できない文字列は既定色を返す()
        => Assert.Equal(Colors.Red, SettingsColorMath.ToColor("not-a-color", Colors.Red));

    [Theory]
    [InlineData(0xFF102030u, "#FF102030")]
    [InlineData(0x80102030u, "#80102030")]
    [InlineData(0x00000000u, "#00000000")]
    internal void ToArgbString_色文字列へ整形する(uint argb, string expected)
        => Assert.Equal(expected, SettingsColorMath.ToArgbString(argb));

    [Fact]
    internal void 変換_色文字列と値の相互変換で値が一致する()
    {
        const string source = "#4C808080";

        var argb = SettingsColorMath.ToArgb(source, Fallback);
        var roundTrip = SettingsColorMath.ToArgbString(argb);

        Assert.Equal(source, roundTrip);
    }

    [Fact]
    internal void 変換_色と値の相互変換で値が一致する()
    {
        var color = Color.FromArgb(128, 16, 32, 48);

        Assert.Equal(0x80102030u, SettingsColorMath.ToArgb(color));
        Assert.Equal(color, SettingsColorMath.ToColor(0x80102030u));
    }
}