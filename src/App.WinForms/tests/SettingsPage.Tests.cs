using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Input;
using EsUtil.SaveEvidence.App.WinForms.Views;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinForms.Tests;

/// <summary>ホットキー設定の表示用の整形を検証するテスト。</summary>
/// <remarks>「クリア」で保存される未設定の値を空文字として表し、それ以外はコアの整形に委ねることを確かめる。</remarks>
public class SettingsPageTests
{
    /// <summary>修飾キーとキーが表示用の文字列へ整形されることを検証する。</summary>
    [Fact]
    public void ホットキーが表示用の文字列へ整形される()
    {
        var setting = EsInput.CreateHotKey(HotKeyModifiers.Control | HotKeyModifiers.Shift, EsKey.Q);

        Assert.Equal("Ctrl + Shift + Q", SettingsPage.FormatHotKey(setting));
        Assert.Equal("Ctrl + Shift + Q", setting.ToString());
    }

    /// <summary>未設定のホットキーが空文字になることを検証する。</summary>
    [Fact]
    public void 未設定のホットキーは空文字になる()
        => Assert.Equal("", SettingsPage.FormatHotKey(EsInput.NoHotKey));

    /// <summary>修飾キーのみの設定でも整形できることを検証する。</summary>
    [Fact]
    public void 修飾キーのみでも整形できる()
    {
        var setting = EsInput.CreateHotKey(HotKeyModifiers.Alt, EsKey.A);

        Assert.Equal("Alt + A", SettingsPage.FormatHotKey(setting));
    }
}
