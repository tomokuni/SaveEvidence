using Xunit;

namespace EsUtil.Feature.MarqueeCore.Tests;

/// <summary><see cref="Marquee.Limits"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 数値パラメータの既定値と、未設定（0 以下）の解決規則のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// UI の依存関係プロパティ経由の反映は UI 側のテスト（ヘッドレス直列）が担う。
/// </remarks>
public class MarqueeLimitsTests
{
    // ====================================================================
    // DomainToDipScaling（既定 1.0）
    // ====================================================================

    /// <summary>倍率の既定値が等倍であることを検証する。</summary>
    [Fact]
    public void DomainToDipScalingDefault_等倍である()
    {
        Assert.Equal(1.0, Marquee.Limits.DomainToDipScalingDefault);
    }

    // ====================================================================
    // ResolveDisplayScale（0 以下は倍率を使う）
    // ====================================================================

    /// <summary>正の指定はそのまま使うことを検証する。</summary>
    /// <param name="displayScaling">指定する表示スケール。</param>
    [Theory]
    [InlineData(1.25)]
    [InlineData(2.0)]
    [InlineData(0.5)]
    public void ResolveDisplayScale_正の指定はそのまま使う(double displayScaling)
    {
        Assert.Equal(displayScaling, Marquee.Limits.ResolveDisplayScale(displayScaling, 1.0));
    }

    /// <summary>0 以下は未指定として倍率を使うことを検証する。</summary>
    /// <param name="displayScaling">指定する表示スケール。</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void ResolveDisplayScale_0以下は倍率を使う(double displayScaling)
    {
        Assert.Equal(1.25, Marquee.Limits.ResolveDisplayScale(displayScaling, 1.25));
    }

    /// <summary>0 以下で倍率も無効な場合は基準値（等倍）になることを検証する。</summary>
    /// <param name="scaling">指定する倍率。</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ResolveDisplayScale_倍率も無効なら等倍になる(double scaling)
    {
        Assert.Equal(1.0, Marquee.Limits.ResolveDisplayScale(displayScaling: 0.0, scaling));
    }
}
