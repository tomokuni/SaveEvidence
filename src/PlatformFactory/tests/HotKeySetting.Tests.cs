using Avalonia.Input;
using Xunit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary><see cref="HotKeySetting"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 値オブジェクトの文字列表現・キー変換のみを検証するため、Avalonia ヘッドレスに依存せず<br/>
/// <c>[Fact]</c> で並列実行する。
/// </remarks>
public class HotKeySettingTests
{
    [Fact]
    public void ToString_修飾キーとキーの組合せを表示する()
    {
        var setting = new HotKeySetting(HotKeyModifiers.Control | HotKeyModifiers.Shift, Key.Q);

        Assert.Equal("Ctrl + Shift + Q", setting.ToString());
    }

    [Fact]
    public void ToString_修飾キーなしはキーのみ表示する()
    {
        var setting = new HotKeySetting(HotKeyModifiers.None, Key.A);

        Assert.Equal("A", setting.ToString());
    }

    [Fact]
    public void ToString_数字キーは数字で表示する()
    {
        var setting = new HotKeySetting(HotKeyModifiers.Alt, Key.D1);

        Assert.Equal("Alt + 1", setting.ToString());
    }

    [Fact]
    public void ToString_記号キーは記号で表示する()
    {
        var setting = new HotKeySetting(HotKeyModifiers.Windows, Key.OemTilde);

        Assert.Equal("Win + `", setting.ToString());
    }

    [Fact]
    public void GetVirtualKeyCode_Keyの値を返す()
    {
        var setting = new HotKeySetting(HotKeyModifiers.None, Key.F5);

        Assert.Equal((uint)Key.F5, setting.GetVirtualKeyCode());
    }

    [Fact]
    public void レコード等価性で同一の組合せは一致する()
    {
        var a = new HotKeySetting(HotKeyModifiers.Control, Key.S);
        var b = new HotKeySetting(HotKeyModifiers.Control, Key.S);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
