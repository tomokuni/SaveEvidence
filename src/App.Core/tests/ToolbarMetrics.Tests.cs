using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="ToolbarMetrics"/> の仕様テスト。</summary>
public class ToolbarMetricsTests
{
    [Fact]
    internal void FontSize_は4UI版で共通の12()
        => Assert.Equal(12, ToolbarMetrics.FontSize);

    [Fact]
    internal void FontSize_は設定ページの本文より小さい()
        => Assert.True(ToolbarMetrics.FontSize < SettingsMetrics.FontSize);

    [Fact]
    internal void ButtonHeight_は全UI版で共通の32()
        => Assert.Equal(32, ToolbarMetrics.ButtonHeight);

    [Fact]
    internal void IconSize_は全UI版で共通の16()
        => Assert.Equal(16, ToolbarMetrics.IconSize);

    [Fact]
    internal void Padding_は各UI版で共通()
    {
        Assert.Equal(8, ToolbarMetrics.PaddingX);
        Assert.Equal(4, ToolbarMetrics.PaddingY);
    }

    [Fact]
    internal void CornerRadius_は共通パレットの値を使う()
        => Assert.Equal(AppPalette.ToolbarButtonCornerRadius, ToolbarMetrics.CornerRadius);
}
