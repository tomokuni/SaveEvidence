using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.LoupeCore.Tests;

/// <summary><see cref="LoupeRenderArgsCalculator"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 描画完了イベント引数の表示情報（切り出し左上・スケール）計算のみを検証するため、<br/>
/// UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// イベント発火は UI 側のテスト（ヘッドレス直列）が担う。
/// </remarks>
public class LoupeRenderArgsCalculatorTests
{
    [Fact]
    public void Compute_切り出し左上は中心から切り出しサイズの半分を引いた位置になる()
    {
        // center=(100,200), pixelSize=160, zoom=4 → captureSize=40 → (100 - 20, 200 - 20) = (80, 180)
        var args = LoupeRenderArgsCalculator.Compute(Request(pixelSize: 160, zoomLevel: 4, new EsPoint(100, 200)));

        Assert.Equal(new EsPoint(80, 180), args.CaptureTopLeft);
    }

    [Fact]
    public void Compute_中心未設定では切り出し左上はnullになる()
    {
        var args = LoupeRenderArgsCalculator.Compute(Request(pixelSize: 160, zoomLevel: 4, center: null));

        Assert.Null(args.CaptureTopLeft);
    }

    [Fact]
    public void Compute_スケールは拡大率と一致する()
    {
        // 描画は切り出し領域を zoomLevel 倍で描くため、ドメイン → 描画ピクセルの変換係数は zoomLevel
        var args = LoupeRenderArgsCalculator.Compute(Request(pixelSize: 160, zoomLevel: 4, new EsPoint(50, 50)));

        Assert.Equal(4.0, args.Scale, 6);
    }

    /// <summary>スケールが切り出しサイズの切り上げ（描画先によるクランプを含む）に影響されないことを検証する。</summary>
    /// <remarks>
    /// 実描画は切り出し領域（切り出しサイズのドメインピクセル）を <c>zoomLevel</c> 倍で描くため、<br/>
    /// 変換係数は切り出しサイズで割った値（<c>物理ピクセル / 切り出しサイズ</c>）ではなく <c>zoomLevel</c> になる。
    /// </remarks>
    [Theory]
    [InlineData(160)]
    [InlineData(150)]
    [InlineData(200)]
    public void Compute_スケールは切り出しサイズの切り上げに影響されない(int pixelSize)
    {
        var args = LoupeRenderArgsCalculator.Compute(Request(pixelSize, zoomLevel: 4, new EsPoint(50, 50)));

        Assert.Equal(4.0, args.Scale, 6);
    }

    /// <summary>スケールが DPI スケーリング係数に依存しないことを検証する。</summary>
    /// <param name="renderScaling">表示 DIP → 物理ピクセルのスケーリング係数。</param>
    /// <remarks>
    /// 物理ピクセル数は DIP とスケーリングに依存するが、描画の倍率（ドメイン → 描画ピクセル）は変わらない。<br/>
    /// 破線オーバーレイの位置が DPI でずれないことを保証する。
    /// </remarks>
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(2.0)]
    public void Compute_スケールはDPIスケーリングに依存しない(double renderScaling)
    {
        var pixelSize = DpiScaler.ScaleToPixel(160, renderScaling);
        var args = LoupeRenderArgsCalculator.Compute(Request(pixelSize, zoomLevel: 4, new EsPoint(50, 50)));

        Assert.Equal(4.0, args.Scale, 6);
    }

    /// <summary>テスト用の描画入力を生成する（外観とスケーリングは既定値）。</summary>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <param name="center">拡大中心（ドメイン座標）。</param>
    /// <returns>描画入力。</returns>
    private static LoupeRenderRequest Request(int pixelSize, int zoomLevel, EsPoint? center)
        => new(center, pixelSize, zoomLevel, new LoupeMetrics(), 1.0);
}
