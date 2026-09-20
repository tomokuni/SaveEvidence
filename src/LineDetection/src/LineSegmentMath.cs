namespace Estable.LineDetection;

/// <summary>検出された線分の座標表現（始点・終点を保持する不変な値オブジェクト）。</summary>
/// <param name="X1">始点 X 座標（ピクセル）。</param>
/// <param name="Y1">始点 Y 座標（ピクセル）。</param>
/// <param name="X2">終点 X 座標（ピクセル）。</param>
/// <param name="Y2">終点 Y 座標（ピクセル）。</param>
/// <remarks>
/// <see cref="LogicLineDetector"/> の内部処理で使用する水平・垂直線分の値オブジェクト。<br/>
/// UI フレームワーク非依存の純粋計算（<see cref="LineSegmentMath"/>）で扱う（移植性・テスト容易性向上）。
/// </remarks>
internal readonly record struct LineSeg(int X1, int Y1, int X2, int Y2);

/// <summary>線分（<see cref="LineSeg"/>）の座標計算を提供する純粋ロジック・クラス。</summary>
/// <remarks>
/// 水平・垂直線分の座標計算（生成・長さ・垂直方向座標・平行方向範囲）を UI 非依存の純粋関数として提供する。<br/>
/// OpenCV 依存を <see cref="LogicLineDetector"/> に閉じ、座標計算を本クラスへ分離する（移植性・テスト容易性向上）。
/// </remarks>
internal static class LineSegmentMath
{
    /// <summary>水平 / 垂直方向の範囲と垂直方向の座標から線分を生成する。</summary>
    /// <param name="start">水平方向の始点座標。</param>
    /// <param name="end">水平方向の終点座標。</param>
    /// <param name="perp">垂直方向の座標（水平線は Y・垂直線は X）。</param>
    /// <param name="isHorizontal">水平線分の場合は true。</param>
    /// <returns>生成した線分。</returns>
    public static LineSeg MakeLine(int start, int end, double perp, bool isHorizontal)
        => isHorizontal
            ? new LineSeg(start, (int)Math.Round(perp), end, (int)Math.Round(perp))
            : new LineSeg((int)Math.Round(perp), start, (int)Math.Round(perp), end);

    /// <summary>線分の長さを計算する。</summary>
    public static double Length(in LineSeg s)
        => Math.Sqrt((s.X2 - s.X1) * (s.X2 - s.X1) + (s.Y2 - s.Y1) * (s.Y2 - s.Y1));

    /// <summary>線分の垂直方向座標（水平線は Y・垂直線は X）を返す。</summary>
    public static double PerpCoord(in LineSeg s, bool isHorizontal)
        => isHorizontal ? (s.Y1 + s.Y2) / 2.0 : (s.X1 + s.X2) / 2.0;

    /// <summary>線分の水平方向の始点座標（水平線は X・垂直線は Y）を返す。</summary>
    public static int ParallelStart(in LineSeg s, bool isHorizontal)
        => isHorizontal ? Math.Min(s.X1, s.X2) : Math.Min(s.Y1, s.Y2);

    /// <summary>線分の水平方向の終点座標（水平線は X・垂直線は Y）を返す。</summary>
    public static int ParallelEnd(in LineSeg s, bool isHorizontal)
        => isHorizontal ? Math.Max(s.X1, s.X2) : Math.Max(s.Y1, s.Y2);

    /// <summary>線分群を垂直方向（水平線は Y・垂直線は X）でクラスタリングする。</summary>
    /// <param name="segments">同一向きの線分群。</param>
    /// <param name="isHorizontal">水平線分の場合は true。</param>
    /// <param name="tolerance">クラスタリングの垂直方向許容差（ピクセル）。</param>
    /// <returns>垂直方向が近い線分のグループ一覧。</returns>
    /// <remarks>UI 非依存の純粋計算のため、ヘッドレス不要の <c>[Fact]</c> で並列テスト可能。</remarks>
    public static List<List<LineSeg>> ClusterByPerpendicular(
        IReadOnlyList<LineSeg> segments, bool isHorizontal, double tolerance)
    {
        var clusters = new List<List<LineSeg>>();
        var anchors = new List<double>();

        foreach (var seg in segments.OrderBy(s => PerpCoord(s, isHorizontal)))
        {
            var perp = PerpCoord(seg, isHorizontal);
            var index = -1;

            for (var i = 0; i < clusters.Count; i++)
            {
                if (Math.Abs(anchors[i] - perp) <= tolerance)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                clusters.Add([seg]);
                anchors.Add(perp);
            }
            else
            {
                clusters[index].Add(seg);
                anchors[index] = (anchors[index] + perp) / 2.0;
            }
        }

        return clusters;
    }

    /// <summary>同一クラスタ内の線分を、水平方向の重なり / ギャップで連結して 1 本の辺へマージする。</summary>
    /// <param name="cluster">垂直方向が近い線分のグループ。</param>
    /// <param name="isHorizontal">水平線分の場合は true。</param>
    /// <param name="gap">連結する最大ギャップ（ピクセル）。</param>
    /// <returns>マージ後の線分一覧。</returns>
    /// <remarks>UI 非依存の純粋計算のため、ヘッドレス不要の <c>[Fact]</c> で並列テスト可能。</remarks>
    public static List<LineSeg> MergeCollinear(
        IReadOnlyList<LineSeg> cluster, bool isHorizontal, int gap)
    {
        var sorted = cluster.OrderBy(s => ParallelStart(s, isHorizontal)).ToList();
        var merged = new List<LineSeg>();

        var start = ParallelStart(sorted[0], isHorizontal);
        var end = ParallelEnd(sorted[0], isHorizontal);
        var perpSum = PerpCoord(sorted[0], isHorizontal);
        var count = 1;

        for (var i = 1; i < sorted.Count; i++)
        {
            var s = sorted[i];
            if (ParallelStart(s, isHorizontal) <= end + gap)
            {
                end = Math.Max(end, ParallelEnd(s, isHorizontal));
                perpSum += PerpCoord(s, isHorizontal);
                count++;
            }
            else
            {
                merged.Add(MakeLine(start, end, perpSum / count, isHorizontal));
                start = ParallelStart(s, isHorizontal);
                end = ParallelEnd(s, isHorizontal);
                perpSum = PerpCoord(s, isHorizontal);
                count = 1;
            }
        }

        merged.Add(MakeLine(start, end, perpSum / count, isHorizontal));
        return merged;
    }

    /// <summary>線分群をクラスタリングしてマージし、マージ後の線分一覧を返す。</summary>
    /// <param name="segments">同一向きの線分群。</param>
    /// <param name="isHorizontal">水平線分の場合は true。</param>
    /// <param name="gap">同一クラスタ内の線分を連結する最大ギャップ（ピクセル）。</param>
    /// <param name="tolerance">クラスタリングの垂直方向許容差（ピクセル）。</param>
    /// <returns>マージ後の線分一覧（長さフィルタは未適用）。</returns>
    /// <remarks>
    /// 1. 垂直方向（水平線は Y・垂直線は X）でクラスタリング<br/>
    /// 2. クラスタ内を水平方向（水平線は X・垂直線は Y）で連結マージ<br/>
    /// UI 非依存の純粋計算のため、ヘッドレス不要の <c>[Fact]</c> で並列テスト可能。
    /// </remarks>
    public static List<LineSeg> MergeCollinearAll(
        IReadOnlyList<LineSeg> segments, bool isHorizontal, int gap, double tolerance)
    {
        var result = new List<LineSeg>();
        foreach (var cluster in ClusterByPerpendicular(segments, isHorizontal, tolerance))
        {
            result.AddRange(MergeCollinear(cluster, isHorizontal, gap));
        }

        return result;
    }
}
