using EsUtil.SaveEvidence.App.WinForms.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinForms.Tests;

/// <summary><see cref="SettingsColorMath"/> の解析と整形を検証するテスト。</summary>
public class SettingsColorMathTests
{
    /// <summary>8 桁の 16 進を ARGB として解析することを検証する。</summary>
    [Fact]
    public void 八桁の十六進をARGBとして解析する()
    {
        var color = SettingsColorMath.ToDrawingColor("#FF102030", Color.Transparent);

        Assert.Equal(0xFF, color.A);
        Assert.Equal(0x10, color.R);
        Assert.Equal(0x20, color.G);
        Assert.Equal(0x30, color.B);
    }

    /// <summary>6 桁の 16 進を RGB として解析し、アルファを不透明として補うことを検証する。</summary>
    [Fact]
    public void 六桁の十六進は不透明として補う()
    {
        var color = SettingsColorMath.ToDrawingColor("#102030", Color.Transparent);

        Assert.Equal(0xFF, color.A);
        Assert.Equal(0x10, color.R);
    }

    /// <summary>先頭の <c>#</c> と前後の空白を省略できることを検証する。</summary>
    [Fact]
    public void 先頭の記号と空白を省略できる()
    {
        var color = SettingsColorMath.ToDrawingColor("  80AABBCC  ", Color.Transparent);

        Assert.Equal(0x80, color.A);
        Assert.Equal(0xAA, color.R);
        Assert.Equal(0xBB, color.G);
        Assert.Equal(0xCC, color.B);
    }

    /// <summary>解析できない文字列はフォールバック色になることを検証する。</summary>
    /// <param name="text">解析できない文字列。</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#12345")]
    [InlineData("ZZZZZZZZ")]
    [InlineData("#GGGGGGGG")]
    public void 解析できない文字列はフォールバック色になる(string? text)
    {
        var fallback = Color.FromArgb(1, 2, 3, 4);

        Assert.Equal(fallback, SettingsColorMath.ToDrawingColor(text, fallback));
    }

    /// <summary>色を <c>#AARRGGBB</c> の大文字 16 進へ整形することを検証する。</summary>
    [Fact]
    public void 色を十六進の文字列へ整形する()
    {
        var text = SettingsColorMath.ToArgbString(Color.FromArgb(0x0A, 0x0B, 0x0C, 0x0D));

        Assert.Equal("#0A0B0C0D", text);
        Assert.Equal(
            Color.FromArgb(0x0A, 0x0B, 0x0C, 0x0D),
            SettingsColorMath.ToDrawingColor(text, Color.Transparent));
    }
}
