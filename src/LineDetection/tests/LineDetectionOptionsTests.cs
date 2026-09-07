using Xunit;

namespace Estable.AvaloniaUI.LineDetection.Tests;

/// <summary><see cref="LineDetectionOptions"/> の単体テスト（不変値・検証付き）。</summary>
/// <remarks>
/// 検出パラメータの既定値・不変条件（各パラメータの範囲・関係）を検証するため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する（テスト全体の高速化）。
/// </remarks>
public sealed class LineDetectionOptionsTests
{
    [Fact]
    public void Default_従来の定数値を持つ()
    {
        var options = LineDetectionOptions.Default;

        Assert.Equal(20, options.CannyThreshold1);
        Assert.Equal(60, options.CannyThreshold2);
        Assert.Equal(40, options.HoughThreshold);
        Assert.Equal(50, options.MinLineLength);
        Assert.Equal(10, options.MaxLineGap);
        Assert.Equal(5.0, options.AngleTolerance);
        Assert.Equal(1, options.ClusterTolerance);
        Assert.Equal(0.6, options.MinEdgeSupportRatio);
        Assert.Equal(8.0, options.ParallelMergeTolerance);
        Assert.Equal(0.2, options.CornerToleranceRatio);
        Assert.Equal(0.13, options.OppositeSpanRatio);
    }

    [Fact]
    public void Validate_既定値は不変条件を満たす()
    {
        // 既定値は例外なく検証できる
        LineDetectionOptions.Default.Validate();
    }

    [Fact]
    public void Validate_不正な値は例外を投げる()
    {
        var options = LineDetectionOptions.Default with { CannyThreshold1 = 0 };
        Assert.Throws<ArgumentException>(() => options.Validate());

        var options2 = LineDetectionOptions.Default with { CannyThreshold2 = 10 };
        Assert.Throws<ArgumentException>(() => options2.Validate());

        var options3 = LineDetectionOptions.Default with { MinEdgeSupportRatio = 1.5 };
        Assert.Throws<ArgumentException>(() => options3.Validate());

        var options4 = LineDetectionOptions.Default with { AngleTolerance = 90 };
        Assert.Throws<ArgumentException>(() => options4.Validate());
    }

    [Fact]
    public void with_で一部のパラメータを差し替えられる()
    {
        var options = LineDetectionOptions.Default with { MinLineLength = 100 };

        Assert.Equal(100, options.MinLineLength);
        Assert.Equal(50, LineDetectionOptions.Default.MinLineLength);
    }
}
