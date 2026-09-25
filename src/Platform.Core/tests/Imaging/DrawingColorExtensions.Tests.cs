using EsUtil.Platform.Core.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Platform.Core.Tests;

/// <summary><see cref="DrawingColorExtensions"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>「アルファ 0 は描かない」の判定を固定する（RGB は判定に使わない）。</remarks>
public class DrawingColorExtensionsTests
{
    [Theory]
    [InlineData(255, 255, 255, 255)]
    [InlineData(128, 0, 0, 0)]
    [InlineData(1, 0, 0, 0)]
    public void IsVisible_アルファが正の場合は可視とする(byte alpha, byte r, byte g, byte b)
        => Assert.True(DrawingColor.FromArgb(alpha, r, g, b).IsVisible());

    [Fact]
    public void IsVisible_アルファ0の場合は不可視とする()
    {
        Assert.False(DrawingColor.Transparent.IsVisible());
        Assert.False(DrawingColor.FromArgb(0, 255, 0, 0).IsVisible());
    }

    [Fact]
    public void IsVisible_既定値の色は不可視とする()
        => Assert.False(default(DrawingColor).IsVisible());
}
