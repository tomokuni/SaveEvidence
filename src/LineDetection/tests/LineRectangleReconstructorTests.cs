using Estable.Platform.Geometry;
using Xunit;

namespace Estable.LineDetection.Tests;

/// <summary><see cref="LineRectangleReconstructor"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 検出された線分（<see cref="LineDetectionResult"/>）から矩形（ウィンドウの 4 辺）を復元するロジックのみを検証するため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する（テスト全体の高速化）。
/// </remarks>
public sealed class LineRectangleReconstructorTests
{
    /// <summary>垂直線の結果を生成する。</summary>
    private static LineDetectionResult VerticalLine(double x)
        => new(new EsPoint(x, 0), new EsPoint(x, 1000), LineOrientation.Vertical, 1000);

    /// <summary>水平線の結果を生成する。</summary>
    private static LineDetectionResult HorizontalLine(double y)
        => new(new EsPoint(0, y), new EsPoint(1000, y), LineOrientation.Horizontal, 1000);

    [Fact]
    public void Reconstruct_垂直2本と水平2本から外側の境界を復元する()
    {
        var results = new[]
        {
            VerticalLine(100), VerticalLine(300),
            HorizontalLine(50), HorizontalLine(200),
        };

        var rects = LineRectangleReconstructor.Reconstruct(results);

        var rect = Assert.Single(rects);
        Assert.Equal(new EsPixelRect(100, 50, 200, 150), rect);
    }

    [Fact]
    public void Reconstruct_垂直または水平が2本未満は空を返す()
    {
        Assert.Empty(LineRectangleReconstructor.Reconstruct([VerticalLine(100), VerticalLine(300), HorizontalLine(50)]));
        Assert.Empty(LineRectangleReconstructor.Reconstruct([VerticalLine(100), HorizontalLine(50), HorizontalLine(200)]));
        Assert.Empty(LineRectangleReconstructor.Reconstruct([]));
    }

    [Fact]
    public void Reconstruct_重複座標は統合して外側の境界を復元する()
    {
        // 重複した垂直・水平線を統合して 1 つの矩形（外側の境界）を復元する
        var results = new[]
        {
            VerticalLine(100), VerticalLine(100), VerticalLine(300),
            HorizontalLine(50), HorizontalLine(50), HorizontalLine(200),
        };

        var rects = LineRectangleReconstructor.Reconstruct(results);

        var rect = Assert.Single(rects);
        Assert.Equal(new EsPixelRect(100, 50, 200, 150), rect);
    }

    [Fact]
    public void Reconstruct_複数の矩形候補を面積昇順で返す()
    {
        // 垂直 3 本 × 水平 3 本 → 複数の矩形候補（面積昇順）
        var results = new[]
        {
            VerticalLine(100), VerticalLine(300), VerticalLine(500),
            HorizontalLine(50), HorizontalLine(200), HorizontalLine(400),
        };

        var rects = LineRectangleReconstructor.Reconstruct(results);

        // 面積が単調増加（昇順）であることを検証する
        for (var i = 1; i < rects.Count; i++)
        {
            Assert.True((long)rects[i - 1].Width * rects[i - 1].Height
                <= (long)rects[i].Width * rects[i].Height);
        }
    }

    [Fact]
    public void Reconstruct_幅または高さが1未満の矩形は除外する()
    {
        // 同一座標のペア（幅・高さ 0）は除外される
        var results = new[]
        {
            VerticalLine(100), VerticalLine(100),
            HorizontalLine(50), HorizontalLine(200),
        };

        var rects = LineRectangleReconstructor.Reconstruct(results);

        Assert.Empty(rects);
    }
}
