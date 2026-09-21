using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace Estable.Loupe.Tests;

/// <summary><see cref="LoupePositionCalculator"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// ルーペの表示サイズ・位置計算のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 表示位置（<c>RenderTransform</c>）への反映は UI 側のテスト（ヘッドレス直列）が担う。
/// </remarks>
public class LoupePositionCalculatorTests
{
    [Fact]
    public void Compute_サイズは基本サイズを親パネルの最小辺にクランプする()
    {
        // 基本 400、親パネル 300x200 → 最小辺 200 にクランプ
        var (size, _) = LoupePositionCalculator.Compute(
            requestedSize: 400, parentSize: new EsSize(300, 200), hasParentSize: true,
            centerDip: new EsPoint(0, 0), offset: 0);

        Assert.Equal(200.0, size.Width);
        Assert.Equal(200.0, size.Height);
    }

    [Fact]
    public void Compute_親パネルサイズより小さい基本サイズは維持される()
    {
        // 基本 100、親パネル 300x200 → 100 を維持
        var (size, _) = LoupePositionCalculator.Compute(
            requestedSize: 100, parentSize: new EsSize(300, 200), hasParentSize: true,
            centerDip: new EsPoint(0, 0), offset: 0);

        Assert.Equal(100.0, size.Width);
        Assert.Equal(100.0, size.Height);
    }

    [Fact]
    public void Compute_親パネルサイズ未確定なら基本サイズをそのまま使う()
    {
        // hasParentSize=false（サイズ 0）→ クランプせず基本サイズ 120
        var (size, _) = LoupePositionCalculator.Compute(
            requestedSize: 120, parentSize: new EsSize(0, 0), hasParentSize: false,
            centerDip: new EsPoint(50, 50), offset: 10);

        Assert.Equal(120.0, size.Width);
        Assert.Equal(120.0, size.Height);
    }

    [Fact]
    public void Compute_位置は中心とオフセットの合計になる()
    {
        // 中心 (50, 50) + オフセット 20 → (70, 70)（親パネル 300x300 内でクランプされない）
        var (_, position) = LoupePositionCalculator.Compute(
            requestedSize: 100, parentSize: new EsSize(300, 300), hasParentSize: true,
            centerDip: new EsPoint(50, 50), offset: 20);

        Assert.Equal(70.0, position.X);
        Assert.Equal(70.0, position.Y);
    }

    [Fact]
    public void Compute_親パネル右下を超える場合はクランプされる()
    {
        // サイズ 100、親パネル 300x300 → 位置の最大値は 300 - 100 = 200
        var (_, position) = LoupePositionCalculator.Compute(
            requestedSize: 100, parentSize: new EsSize(300, 300), hasParentSize: true,
            centerDip: new EsPoint(290, 290), offset: 50);

        Assert.Equal(200.0, position.X);
        Assert.Equal(200.0, position.Y);
    }

    [Fact]
    public void Compute_負の位置は0にクランプされる()
    {
        var (_, position) = LoupePositionCalculator.Compute(
            requestedSize: 100, parentSize: new EsSize(300, 300), hasParentSize: true,
            centerDip: new EsPoint(0, 0), offset: -50);

        Assert.Equal(0.0, position.X);
        Assert.Equal(0.0, position.Y);
    }

    [Fact]
    public void Compute_親パネルサイズ未確定なら位置をクランプしない()
    {
        // hasParentSize=false → クランプせず中心 + オフセットをそのまま返す
        var (_, position) = LoupePositionCalculator.Compute(
            requestedSize: 120, parentSize: new EsSize(0, 0), hasParentSize: false,
            centerDip: new EsPoint(50, 50), offset: 10);

        Assert.Equal(60.0, position.X);
        Assert.Equal(60.0, position.Y);
    }
}
