using EsUtil.Feature.LoupeCore.Decoration;
using Xunit;

namespace EsUtil.Feature.LoupeCore.Tests;

/// <summary><see cref="LoupeMetricsScaler"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 外観値（DIP）の物理ピクセル換算のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 両 UI（Avalonia / MewUI）は本換算結果を使う（UI ごとに換算すると DPI で見た目がずれる）。<br/>
/// 中心座標ラベルは DIP のまま UI 要素として配置するため、本クラスの対象外である。
/// </remarks>
public class LoupeMetricsScalerTests
{
    /// <summary>市松模様のタイルサイズがスケーリング係数に追従することを検証する。</summary>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <param name="expected">期待する物理ピクセルサイズ。</param>
    [Theory]
    [InlineData(1.0, 8)]
    [InlineData(1.25, 10)]
    [InlineData(1.5, 12)]
    [InlineData(2.0, 16)]
    public void ScaleCheckerSize_スケーリング係数に追従する(double scaling, int expected)
        => Assert.Equal(expected, LoupeMetricsScaler.ScaleCheckerSize(new LoupeMetrics { CheckerSize = 8 }, scaling));

    /// <summary>安全域外の指定が型（<see cref="LoupeMetrics"/>）で補正済みの値として換算されることを検証する。</summary>
    [Fact]
    public void ScaleCheckerSize_安全域外の指定も補正済みの値で換算する()
    {
        // CheckerSize=0 → 外観が最小 1 へ補正 → 2 倍で 2 ピクセル
        Assert.Equal(2, LoupeMetricsScaler.ScaleCheckerSize(new LoupeMetrics { CheckerSize = 0 }, 2.0));
        // CheckerSize=1000 → 外観が最大 128 へ補正 → 2 倍で 256 ピクセル
        Assert.Equal(256, LoupeMetricsScaler.ScaleCheckerSize(new LoupeMetrics { CheckerSize = 1000 }, 2.0));
    }

    /// <summary>外枠の太さがスケーリング係数に追従する（切り上げ・最低 1）ことを検証する。</summary>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <param name="expected">期待する物理ピクセルサイズ。</param>
    [Theory]
    [InlineData(1.0, 2)]
    [InlineData(1.25, 3)]
    [InlineData(2.0, 4)]
    public void ScaleFrameWidth_スケーリング係数に追従する(double scaling, int expected)
        => Assert.Equal(expected, LoupeMetricsScaler.ScaleFrameWidth(new LoupeMetrics { FrameWidth = 2 }, scaling));

    /// <summary>安全域外の指定が型で補正済みの値として換算されることを検証する。</summary>
    [Fact]
    public void ScaleFrameWidth_安全域外の指定も補正済みの値で換算する()
    {
        // FrameWidth=0 → 外観が最小 1 へ補正 → 1 倍で 1 ピクセル
        Assert.Equal(1, LoupeMetricsScaler.ScaleFrameWidth(new LoupeMetrics { FrameWidth = 0 }, 1.0));
        // FrameWidth=1000 → 外観が最大 64 へ補正 → 2 倍で 128 ピクセル
        Assert.Equal(128, LoupeMetricsScaler.ScaleFrameWidth(new LoupeMetrics { FrameWidth = 1000 }, 2.0));
    }
}
