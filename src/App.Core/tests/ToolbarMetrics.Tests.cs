using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="ToolbarMetrics"/> の仕様テスト（ツールバーの文字サイズ）。</summary>
public class ToolbarMetricsTests
{
    [Fact]
    internal void FontSize_は4UI版で共通の12()
        => Assert.Equal(12, ToolbarMetrics.FontSize);

    [Fact]
    internal void FontSize_は設定ページの本文より小さい()
        => Assert.True(ToolbarMetrics.FontSize < SettingsMetrics.FontSize);
}
