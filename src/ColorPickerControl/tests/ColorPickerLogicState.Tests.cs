using Xunit;

namespace Estable.AvaloniaUI.ColorPicker.Tests;

/// <summary><see cref="ColorPickerControl"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 色の妥当性検証（<see cref="ColorPickerControl.IsValidColor"/>）のみを検証するため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// ヘッドレス必須のテスト（コントロール生成・イベント処理等）は <see cref="ColorPickerLogicTests"/> が直列コレクションで検証する。
/// </remarks>
public class ColorPickerLogicStateTests
{
    [Fact]
    public void IsValidColor_nullはfalseを返す()
    {
        Assert.False(ColorPickerControl.IsValidColor(null));
    }

    [Fact]
    public void IsValidColor_有効な色はtrueを返す()
    {
        Assert.True(ColorPickerControl.IsValidColor("Red"));
        Assert.True(ColorPickerControl.IsValidColor("#00FF00"));
    }

    [Fact]
    public void IsValidColor_無効な色はfalseを返す()
    {
        Assert.False(ColorPickerControl.IsValidColor("NotAColor"));
    }
}
