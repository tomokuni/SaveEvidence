using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="NavPanelMetrics"/> の仕様テスト（幅と文字サイズ）。</summary>
/// <remarks>
/// 幅は 4 つの UI 版で共通に使うため、値そのものを固定する（UI ごとに固定値を書かない）。<br/>
/// 展開時の幅が「アイコン + 文字 + 余白」で決まっていることも検証する。
/// </remarks>
public class NavPanelMetricsTests
{
    [Fact]
    internal void ExpandedWidth_はアイコンと文字と余白の合計()
        => Assert.Equal(
            (NavPanelMetrics.ItemHorizontalPadding * 2) + NavPanelMetrics.IconSize
                + NavPanelMetrics.IconSpacing + NavPanelMetrics.LabelWidth + NavPanelMetrics.TrailingSlack,
            NavPanelMetrics.ExpandedWidth);

    [Fact]
    internal void ExpandedWidth_は従来の180より狭い()
        => Assert.InRange(NavPanelMetrics.ExpandedWidth, 100, 150);

    [Fact]
    internal void CompactWidth_は左右の余白とアイコンの合計()
        => Assert.Equal((NavPanelMetrics.ItemHorizontalPadding * 2) + NavPanelMetrics.IconSize, NavPanelMetrics.CompactWidth);

    [Fact]
    internal void Width_は展開状態で切り替わる()
    {
        Assert.Equal(NavPanelMetrics.ExpandedWidth, NavPanelMetrics.Width(true));
        Assert.Equal(NavPanelMetrics.CompactWidth, NavPanelMetrics.Width(false));
    }

    [Fact]
    internal void ItemFontSize_はAvaloniaUI版の項目と同じ14()
        => Assert.Equal(14, NavPanelMetrics.ItemFontSize);
}
