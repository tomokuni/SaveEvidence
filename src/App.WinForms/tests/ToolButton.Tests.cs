using EsUtil.SaveEvidence.App.WinForms.Views;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinForms.Tests;

/// <summary><see cref="ToolButton"/> の仕様テスト（ツールバーとナビゲーションの描き分け）。</summary>
/// <remarks>
/// 同じ <see cref="ToolButton"/> でも、ツールバーは WinUI 3 の既定のボタンと同じ外観（面 + 枠線、<br/>
/// 選択中はアクセント色）、ナビゲーションは下地を持たない（選択の帯だけで示す）。<br/>
/// 配色は <see cref="AppStyles"/>（App.Core の <c>AppPalette</c> を写した値）が単一所有する。
/// </remarks>
public class ToolButtonTests
{
    /// <summary>ツールバーのボタンが面・枠線を持ち、選択中はアクセント色になることを検証する。</summary>
    [Fact]
    public void ツールバーのボタンは面と枠線を持ち選択中はアクセント色になる()
    {
        using var button = new ToolButton(icon: null, toolTip: "ツールバー", caption: "最新の情報に更新")
        {
            IsToolbarButton = true,
        };

        Assert.Equal(AppStyles.ButtonFace, button.BackColor);
        Assert.Equal(AppStyles.ButtonBorder, button.FlatAppearance.BorderColor);
        Assert.Equal(1, button.FlatAppearance.BorderSize);

        button.IsSelected = true;

        Assert.Equal(AppStyles.SelectedButtonFace, button.BackColor);
        Assert.Equal(AppStyles.SelectedButtonBorder, button.FlatAppearance.BorderColor);
        Assert.Equal(AppStyles.SelectedButtonForeground, button.ForeColor);
    }

    /// <summary>ナビゲーションの項目が下地を持たないことを検証する。</summary>
    [Fact]
    public void ナビゲーションの項目は下地を持たない()
    {
        using var button = new ToolButton(icon: null, toolTip: "ナビゲーション");

        Assert.Equal(AppStyles.NavBackground, button.BackColor);
        Assert.Equal(0, button.FlatAppearance.BorderSize);
    }
}
