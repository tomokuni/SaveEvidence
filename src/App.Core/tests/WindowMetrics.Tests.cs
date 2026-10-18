using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="WindowMetrics"/> の仕様テスト（ウィンドウの寸法）。</summary>
/// <remarks>
/// 寸法は 4 つの UI 版で共通に使うため、値そのものを固定する（UI ごとに固定値を書かない）。
/// </remarks>
public class WindowMetricsTests
{
    [Fact]
    internal void 最小サイズ_は700x400()
    {
        Assert.Equal(700, WindowMetrics.MinimumWidth);
        Assert.Equal(400, WindowMetrics.MinimumHeight);
    }

    [Fact]
    internal void 既定サイズ_は700x400()
    {
        Assert.Equal(700, WindowMetrics.DefaultWidth);
        Assert.Equal(400, WindowMetrics.DefaultHeight);
    }

    [Fact]
    internal void 既定サイズ_は最小サイズ以上()
    {
        Assert.True(WindowMetrics.DefaultWidth >= WindowMetrics.MinimumWidth);
        Assert.True(WindowMetrics.DefaultHeight >= WindowMetrics.MinimumHeight);
    }

    [Fact]
    internal void 最小幅_は収納時のナビ領域より広い()
        => Assert.True(WindowMetrics.MinimumWidth > NavPanelMetrics.CompactWidth);
}