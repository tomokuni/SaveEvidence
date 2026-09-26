using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.LoupeCore.Setting;
using EsUtil.Feature.LoupeCore.Drawing;

namespace EsUtil.Feature.LoupeCore.Tests;

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

    /// <summary>描画先の全領域が原点 0 の正方形になることを検証する。</summary>
    [Fact]
    public void SurfaceBounds_物理ピクセルサイズの正方形を原点0で返す()
    {
        var request = LoupeRenderRequest.Create(new EsPoint(3, 4), 100, 1.25, new LoupeOptions());

        // 下地・市松模様は描画先の全面へ描くため、矩形は契約が単一所有する（UI 側で組み立てない）
        Assert.Equal(125, request.PixelSize);
        Assert.Equal(new EsRect(0, 0, 125, 125), request.SurfaceBounds);
    }

    /// <summary>中心・拡大率・外観・スケーリングがそのまま保持されることを検証する。</summary>
    [Fact]
    public void Create_入力値をそのまま保持する()
    {
        var appearance = new LoupeMetrics { CheckerSize = 8 };
        var options = new LoupeOptions { ZoomLevel = 8, Metrics = appearance };

        var request = LoupeRenderRequest.Create(new EsPoint(3, 4), 100, 1.5, options);

        Assert.Equal(new EsPoint(3, 4), request.Center);
        Assert.Equal(8, request.ZoomLevel);
        Assert.Equal(1.5, request.Scaling);
        Assert.Same(appearance, request.Metrics);
    }

    /// <summary>中心が未設定（null）でも組み立てられることを検証する。</summary>
    [Fact]
    public void Create_中心未設定でも組み立てられる()
    {
        var request = LoupeRenderRequest.Create(center: null, 100, 1.0, new LoupeOptions());

        Assert.Null(request.Center);
    }

    /// <summary>表示サイズとスケーリングから物理ピクセルを求めることを検証する。</summary>
    /// <param name="size">表示サイズ（DIP）。</param>
    /// <param name="scaling">スケーリング係数。</param>
    /// <param name="expected">期待する物理ピクセルサイズ。</param>
    [Theory]
    [InlineData(100, 1.0, 100)]
    [InlineData(100, 1.5, 150)]
    [InlineData(33, 1.1, 37)]
    [InlineData(160.4, 1.0, 161)]
    public void ResolvePixelSize_表示サイズとスケーリングから物理ピクセルを求める(double size, double scaling, int expected)
        => Assert.Equal(expected, LoupeRenderRequest.ResolvePixelSize(size, scaling));

    /// <summary>換算結果の下限が 1 になることを検証する。</summary>
    /// <param name="size">表示サイズ（DIP）。</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ResolvePixelSize_最低1になる(double size)
        => Assert.Equal(1, LoupeRenderRequest.ResolvePixelSize(size, 1.0));

    /// <summary>描画先の領域を指定すると最小辺へクランプされることを検証する。</summary>
    [Fact]
    public void ResolvePixelSize_描画先の領域を指定するとクランプされる()
    {
        // 160 DIP × 2.0 = 320 ピクセル → 描画先 200×200 に合わせて 200
        Assert.Equal(200, LoupeRenderRequest.ResolvePixelSize(160, 2.0, new EsSize(200, 200)));
    }

    /// <summary>描画先の領域が未確定ならクランプしないことを検証する。</summary>
    [Fact]
    public void ResolvePixelSize_領域が未確定ならクランプしない()
    {
        Assert.Equal(320, LoupeRenderRequest.ResolvePixelSize(160, 2.0, new EsSize(0, 0)));
        Assert.Equal(320, LoupeRenderRequest.ResolvePixelSize(160, 2.0));
    }
}
