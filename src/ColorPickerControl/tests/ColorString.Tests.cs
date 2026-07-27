using Avalonia.Media;
using Xunit;

namespace Estable.AvaloniaUI.ColorPicker.Tests;

/// <summary><see cref="ColorString"/> の単体テスト。</summary>
/// <remarks>
/// 色と不透明度を統合した文字列形式（色名 / #RRGGBB / #AARRGGBB / +{n}%）の
/// 解析・整形・正規化を検証する。
/// </remarks>
public class ColorStringTests
{
    // ====================================================================
    // IsDefinedColorName
    // ====================================================================

    [Fact]
    public void IsDefinedColorName_定義済み色名はtrueを返す()
    {
        Assert.True(ColorString.IsDefinedColorName("Red"));
        Assert.True(ColorString.IsDefinedColorName("White"));
        Assert.True(ColorString.IsDefinedColorName("Blue"));
    }

    [Fact]
    public void IsDefinedColorName_大文字小文字を無視する()
    {
        Assert.True(ColorString.IsDefinedColorName("RED"));
        Assert.True(ColorString.IsDefinedColorName("red"));
    }

    [Fact]
    public void IsDefinedColorName_未定義の文字列はfalseを返す()
    {
        Assert.False(ColorString.IsDefinedColorName("NotAColor"));
        Assert.False(ColorString.IsDefinedColorName(""));
        Assert.False(ColorString.IsDefinedColorName("#FF0000"));
    }

    // ====================================================================
    // TryParse
    // ====================================================================

    [Fact]
    public void TryParse_色名のみは不透明度1で解析される()
    {
        Assert.True(ColorString.TryParse("Red", out var color, out var opacity));

        Assert.Equal(Color.FromArgb(255, 255, 0, 0), color);
        Assert.Equal(1.0, opacity);
    }

    [Fact]
    public void TryParse_16進6桁は不透明度1で解析される()
    {
        Assert.True(ColorString.TryParse("#336699", out var color, out var opacity));

        Assert.Equal(Color.FromArgb(255, 0x33, 0x66, 0x99), color);
        Assert.Equal(1.0, opacity);
    }

    [Fact]
    public void TryParse_16進8桁はアルファを保持して解析される()
    {
        Assert.True(ColorString.TryParse("#80FF0000", out var color, out var opacity));

        Assert.Equal(Color.FromArgb(128, 255, 0, 0), color);
        // +{n}% が無い場合は不透明度は 1.0 として扱う（アルファ成分は色に保持される）
        Assert.Equal(1.0, opacity);
    }

    [Fact]
    public void TryParse_色名と不透明度はアルファへ合成される()
    {
        Assert.True(ColorString.TryParse("Red+50%", out var color, out var opacity));

        Assert.Equal(Color.FromArgb(128, 255, 0, 0), color);
        Assert.Equal(0.5, opacity);
    }

    [Fact]
    public void TryParse_16進と不透明度はアルファへ合成される()
    {
        Assert.True(ColorString.TryParse("#336699+75%", out var color, out var opacity));

        // 75% → 0.75 * 255 = 191.25 → 丸めで 191 (#BF)
        Assert.Equal(Color.FromArgb(191, 0x33, 0x66, 0x99), color);
        Assert.Equal(0.75, opacity);
    }

    [Fact]
    public void TryParse_不透明度0はアルファ0になる()
    {
        Assert.True(ColorString.TryParse("Red+0%", out var color, out var opacity));

        Assert.Equal(Color.FromArgb(0, 255, 0, 0), color);
        Assert.Equal(0.0, opacity);
    }

    [Fact]
    public void TryParse_不透明度100はアルファ255になる()
    {
        Assert.True(ColorString.TryParse("Red+100%", out var color, out var opacity));

        Assert.Equal(Color.FromArgb(255, 255, 0, 0), color);
        Assert.Equal(1.0, opacity);
    }

    [Fact]
    public void TryParse_不透明度の丸めは四捨五入される()
    {
        // 25% → 0.25 * 255 = 63.75 → 64 (#40)
        Assert.True(ColorString.TryParse("Red+25%", out var color, out _));
        Assert.Equal(64, color.A);

        // 33% → 0.33 * 255 = 84.15 → 84 (#54)
        Assert.True(ColorString.TryParse("Red+33%", out var color2, out _));
        Assert.Equal(84, color2.A);
    }

    [Fact]
    public void TryParse_nullや空文字はfalseを返す()
    {
        Assert.False(ColorString.TryParse(null, out _, out _));
        Assert.False(ColorString.TryParse("", out _, out _));
        Assert.False(ColorString.TryParse("   ", out _, out _));
    }

    [Fact]
    public void TryParse_未知の文字列はfalseを返す()
    {
        Assert.False(ColorString.TryParse("NotAColor", out _, out _));
    }

    [Fact]
    public void TryParse_不透明度が数値でない場合はfalseを返す()
    {
        Assert.False(ColorString.TryParse("Red+abc%", out _, out _));
    }

    [Fact]
    public void TryParse_不透明度が範囲外の場合はfalseを返す()
    {
        Assert.False(ColorString.TryParse("Red+101%", out _, out _));
        Assert.False(ColorString.TryParse("Red+-1%", out _, out _));
    }

    [Fact]
    public void TryParse_先頭にプラスのみの場合はfalseを返す()
    {
        // "+50%" は色部分が空になるため解析できない
        Assert.False(ColorString.TryParse("+50%", out _, out _));
    }

    [Fact]
    public void TryParse_末尾がパーセントでないプラスは色名として扱われる()
    {
        // 色名に + を含む文字列（"+" は色名に無いため解析失敗）
        Assert.False(ColorString.TryParse("Red+50", out _, out _));
    }

    [Fact]
    public void TryParse_不透明度が有効でも色が無効ならfalseを返す()
    {
        // "+{n}%" 部分は有効だが色部分が解釈不能な場合（&& の最終条件で失敗）
        Assert.False(ColorString.TryParse("NotAColor+50%", out _, out _));
    }

    // ====================================================================
    // Format
    // ====================================================================

    [Fact]
    public void Format_色名あり不透明度1は色名のみを返す()
    {
        Assert.Equal("Red", ColorString.Format(Colors.Red, 1.0));
    }

    [Fact]
    public void Format_色名あり不透明度はパーセント付きで返す()
    {
        Assert.Equal("Red+50%", ColorString.Format(Colors.Red, 0.5));
    }

    [Fact]
    public void Format_色名なしは16進で返す()
    {
        Assert.Equal("#336699", ColorString.Format(Color.FromRgb(0x33, 0x66, 0x99), 1.0));
        Assert.Equal("#336699+75%", ColorString.Format(Color.FromRgb(0x33, 0x66, 0x99), 0.75));
    }

    [Fact]
    public void Format_アルファは無視されRGBで色名を判定する()
    {
        // アルファ付きでも RGB が一致すれば色名で表す
        Assert.Equal("Red+50%", ColorString.Format(Color.FromArgb(128, 255, 0, 0), 0.5));
    }

    [Fact]
    public void Format_不透明度はパーセント整数へ丸める()
    {
        Assert.Equal("Red+33%", ColorString.Format(Colors.Red, 0.333));
        Assert.Equal("Red+67%", ColorString.Format(Colors.Red, 0.666));
    }

    [Fact]
    public void Format_不透明度0は0パーセントを返す()
    {
        Assert.Equal("Red+0%", ColorString.Format(Colors.Red, 0.0));
    }

    [Fact]
    public void Format_不透明度は0から1にクランプされる()
    {
        Assert.Equal("Red+0%", ColorString.Format(Colors.Red, -0.5));
        Assert.Equal("Red", ColorString.Format(Colors.Red, 1.5));
    }

    // ====================================================================
    // ToDisplayText
    // ====================================================================

    [Fact]
    public void ToDisplayText_アルファ付き16進は色名とパーセントで表示される()
    {
        Assert.Equal("Red+50%", ColorString.ToDisplayText("#80FF0000"));
    }

    [Fact]
    public void ToDisplayText_不透明な色名はそのまま表示される()
    {
        Assert.Equal("Red", ColorString.ToDisplayText("Red"));
    }

    [Fact]
    public void ToDisplayText_パーセント指定済みはそのまま表示される()
    {
        Assert.Equal("Red+50%", ColorString.ToDisplayText("Red+50%"));
        Assert.Equal("#336699+75%", ColorString.ToDisplayText("#336699+75%"));
    }

    [Fact]
    public void ToDisplayText_色名のないアルファ付き16進は16進とパーセントで表示される()
    {
        Assert.Equal("#336699+75%", ColorString.ToDisplayText("#BF336699"));
    }

    [Fact]
    public void ToDisplayText_16進6桁は色名があれば色名で表示される()
    {
        Assert.Equal("Blue", ColorString.ToDisplayText("#0000FF"));
    }

    [Fact]
    public void ToDisplayText_nullや空文字はそのまま返す()
    {
        Assert.Equal("", ColorString.ToDisplayText(null));
        Assert.Equal("", ColorString.ToDisplayText(""));
    }

    [Fact]
    public void ToDisplayText_解析できない値はそのまま返す()
    {
        Assert.Equal("NotAColor", ColorString.ToDisplayText("NotAColor"));
    }

    [Fact]
    public void ToDisplayText_100パーセントは省略される()
    {
        Assert.Equal("White", ColorString.ToDisplayText("White+100%"));
    }

    [Fact]
    public void ToDisplayText_半透明の白は色名Whiteで表示される()
    {
        // White と Transparent は同 RGB のため、RGB 一致では不透明（A=255）の色名を優先する
        Assert.Equal("White+50%", ColorString.ToDisplayText("#80FFFFFF"));
    }

    // ====================================================================
    // ToArgbHex
    // ====================================================================

    [Fact]
    public void ToArgbHex_色名はアルファ255の8桁に正規化される()
    {
        Assert.Equal("#FFFF0000", ColorString.ToArgbHex("Red"));
        Assert.Equal("#FFFFFFFF", ColorString.ToArgbHex("White"));
    }

    [Fact]
    public void ToArgbHex_パーセント付きはアルファへ合成される()
    {
        Assert.Equal("#80FF0000", ColorString.ToArgbHex("Red+50%"));
    }

    [Fact]
    public void ToArgbHex_16進6桁はアルファ255に正規化される()
    {
        Assert.Equal("#FF336699", ColorString.ToArgbHex("#336699"));
    }

    [Fact]
    public void ToArgbHex_16進8桁はそのまま返す()
    {
        Assert.Equal("#80FF0000", ColorString.ToArgbHex("#80FF0000"));
    }

    [Fact]
    public void ToArgbHex_nullや空文字はそのまま返す()
    {
        Assert.Equal("", ColorString.ToArgbHex(null));
        Assert.Equal("", ColorString.ToArgbHex(""));
    }

    [Fact]
    public void ToArgbHex_解析できない値はそのまま返す()
    {
        Assert.Equal("NotAColor", ColorString.ToArgbHex("NotAColor"));
    }

    // ====================================================================
    // ToArgbHex(Color)
    // ====================================================================

    [Fact]
    public void ToArgbHex_色オブジェクトはアルファ先頭の8桁に変換される()
    {
        Assert.Equal("#80FF0000", ColorString.ToArgbHex(Color.FromArgb(0x80, 0xFF, 0x00, 0x00)));
    }

    [Fact]
    public void ToArgbHex_色オブジェクトのアルファ255でも省略せず8桁で返す()
    {
        Assert.Equal("#FF123456", ColorString.ToArgbHex(Color.FromRgb(0x12, 0x34, 0x56)));
    }
}
