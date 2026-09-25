using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary><see cref="EsSizeMath"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 「領域が未確定ならクランプしない」の判定と、軸ごとのクランプを固定する。<br/>
/// レイアウト前の領域（幅・高さが 0）で値を勝手に動かさないことが要件である。
/// </remarks>
public class EsSizeMathTests
{
    [Theory]
    [InlineData(200, 100, true)]
    [InlineData(1, 1, true)]
    [InlineData(0, 100, false)]
    [InlineData(200, 0, false)]
    [InlineData(-1, -1, false)]
    public void HasArea_幅と高さがともに正の場合のみ確定とする(double width, double height, bool expected)
        => Assert.Equal(expected, EsSizeMath.HasArea(new EsSize(width, height)));

    [Theory]
    [InlineData(160, 200, 150, 150)]
    [InlineData(160, 100, 100, 100)]
    [InlineData(100, 200, 150, 100)]
    public void ClampToArea_領域の最小辺を上限とする(double size, double areaWidth, double areaHeight, double expected)
        => Assert.Equal(expected, EsSizeMath.ClampToArea(size, new EsSize(areaWidth, areaHeight)));

    [Theory]
    [InlineData(0, 100)]
    [InlineData(0, 0)]
    [InlineData(200, 0)]
    public void ClampToArea_領域が未確定ならクランプしない(double areaWidth, double areaHeight)
        => Assert.Equal(160, EsSizeMath.ClampToArea(160, new EsSize(areaWidth, areaHeight)));

    [Theory]
    [InlineData(100, 200, 160, 40)]
    [InlineData(-50, 200, 160, 0)]
    [InlineData(0, 200, 160, 0)]
    public void ClampOffsetToArea_領域内へ収まる範囲にクランプする(double offset, double areaLength, double size, double expected)
        => Assert.Equal(expected, EsSizeMath.ClampOffsetToArea(offset, areaLength, size));

    [Theory]
    [InlineData(0)]
    [InlineData(-20)]
    public void ClampOffsetToArea_領域が未確定ならクランプしない(double areaLength)
        => Assert.Equal(100, EsSizeMath.ClampOffsetToArea(100, areaLength, size: 160));

    [Fact]
    public void ClampOffsetToArea_対象が領域より大きい場合は0へ寄せる()
        => Assert.Equal(0, EsSizeMath.ClampOffsetToArea(50, areaLength: 100, size: 200));
}
