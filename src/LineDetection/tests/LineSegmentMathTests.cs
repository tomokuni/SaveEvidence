using Xunit;

namespace Estable.LineDetection.Tests;

/// <summary><see cref="LineSegmentMath"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 線分（<see cref="LineSeg"/>）の座標計算（生成・長さ・垂直方向座標・平行方向範囲）のみを検証するため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する（テスト全体の高速化）。
/// </remarks>
public sealed class LineSegmentMathTests
{
    // ====================================================================
    // MakeLine
    // ====================================================================

    [Fact]
    public void MakeLine_水平線分を生成する()
    {
        var seg = LineSegmentMath.MakeLine(10, 50, perp: 20, isHorizontal: true);

        Assert.Equal(new LineSeg(10, 20, 50, 20), seg);
    }

    [Fact]
    public void MakeLine_垂直線分を生成する()
    {
        var seg = LineSegmentMath.MakeLine(10, 50, perp: 30, isHorizontal: false);

        Assert.Equal(new LineSeg(30, 10, 30, 50), seg);
    }

    // ====================================================================
    // Length
    // ====================================================================

    [Fact]
    public void Length_水平線分の長さを計算する()
    {
        var seg = new LineSeg(10, 20, 40, 20);

        Assert.Equal(30, LineSegmentMath.Length(seg));
    }

    [Fact]
    public void Length_斜め線分の長さを計算する()
    {
        var seg = new LineSeg(0, 0, 3, 4);

        Assert.Equal(5, LineSegmentMath.Length(seg));
    }

    // ====================================================================
    // PerpCoord
    // ====================================================================

    [Fact]
    public void PerpCoord_水平線分はY座標を返す()
    {
        var seg = new LineSeg(10, 30, 50, 30);

        Assert.Equal(30, LineSegmentMath.PerpCoord(seg, isHorizontal: true));
    }

    [Fact]
    public void PerpCoord_垂直線分はX座標を返す()
    {
        var seg = new LineSeg(40, 10, 40, 50);

        Assert.Equal(40, LineSegmentMath.PerpCoord(seg, isHorizontal: false));
    }

    // ====================================================================
    // ParallelStart / ParallelEnd
    // ====================================================================

    [Fact]
    public void ParallelStart_水平線分は小さなX座標を返す()
    {
        var seg = new LineSeg(50, 20, 10, 20);

        Assert.Equal(10, LineSegmentMath.ParallelStart(seg, isHorizontal: true));
        Assert.Equal(50, LineSegmentMath.ParallelEnd(seg, isHorizontal: true));
    }

    [Fact]
    public void ParallelStart_垂直線分は小さなY座標を返す()
    {
        var seg = new LineSeg(30, 50, 30, 10);

        Assert.Equal(10, LineSegmentMath.ParallelStart(seg, isHorizontal: false));
        Assert.Equal(50, LineSegmentMath.ParallelEnd(seg, isHorizontal: false));
    }

    // ====================================================================
    // ClusterByPerpendicular
    // ====================================================================

    [Fact]
    public void ClusterByPerpendicular_垂直方向が近い線分をグループ化する()
    {
        // 水平線分 3 本（Y=10, 12, 100）: Y が近い 2 本（10, 12）を 1 グループ、100 を別グループ
        var segments = new[]
        {
            new LineSeg(0, 10, 100, 10),
            new LineSeg(0, 12, 100, 12),
            new LineSeg(0, 100, 100, 100),
        };

        var clusters = LineSegmentMath.ClusterByPerpendicular(segments, isHorizontal: true, tolerance: 3);

        Assert.Equal(2, clusters.Count);
        Assert.Equal(2, clusters[0].Count);
        Assert.Single(clusters[1]);
    }

    [Fact]
    public void ClusterByPerpendicular_許容差を超える線分は別グループになる()
    {
        // 水平線分 2 本（Y=10, 50）: 許容差 3 では別グループ
        var segments = new[]
        {
            new LineSeg(0, 10, 100, 10),
            new LineSeg(0, 50, 100, 50),
        };

        var clusters = LineSegmentMath.ClusterByPerpendicular(segments, isHorizontal: true, tolerance: 3);

        Assert.Equal(2, clusters.Count);
    }

    // ====================================================================
    // MergeCollinear
    // ====================================================================

    [Fact]
    public void MergeCollinear_ギャップ以内の断片を連結する()
    {
        // 水平線分 2 本（X=0〜40 と X=45〜100）: ギャップ 5 以内 → 1 本に連結
        var cluster = new[]
        {
            new LineSeg(0, 20, 40, 20),
            new LineSeg(45, 20, 100, 20),
        };

        var merged = LineSegmentMath.MergeCollinear(cluster, isHorizontal: true, gap: 5);

        var seg = Assert.Single(merged);
        Assert.Equal(new LineSeg(0, 20, 100, 20), seg);
    }

    [Fact]
    public void MergeCollinear_ギャップを超える断片は別の辺になる()
    {
        // 水平線分 2 本（X=0〜40 と X=100〜200）: ギャップ 5 を超える → 別の 2 本
        var cluster = new[]
        {
            new LineSeg(0, 20, 40, 20),
            new LineSeg(100, 20, 200, 20),
        };

        var merged = LineSegmentMath.MergeCollinear(cluster, isHorizontal: true, gap: 5);

        Assert.Equal(2, merged.Count);
    }

    // ====================================================================
    // MergeCollinearAll（クラスタリング + 連結マージ）
    // ====================================================================

    [Fact]
    public void MergeCollinearAll_クラスタリングして連結マージする()
    {
        // 水平線分 4 本: Y=10 の断片 2 本（連結）、Y=20 の断片 1 本 → 2 本にマージ
        var segments = new[]
        {
            new LineSeg(0, 10, 40, 10),
            new LineSeg(45, 10, 100, 10),
            new LineSeg(0, 20, 100, 20),
        };

        var merged = LineSegmentMath.MergeCollinearAll(segments, isHorizontal: true, gap: 5, tolerance: 3);

        Assert.Equal(2, merged.Count);
        Assert.Equal(new LineSeg(0, 10, 100, 10), merged[0]);
        Assert.Equal(new LineSeg(0, 20, 100, 20), merged[1]);
    }
}
