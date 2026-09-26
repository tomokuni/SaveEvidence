using EsUtil.Feature.LoupeCore.Decoration;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.LoupeCore.Tests.Decoration;

/// <summary><see cref="LoupeDecorationGeometry"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 十字線・外枠の描画矩形の計算のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 各 UI（Avalonia / MewUI）の描画は <see cref="EsRect"/> を自身の描画 API へ渡すだけであり、<br/>
/// 本テストが両 UI の見た目を保証する（矩形計算の単一所有）。
/// </remarks>
public class LoupeDecorationGeometryTests
{
    /// <summary>十字線が縦横 2 本の矩形で返ることを検証する。</summary>
    [Fact]
    public void ComputeCrosshairRects_十字線の縦横2本の矩形を返す()
    {
        // pixelSize=100, zoomLevel=4 → blocks = ceil(100/4) = 25、start = 25/2*4 = 48
        var rects = LoupeDecorationGeometry.ComputeCrosshairRects(100, 4);

        Assert.Equal(2, rects.Count);
        Assert.Equal(new EsRect(48, 0, 4, 100), rects[0]); // 縦
        Assert.Equal(new EsRect(0, 48, 100, 4), rects[1]); // 横
    }

    /// <summary>拡大率 1 の場合は中央を 1 ピクセルで描画することを検証する。</summary>
    [Fact]
    public void ComputeCrosshairRects_zoomLevel1では中央を1ピクセルで描画する()
    {
        // pixelSize=100, zoomLevel=1 → blocks = 100、start = 100/2*1 = 50
        var rects = LoupeDecorationGeometry.ComputeCrosshairRects(100, 1);

        Assert.Equal(new EsRect(50, 0, 1, 100), rects[0]);
        Assert.Equal(new EsRect(0, 50, 100, 1), rects[1]);
    }

    /// <summary>外枠が 4 辺（上下左右）の矩形で返ることを検証する。</summary>
    [Fact]
    public void ComputeFrameRects_外枠の4辺の矩形を返す()
    {
        // 一辺 100・width=2 → 上 = 100×2、内側 = 100-4 = 96
        var rects = LoupeDecorationGeometry.ComputeFrameRects(new EsRect(0, 0, 100, 100), 2.0);

        Assert.Equal(4, rects.Count);
        Assert.Equal(new EsRect(0, 0, 100, 2), rects[0]);   // 上
        Assert.Equal(new EsRect(0, 98, 100, 2), rects[1]);  // 下
        Assert.Equal(new EsRect(0, 2, 2, 96), rects[2]);    // 左
        Assert.Equal(new EsRect(98, 2, 2, 96), rects[3]);   // 右
    }

    /// <summary>外枠の太さが描画面より大きい場合は面の短辺で上限を取り、内側が 0 になることを検証する。</summary>
    /// <remarks>太さの安全域への補正は設定（<c>LoupeOptions.FrameWidth</c> の生成時）が担う。</remarks>
    [Fact]
    public void ComputeFrameRects_幅が描画面を超えるなら内側が0になる()
    {
        var rects = LoupeDecorationGeometry.ComputeFrameRects(new EsRect(0, 0, 100, 100), 200.0);

        Assert.Equal(new EsRect(0, 0, 100, 100), rects[0]);  // 上（全面）
        Assert.Equal(new EsRect(0, 0, 100, 100), rects[1]);  // 下（全面）
        Assert.Equal(new EsRect(0, 100, 100, 0), rects[2]);  // 左（高さ 0）
        Assert.Equal(new EsRect(0, 100, 100, 0), rects[3]);  // 右（高さ 0）
    }
}
