using Xunit;

namespace EsUtil.Feature.LoupeCore.Tests;

/// <summary><see cref="Loupe.Limits"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 数値パラメータの安全域への補正（clamp）のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// UI の <c>StyledProperty</c> coerce 経由の反映は UI 側のテスト（ヘッドレス直列）が担う。
/// </remarks>
public class LoupeLimitsTests
{
    // ====================================================================
    // RequestedSize（1〜4096、既定 160）
    // ====================================================================

    [Fact]
    public void CoerceRequestedSize_範囲内はそのまま返す()
    {
        Assert.Equal(160, Loupe.Limits.CoerceRequestedSize(160));
    }

    [Fact]
    public void CoerceRequestedSize_下限未満は1に補正する()
    {
        Assert.Equal(1, Loupe.Limits.CoerceRequestedSize(0));
        Assert.Equal(1, Loupe.Limits.CoerceRequestedSize(-100));
    }

    [Fact]
    public void CoerceRequestedSize_上限超過は4096に補正する()
    {
        Assert.Equal(4096, Loupe.Limits.CoerceRequestedSize(5000));
    }

    // ====================================================================
    // ZoomLevel（1〜64、既定 4）
    // ====================================================================

    [Fact]
    public void CoerceZoomLevel_範囲内はそのまま返す()
    {
        Assert.Equal(4, Loupe.Limits.CoerceZoomLevel(4));
    }

    [Fact]
    public void CoerceZoomLevel_下限未満は1に補正する()
    {
        Assert.Equal(1, Loupe.Limits.CoerceZoomLevel(0));
    }

    [Fact]
    public void CoerceZoomLevel_上限超過は64に補正する()
    {
        Assert.Equal(64, Loupe.Limits.CoerceZoomLevel(100));
    }

    // ====================================================================
    // RenderScaling（0.1〜8.0、既定 1.0）
    // ====================================================================

    [Fact]
    public void CoerceRenderScaling_範囲内はそのまま返す()
    {
        Assert.Equal(2.0, Loupe.Limits.CoerceDipToDeviceScaling(2.0));
    }

    [Fact]
    public void CoerceRenderScaling_範囲外は最小値と最大値に補正する()
    {
        Assert.Equal(0.1, Loupe.Limits.CoerceDipToDeviceScaling(0.0));
        Assert.Equal(8.0, Loupe.Limits.CoerceDipToDeviceScaling(10.0));
    }

    [Fact]
    public void CoerceRenderScaling_非有限値は既定値に補正する()
    {
        Assert.Equal(1.0, Loupe.Limits.CoerceDipToDeviceScaling(double.NaN));
        Assert.Equal(1.0, Loupe.Limits.CoerceDipToDeviceScaling(double.PositiveInfinity));
    }

    // ====================================================================
    // CheckerSize（1〜128、既定 6）
    // ====================================================================

    [Fact]
    public void CoerceCheckerSize_範囲内はそのまま返す()
    {
        Assert.Equal(6, Loupe.Limits.CoerceCheckerSize(6));
    }

    [Fact]
    public void CoerceCheckerSize_範囲外は最小値と最大値に補正する()
    {
        Assert.Equal(1, Loupe.Limits.CoerceCheckerSize(0));
        Assert.Equal(128, Loupe.Limits.CoerceCheckerSize(200));
    }

    // ====================================================================
    // FrameWidth（1〜64、既定 2）
    // ====================================================================

    [Fact]
    public void CoerceFrameWidth_範囲内はそのまま返す()
    {
        Assert.Equal(2, Loupe.Limits.CoerceFrameWidth(2));
    }

    [Fact]
    public void CoerceFrameWidth_範囲外は最小値と最大値に補正する()
    {
        Assert.Equal(1, Loupe.Limits.CoerceFrameWidth(0));
        Assert.Equal(64, Loupe.Limits.CoerceFrameWidth(100));
    }
}
