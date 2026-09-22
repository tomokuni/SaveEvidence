using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace Estable.Loupe.Tests;

/// <summary><see cref="LoupeRenderRequest"/> の組み立て（UI 非依存・並列実行）のテスト。</summary>
/// <remarks>
/// 表示サイズ（DIP）からの物理ピクセルサイズの解決と、描画先によるクランプのみを検証するため、<br/>
/// UI 初期化に依存せず <c>[Fact]</c> で並列実行する（両 UI が同じ手順で描画入力を組み立てることを保証する）。
/// </remarks>
public class LoupeRenderRequestTests
{
    /// <summary>表示サイズが物理ピクセルへ換算されることを検証する。</summary>
    /// <param name="displaySize">表示サイズ（DIP）。</param>
    /// <param name="renderScaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <param name="expectedPixelSize">期待する物理ピクセルサイズ。</param>
    [Theory]
    [InlineData(160, 1.0, 160)]
    [InlineData(160, 1.25, 200)]
    [InlineData(160, 2.0, 320)]
    [InlineData(0.4, 1.0, 1)]
    public void Create_表示サイズを物理ピクセルへ換算する(double displaySize, double renderScaling, int expectedPixelSize)
    {
        var request = LoupeRenderRequest.Create(
            center: new EsPoint(10, 20), displaySize, renderScaling, new LoupeOptions());

        Assert.Equal(expectedPixelSize, request.PixelSize);
    }

    /// <summary>描画先の領域が指定された場合はクランプされることを検証する。</summary>
    [Fact]
    public void Create_描画先の領域へクランプする()
    {
        var request = LoupeRenderRequest.Create(
            center: null, displaySize: 640, renderScaling: 1.0, new LoupeOptions(),
            availableSize: new EsSize(200, 150));

        // 領域の最小辺（150）へクランプされる
        Assert.Equal(150, request.PixelSize);
    }

    /// <summary>描画先の領域が未確定（0 以下）の場合はクランプしないことを検証する。</summary>
    [Fact]
    public void Create_描画先が未確定ならクランプしない()
    {
        var request = LoupeRenderRequest.Create(
            center: null, displaySize: 160, renderScaling: 1.0, new LoupeOptions(),
            availableSize: new EsSize(0, 0));

        Assert.Equal(160, request.PixelSize);
    }

    /// <summary>中心・拡大率・外観・スケーリングがそのまま保持されることを検証する。</summary>
    [Fact]
    public void Create_入力値をそのまま保持する()
    {
        var appearance = new LoupeAppearance { CheckerSize = 8 };
        var options = new LoupeOptions { ZoomLevel = 8, Appearance = appearance };

        var request = LoupeRenderRequest.Create(new EsPoint(3, 4), 100, 1.5, options);

        Assert.Equal(new EsPoint(3, 4), request.Center);
        Assert.Equal(8, request.ZoomLevel);
        Assert.Equal(1.5, request.Scaling);
        Assert.Same(appearance, request.Appearance);
    }

    /// <summary>中心が未設定（null）でも組み立てられることを検証する。</summary>
    [Fact]
    public void Create_中心未設定でも組み立てられる()
    {
        var request = LoupeRenderRequest.Create(center: null, 100, 1.0, new LoupeOptions());

        Assert.Null(request.Center);
    }
}
