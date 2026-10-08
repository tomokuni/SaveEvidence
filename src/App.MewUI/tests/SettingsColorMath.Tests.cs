using EsUtil.SaveEvidence.App.MewUI.Services;
using Xunit;
using DrawingColor = System.Drawing.Color;
using MewUIColor = Aprillz.MewUI.Color;

namespace EsUtil.SaveEvidence.App.MewUI.Tests;

/// <summary><see cref="SettingsColorMath"/> の単体テスト（色文字列の解析と整形）。</summary>
/// <remarks>
/// 設定は色を <c>#AARRGGBB</c> の文字列で保持する。 桁数の違い（6 / 8）・大文字小文字・不正入力を固定する。
/// </remarks>
public class SettingsColorMathTests
{
    private static readonly MewUIColor s_fallback = MewUIColor.FromArgb(255, 1, 2, 3);

    [Theory]
    [InlineData("#FF102030", 255, 16, 32, 48)]
    [InlineData("FF102030", 255, 16, 32, 48)]
    [InlineData("#80102030", 128, 16, 32, 48)]
    [InlineData("  #ff102030  ", 255, 16, 32, 48)]
    [InlineData("#102030", 255, 16, 32, 48)]
    [InlineData("#00000000", 0, 0, 0, 0)]
    internal void ToMewUIColor_有効な色文字列を解析する(
        string argb, byte expectedA, byte expectedR, byte expectedG, byte expectedB)
    {
        var color = SettingsColorMath.ToMewUIColor(argb, s_fallback);

        Assert.Equal(expectedA, color.A);
        Assert.Equal(expectedR, color.R);
        Assert.Equal(expectedG, color.G);
        Assert.Equal(expectedB, color.B);
    }

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
    internal void ToMewUIColor_解析できない文字列は既定色を返す(string? argb)
    {
        var color = SettingsColorMath.ToMewUIColor(argb, s_fallback);

        Assert.Equal(s_fallback, color);
    }

    [Theory]
    [InlineData("#FF102030", 255, 16, 32, 48)]
    [InlineData("#80102030", 128, 16, 32, 48)]
    [InlineData("#102030", 255, 16, 32, 48)]
    internal void ToDrawingColor_有効な色文字列を解析する(
        string argb, byte expectedA, byte expectedR, byte expectedG, byte expectedB)
    {
        var color = SettingsColorMath.ToDrawingColor(argb, DrawingColor.Red);

        Assert.Equal(expectedA, color.A);
        Assert.Equal(expectedR, color.R);
        Assert.Equal(expectedG, color.G);
        Assert.Equal(expectedB, color.B);
    }

    [Fact]
    internal void ToDrawingColor_解析できない文字列は既定色を返す()
    {
        Assert.Equal(DrawingColor.Red, SettingsColorMath.ToDrawingColor("not-a-color", DrawingColor.Red));
    }

    [Theory]
    [InlineData(255, 16, 32, 48, "#FF102030")]
    [InlineData(128, 16, 32, 48, "#80102030")]
    [InlineData(0, 0, 0, 0, "#00000000")]
    internal void ToMewUIColor_色文字列へ整形する(byte a, byte r, byte g, byte b, string expected)
        => Assert.Equal(expected, SettingsColorMath.ToArgbString(MewUIColor.FromArgb(a, r, g, b)));

    [Theory]
    [InlineData(255, 16, 32, 48, "#FF102030")]
    [InlineData(128, 16, 32, 48, "#80102030")]
    internal void ToArgbString_DrawingColorを色文字列へ整形する(byte a, byte r, byte g, byte b, string expected)
        => Assert.Equal(expected, SettingsColorMath.ToArgbString(DrawingColor.FromArgb(a, r, g, b)));

    [Fact]
    internal void 変換_色文字列と色の相互変換で値が一致する()
    {
        const string source = "#4C808080";

        var color = SettingsColorMath.ToMewUIColor(source, s_fallback);
        var roundTrip = SettingsColorMath.ToArgbString(color);

        Assert.Equal(source, roundTrip);
    }
}
