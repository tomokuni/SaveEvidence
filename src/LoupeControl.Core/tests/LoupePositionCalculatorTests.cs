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
            requestedSize: 400, parentSize: new EsSize(300, 200),
            centerDip: new EsPoint(0, 0), offset: 0);

        Assert.Equal(200.0, size.Width);
        Assert.Equal(200.0, size.Height);
    }

    [Fact]
    public void Compute_親パネルサイズより小さい基本サイズは維持される()
    {
        // 基本 100、親パネル 300x200 → 100 を維持
        var (size, _) = LoupePositionCalculator.Compute(
            requestedSize: 100, parentSize: new EsSize(300, 200),
            centerDip: new EsPoint(0, 0), offset: 0);

        Assert.Equal(100.0, size.Width);
        Assert.Equal(100.0, size.Height);
    }

    [Fact]
    public void Compute_親パネルサイズ0なら基本サイズをそのまま使う()
    {
        // 親パネルサイズ 0（未確定）→ クランプせず基本サイズ 120
        var (size, _) = LoupePositionCalculator.Compute(
            requestedSize: 120, parentSize: new EsSize(0, 0),
            centerDip: new EsPoint(50, 50), offset: 10);

        Assert.Equal(120.0, size.Width);
        Assert.Equal(120.0, size.Height);
    }

    [Fact]
    public void Compute_位置は中心とオフセットの合計になる()
    {
        // 中心 (50, 50) + オフセット 20 → (70, 70)（親パネル 300x300 内でクランプされない）
        var (_, position) = LoupePositionCalculator.Compute(
            requestedSize: 100, parentSize: new EsSize(300, 300),
            centerDip: new EsPoint(50, 50), offset: 20);

        Assert.Equal(70.0, position.X);
        Assert.Equal(70.0, position.Y);
    }

    [Fact]
    public void Compute_親パネル右下を超える場合はクランプされる()
    {
        // サイズ 100、親パネル 300x300 → 位置の最大値は 300 - 100 = 200
        var (_, position) = LoupePositionCalculator.Compute(
            requestedSize: 100, parentSize: new EsSize(300, 300),
            centerDip: new EsPoint(290, 290), offset: 50);

        Assert.Equal(200.0, position.X);
        Assert.Equal(200.0, position.Y);
    }

    [Fact]
    public void Compute_負の位置は0にクランプされる()
    {
        var (_, position) = LoupePositionCalculator.Compute(
            requestedSize: 100, parentSize: new EsSize(300, 300),
            centerDip: new EsPoint(0, 0), offset: -50);

        Assert.Equal(0.0, position.X);
        Assert.Equal(0.0, position.Y);
    }

    [Fact]
    public void Compute_親パネルサイズ0なら位置をクランプしない()
    {
        // 親パネルサイズ 0（未確定）→ クランプせず中心 + オフセットをそのまま返す
        var (_, position) = LoupePositionCalculator.Compute(
            requestedSize: 120, parentSize: new EsSize(0, 0),
            centerDip: new EsPoint(50, 50), offset: 10);

        Assert.Equal(60.0, position.X);
        Assert.Equal(60.0, position.Y);
    }

    [Fact]
    public void ComputePosition_位置は中心とオフセットの合計になる()
    {
        // 中心 (50, 50) + オフセット 20 → (70, 70)（親パネル 300x300 内でクランプされない）
        var position = LoupePositionCalculator.ComputePosition(
            displaySize: 100, parentSize: new EsSize(300, 300),
            centerDip: new EsPoint(50, 50), offset: 20);

        Assert.Equal(70.0, position.X);
        Assert.Equal(70.0, position.Y);
    }

    [Fact]
    public void ComputePosition_親パネル右下を超える場合はクランプされる()
    {
        // 表示サイズ 100、親パネル 300x300 → 位置の最大値は 300 - 100 = 200
        var position = LoupePositionCalculator.ComputePosition(
            displaySize: 100, parentSize: new EsSize(300, 300),
            centerDip: new EsPoint(290, 290), offset: 50);

        Assert.Equal(200.0, position.X);
        Assert.Equal(200.0, position.Y);
    }

    [Fact]
    public void ComputePosition_負の位置は0にクランプされる()
    {
        var position = LoupePositionCalculator.ComputePosition(
            displaySize: 100, parentSize: new EsSize(300, 300),
            centerDip: new EsPoint(0, 0), offset: -50);

        Assert.Equal(0.0, position.X);
        Assert.Equal(0.0, position.Y);
    }

    [Fact]
    public void ComputePosition_親パネルサイズ0なら位置をクランプしない()
    {
        // 親パネルサイズ 0（未確定）→ クランプせず中心 + オフセットをそのまま返す
        var position = LoupePositionCalculator.ComputePosition(
            displaySize: 120, parentSize: new EsSize(0, 0),
            centerDip: new EsPoint(50, 50), offset: 10);

        Assert.Equal(60.0, position.X);
        Assert.Equal(60.0, position.Y);
    }

    [Fact]
    public void ComputePosition_表示サイズが親パネルより大きくても負の位置にしない()
    {
        // 表示サイズが親パネル（100）より大きい場合は最大値が 0 になり、負の位置へはならない
        var position = LoupePositionCalculator.ComputePosition(
            displaySize: 300, parentSize: new EsSize(100, 100),
            centerDip: new EsPoint(0, 0), offset: -50);

        Assert.Equal(0.0, position.X);
        Assert.Equal(0.0, position.Y);
    }

    [Fact]
    public void ClampSizeToArea_領域より大きいサイズは最小辺へクランプする()
    {
        // 領域 300x200 → 最小辺 200 にクランプする
        Assert.Equal(200.0, LoupePositionCalculator.ClampSizeToArea(400, new EsSize(300, 200)));
    }

    [Fact]
    public void ClampSizeToArea_領域より小さいサイズはそのまま返す()
    {
        Assert.Equal(120.0, LoupePositionCalculator.ClampSizeToArea(120, new EsSize(300, 200)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 200)]
    [InlineData(300, 0)]
    [InlineData(-1, -1)]
    public void ClampSizeToArea_領域が未確定ならクランプしない(double width, double height)
    {
        // 幅または高さが 0 以下の場合は「未確定」として扱い、サイズをそのまま返す
        Assert.Equal(500.0, LoupePositionCalculator.ClampSizeToArea(500, new EsSize(width, height)));
    }

    [Theory]
    [InlineData(100, 1.0, 100)]
    [InlineData(100, 1.5, 150)]
    [InlineData(33, 1.1, 37)]
    [InlineData(160.4, 1.0, 161)]
    public void ResolvePixelSize_表示サイズとスケーリングから物理ピクセルを求める(double size, double scaling, int expected)
    {
        Assert.Equal(expected, LoupePositionCalculator.ResolvePixelSize(size, scaling));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ResolvePixelSize_最低1になる(double size)
    {
        Assert.Equal(1, LoupePositionCalculator.ResolvePixelSize(size, 1.0));
    }

    [Fact]
    public void ResolvePixelSize_描画先の領域を指定するとクランプされる()
    {
        // 160 DIP × 2.0 = 320 ピクセル → 描画先 200×200 に合わせて 200
        Assert.Equal(200, LoupePositionCalculator.ResolvePixelSize(160, 2.0, new EsSize(200, 200)));
    }

    [Fact]
    public void ResolvePixelSize_領域が未確定ならクランプしない()
    {
        Assert.Equal(320, LoupePositionCalculator.ResolvePixelSize(160, 2.0, new EsSize(0, 0)));
        Assert.Equal(320, LoupePositionCalculator.ResolvePixelSize(160, 2.0));
    }
}
