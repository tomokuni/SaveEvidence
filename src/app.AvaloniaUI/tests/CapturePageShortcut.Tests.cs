using Estable.SaveEvidence.Views.Pages;
using Avalonia.Input;
using Xunit;

namespace Estable.SaveEvidence.Tests;

/// <summary><see cref="CapturePage.IsCtrlShortcut"/> の単体テスト。</summary>
/// <remarks>
/// Ctrl ショートカット（<see cref="CapturePage.OnGlobalKeyDown"/>）の修飾キー判定を検証する。<br/>
/// Ctrl のみ押下時は true、Ctrl なし・Alt / Shift 併用は false となることを確認する。
/// </remarks>
public class CapturePageShortcutTests
{
    [Theory]
    [InlineData(KeyModifiers.Control)]
    [InlineData(KeyModifiers.Control | KeyModifiers.Meta)] // Meta（Windows キー）は許可（Alt / Shift 以外の併用）
    public void IsCtrlShortcut_CtrlのみまたはMeta併用はtrue(KeyModifiers modifiers)
    {
        Assert.True(CapturePage.IsCtrlShortcut(modifiers));
    }

    [Theory]
    [InlineData(KeyModifiers.None)]
    [InlineData(KeyModifiers.Alt)]
    [InlineData(KeyModifiers.Shift)]
    [InlineData(KeyModifiers.Alt | KeyModifiers.Shift)]
    [InlineData(KeyModifiers.Alt | KeyModifiers.Control)]
    [InlineData(KeyModifiers.Shift | KeyModifiers.Control)]
    [InlineData(KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Control)]
    public void IsCtrlShortcut_CtrlなしまたはAltShift併用はfalse(KeyModifiers modifiers)
    {
        Assert.False(CapturePage.IsCtrlShortcut(modifiers));
    }
}
