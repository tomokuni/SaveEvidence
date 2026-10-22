using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="WindowPlacementRule"/> の仕様テスト（復元可否・表示状態・はみ出しの補正）。</summary>
public class WindowPlacementRuleTests
{
    [Fact]
    internal void WindowMetrics_最小外寸と既定外寸を共有する()
    {
        Assert.Equal(640, WindowMetrics.MinimumWidth);
        Assert.Equal(400, WindowMetrics.MinimumHeight);
        Assert.Equal(1200, WindowMetrics.DefaultWidth);
        Assert.Equal(800, WindowMetrics.DefaultHeight);
    }

    [Fact]
    internal void CanRestore_画面内の配置は復元できる()
    {
        Assert.True(WindowPlacementRule.CanRestore(
            new EsRectInt(100, 100, 800, 400), new EsRectInt(0, 0, 1920, 1080)));
    }

    [Fact]
    internal void CanRestore_画面外の配置は復元しない()
    {
        Assert.False(WindowPlacementRule.CanRestore(
            new EsRectInt(-32000, -32000, 800, 400), new EsRectInt(0, 0, 1920, 1080)));
    }

    [Fact]
    internal void CanRestore_重なりが最小未満なら復元しない()
    {
        // 右端が表示領域から 63 ピクセルだけ内側（64 未満）
        Assert.False(WindowPlacementRule.CanRestore(
            new EsRectInt(-800 + 63, 100, 800, 400), new EsRectInt(0, 0, 1920, 1080)));
    }

    [Fact]
    internal void CanRestore_壊れたサイズは復元しない()
    {
        Assert.False(WindowPlacementRule.CanRestore(null, new EsRectInt(0, 0, 1920, 1080)));
        Assert.False(WindowPlacementRule.CanRestore(new EsRectInt(0, 0, 0, 400), new EsRectInt(0, 0, 1920, 1080)));
    }

    [Fact]
    internal void ToRestorableState_最小化は通常表示にする()
    {
        Assert.Equal(EsWindowState.Normal, WindowPlacementRule.ToRestorableState(EsWindowState.Minimized));
        Assert.Equal(EsWindowState.Maximized, WindowPlacementRule.ToRestorableState(EsWindowState.Maximized));
        Assert.Equal(EsWindowState.Normal, WindowPlacementRule.ToRestorableState(EsWindowState.Normal));
    }

    [Fact]
    internal void ClampToVisibleArea_はみ出した位置だけを内側へ収める()
    {
        var clamped = WindowPlacementRule.ClampToVisibleArea(
            new EsRectInt(-500, -200, 800, 400), new EsRectInt(0, 0, 1920, 1080));

        Assert.Equal(0, clamped.X);
        Assert.Equal(0, clamped.Y);
        Assert.Equal(800, clamped.Width);
        Assert.Equal(400, clamped.Height);
    }

    [Fact]
    internal void ClampToVisibleArea_画面より大きい場合は左上を表示領域へ合わせる()
    {
        var clamped = WindowPlacementRule.ClampToVisibleArea(
            new EsRectInt(100, 100, 3000, 2000), new EsRectInt(0, 0, 1920, 1080));

        Assert.Equal(0, clamped.X);
        Assert.Equal(0, clamped.Y);
    }

    [Fact]
    internal void ToPhysicalLength_DPI倍率を掛ける()
    {
        Assert.Equal(800, WindowPlacementRule.ToPhysicalLength(800, 1.0));
        Assert.Equal(1000, WindowPlacementRule.ToPhysicalLength(800, 1.25));
        Assert.Equal(1200, WindowPlacementRule.ToPhysicalLength(800, 1.5));
    }

    [Fact]
    internal void ToClientLength_外寸DIPから物理枠を差し引く()
        => Assert.Equal(627.2, WindowPlacementRule.ToClientLength(640.0, 16, 1.25), 5);

    [Fact]
    internal void ToOuterPhysicalLength_クライアントDIPへ物理枠を加える()
        => Assert.Equal(800, WindowPlacementRule.ToOuterPhysicalLength(627.2, 16, 1.25));

    [Fact]
    internal void ToClientLength_外寸物理ピクセルから物理枠を差し引く()
        => Assert.Equal(627.2, WindowPlacementRule.ToClientLength(800, 16, 1.25), 5);
}
