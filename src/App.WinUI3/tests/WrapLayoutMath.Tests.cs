using EsUtil.SaveEvidence.App.WinUI3.Views;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinUI3.Tests;

/// <summary><see cref="WrapLayoutMath"/> の単体テスト（一覧の列数と配置）。</summary>
public class WrapLayoutMathTests
{
    [Theory]
    [InlineData(1000, 100, 0, 10)]
    [InlineData(1045, 100, 5, 10)]
    [InlineData(1044, 100, 5, 9)]
    [InlineData(999, 100, 0, 9)]
    [InlineData(100, 100, 0, 1)]
    [InlineData(99, 100, 0, 1)]
    [InlineData(0, 100, 0, 1)]
    [InlineData(-10, 100, 0, 1)]
    [InlineData(double.NaN, 100, 0, 1)]
    [InlineData(1000, 0, 0, 1)]
    internal void Columns_幅に収まる列数を返す(double width, double itemWidth, double spacing, int expected)
        => Assert.Equal(expected, WrapLayoutMath.Columns(width, itemWidth, spacing));

    [Theory]
    [InlineData(0, 3, 100, 10, 0)]
    [InlineData(1, 3, 100, 10, 110)]
    [InlineData(2, 3, 100, 10, 220)]
    [InlineData(3, 3, 100, 10, 0)]
    [InlineData(4, 3, 100, 10, 110)]
    internal void Left_列に応じた横位置を返す(int index, int columns, double itemWidth, double spacing, double expected)
        => Assert.Equal(expected, WrapLayoutMath.Left(index, columns, itemWidth, spacing));

    [Theory]
    [InlineData(0, 3, 100, 10, 0)]
    [InlineData(2, 3, 100, 10, 0)]
    [InlineData(3, 3, 100, 10, 110)]
    [InlineData(5, 3, 100, 10, 110)]
    [InlineData(6, 3, 100, 10, 220)]
    internal void Top_行に応じた縦位置を返す(int index, int columns, double itemHeight, double spacing, double expected)
        => Assert.Equal(expected, WrapLayoutMath.Top(index, columns, itemHeight, spacing));
}