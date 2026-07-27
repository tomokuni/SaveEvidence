using app.Controls;
using Xunit;

namespace app.Tests;

/// <summary>
/// ScrollMetrics（スクロールバーの幾何計算・純粋関数）の回帰テスト。
/// </summary>
/// <remarks>
/// スクロールバーの範囲・ビューポートサイズ・サムサイズの計算を検証する。<br/>
/// バグ修正（スクロールバーが表示されない・範囲が正しくない）の回帰防止を目的とする。
/// </remarks>
public class ScrollMetricsTests
{
    // ====================================================================
    // MaxOffset（スクロール範囲）
    // ====================================================================

    [Fact]
    public void MaxOffset_コンテンツがビューポートより大きい場合は差分を返す()
    {
        // コンテンツ 1000、ビューポート 400、scale 1 → 1000 - 400 = 600
        var result = ScrollMetrics.MaxOffset(1000, 400, 1.0);
        Assert.Equal(600.0, result);
    }

    [Fact]
    public void MaxOffset_ズーム時はビューポートをスケールで除する()
    {
        // コンテンツ 1000、ビューポート 400、scale 2 → 1000 - 400/2 = 800
        var result = ScrollMetrics.MaxOffset(1000, 400, 2.0);
        Assert.Equal(800.0, result);
    }

    [Fact]
    public void MaxOffset_コンテンツがビューポート以下の場合は0を返す()
    {
        // コンテンツ 300、ビューポート 400 → 0（スクロール不要）
        var result = ScrollMetrics.MaxOffset(300, 400, 1.0);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void MaxOffset_スケールが0でもゼロ除算しない()
    {
        // scale 0 → 1 として扱う
        var result = ScrollMetrics.MaxOffset(1000, 400, 0.0);
        Assert.Equal(600.0, result);
    }

    // ====================================================================
    // ViewportSize（コンテンツ座標での表示範囲）
    // ====================================================================

    [Fact]
    public void ViewportSize_ビューポートをスケールで除する()
    {
        // ビューポート 400、scale 2 → 200
        var result = ScrollMetrics.ViewportSize(400, 2.0);
        Assert.Equal(200.0, result);
    }

    [Fact]
    public void ViewportSize_スケールが0でもゼロ除算しない()
    {
        var result = ScrollMetrics.ViewportSize(400, 0.0);
        Assert.Equal(400.0, result);
    }

    // ====================================================================
    // IsScrollable（スクロールバー表示判定）
    // ====================================================================

    [Fact]
    public void IsScrollable_コンテンツがビューポートより大きい場合はtrue()
    {
        Assert.True(ScrollMetrics.IsScrollable(1000, 400, 1.0));
    }

    [Fact]
    public void IsScrollable_コンテンツがビューポート以下の場合はfalse()
    {
        Assert.False(ScrollMetrics.IsScrollable(300, 400, 1.0));
    }

    [Fact]
    public void IsScrollable_ズームでビューポート相当が小さくなるとtrueになる()
    {
        // コンテンツ 500、ビューポート 400、scale 2 → 表示範囲 200 < 500 → スクロール必要
        Assert.True(ScrollMetrics.IsScrollable(500, 400, 2.0));
        // scale 1 では 400 相当... いや、500 > 400 なので true
        Assert.True(ScrollMetrics.IsScrollable(500, 400, 1.0));
    }

    // ====================================================================
    // ThumbRatio（サムのサイズ割合）
    // ====================================================================

    [Fact]
    public void ThumbRatio_コンテンツがビューポートより大きい場合は表示範囲の割合()
    {
        // コンテンツ 1000、ビューポート 400、scale 1 → 400/1000 = 0.4
        var result = ScrollMetrics.ThumbRatio(1000, 400, 1.0);
        Assert.Equal(0.4, result);
    }

    [Fact]
    public void ThumbRatio_コンテンツがビューポート以下の場合は1()
    {
        var result = ScrollMetrics.ThumbRatio(300, 400, 1.0);
        Assert.Equal(1.0, result);
    }

    [Fact]
    public void ThumbRatio_ズーム時はスケールで除したビューポートの割合()
    {
        // コンテンツ 1000、ビューポート 400、scale 2 → 表示範囲 200 → 200/1000 = 0.2
        var result = ScrollMetrics.ThumbRatio(1000, 400, 2.0);
        Assert.Equal(0.2, result);
    }
}
