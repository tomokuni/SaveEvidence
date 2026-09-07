using Estable.AvaloniaUI.Loupe;
using Xunit;

namespace Estable.AvaloniaUI.Loupe.Tests;

/// <summary><see cref="LoupeValueCoercer"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 数値プロパティの安全域への補正（clamp）のみを検証するため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// <c>StyledProperty</c> の coerce 経由の反映は <see cref="LoupeControlTests"/>（ヘッドレス直列）が担う。
/// </remarks>
public class LoupeValueCoercerTests
{
    // ====================================================================
    // RequestedSize（1〜4096、既定 160）
    // ====================================================================

    [Fact]
    public void CoerceRequestedSize_範囲内はそのまま返す()
    {
        Assert.Equal(160, LoupeValueCoercer.CoerceRequestedSize(160));
    }

    [Fact]
    public void CoerceRequestedSize_下限未満は1に補正する()
    {
        Assert.Equal(1, LoupeValueCoercer.CoerceRequestedSize(0));
        Assert.Equal(1, LoupeValueCoercer.CoerceRequestedSize(-100));
    }

    [Fact]
    public void CoerceRequestedSize_上限超過は4096に補正する()
    {
        Assert.Equal(4096, LoupeValueCoercer.CoerceRequestedSize(5000));
    }

    // ====================================================================
    // ZoomLevel（1〜64、既定 4）
    // ====================================================================

    [Fact]
    public void CoerceZoomLevel_範囲内はそのまま返す()
    {
        Assert.Equal(4, LoupeValueCoercer.CoerceZoomLevel(4));
    }

    [Fact]
    public void CoerceZoomLevel_下限未満は1に補正する()
    {
        Assert.Equal(1, LoupeValueCoercer.CoerceZoomLevel(0));
    }

    [Fact]
    public void CoerceZoomLevel_上限超過は64に補正する()
    {
        Assert.Equal(64, LoupeValueCoercer.CoerceZoomLevel(100));
    }

    // ====================================================================
    // RenderScaling（0.1〜8.0、既定 1.0）
    // ====================================================================

    [Fact]
    public void CoerceRenderScaling_範囲内はそのまま返す()
    {
        Assert.Equal(2.0, LoupeValueCoercer.CoerceRenderScaling(2.0));
    }

    [Fact]
    public void CoerceRenderScaling_範囲外は最小値と最大値に補正する()
    {
        Assert.Equal(0.1, LoupeValueCoercer.CoerceRenderScaling(0.0));
        Assert.Equal(8.0, LoupeValueCoercer.CoerceRenderScaling(10.0));
    }

    [Fact]
    public void CoerceRenderScaling_非有限値は既定値に補正する()
    {
        Assert.Equal(1.0, LoupeValueCoercer.CoerceRenderScaling(double.NaN));
        Assert.Equal(1.0, LoupeValueCoercer.CoerceRenderScaling(double.PositiveInfinity));
    }

    // ====================================================================
    // CheckerSize（1〜128、既定 6）
    // ====================================================================

    [Fact]
    public void CoerceCheckerSize_範囲内はそのまま返す()
    {
        Assert.Equal(6, LoupeValueCoercer.CoerceCheckerSize(6));
    }

    [Fact]
    public void CoerceCheckerSize_範囲外は最小値と最大値に補正する()
    {
        Assert.Equal(1, LoupeValueCoercer.CoerceCheckerSize(0));
        Assert.Equal(128, LoupeValueCoercer.CoerceCheckerSize(200));
    }

    // ====================================================================
    // FrameWidth（1〜64、既定 2）
    // ====================================================================

    [Fact]
    public void CoerceFrameWidth_範囲内はそのまま返す()
    {
        Assert.Equal(2, LoupeValueCoercer.CoerceFrameWidth(2));
    }

    [Fact]
    public void CoerceFrameWidth_範囲外は最小値と最大値に補正する()
    {
        Assert.Equal(1, LoupeValueCoercer.CoerceFrameWidth(0));
        Assert.Equal(64, LoupeValueCoercer.CoerceFrameWidth(100));
    }

    // ====================================================================
    // CoordLabelFontSize（6〜48、既定 12.0）
    // ====================================================================

    [Fact]
    public void CoerceCoordLabelFontSize_範囲内はそのまま返す()
    {
        Assert.Equal(12.0, LoupeValueCoercer.CoerceCoordLabelFontSize(12.0));
    }

    [Fact]
    public void CoerceCoordLabelFontSize_範囲外は最小値と最大値に補正する()
    {
        Assert.Equal(6.0, LoupeValueCoercer.CoerceCoordLabelFontSize(1.0));
        Assert.Equal(48.0, LoupeValueCoercer.CoerceCoordLabelFontSize(100.0));
    }

    [Fact]
    public void CoerceCoordLabelFontSize_非有限値は既定値に補正する()
    {
        Assert.Equal(12.0, LoupeValueCoercer.CoerceCoordLabelFontSize(double.NaN));
    }
}
