using Xunit;

namespace EsUtil.SaveEvidence.CropView.Tests;

/// <summary><see cref="OverlayLineDash"/> の仕様テスト（破線パターンの単一所有）。</summary>
/// <remarks>
/// ベクター描画とラスタ描画が同じダッシュ長を共有することを検証する（片方だけ変更される事故の防止）。<br/>
/// UI 初期化を必要としないため、Avalonia ヘッドレスに依存せず並列実行できる。
/// </remarks>
public class OverlayLineDashTests
{
    [Fact]
    public void Get_実線はダッシュ長と間隔が0になる()
    {
        var (dashLength, gapLength) = OverlayLineDash.Get(OverlayLineStyle.Solid);

        Assert.Equal(0.0, dashLength);
        Assert.Equal(0.0, gapLength);
    }

    [Fact]
    public void Get_破線は8と4になる()
    {
        var (dashLength, gapLength) = OverlayLineDash.Get(OverlayLineStyle.Dashed);

        Assert.Equal(8.0, dashLength);
        Assert.Equal(4.0, gapLength);
    }

    [Fact]
    public void Get_点線は2と2になる()
    {
        var (dashLength, gapLength) = OverlayLineDash.Get(OverlayLineStyle.Dotted);

        Assert.Equal(2.0, dashLength);
        Assert.Equal(2.0, gapLength);
    }

    [Fact]
    public void Get_定義済みの全スタイルでダッシュ長は非負になる()
    {
        foreach (var style in Enum.GetValues<OverlayLineStyle>())
        {
            var (dashLength, gapLength) = OverlayLineDash.Get(style);

            Assert.True(dashLength >= 0, $"{style} のダッシュ長が負です。");
            Assert.True(gapLength >= 0, $"{style} の間隔が負です。");
        }
    }

    [Theory]
    [InlineData(OverlayLineStyle.Solid, false)]
    [InlineData(OverlayLineStyle.Dashed, true)]
    [InlineData(OverlayLineStyle.Dotted, true)]
    public void IsDashed_実線のみfalseになる(OverlayLineStyle style, bool expected)
    {
        Assert.Equal(expected, OverlayLineDash.IsDashed(style));
    }

    [Fact]
    public void SolidLength_実線の定数は0である()
    {
        Assert.Equal(0.0, OverlayLineDash.SolidLength);
    }
}
