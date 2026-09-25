using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.LineDetection;

/// <summary>検出された線分（<see cref="LineDetectionResult"/>）から矩形（ウィンドウの 4 辺）を復元する純粋ロジック。</summary>
/// <remarks>
/// <see cref="LogicLineDetector"/> の検出結果（水平・垂直の線分）から、<b>4 辺が実際に矩形を構成する</b>組み合わせのみを<br/>
/// 矩形候補として生成する。これにより複数ウィンドウを含む画像では複数の矩形候補が得られる。<br/>
/// 候補にする条件は「左右の垂直線が矩形の縦範囲を覆い、上下の水平線が矩形の横範囲を覆う」ことである<br/>
/// （覆わない組み合わせは矩形を構成しないため候補にしない）。<br/>
/// 同一座標の線分は 1 本に統合し、その範囲（最小〜最大）を合併する。<br/>
/// 候補数は <see cref="MaxCandidates"/> を上限とし、面積の小さい順に返す（UI が全候補を描画するため）。<br/>
/// UI フレームワーク非依存の純粋計算のため、ヘッドレス不要の <c>[Fact]</c> で並列テストできる（移植性・テスト容易性向上）。
/// </remarks>
public static class LineRectangleReconstructor
{
    /// <summary>線分の端と矩形の辺との丸め誤差として許容する長さ（ピクセル）。</summary>
    /// <remarks>線分の座標はピクセルへ丸めて扱うため、コーナーで数ピクセル短い線分でも矩形を構成できるようにする。</remarks>
    private const int SpanTolerancePixels = 5;

    /// <summary>復元する矩形候補の上限（面積の小さい順に保持する）。</summary>
    /// <remarks>
    /// 検出線分が多い画像（文字列や罫線の多い画面）では組み合わせが爆発しうるため、<br/>
    /// 候補数を上限で抑えて UI（全候補を描画する）の負荷とメモリ使用量を制限する。<br/>
    /// 上限を超えた場合は面積の小さい候補を残す（UI は面積最小の候補を優先して選ぶため）。
    /// </remarks>
    private const int MaxCandidates = 256;

    /// <summary>1 つの垂直線ペアから生成する矩形候補の上限。</summary>
    /// <remarks>
    /// すべての水平線が左右の範囲を覆う画像（罫線の多い表など）では、1 つの垂直線ペアから<br/>
    /// 水平線のペア数（数万通り）だけ候補が生まれ、全体では数億通りに達しうる。<br/>
    /// そこでペアごとの生成数にも上限を設け、最悪時の処理時間を組み合わせ数に依存させない。<br/>
    /// 生成した候補は最終的に面積の小さい順に間引かれるため、上限を超えた分は順位の低い候補になる。
    /// </remarks>
    private const int MaxCandidatesPerPair = MaxCandidates * 4;

    /// <summary>検出された線分から矩形（ウィンドウの 4 辺）の候補を復元する。</summary>
    /// <param name="results">検出された線分一覧。</param>
    /// <returns>復元した矩形候補の一覧（画像座標・閉区間 <see cref="EsRectInt"/>。面積昇順・最大 <see cref="MaxCandidates"/> 件）。</returns>
    /// <remarks>
    /// 垂直 2 本・水平 2 本が揃わない場合は復元できない（空リストを返す）。<br/>
    /// 幅・高さが 1 未満（同一座標のペア）のものは除外する。<br/>
    /// 処理フロー:<br/>
    /// 1. 同一座標の線分を 1 本に統合し、各線分の範囲（垂直線は Y 範囲・水平線は X 範囲）を求める<br/>
    /// 2. 垂直線のペアごとに、2 本が共通に覆う Y 範囲と X 範囲（左端〜右端）を求める<br/>
    /// 3. その範囲を覆う水平線のみを対象に、上下の組み合わせで矩形候補を生成する（ペアごとの生成数にも上限を設ける）<br/>
    /// 4. 候補が上限の 2 倍に達した時点で面積の小さい順に上限まで間引く（メモリと件数を抑える）<br/>
    /// 5. 面積昇順で返す（上限を超える分は切り捨てる）<br/>
    /// 「検出→矩形復元」の責務を <see cref="LogicLineDetector"/>（検出）から分離し、単一所有（DRY）とする。
    /// </remarks>
    public static IReadOnlyList<EsRectInt> Reconstruct(IReadOnlyList<LineDetectionResult> results)
    {
        var verticals = BuildSpans(results, LineOrientation.Vertical);
        var horizontals = BuildSpans(results, LineOrientation.Horizontal);

        // 垂直 2 本・水平 2 本が揃わない場合は復元できない
        if (verticals.Count < 2 || horizontals.Count < 2)
        {
            return [];
        }

        var rects = new List<EsRectInt>();

        for (var i = 0; i < verticals.Count - 1; i++)
        {
            var left = verticals[i];
            for (var j = i + 1; j < verticals.Count; j++)
            {
                var right = verticals[j];
                var width = right.Coord - left.Coord;
                if (width <= 0)
                {
                    continue;
                }

                // 左右の垂直線が共通に覆う Y 範囲（この範囲の外側では矩形を構成できない）
                var top = Math.Max(left.Min, right.Min);
                var bottom = Math.Min(left.Max, right.Max);
                if (bottom - top <= 0)
                {
                    continue;
                }

                // 左右の X 範囲（左端〜右端）を覆い、Y が [top, bottom] 内にある水平線のみを対象にする
                var spans = new List<ParallelSpan>();
                foreach (var h in horizontals)
                {
                    if (h.Coord < top - SpanTolerancePixels || h.Coord > bottom + SpanTolerancePixels)
                    {
                        continue;
                    }

                    if (h.Min > left.Coord + SpanTolerancePixels || h.Max < right.Coord - SpanTolerancePixels)
                    {
                        continue;
                    }

                    spans.Add(h);
                }

                if (spans.Count < 2)
                {
                    continue;
                }

                var generated = 0;
                var aborted = false;
                for (var a = 0; a < spans.Count - 1 && !aborted; a++)
                {
                    for (var b = a + 1; b < spans.Count; b++)
                    {
                        var height = spans[b].Coord - spans[a].Coord;
                        if (height <= 0)
                        {
                            continue;
                        }

                        rects.Add(new EsRectInt(left.Coord, spans[a].Coord, width, height));

                        // 1 つの垂直線ペアからの生成数にも上限を設ける（表のように全水平線が範囲を覆う画像での爆発を防ぐ）
                        if (++generated >= MaxCandidatesPerPair)
                        {
                            aborted = true;
                            break;
                        }
                    }
                }

                // 上限の 2 倍に達したら面積の小さい順に間引く（組み合わせ爆発によるメモリ増加を防ぐ）
                if (rects.Count >= MaxCandidates * 2)
                {
                    TrimCandidates(rects);
                }
            }
        }

        rects.Sort(static (x, y) => Area(x).CompareTo(Area(y)));
        if (rects.Count > MaxCandidates)
        {
            rects.RemoveRange(MaxCandidates, rects.Count - MaxCandidates);
        }

        return rects;
    }

    /// <summary>矩形の面積（ピクセル数）を求める。</summary>
    /// <param name="rect">対象の矩形。</param>
    /// <returns>面積（幅 × 高さ）。桁あふれを避けるため 64 ビットで計算する。</returns>
    private static long Area(EsRectInt rect) => (long)rect.Width * rect.Height;

    /// <summary>候補を面積の小さい順に <see cref="MaxCandidates"/> 件まで間引く。</summary>
    /// <param name="rects">対象の候補一覧（このリストを直接書き換える）。</param>
    private static void TrimCandidates(List<EsRectInt> rects)
    {
        rects.Sort(static (x, y) => Area(x).CompareTo(Area(y)));
        rects.RemoveRange(MaxCandidates, rects.Count - MaxCandidates);
    }

    /// <summary>同一座標の線分を統合し、各線分の範囲（平行方向の最小〜最大）を求める。</summary>
    /// <param name="results">検出された線分一覧。</param>
    /// <param name="orientation">対象の向き（垂直は Y 範囲・水平は X 範囲を求める）。</param>
    /// <returns>座標の昇順に並んだ線分の範囲一覧。</returns>
    /// <remarks>同一座標の線分は 1 本としてまとめ、範囲を合併する（断片化した辺を 1 本として扱う）。</remarks>
    private static List<ParallelSpan> BuildSpans(IReadOnlyList<LineDetectionResult> results, LineOrientation orientation)
    {
        var spans = new Dictionary<int, (int Min, int Max)>();
        var isVertical = orientation == LineOrientation.Vertical;

        foreach (var result in results)
        {
            if (result.Orientation != orientation)
            {
                continue;
            }

            // 垂直線は X 座標ごとに Y 範囲、水平線は Y 座標ごとに X 範囲を求める
            var coord = (int)Math.Round(isVertical ? result.Start.X : result.Start.Y);
            var start = (int)Math.Round(isVertical ? result.Start.Y : result.Start.X);
            var end = (int)Math.Round(isVertical ? result.End.Y : result.End.X);
            var min = Math.Min(start, end);
            var max = Math.Max(start, end);

            spans[coord] = spans.TryGetValue(coord, out var current)
                ? (Math.Min(current.Min, min), Math.Max(current.Max, max))
                : (min, max);
        }

        return
        [
            .. spans
                .OrderBy(static pair => pair.Key)
                .Select(static pair => new ParallelSpan(pair.Key, pair.Value.Min, pair.Value.Max)),
        ];
    }

    /// <summary>同一座標の線分を統合した範囲（座標と平行方向の最小〜最大）。</summary>
    /// <param name="Coord">垂直線は X 座標、水平線は Y 座標。</param>
    /// <param name="Min">平行方向の最小値（垂直線は最小 Y・水平線は最小 X）。</param>
    /// <param name="Max">平行方向の最大値（垂直線は最大 Y・水平線は最大 X）。</param>
    private readonly record struct ParallelSpan(int Coord, int Min, int Max);
}
