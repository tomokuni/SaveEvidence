using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="AppPalette"/> の仕様テスト（テーマ別の配色）。</summary>
/// <remarks>
/// 色は 4 つの UI 版で共通に使うため、指定された値そのものを固定する。
/// </remarks>
public class AppPaletteTests
{
    [Fact]
    internal void 画像領域_は明色で指定色を使う()
        => Assert.Equal(0xFFF7F7F7u, AppPalette.ImageArea(isDark: false));

    [Fact]
    internal void ナビパネル_は明色で指定色を使う()
        => Assert.Equal(0xFFF0F0F0u, AppPalette.NavPanel(isDark: false));

    [Fact]
    internal void 暗色_は明色より暗い()
    {
        Assert.True(Luminance(AppPalette.ImageArea(isDark: true)) < Luminance(AppPalette.ImageArea(isDark: false)));
        Assert.True(Luminance(AppPalette.NavPanel(isDark: true)) < Luminance(AppPalette.NavPanel(isDark: false)));
    }

    [Fact]
    internal void 暗色_は不透明()
    {
        Assert.Equal(0xFFu, AppPalette.ImageArea(isDark: true) >> 24);
        Assert.Equal(0xFFu, AppPalette.NavPanel(isDark: true) >> 24);
    }

    [Fact]
    internal void ツールバーのボタン_は押している間がホバーより濃い()
    {
        // 明色は「より暗く」、暗色は「より明るく」して、押している間の変化を強める
        Assert.True(
            Luminance(AppPalette.ToolbarButtonBackground(isDark: false, isPressed: true))
            < Luminance(AppPalette.ToolbarButtonBackground(isDark: false, isPressed: false)));
        Assert.True(
            Luminance(AppPalette.ToolbarButtonBackground(isDark: true, isPressed: true))
            > Luminance(AppPalette.ToolbarButtonBackground(isDark: true, isPressed: false)));
    }

    [Fact]
    internal void ツールバーのボタン_は角丸で描く()
        => Assert.True(AppPalette.ToolbarButtonCornerRadius > 0);

    [Fact]
    internal void ボタンの面と枠線_は明色でWinUI3の既定のボタンと同じ値()
    {
        Assert.Equal(0xFFFFFFFFu, AppPalette.ButtonFace(isDark: false));
        Assert.Equal(0xFFD6D6D6u, AppPalette.ButtonBorder(isDark: false));
    }

    [Fact]
    internal void ボタンの面と枠線_は暗色でも指定色を使う()
    {
        Assert.Equal(0xFF2D2D2Du, AppPalette.ButtonFace(isDark: true));
        Assert.Equal(0xFF303030u, AppPalette.ButtonBorder(isDark: true));
    }

    [Fact]
    internal void 選択中のトグル_はアクセント色で塗り文字色を反転する()
    {
        Assert.Equal(0xFF0067C0u, AppPalette.ButtonCheckedFace(isDark: false));
        Assert.Equal(0xFFFFFFFFu, AppPalette.ButtonCheckedForeground(isDark: false));
        Assert.Equal(0xFF4CC2FFu, AppPalette.ButtonCheckedFace(isDark: true));
        Assert.Equal(0xFF000000u, AppPalette.ButtonCheckedForeground(isDark: true));
    }

    [Fact]
    internal void 一覧の背景_は明色で指定色を使う()
        => Assert.Equal(0xFFF2F2F2u, AppPalette.ListBackground(isDark: false));

    /// <summary>色の明るさ（0〜255 の単純平均）を求める。</summary>
    /// <param name="argb">色（0xAARRGGBB）。</param>
    /// <returns>明るさ。</returns>
    private static int Luminance(uint argb)
        => (int)(((argb >> 16) & 0xFF) + ((argb >> 8) & 0xFF) + (argb & 0xFF)) / 3;
}
