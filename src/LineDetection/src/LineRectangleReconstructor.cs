using Estable.Platform.Geometry;

namespace Estable.LineDetection;

/// <summary>検出された線分（<see cref="LineDetectionResult"/>）から矩形（ウィンドウの 4 辺）を復元する純粋ロジック。</summary>
/// <remarks>
/// <see cref="LogicLineDetector"/> の検出結果（水平・垂直の線分）から、<br/>
/// 垂直線のペア × 水平線のペアの<b>全組み合わせ</b>で矩形候補を生成する。<br/>
/// これにより複数ウィンドウを含む画像では複数の矩形候補が得られる。<br/>
/// UI フレームワーク非依存の純粋計算のため、ヘッドレス不要の <c>[Fact]</c> で並列テストできる（移植性・テスト容易性向上）。
/// </remarks>
public static class LineRectangleReconstructor
{
    /// <summary>検出された線分から矩形（ウィンドウの 4 辺）の候補を復元する。</summary>
    /// <param name="results">検出された線分一覧。</param>
    /// <returns>復元した矩形候補の一覧（画像座標・閉区間 <see cref="EsPixelRect"/>。面積昇順）。</returns>
    /// <remarks>
    /// 垂直 2 本・水平 2 本が揃わない場合は復元できない（空リストを返す）。<br/>
    /// 幅・高さが 1 未満（同一座標のペア）のものは除外する。<br/>
    /// 「検出→矩形復元」の責務を <see cref="LogicLineDetector"/>（検出）から分離し、単一所有（DRY）とする。
    /// </remarks>
    public static IReadOnlyList<EsPixelRect> Reconstruct(IReadOnlyList<LineDetectionResult> results)
    {
        var verticalX = results
            .Where(r => r.Orientation == LineOrientation.Vertical)
            .Select(r => (int)Math.Round(r.Start.X))
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        var horizontalY = results
            .Where(r => r.Orientation == LineOrientation.Horizontal)
            .Select(r => (int)Math.Round(r.Start.Y))
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        // 垂直 2 本・水平 2 本が揃わない場合は復元できない
        if (verticalX.Count < 2 || horizontalY.Count < 2)
        {
            return [];
        }

        // 垂直線ペア × 水平線ペアの全組み合わせから矩形候補を生成する（面積昇順）
        var rects = new List<EsPixelRect>();
        for (var xi = 0; xi < verticalX.Count - 1; xi++)
        {
            for (var xj = xi + 1; xj < verticalX.Count; xj++)
            {
                var left = verticalX[xi];
                var right = verticalX[xj];
                for (var yi = 0; yi < horizontalY.Count - 1; yi++)
                {
                    for (var yj = yi + 1; yj < horizontalY.Count; yj++)
                    {
                        var top = horizontalY[yi];
                        var bottom = horizontalY[yj];
                        var width = right - left;
                        var height = bottom - top;
                        if (width <= 0 || height <= 0)
                        {
                            continue;
                        }

                        rects.Add(new EsPixelRect(left, top, width, height));
                    }
                }
            }
        }

        return rects.OrderBy(r => (long)r.Width * r.Height).ToList();
    }
}
