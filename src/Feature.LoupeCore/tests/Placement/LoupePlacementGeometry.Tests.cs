using EsUtil.Feature.LoupeCore.Placement;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.LoupeCore.Tests.Placement;

/// <summary><see cref="LoupePlacementGeometry.ComputePosition"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// ルーペの表示位置計算のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 表示位置（<c>RenderTransform</c>）への反映は UI 側のテスト（ヘッドレス直列）が担う。
/// </remarks>
public class LoupePlacementGeometryTests
{
    [Fact]
    public void ComputePosition_位置は中心とオフセットの合計になる()
    {
        // 中心 (50, 50) + オフセット 20 → (70, 70)（親パネル 300x300 内でクランプされない）
        var position = LoupePlacementGeometry.ComputePosition(
            displaySize: 100, parentSize: new EsSize(300, 300),
            centerDip: new EsPoint(50, 50), offset: 20);

        Assert.Equal(70.0, position.X);
        Assert.Equal(70.0, position.Y);
    }

    [Fact]
    public void ComputePosition_親パネル右下を超える場合はクランプされる()
    {
        // 表示サイズ 100、親パネル 300x300 → 位置の最大値は 300 - 100 = 200
        var position = LoupePlacementGeometry.ComputePosition(
            displaySize: 100, parentSize: new EsSize(300, 300),
            centerDip: new EsPoint(290, 290), offset: 50);

        Assert.Equal(200.0, position.X);
        Assert.Equal(200.0, position.Y);
    }

    [Fact]
    public void ComputePosition_負の位置は0にクランプされる()
    {
        var position = LoupePlacementGeometry.ComputePosition(
            displaySize: 100, parentSize: new EsSize(300, 300),
            centerDip: new EsPoint(0, 0), offset: -50);

        Assert.Equal(0.0, position.X);
        Assert.Equal(0.0, position.Y);
    }

    [Fact]
    public void ComputePosition_親パネルサイズ0なら位置をクランプしない()
    {
        // 親パネルサイズ 0（未確定）→ クランプせず中心 + オフセットをそのまま返す
        var position = LoupePlacementGeometry.ComputePosition(
            displaySize: 120, parentSize: new EsSize(0, 0),
            centerDip: new EsPoint(50, 50), offset: 10);

        Assert.Equal(60.0, position.X);
        Assert.Equal(60.0, position.Y);
    }

    [Fact]
    public void ComputePosition_表示サイズが親パネルより大きくても負の位置にしない()
    {
        // 表示サイズが親パネル（100）より大きい場合は最大値が 0 になり、負の位置へはならない
        var position = LoupePlacementGeometry.ComputePosition(
            displaySize: 300, parentSize: new EsSize(100, 100),
            centerDip: new EsPoint(0, 0), offset: -50);

        Assert.Equal(0.0, position.X);
        Assert.Equal(0.0, position.Y);
    }
}
