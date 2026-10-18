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

    /// <summary>項目の高さが 4 つの UI 版で同じ値（40 DIP）であることを検証する。</summary>
    /// <remarks>開閉・文字の有無で高さが変わると、項目の位置が UI 版ごとにずれる。</remarks>
    [Fact]
    internal void ItemHeight_は4つのUI版で共通の40()
        => Assert.Equal(40, NavPanelMetrics.ItemHeight);

    /// <summary>選択中のマークの寸法が 4 つの UI 版で同じであることを検証する。</summary>
    [Fact]
    internal void SelectionMark_は幅3で左端4()
    {
        Assert.Equal(3, NavPanelMetrics.SelectionMarkWidth);
        Assert.Equal(4, NavPanelMetrics.SelectionMarkLeft);
    }
}
