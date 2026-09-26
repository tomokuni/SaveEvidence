using EsUtil.SaveEvidence.Common.Imaging;
using Xunit;

namespace EsUtil.SaveEvidence.Common.Tests.Imaging;

/// <summary><see cref="EsLabelMetrics"/> のフォントサイズ補正テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// ラベルのフォントサイズの安全域（6〜48）と「非数は既定値」の規則を固定する。<br/>
/// ラベルごとに既定値だけが異なるため、既定値が引数で正しく反映されることも検証する。
/// </remarks>
public class EsLabelMetricsTests
{
    [Theory]
    [InlineData(6.0, 6.0)]
    [InlineData(12.0, 12.0)]
    [InlineData(48.0, 48.0)]
    [InlineData(20.5, 20.5)]
    public void CoerceFontSize_安全域内はそのまま(double value, double expected)
    {
        Assert.Equal(expected, EsLabelMetrics.CoerceFontSize(value, defaultValue: 12.0));
    }

    [Theory]
    [InlineData(5.9, 6.0)]
    [InlineData(0.0, 6.0)]
    [InlineData(-100.0, 6.0)]
    public void CoerceFontSize_最小未満は最小へ補正する(double value, double expected)
    {
        Assert.Equal(expected, EsLabelMetrics.CoerceFontSize(value, defaultValue: 12.0));
    }

    [Theory]
    [InlineData(48.1, 48.0)]
    [InlineData(1000.0, 48.0)]
    public void CoerceFontSize_最大超は最大へ補正する(double value, double expected)
    {
        Assert.Equal(expected, EsLabelMetrics.CoerceFontSize(value, defaultValue: 12.0));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void CoerceFontSize_非数は既定値になる(double value)
    {
        Assert.Equal(12.0, EsLabelMetrics.CoerceFontSize(value, defaultValue: 12.0));
        Assert.Equal(14.0, EsLabelMetrics.CoerceFontSize(value, defaultValue: 14.0));
    }

    [Fact]
    public void FontSizeの安全域は6から48()
    {
        Assert.Equal(6.0, EsLabelMetrics.FontSizeMin);
        Assert.Equal(48.0, EsLabelMetrics.FontSizeMax);
    }
}
