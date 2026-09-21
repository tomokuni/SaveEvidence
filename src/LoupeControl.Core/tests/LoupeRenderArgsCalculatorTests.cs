using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace Estable.Loupe.Tests;

/// <summary><see cref="LoupeRenderArgsCalculator"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 描画完了イベント引数の表示情報（ピクセルサイズ・切り出しサイズ・切り出し左上・スケール）計算のみを検証するため、<br/>
/// UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// イベント発火は UI 側のテスト（ヘッドレス直列）が担う。
/// </remarks>
public class LoupeRenderArgsCalculatorTests
{
    [Fact]
    public void Compute_ピクセルサイズは表示サイズにスケーリング係数を掛けた値の切り上げになる()
    {
        // size=100, renderScaling=1.5 → ceil(150) = 150
        var args = LoupeRenderArgsCalculator.Compute(100, 1.5, 4, new EsPoint(50, 50));

        Assert.Equal(150, args.PixelSize);
    }

    [Fact]
    public void Compute_ピクセルサイズは最低1になる()
    {
        // size=0 → Math.Max(1, 0) = 1
        var args = LoupeRenderArgsCalculator.Compute(0, 1.0, 4, new EsPoint(0, 0));

        Assert.Equal(1, args.PixelSize);
    }

    [Fact]
    public void Compute_切り出しサイズはピクセルサイズをズームレベルで割った値を切り上げる()
    {
        // pixelSize=160, zoomLevel=4 → ceil(160/4) = 40
        var args = LoupeRenderArgsCalculator.Compute(160, 1.0, 4, new EsPoint(50, 50));

        Assert.Equal(40, args.CaptureSize);
    }

    [Fact]
    public void Compute_切り出し左上は中心から切り出しサイズの半分を引いた位置になる()
    {
        // center=(100,200), captureSize=40 → (100 - 20, 200 - 20) = (80, 180)
        var args = LoupeRenderArgsCalculator.Compute(160, 1.0, 4, new EsPoint(100, 200));

        Assert.Equal(new EsPoint(80, 180), args.CaptureTopLeft);
    }

    [Fact]
    public void Compute_中心未設定では切り出し左上はnullになる()
    {
        var args = LoupeRenderArgsCalculator.Compute(160, 1.0, 4, center: null);

        Assert.Null(args.CaptureTopLeft);
    }

    [Fact]
    public void Compute_スケールはサイズにズームレベルを掛けてピクセルサイズで割った値になる()
    {
        // size=160, zoom=4, pixelSize=160 → 160*4/160 = 4
        var args = LoupeRenderArgsCalculator.Compute(160, 1.0, 4, new EsPoint(50, 50));

        Assert.Equal(4.0, args.Scale, 6);
    }
}
