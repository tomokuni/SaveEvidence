using EsUtil.Feature.LoupeCore.Checkerboard;
using Xunit;

namespace EsUtil.Feature.LoupeCore.Tests;

/// <summary><see cref="LoupeDivMath"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>切り上げ除算は「余りを 1 区画として数える」規則であり、境界（0・割り切れる値・余りあり）を固定する。</remarks>
public class LoupeDivMathTests
{
    [Theory]
    [InlineData(64, 8, 8)]
    [InlineData(100, 4, 25)]
    [InlineData(1, 1, 1)]
    public void CeilDiv_割り切れる場合は商をそのまま返す(int value, int divisor, int expected)
        => Assert.Equal(expected, LoupeDivMath.CeilDiv(value, divisor));

    [Theory]
    [InlineData(10, 4, 3)]
    [InlineData(65, 8, 9)]
    [InlineData(1, 64, 1)]
    [InlineData(0, 4, 0)]
    public void CeilDiv_余りがある場合は切り上げる(int value, int divisor, int expected)
        => Assert.Equal(expected, LoupeDivMath.CeilDiv(value, divisor));

    [Fact]
    public void CeilDiv_除数1は値そのものを返す()
        => Assert.Equal(7, LoupeDivMath.CeilDiv(7, 1));
}
