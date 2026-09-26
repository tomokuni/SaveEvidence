using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.LoupeCore.Setting;
using EsUtil.Feature.LoupeCore.Drawing;

namespace EsUtil.Feature.LoupeCore.Tests.Drawing;

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
        var options = LoupeOptions.Default with { ZoomLevel = 8, CheckerSize = 8 };

        var request = LoupeRenderRequest.Create(new EsPoint(3, 4), 100, 1.5, options);

        Assert.Equal(new EsPoint(3, 4), request.Center);
        Assert.Equal(8, request.ZoomLevel);
        Assert.Equal(1.5, request.DipToDeviceScaling);
        Assert.Equal(options, request.Options);
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

    // ====================================================================
    // 切り出し領域の導出（CaptureSize / CaptureTopLeft / MagnifiedImage）
    // ====================================================================

    /// <summary>切り出し領域の一辺がドメインピクセルの切り上げで求まることを検証する。</summary>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <param name="expected">期待する切り出し領域の一辺（ドメインピクセル）。</param>
    [Theory]
    [InlineData(100, 4, 25)]
    [InlineData(100, 3, 34)]
    [InlineData(64, 4, 16)]
    [InlineData(10, 64, 1)]
    [InlineData(16, 4, 4)]
    [InlineData(25, 4, 7)]
    [InlineData(1, 1, 1)]
    public void CaptureSize_切り上げてドメインピクセルを返す(int pixelSize, int zoomLevel, int expected)
        => Assert.Equal(expected, Request(pixelSize, zoomLevel).CaptureSize);

    /// <summary>拡大中心から切り出し領域の左上（ドメイン座標）が求まることを検証する。</summary>
    /// <param name="centerX">拡大中心のX座標。</param>
    /// <param name="centerY">拡大中心のY座標。</param>
    /// <param name="size">切り出し領域の一辺（ドメインピクセル）。物理ピクセルサイズは <c>size × zoomLevel</c> で与える。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <param name="expectedLeft">期待する左上のX座標。</param>
    /// <param name="expectedTop">期待する左上のY座標。</param>
    [Theory]
    [InlineData(50, 50, 4, 4, 48, 48)]
    [InlineData(0, 0, 4, 4, -2, -2)]
    [InlineData(101, 99, 25, 25, 89, 87)]
    public void CaptureTopLeft_中心を左上として指定サイズ領域を計算する(
        int centerX, int centerY, int size, int zoomLevel, int expectedLeft, int expectedTop)
    {
        // 切り出し領域の一辺が size になるよう、物理ピクセルサイズを size × 拡大率で与える
        var topLeft = Request(new EsPoint(centerX, centerY), size * zoomLevel, zoomLevel).CaptureTopLeft;

        Assert.NotNull(topLeft);
        Assert.Equal(expectedLeft, (int)topLeft.Value.X);
        Assert.Equal(expectedTop, (int)topLeft.Value.Y);
    }

    /// <summary>拡大中心が未設定の場合は切り出し領域の左上が null（描画しない）になることを検証する。</summary>
    [Fact]
    public void CaptureTopLeft_中心未設定ならnullになる()
    {
        Assert.Null(Request(center: null, 32, 4).CaptureTopLeft);
        Assert.Null(Request(center: null, 32, 4).MagnifiedImage);
    }

    /// <summary>拡大画像の切り出し・描画元・描画先の矩形が求まることを検証する。</summary>
    /// <remarks>中心 (10, 10)・pixelSize=16・zoom=4 → 切り出し 4 px、左上 (8, 8)、描画元 4 px、描画先 16 px。</remarks>
    [Fact]
    public void MagnifiedImage_切り出し領域と描画元と描画先を解決する()
    {
        var image = Assert.NotNull(Request(new EsPoint(10, 10), 16, 4).MagnifiedImage);

        Assert.Equal(new EsRectInt(8, 8, 4, 4), image.Crop);
        Assert.Equal(new EsRect(0, 0, 4, 4), image.Source);
        Assert.Equal(new EsRect(0, 0, 16, 16), image.Destination);
    }

    /// <summary>画像端では切り出し領域が負の左上になることを検証する（透明パディングは切り出し側が担う）。</summary>
    [Fact]
    public void MagnifiedImage_画像端では切り出し左上が負になる()
    {
        var image = Assert.NotNull(Request(new EsPoint(0, 0), 16, 4).MagnifiedImage);

        Assert.Equal(new EsRectInt(-2, -2, 4, 4), image.Crop);
    }

    /// <summary>描画先が切り出しサイズ × 拡大率になることを検証する（切り上げ分だけ表面より大きくなりうる）。</summary>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <param name="expectedDestination">期待する描画先の一辺。</param>
    [Theory]
    [InlineData(16, 4, 16)]
    [InlineData(25, 4, 28)]
    [InlineData(1, 1, 1)]
    public void MagnifiedImage_描画先は切り出しサイズの拡大率倍になる(
        int pixelSize, int zoomLevel, int expectedDestination)
    {
        var image = Assert.NotNull(Request(new EsPoint(100, 100), pixelSize, zoomLevel).MagnifiedImage);

        Assert.Equal(new EsRect(0, 0, expectedDestination, expectedDestination), image.Destination);
    }

    /// <summary>テスト用の描画入力を生成する（表示サイズ＝物理ピクセル・スケーリング 1.0・外観は既定）。</summary>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <returns>描画入力。</returns>
    private static LoupeRenderRequest Request(int pixelSize, int zoomLevel)
        => Request(new EsPoint(50, 50), pixelSize, zoomLevel);

    /// <summary>テスト用の描画入力を生成する（表示サイズ＝物理ピクセル・スケーリング 1.0・外観は既定）。</summary>
    /// <param name="center">拡大中心（ドメイン座標）。未設定は null。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <returns>描画入力。</returns>
    private static LoupeRenderRequest Request(EsPoint? center, int pixelSize, int zoomLevel)
        => new(center, pixelSize, pixelSize, LoupeOptions.Default with { ZoomLevel = zoomLevel }, 1.0, CheckerSizePixels: 6, FrameWidthPixels: 2);
}
