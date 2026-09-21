using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;

namespace Estable.MewUI.Loupe.Tests;

/// <summary><see cref="LoupeRenderedEventArgs"/>（ルーペの描画完了通知）のテスト。</summary>
/// <remarks>
/// イベントの発火は実際の描画（ウィンドウ）を必要とするため、引数の契約（保持する値）のみを検証する。<br/>
/// 描画完了時のオーバーレイ描画はホスト（<c>FadeMaskDragRectControl.MewUI</c>）のテストが担う。
/// </remarks>
public class LoupeRenderedEventArgsTests
{
    /// <summary>保持した値がそのまま取得できることを検証する。</summary>
    [Fact]
    public void 保持した値を取得できる()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(32, 32));
        using var canvas = new SKCanvas(bitmap);
        var center = new EsPoint(10, 20);
        var topLeft = new EsPoint(4, 6);

        var args = new LoupeRenderedEventArgs(canvas, 32, 8, center, topLeft, 2.5);

        Assert.Same(canvas, args.Canvas);
        Assert.Equal(32, args.PixelSize);
        Assert.Equal(8, args.ZoomLevel);
        Assert.Equal(center, args.CenterPoint);
        Assert.Equal(topLeft, args.CaptureTopLeft);
        Assert.Equal(2.5, args.Scale);
    }

    /// <summary>中心が未設定の場合は切り出し左上と中心が null になることを検証する。</summary>
    [Fact]
    public void 中心が未設定の場合は切り出し左上がnullになる()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(32, 32));
        using var canvas = new SKCanvas(bitmap);

        var args = new LoupeRenderedEventArgs(canvas, 32, 4, null, null, 1.0);

        Assert.Null(args.CenterPoint);
        Assert.Null(args.CaptureTopLeft);
    }
}
