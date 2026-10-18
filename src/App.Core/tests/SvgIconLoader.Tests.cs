using EsUtil.SaveEvidence.App.Core.Icons;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="SvgIconLoader"/> の仕様テスト（SVG アイコンの読み込み）。</summary>
/// <remarks>
/// アイコンの出所は本プロジェクトの <c>Icons/</c> に固定する（4 つの UI 版が同じファイルを使う）。
/// </remarks>
public class SvgIconLoaderTests
{
    [Fact]
    internal void 読み込み_はパスとviewBoxを返す()
    {
        var icon = SvgIconLoader.Load("ic_fluent_save_24_regular.svg");

        Assert.Equal("ic_fluent_save_24_regular.svg", icon.FileName);
        Assert.Equal(24, icon.ViewBoxWidth);
        Assert.Equal(24, icon.ViewBoxHeight);
        Assert.NotEmpty(icon.PathData);
        Assert.Equal(0xFF000000u, icon.FillArgb);
    }

    [Fact]
    internal void 読み込み_はナビの開閉アイコンを読める()
    {
        Assert.NotEmpty(SvgIconLoader.Load("ic_fluent_panel_left_contract_24_filled.svg").PathData);
        Assert.NotEmpty(SvgIconLoader.Load("ic_fluent_panel_right_contract_24_filled.svg").PathData);
    }

    [Fact]
    internal void 読み込み_は存在しないファイルでも空のアイコンを返す()
    {
        var icon = SvgIconLoader.Load("not_found.svg");

        Assert.Empty(icon.PathData);
    }

    [Fact]
    internal void 一覧_はアプリが使うアイコンを含む()
    {
        var names = SvgIconLoader.Enumerate();

        Assert.Contains("ic_fluent_scan_table_24_regular.svg", names);
        Assert.Contains("ic_fluent_panel_left_contract_24_filled.svg", names);
        Assert.Contains("ic_fluent_panel_right_contract_24_filled.svg", names);
    }
}