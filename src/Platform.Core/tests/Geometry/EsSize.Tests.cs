using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary><see cref="EsSize"/> の領域判定とクランプのテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 「領域が未確定ならクランプしない」の判定と、軸ごとのクランプを固定する。<br/>
/// レイアウト前の領域（幅・高さが 0）で値を勝手に動かさないことが要件である。
/// </remarks>
public class EsSizeTests
{
    // ====================================================================
    // HasArea（領域の確定判定）
    // ====================================================================

    [Theory]
    [InlineData(200, 100, true)]
    [InlineData(1, 1, true)]
    [InlineData(0, 100, false)]
    [InlineData(200, 0, false)]
    [InlineData(-1, -1, false)]
    public void HasArea_幅と高さがともに正の場合のみ確定とする(double width, double height, bool expected)
        => Assert.Equal(expected, new EsSize(width, height).HasArea);

    // ====================================================================
    // ClampToArea（サイズのクランプ）
    // ====================================================================

    [Theory]
    [InlineData(160, 200, 150, 150)]
    [InlineData(160, 100, 100, 100)]
    [InlineData(100, 200, 150, 100)]
    public void ClampToArea_領域の最小辺を上限とする(double size, double areaWidth, double areaHeight, double expected)
        => Assert.Equal(expected, new EsSize(areaWidth, areaHeight).ClampToArea(size));

    [Theory]
    [InlineData(0, 100)]
    [InlineData(0, 0)]
    [InlineData(200, 0)]
    public void ClampToArea_領域が未確定ならクランプしない(double areaWidth, double areaHeight)
        => Assert.Equal(160, new EsSize(areaWidth, areaHeight).ClampToArea(160));

    // ====================================================================
    // ClampOffset（位置のクランプ）
    // ====================================================================

    [Theory]
    [InlineData(100, 40)]
    [InlineData(-50, 0)]
    [InlineData(0, 0)]
    public void ClampOffset_領域内へ収まる範囲にクランプする(double offset, double expected)
        => Assert.Equal(expected, new EsSize(200, 100).ClampOffset(offset, size: 160, EsAxis.X));

    [Fact]
    public void ClampOffset_軸を指定して幅と高さを使い分ける()
    {
        var area = new EsSize(200, 100);

        // 幅 200 では上限 40、高さ 100 では上限 0（対象 160 が収まらない）
        Assert.Equal(40, area.ClampOffset(100, size: 160, EsAxis.X));
        Assert.Equal(0, area.ClampOffset(100, size: 160, EsAxis.Y));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(0, 0)]
    [InlineData(-20, 100)]
    public void ClampOffset_対象の軸の長さが0以下ならクランプしない(double areaWidth, double areaHeight)
        => Assert.Equal(100, new EsSize(areaWidth, areaHeight).ClampOffset(100, size: 160, EsAxis.X));

    [Fact]
    public void ClampOffset_軸ごとに確定を判定する()
    {
        // 幅だけが確定している領域では、高さのクランプは行わない（軸ごとに判定する）
        var area = new EsSize(200, 0);

        Assert.Equal(40, area.ClampOffset(100, size: 160, EsAxis.X));
        Assert.Equal(100, area.ClampOffset(100, size: 160, EsAxis.Y));
    }

    [Fact]
    public void ClampOffset_対象が領域より大きい場合は0へ寄せる()
        => Assert.Equal(0, new EsSize(100, 100).ClampOffset(50, size: 200, EsAxis.X));

    [Fact]
    public void ClampOffset_未定義の軸は例外()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new EsSize(200, 100).ClampOffset(10, size: 5, (EsAxis)99));
}
