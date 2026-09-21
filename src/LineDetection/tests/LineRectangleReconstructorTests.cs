using EsUtil.Platform.Core.Geometry;
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

    /// <summary>範囲（Y の開始〜終了）を持つ垂直線の結果を生成する。</summary>
    private static LineDetectionResult VerticalSpan(double x, double y1, double y2)
        => new(new EsPoint(x, y1), new EsPoint(x, y2), LineOrientation.Vertical, Math.Abs(y2 - y1));

    /// <summary>範囲（X の開始〜終了）を持つ水平線の結果を生成する。</summary>
    private static LineDetectionResult HorizontalSpan(double y, double x1, double x2)
        => new(new EsPoint(x1, y), new EsPoint(x2, y), LineOrientation.Horizontal, Math.Abs(x2 - x1));

    [Fact]
    public void Reconstruct_上下の垂直線が重ならない組み合わせは候補にしない()
    {
        // 左辺（Y 0〜100）と右辺（Y 500〜900）は共通の Y 範囲を持たないため矩形を構成しない
        var results = new[]
        {
            VerticalSpan(100, 0, 100), VerticalSpan(300, 500, 900),
            HorizontalLine(50), HorizontalLine(200),
        };

        Assert.Empty(LineRectangleReconstructor.Reconstruct(results));
    }

    [Fact]
    public void Reconstruct_左右の範囲を覆わない水平線は辺に使わない()
    {
        // 左辺・右辺は揃っているが、上辺（Y=50）が右端（X=300）まで届かず矩形を構成できない。
        // 届いている下辺（Y=200）だけでは辺が 1 本しかないため候補は空になる
        var results = new[]
        {
            VerticalSpan(100, 0, 1000), VerticalSpan(300, 0, 1000),
            HorizontalSpan(50, 0, 150), HorizontalSpan(200, 0, 1000),
        };

        Assert.Empty(LineRectangleReconstructor.Reconstruct(results));
    }

    [Fact]
    public void Reconstruct_分断された辺は範囲を合併して矩形を構成する()
    {
        // 左辺（X=100）が Y 0〜400 と Y 600〜1000 に分断されていても、範囲を合併して 1 本の辺として扱う
        var results = new[]
        {
            VerticalSpan(100, 0, 400), VerticalSpan(100, 600, 1000), VerticalSpan(300, 0, 1000),
            HorizontalLine(50), HorizontalLine(200),
        };

        var rect = Assert.Single(LineRectangleReconstructor.Reconstruct(results));
        Assert.Equal(new EsPixelRect(100, 50, 200, 150), rect);
    }

    [Fact]
    public void Reconstruct_候補数は上限を超えず面積の小さい順に返す()
    {
        // 縦横とも全域を覆う線分（40 本ずつ）は組み合わせが 608,400 通りになるが、上限で抑えられる
        var results = new List<LineDetectionResult>();
        for (var i = 0; i < 40; i++)
        {
            results.Add(VerticalSpan(100 + i * 10, 0, 1000));
            results.Add(HorizontalSpan(100 + i * 10, 0, 1000));
        }

        var rects = LineRectangleReconstructor.Reconstruct(results);

        Assert.Equal(256, rects.Count);
        for (var i = 1; i < rects.Count; i++)
        {
            Assert.True(
                (long)rects[i - 1].Width * rects[i - 1].Height <= (long)rects[i].Width * rects[i].Height,
                "面積が昇順になっていない");
        }

        // 面積の小さい順に残す（最小は隣接する線分どうしの 10 x 10）
        Assert.Equal(10, rects[0].Width);
        Assert.Equal(10, rects[0].Height);
    }

    [Fact]
    public void Reconstruct_線分が多い画像でも上限内で完了する()
    {
        // 実画面の検出結果に相当する規模（水平 200 本・垂直 60 本）。
        // 全組み合わせを候補にすると 3,500 万件（実測 7 秒・3.5 GB）になる規模だが、上限内で完了する
        var results = new List<LineDetectionResult>();
        for (var i = 0; i < 200; i++)
        {
            results.Add(HorizontalSpan(100 + i * 7, 0, 3840));
        }

        for (var i = 0; i < 60; i++)
        {
            results.Add(VerticalSpan(100 + i * 11, 0, 2160));
        }

        var rects = LineRectangleReconstructor.Reconstruct(results);

        Assert.Equal(256, rects.Count);
        Assert.All(rects, r => Assert.True(r.Width > 0 && r.Height > 0));
    }
}
