using Avalonia;
using OpenCvSharp;
using AvaloniaPoint = Avalonia.Point;

namespace Estable.AvaloniaUI.LineDetection;

/// <summary>ロジック方式: Canny エッジ + HoughLinesP による水平・垂直の辺検出。</summary>
/// <remarks>
/// グレースケール化 → Canny でエッジを抽出し、HoughLinesP で線分を検出する。<br/>
/// 線分を角度で水平 / 垂直に分類し、同一直線上に並ぶ線分をクラスタリングして長い辺へ統合する。<br/>
/// ウィンドウ枠は長くて真っ直ぐな水平・垂直の辺で構成されるため、最小長でフィルタして候補とする。<br/>
/// 文字列（テキスト）の水平エッジは断続的であるため、マージ後の線分上の暗いピクセル割合<br/>
/// （<see cref="MinEdgeSupportRatio"/>）で除外する（ウィンドウ枠は連続エッジのため残る）。<br/>
/// 線分が矩形（ウィンドウの 4 辺）を構成できる場合は、4 つの辺がコーナー近傍で交差する<br/>
/// 閉区間（<see cref="BuildRectangles"/>）として出力する。<br/>
/// モデルは使用せず、画像の幾何学的特徴のみで検出する。
/// </remarks>
public sealed class LogicLineDetector : ILineDetector
{
    /// <summary>方式の表示名。</summary>
    public string Name => "ロジック（Canny + Hough）";

    /// <summary>モデルサイズ（モデル不使用）。</summary>
    public string ModelInfo => "";

    /// <summary>モデル読込・実行エラー（モデル不使用のため常に null）。</summary>
    public string? ErrorMessage => null;

    /// <summary>Canny の低閾値。</summary>
    /// <remarks>
    /// 実画面のキャプチャではウィンドウ枠がアンチエイリアスで薄くなる（輝度差 50〜180）ため、<br/>
    /// 低いしきい値で薄いエッジも検出する（<see cref="CannyThreshold2"/> はその 3 倍）。
    /// </remarks>
    public int CannyThreshold1 { get; set; } = 20;

    /// <summary>Canny の高閾値。</summary>
    /// <remarks><see cref="CannyThreshold1"/> の 3 倍。強いエッジを優先しつつ薄いエッジも拾う。</remarks>
    public int CannyThreshold2 { get; set; } = 60;

    /// <summary>HoughLinesP の投票閾値（小さいほど多くの線分を検出）。</summary>
    /// <remarks>薄い枠線はエッジが弱いため、投票数が少なくなる。低めに設定して検出を漏らさない。</remarks>
    public int HoughThreshold { get; set; } = 40;

    /// <summary>検出する線分の最小長さ（ピクセル）。マージ後の辺にも適用する。</summary>
    /// <remarks>
    /// テキスト文字列の水平エッジは短い線分として検出されるため、ウィンドウ枠（長い直線）との
    /// 識別を目的に十分な長さを設定する（<see cref="MinEdgeSupportRatio"/> と併用して誤検出を抑制）。
    /// </remarks>
    public int MinLineLength { get; set; } = 50;

    /// <summary>同一線分とみなす最大ギャップ（ピクセル）。</summary>
    public int MaxLineGap { get; set; } = 10;

    /// <summary>水平 / 垂直とみなす角度許容差（度）。</summary>
    public double AngleTolerance { get; set; } = 5.0;

    /// <summary>辺のクラスタリング許容差（ピクセル）。</summary>
    /// <remarks>
    /// 同一線分の断片（Y 方向に分割された垂直線・X 方向に分割された水平線）を連結するための許容差。<br/>
    /// 枠線とウィンドウ内の罫線（近接する別の平行線）を混ぜないよう 1px に絞る。<br/>
    /// 近接した平行線の統合は <see cref="MergeParallelLines"/>（外縁採用）が行う。
    /// </remarks>
    public int ClusterTolerance { get; set; } = 1;

    /// <summary>線分に沿ったエッジピクセルの最小割合（0.0〜1.0）。</summary>
    /// <remarks>
    /// マージ後の線分上で Canny エッジが存在するピクセルの割合が本値を下回る線分は除外する。<br/>
    /// ウィンドウ枠は連続エッジ（割合が高い）で、文字列の水平エッジは断続的（割合が低い）ため、<br/>
    /// テキスト部分の誤検出を除外できる。
    /// </remarks>
    public double MinEdgeSupportRatio { get; set; } = 0.6;

    /// <summary>矩形の辺に統合する平行線の最大距離（ピクセル）。</summary>
    /// <remarks>
    /// ウィンドウ枠は線幅があるため、外縁エッジと内縁エッジが別々に検出される。<br/>
    /// この距離以内の平行線を平均化して 1 本の辺へ統合する（<see cref="BuildRectangles"/>）。
    /// </remarks>
    public double ParallelMergeTolerance { get; set; } = 8.0;

    /// <summary>矩形のコーナー判定の許容差（辺の長さに対する比率）。</summary>
    /// <remarks>
    /// ウィンドウ枠はコーナー付近で欠ける（角丸・他ウィンドウとの重なり）ため、<br/>
    /// 辺の端点とコーナー座標の距離が、辺の長さ × 本比率以内なら近接とみなす。<br/>
    /// 長い辺（ウィンドウ枠）はコーナー欠け（最大 100px 超）を許容し、<br/>
    /// ウィンドウ上部の別要素の境界（欠け 300px 超）は除外する。
    /// </remarks>
    public double CornerToleranceRatio { get; set; } = 0.2;

    /// <summary>矩形の対辺（上辺と下辺・左辺と右辺）の長さの差の許容比率。</summary>
    /// <remarks>
    /// コーナーが欠けてスパンが揃わないことがあるため、対辺の長さの差は比率で許容する。<br/>
    /// ウィンドウ上部の別要素の境界（対辺差が大きい）を除外しつつ、<br/>
    /// 別ウィンドウの重なりで端点が欠ける辺（対辺差 0.12 前後）を許容する。
    /// </remarks>
    public double OppositeSpanRatio { get; set; } = 0.13;

    /// <summary>コーナー近接判定の最小許容差（ピクセル）。</summary>
    /// <remarks>短い線分で許容差が過小になるのを防ぐ。</remarks>
    private const double MinCornerTolerance = 5.0;

    /// <summary>線分の記録（始点・終点）。</summary>
    private readonly record struct LineSeg(int X1, int Y1, int X2, int Y2);

    /// <summary>画像から水平・垂直の辺（線分）を検出する。</summary>
    /// <param name="image">解析対象の画像（BGRA）。</param>
    /// <returns>検出した線分のリスト（スコア = 長さ・降順）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. グレースケール化 → Canny エッジ検出<br/>
    /// 2. HoughLinesP で線分を抽出し、角度で水平 / 垂直に分離<br/>
    /// 3. 水平線分を Y 座標で、垂直線分を X 座標でクラスタリング<br/>
    /// 4. 同一クラスタ内の線分を、重なり / ギャップで連結して長い辺へマージ<br/>
    /// 5. マージした線分が矩形（ウィンドウの 4 辺）を構成する場合は、<br/>
    ///    4 辺をコーナー近傍で交差する閉区間として出力する（<see cref="BuildRectangles"/>）<br/>
    /// 6. 矩形を構成できない場合は、最小長・暗いピクセル割合でフィルタしてスコア（長さ）降順で返す
    /// </remarks>
    public IReadOnlyList<LineDetectionResult> Detect(Mat image)
    {
        using var gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGRA2GRAY);
        using var edges = new Mat();
        Cv2.Canny(gray, edges, CannyThreshold1, CannyThreshold2);

        var horizontals = new List<LineSeg>();
        var verticals = new List<LineSeg>();

        var houghLines = Cv2.HoughLinesP(edges, 1, Math.PI / 180, HoughThreshold, MinLineLength, MaxLineGap);
        foreach (var seg in houghLines)
        {
            var line = new LineSeg(seg.P1.X, seg.P1.Y, seg.P2.X, seg.P2.Y);
            var angle = Math.Abs(Math.Atan2(line.Y2 - line.Y1, line.X2 - line.X1) * 180 / Math.PI);

            if (angle < AngleTolerance || angle > 180 - AngleTolerance)
            {
                horizontals.Add(line);
            }
            else if (Math.Abs(angle - 90) < AngleTolerance)
            {
                verticals.Add(line);
            }
        }

        // マージ後の線分（水平・垂直）
        var mergedH = MergeCollinearAll(horizontals, isHorizontal: true, gap: MaxLineGap);
        var mergedV = MergeCollinearAll(verticals, isHorizontal: false, gap: MaxLineGap);

        // 矩形（ウィンドウの 4 辺）を構成できる場合は、閉区間として優先出力する
        var rectangles = BuildRectangles(mergedH, mergedV, image.Width, image.Height, gray, edges);
        if (rectangles.Count > 0)
        {
            return rectangles.OrderByDescending(r => r.Score).ToList();
        }

        // 矩形を構成できない場合は、従来どおりフィルタして出力する
        var results = new List<LineDetectionResult>();
        results.AddRange(FilterAndScore(mergedH, isHorizontal: true, edges));
        results.AddRange(FilterAndScore(mergedV, isHorizontal: false, edges));

        return results.OrderByDescending(r => r.Score).ToList();
    }

    /// <summary>線分群をクラスタリングしてマージし、マージ後の線分一覧を返す。</summary>
    /// <param name="segments">同一向きの線分群。</param>
    /// <param name="isHorizontal">水平線分の場合は true。</param>
    /// <param name="gap">同一クラスタ内の線分を連結する最大ギャップ（ピクセル）。</param>
    /// <returns>マージ後の線分一覧（長さフィルタは未適用）。</returns>
    /// <remarks>
    /// 1. 垂直方向（水平線は Y・垂直線は X）でクラスタリング<br/>
    /// 2. クラスタ内を水平方向（水平線は X・垂直線は Y）で連結マージ
    /// </remarks>
    private List<LineSeg> MergeCollinearAll(List<LineSeg> segments, bool isHorizontal, int gap)
    {
        var result = new List<LineSeg>();
        foreach (var cluster in ClusterByPerpendicular(segments, isHorizontal))
        {
            result.AddRange(MergeCollinear(cluster, isHorizontal, gap));
        }

        return result;
    }

    /// <summary>マージ後の線分をフィルタしてスコア付きの線分結果を生成する。</summary>
    /// <param name="segments">マージ後の線分一覧。</param>
    /// <param name="isHorizontal">水平線分の場合は true。</param>
    /// <param name="edges">Canny エッジ画像（エッジ支持率の判定に使用）。</param>
    /// <returns>フィルタ後の線分結果（最小長・エッジ支持率でフィルタ済み）。</returns>
    /// <remarks>
    /// 1. マージ後の長さが最小長未満の辺は除外<br/>
    /// 2. 線分に沿ったエッジの割合（<see cref="MinEdgeSupportRatio"/>）未満の辺は除外（テキスト誤検出の抑制）
    /// </remarks>
    private IEnumerable<LineDetectionResult> FilterAndScore(List<LineSeg> segments, bool isHorizontal, Mat edges)
    {
        foreach (var merged in segments)
        {
            var length = Length(merged);
            if (length < MinLineLength)
            {
                continue;
            }

            // 文字列の水平エッジ（断続的）を除外する: 線分に沿ったエッジの割合が低いものは捨てる
            if (EdgeSupportRatio(merged, edges, isHorizontal) < MinEdgeSupportRatio)
            {
                continue;
            }

            yield return new LineDetectionResult(
                new AvaloniaPoint(merged.X1, merged.Y1),
                new AvaloniaPoint(merged.X2, merged.Y2),
                isHorizontal ? LineOrientation.Horizontal : LineOrientation.Vertical,
                length);
        }
    }

    /// <summary>マージ後の線分からウィンドウの矩形（4 辺の閉区間）を構成する。</summary>
    /// <param name="horizontals">マージ後の水平線分一覧。</param>
    /// <param name="verticals">マージ後の垂直線分一覧。</param>
    /// <param name="imageWidth">画像の幅（矩形の判定に使用）。</param>
    /// <param name="imageHeight">画像の高さ（矩形の判定に使用）。</param>
    /// <param name="gray">グレースケール画像（枠の外側の境界の補正に使用）。</param>
    /// <param name="edges">Canny エッジ画像（エッジ支持率の判定に使用）。</param>
    /// <returns>構成した矩形の 4 辺（コーナー近傍で交差する線分）の一覧。構成できない場合は空。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. 同じ垂直座標に並ぶ断片を広いギャップ（<see cref="RectMergeGap"/>）で連結し、<br/>
    ///    薄い枠線（HoughLinesP で断片化される）を長い辺として復元する<br/>
    /// 2. 各辺を<b>元画像の輝度プロファイルの枠の外側の境界（排他座標の辺）</b>へ補正する（<see cref="SnapToEdgeByGradient"/>）<br/>
    /// 3. 近接する平行線（<see cref="ParallelMergeTolerance"/> 以内）を 1 本に統合する<br/>
    ///    （枠は透明度で背景色と混ざるため、1 本の枠線が複数の近接平行線として検出されることがある）<br/>
    /// 4. 線分に沿ったエッジの割合（<see cref="MinEdgeSupportRatio"/>）未満の線分を除外する<br/>
    ///    （文字列の水平エッジは断続的で割合が低く、ウィンドウ枠は連続エッジで割合が高い）<br/>
    /// 5. 水平線のペア（上・下）と垂直線のペア（左・右）の組み合わせを探索し、<br/>
    ///    以下の<b>矩形の幾何学的一貫性</b>を満たすもののうち、<br/>
    ///    画像の端からの距離合計が最小（= ウィンドウ外枠に近い）ものを選択する<br/>
    ///    ・対辺の長さが揃っている（上辺 ≈ 下辺・左辺 ≈ 右辺。比率 <see cref="OppositeSpanRatio"/> で許容）<br/>
    ///    ・4 つのコーナーそれぞれで、水平線または垂直線の<b>少なくとも一方の端点</b>が<br/>
    ///      コーナー座標の近傍（辺の長さ × <see cref="CornerToleranceRatio"/>）にある<br/>
    ///    （ウィンドウ枠はコーナーが丸く欠ける・他ウィンドウが重なるため、<br/>
    ///    完全な交差ではなく近接で判定する。<br/>
    ///    長い辺（ウィンドウ枠）はコーナー欠けを許容し、短い線分（誤検出）はコーナーから遠いため除外される。<br/>
    ///    タイトルバー下端などのウィンドウ内の罫線は画像端から遠いため選ばれない）<br/>
    /// 6. 4 辺の端点をコーナー座標に揃えて出力（上辺 (x1,y1)-(x2,y1) 等・閉区間）
    /// </remarks>
    private List<LineDetectionResult> BuildRectangles(
        List<LineSeg> horizontals, List<LineSeg> verticals, int imageWidth, int imageHeight, Mat gray, Mat edges)
    {
        // 断片を広いギャップで連結して長い辺を復元し、枠の外側の境界へ補正する。
        // 透明度による背景混合で 1 本の枠線が複数の近接平行線として検出されるため、
        // 近接する平行線を 1 本に統合してから、文字列の断片（エッジ支持率が低い）を除外する
        var hs = MergeParallelLines(
                MergeCollinearAll(horizontals, isHorizontal: true, gap: RectMergeGap)
                    .Select(s => SnapToEdgeByGradient(s, isHorizontal: true, imageHeight / 2.0, gray))
                    .ToList(),
                isHorizontal: true, ParallelMergeTolerance, imageHeight / 2.0, edges)
            .Where(s => Length(s) >= MinLineLength && EdgeSupportRatio(s, edges, isHorizontal: true) >= MinEdgeSupportRatio)
            .ToList();

        var vs = MergeParallelLines(
                MergeCollinearAll(verticals, isHorizontal: false, gap: RectMergeGap)
                    .Select(s => SnapToEdgeByGradient(s, isHorizontal: false, imageWidth / 2.0, gray))
                    .ToList(),
                isHorizontal: false, ParallelMergeTolerance, imageWidth / 2.0, edges)
            .Where(s => Length(s) >= MinLineLength && EdgeSupportRatio(s, edges, isHorizontal: false) >= MinEdgeSupportRatio)
            .ToList();

        // 水平線のペア（上・下）と垂直線のペア（左・右）の組み合わせを探索し、
        // 矩形の幾何学的一貫性（対辺同長・コーナー近接）を満たすもののうち、
        // 画像の端からの距離合計が最小（= ウィンドウ外枠に近い）ものを選ぶ
        var best = (H1: default(LineSeg), H2: default(LineSeg), V1: default(LineSeg), V2: default(LineSeg));
        var bestEdgeDistance = double.MaxValue;
        var found = false;

        foreach (var h1 in hs)
        {
            var y1 = PerpCoord(h1, isHorizontal: true);
            foreach (var h2 in hs)
            {
                if (h1 == h2)
                {
                    continue;
                }

                var y2 = PerpCoord(h2, isHorizontal: true);
                if (Math.Abs(y2 - y1) < MinLineLength)
                {
                    continue;
                }

                // 対辺（上辺と下辺）の長さが揃っていること（矩形の対辺は同じ長さ）。
                // コーナーが欠けてスパンが揃わないことがあるため、比率で許容する
                if (!SameLength(ParallelSpan(h1, isHorizontal: true), ParallelSpan(h2, isHorizontal: true)))
                {
                    continue;
                }

                foreach (var v1 in vs)
                {
                    var x1 = PerpCoord(v1, isHorizontal: false);
                    foreach (var v2 in vs)
                    {
                        if (v1 == v2)
                        {
                            continue;
                        }

                        var x2 = PerpCoord(v2, isHorizontal: false);
                        if (Math.Abs(x2 - x1) < MinLineLength)
                        {
                            continue;
                        }

                        // 対辺（左辺と右辺）の長さが揃っていること（矩形の対辺は同じ長さ）
                        if (!SameLength(ParallelSpan(v1, isHorizontal: false), ParallelSpan(v2, isHorizontal: false)))
                        {
                            continue;
                        }

                        // 4 つのコーナーそれぞれで、水平線（h1/h2）または垂直線（v1/v2）の
                        // 少なくとも一方の端点（x と y の両方）がコーナー座標の近傍にあることを確認する。
                        // ウィンドウ枠は角丸・他ウィンドウとの重なりでコーナー付近が欠けるため、
                        // 完全な交差ではなく「どちらかの辺の端点が近くにあれば良い」と緩和する。
                        var xLeft = Math.Min(x1, x2);
                        var xRight = Math.Max(x1, x2);
                        var yTop = Math.Min(y1, y2);
                        var yBottom = Math.Max(y1, y2);
                        if (!HasCornerSupport(h1, h2, v1, v2, xLeft, yTop) ||
                            !HasCornerSupport(h1, h2, v1, v2, xRight, yTop) ||
                            !HasCornerSupport(h1, h2, v1, v2, xLeft, yBottom) ||
                            !HasCornerSupport(h1, h2, v1, v2, xRight, yBottom))
                        {
                            continue;
                        }

                        // スコア: 画像の端からの距離合計（小さいほどウィンドウ外枠に近い。
                        // タイトルバー下端などのウィンドウ内の罫線は端から遠いため選ばれない）
                        var edgeDistance = yTop + (imageHeight - yBottom) + xLeft + (imageWidth - xRight);

                        if (edgeDistance < bestEdgeDistance)
                        {
                            bestEdgeDistance = edgeDistance;
                            best = (h1, h2, v1, v2);
                            found = true;
                        }
                    }
                }
            }
        }

        if (!found)
        {
            return [];
        }

        // 4 辺の端点をコーナー座標に揃えて出力する（枠の外側の境界・排他座標）
        var (bh1, bh2, bv1, bv2) = best;
        var by1 = (int)Math.Round(Math.Min(PerpCoord(bh1, isHorizontal: true), PerpCoord(bh2, isHorizontal: true)));
        var by2 = (int)Math.Round(Math.Max(PerpCoord(bh1, isHorizontal: true), PerpCoord(bh2, isHorizontal: true)));
        var bx1 = (int)Math.Round(Math.Min(PerpCoord(bv1, isHorizontal: false), PerpCoord(bv2, isHorizontal: false)));
        var bx2 = (int)Math.Round(Math.Max(PerpCoord(bv1, isHorizontal: false), PerpCoord(bv2, isHorizontal: false)));

        return
        [
            MakeRectSide(bx1, by1, bx1, by2, LineOrientation.Vertical),
            MakeRectSide(bx2, by1, bx2, by2, LineOrientation.Vertical),
            MakeRectSide(bx1, by1, bx2, by1, LineOrientation.Horizontal),
            MakeRectSide(bx1, by2, bx2, by2, LineOrientation.Horizontal),
        ];
    }

    /// <summary>矩形の辺を復元する際に、同一垂直座標の断片を連結する最大ギャップ（ピクセル）。</summary>
    /// <remarks>
    /// 薄い枠線は HoughLinesP で複数の断片に分割され、コーナー付近や他ウィンドウとの重なりで<br/>
    /// 広いギャップ（100px 超）が生じるため、<see cref="MaxLineGap"/>（10px）より大幅に広い<br/>
    /// 200px で連結して 1 本の辺を復元する。<br/>
    /// 文字列の断片も連結されうるが、連結後のエッジ支持率が低いため<br/>
    /// <see cref="MinEdgeSupportRatio"/> フィルタで除外される。
    /// </remarks>
    private const int RectMergeGap = 200;

    /// <summary>コーナー座標の近傍に、水平線と垂直線の対応する端点が両方あるか判定する。</summary>
    /// <param name="h1">水平線の候補1。</param>
    /// <param name="h2">水平線の候補2。</param>
    /// <param name="v1">垂直線の候補1。</param>
    /// <param name="v2">垂直線の候補2。</param>
    /// <param name="cx">コーナーの X 座標。</param>
    /// <param name="cy">コーナーの Y 座標。</param>
    /// <returns>コーナーを構成する水平辺と垂直辺の<b>対応する端点</b>（x と y の両方）がコーナー座標の近傍にある場合は true。</returns>
    /// <remarks>
    /// コーナーを構成する<b>水平辺の当該端点</b>（cx に最も近い側：左端 or 右端）と
    /// <b>垂直辺の当該端点</b>（cy に最も近い側：上端 or 下端）の<b>両方</b>がコーナー座標の近傍にある必要がある。<br/>
    /// 「どちらかの辺の端点が近くにあれば良い」とすると、ウィンドウの左辺/右辺だけが近接し、
    /// 上辺がコーナーから大きく離れている場合（例: 別の要素の境界線）に誤って合格してしまうため、
    /// 両方の辺でコーナーを構成できることを要求する。<br/>
    /// ウィンドウ枠は角丸・他ウィンドウとの重なりでコーナー付近が欠けるが、欠けは限定的（辺の長さ × <see cref="CornerToleranceRatio"/>）なため、
    /// 対応する端点が近傍にあれば合格する。
    /// </remarks>
    private bool HasCornerSupport(in LineSeg h1, in LineSeg h2, in LineSeg v1, in LineSeg v2, double cx, double cy)
    {
        // 水平辺（h1 or h2）の cx に最も近い端点が近傍、かつ垂直辺（v1 or v2）の cy に最も近い端点が近傍
        return (HasHorizontalCorner(h1, cx, cy) || HasHorizontalCorner(h2, cx, cy))
            && (HasVerticalCorner(v1, cx, cy) || HasVerticalCorner(v2, cx, cy));
    }

    /// <summary>水平線の cx に最も近い端点（左端 or 右端）がコーナー座標の近傍にあるか判定する。</summary>
    /// <param name="seg">水平線分。</param>
    /// <param name="cx">コーナーの X 座標。</param>
    /// <param name="cy">コーナーの Y 座標。</param>
    /// <returns>水平線の cx に対応する端点がコーナー座標の近傍（許容差以内）にある場合は true。</returns>
    /// <remarks>コーナーを構成する水平辺の端点（cx に最も近い側）と Y 座標がコーナー近傍にある必要がある。</remarks>
    private bool HasHorizontalCorner(in LineSeg seg, double cx, double cy)
    {
        var tolerance = Math.Max(ParallelSpan(seg, isHorizontal: true) * CornerToleranceRatio, MinCornerTolerance);

        var y = PerpCoord(seg, isHorizontal: true);
        if (Math.Abs(y - cy) > tolerance)
        {
            return false;
        }

        var left = Math.Min(seg.X1, seg.X2);
        var right = Math.Max(seg.X1, seg.X2);
        // cx に最も近い端点を判定する
        var nearest = Math.Abs(left - cx) <= Math.Abs(right - cx) ? left : right;
        return Math.Abs(nearest - cx) <= tolerance;
    }

    /// <summary>垂直線の cy に最も近い端点（上端 or 下端）がコーナー座標の近傍にあるか判定する。</summary>
    /// <param name="seg">垂直線分。</param>
    /// <param name="cx">コーナーの X 座標。</param>
    /// <param name="cy">コーナーの Y 座標。</param>
    /// <returns>垂直線の cy に対応する端点がコーナー座標の近傍（許容差以内）にある場合は true。</returns>
    /// <remarks>コーナーを構成する垂直辺の端点（cy に最も近い側）と X 座標がコーナー近傍にある必要がある。</remarks>
    private bool HasVerticalCorner(in LineSeg seg, double cx, double cy)
    {
        var tolerance = Math.Max(ParallelSpan(seg, isHorizontal: false) * CornerToleranceRatio, MinCornerTolerance);

        var x = PerpCoord(seg, isHorizontal: false);
        if (Math.Abs(x - cx) > tolerance)
        {
            return false;
        }

        var top = Math.Min(seg.Y1, seg.Y2);
        var bottom = Math.Max(seg.Y1, seg.Y2);
        // cy に最も近い端点を判定する
        var nearest = Math.Abs(top - cy) <= Math.Abs(bottom - cy) ? top : bottom;
        return Math.Abs(nearest - cy) <= tolerance;
    }

    /// <summary>2 つのスパン（平行方向の長さ）がほぼ同じか判定する。</summary>
    /// <param name="span1">一方のスパン。</param>
    /// <param name="span2">他方のスパン。</param>
    /// <returns>スパンの差が大きい方の比率 <see cref="OppositeSpanRatio"/> 以内なら true。</returns>
    /// <remarks>矩形の対辺（上辺と下辺・左辺と右辺）の長さ比較に使用する。<br/>
    /// コーナーが欠けてスパンが揃わないことがあるため、比率で許容する。</remarks>
    private bool SameLength(double span1, double span2)
    {
        var max = Math.Max(span1, span2);
        return max <= 0 || Math.Abs(span1 - span2) / max <= OppositeSpanRatio;
    }

    /// <summary>線分の平行方向の長さ（水平線は X 幅・垂直線は Y 幅）を返す。</summary>
    /// <param name="seg">対象の線分。</param>
    /// <param name="isHorizontal">水平線分の場合は true。</param>
    /// <returns>平行方向の長さ（ピクセル）。</returns>
    /// <remarks>矩形の対辺（上辺と下辺・左辺と右辺）の長さ比較に使用する。</remarks>
    private static double ParallelSpan(in LineSeg seg, bool isHorizontal)
    {
        return isHorizontal ? Math.Abs(seg.X2 - seg.X1) : Math.Abs(seg.Y2 - seg.Y1);
    }

    /// <summary>近接する平行線（外縁・内縁）を 1 本の線分へ統合する。</summary>
    /// <param name="segments">同一向きの線分群。</param>
    /// <param name="isHorizontal">水平線分の場合は true。</param>
    /// <param name="tolerance">統合する平行線の最大距離（ピクセル）。</param>
    /// <param name="imageCenter">画像中心の垂直方向座標（水平線分は Y・垂直線分は X）。代表座標の決定に使用。</param>
    /// <param name="edges">Canny エッジ画像（エッジ支持率の判定に使用）。</param>
    /// <returns>統合後の線分一覧（垂直座標は代表線分・水平範囲はマージ）。</returns>
    /// <remarks>
    /// ウィンドウ枠は線幅があるため外縁と内縁が別々に検出される。<br/>
    /// <paramref name="tolerance"/> 以内の平行線を 1 本に統合する。<br/>
    /// 代表座標は「エッジ支持率（<see cref="EdgeSupportRatio"/>）が最も高い線分」を採用する。<br/>
    /// 透過枠や別ウィンドウの重なりで複数の平行線に分裂しても、連続エッジ（= 実際の枠線）を優先する。<br/>
    /// 同点（どちらも連続エッジ）の場合は画像中心に近い側（ウィンドウ内側の枠線）を採用する。
    /// </remarks>
    private List<LineSeg> MergeParallelLines(
        List<LineSeg> segments, bool isHorizontal, double tolerance, double imageCenter, Mat edges)
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

        var result = new List<LineSeg>();
        foreach (var cluster in clusters)
        {
            if (cluster.Count == 0)
            {
                continue;
            }

            var start = int.MaxValue;
            var end = int.MinValue;
            foreach (var seg in cluster)
            {
                start = Math.Min(start, ParallelStart(seg, isHorizontal));
                end = Math.Max(end, ParallelEnd(seg, isHorizontal));
            }

            // 代表座標は「エッジ支持率が最も高い線分」を採用する。
            // 透過枠は内縁・外縁・別ウィンドウの線に分裂するが、連続エッジ（= 実際の枠線）を優先する。
            // 同点の場合は「平行方向の長さ（スパン）が最も長い線分」を採用する
            // （枠線はウィンドウの全長に達し、罫線や別ウィンドウの線は短い）
            var bestSeg = cluster[0];
            var bestSupport = -1.0;
            foreach (var seg in cluster)
            {
                var support = EdgeSupportRatio(seg, edges, isHorizontal);
                var isLonger = ParallelSpan(seg, isHorizontal) > ParallelSpan(bestSeg, isHorizontal);
                if (support > bestSupport + 1e-9 || (Math.Abs(support - bestSupport) <= 1e-9 && isLonger))
                {
                    bestSupport = support;
                    bestSeg = seg;
                }
            }

            result.Add(MakeLine(start, end, PerpCoord(bestSeg, isHorizontal), isHorizontal));
        }

        return result;
    }

    /// <summary>線分の垂直方向座標（水平線は Y・垂直線は X）を、元画像の輝度プロファイルの枠の外側の境界（排他座標の辺）へ補正する。</summary>
    /// <param name="seg">対象の線分（クラスタ平均の位置）。</param>
    /// <param name="isHorizontal">水平線分の場合は true。</param>
    /// <param name="imageCenter">画像中心の垂直方向座標（水平線分は Y・垂直線分は X）。背景（ウィンドウ内側）の判定に使用。</param>
    /// <param name="gray">グレースケール画像（輝度プロファイルの算出に使用）。</param>
    /// <returns>枠の外側の境界（排他座標）へ補正した線分。枠線が見つからない場合は元の線分。</returns>
    /// <remarks>
    /// Canny のガウシアンブラーはエッジ位置を内側にシフトさせるため、<br/>
    /// 元画像の輝度プロファイルから枠線の位置を直接求める。<br/>
    /// 1. 線分の垂直方向座標の近傍で、各列（垂直線）/各行（水平線）の平均輝度を計算する<br/>
    /// 2. <b>クラスタ中心より画像中心側（= ウィンドウ内側）のピクセル群の中央値</b>を背景の輝度の初期値とし、<br/>
    ///    背景と異なるピクセル（枠線）を除外しながら平均を収束させる（反復平均）<br/>
    ///    （ウィンドウ内の背景はクラスタ中心の内側にある。上辺なら下側・左辺なら右側）<br/>
    /// 3. 枠線コア（クラスタ中心の近傍 ±<see cref="BandRadius"/> で背景と異なるピクセルのうち<br/>
    ///    クラスタ中心に最も近いもの）を求め、そこから<b>ウィンドウ外側方向</b>（右辺・下辺は +、左辺・上辺は −）へ<br/>
    ///    背景と異なるピクセルが続く限り進み、<b>外側の境界（排他座標の辺）</b>を採用する<br/>
    ///    例: 外枠 (50,40)-(850,600)・線幅4px は右辺 852・左辺 48・下辺 602・上辺 38<br/>
    /// 背景の色（黒・白・青等）や線の色に依存せず、輝度の変化点で枠線を検出できる。
    /// </remarks>
    private static LineSeg SnapToEdgeByGradient(in LineSeg seg, bool isHorizontal, double imageCenter, Mat gray)
    {
        var perp = PerpCoord(seg, isHorizontal);
        var center = (int)Math.Round(perp);
        const int searchRadius = EdgeSearchRadius;
        var grayWidth = gray.Width;
        var grayHeight = gray.Height;

        if (isHorizontal)
        {
            // 水平線分: Y 方向の輝度プロファイル（線分の X 範囲で平均）を計算する
            var xStart = Math.Min(seg.X1, seg.X2);
            var xEnd = Math.Max(seg.X1, seg.X2);
            var yLo = Math.Max(0, center - searchRadius);
            var yHi = Math.Min(grayHeight - 1, center + searchRadius);
            var lumas = new double[yHi - yLo + 1];
            for (var y = yLo; y <= yHi; y++)
            {
                double sum = 0;
                var count = 0;
                for (var x = xStart; x <= xEnd; x++)
                {
                    if (x >= 0 && x < grayWidth)
                    {
                        sum += gray.At<byte>(y, x);
                        count++;
                    }
                }

                lumas[y - yLo] = count > 0 ? sum / count : -1;
            }

            var edgeY = FindFrameCorePosition(lumas, yLo, perp, imageCenter);
            if (edgeY is { } newY)
            {
                return new LineSeg(seg.X1, (int)Math.Round(newY), seg.X2, (int)Math.Round(newY));
            }
        }
        else
        {
            // 垂直線分: X 方向の輝度プロファイル（線分の Y 範囲で平均）を計算する
            var yStart = Math.Min(seg.Y1, seg.Y2);
            var yEnd = Math.Max(seg.Y1, seg.Y2);
            var xLo = Math.Max(0, center - searchRadius);
            var xHi = Math.Min(grayWidth - 1, center + searchRadius);
            var lumas = new double[xHi - xLo + 1];
            for (var x = xLo; x <= xHi; x++)
            {
                double sum = 0;
                var count = 0;
                for (var y = yStart; y <= yEnd; y++)
                {
                    if (y >= 0 && y < grayHeight)
                    {
                        sum += gray.At<byte>(y, x);
                        count++;
                    }
                }

                lumas[x - xLo] = count > 0 ? sum / count : -1;
            }

            var edgeX = FindFrameCorePosition(lumas, xLo, perp, imageCenter);
            if (edgeX is { } newX)
            {
                return new LineSeg((int)Math.Round(newX), seg.Y1, (int)Math.Round(newX), seg.Y2);
            }
        }

        return seg;
    }

    /// <summary>輝度プロファイルから、クラスタ中心の近傍（枠線の帯）で枠の外側の境界（排他座標の辺）の位置を求める。</summary>
    /// <param name="lumas">垂直方向座標の近傍の各列/行の平均輝度（-1 は無効）。</param>
    /// <param name="baseCoord">輝度プロファイルの先頭に対応する座標（X または Y）。</param>
    /// <param name="centerCoord">検出線分の垂直方向座標（クラスタ中心・X または Y）。</param>
    /// <param name="imageCenter">画像中心の垂直方向座標（X または Y）。背景（ウィンドウ内側）の判定に使用。</param>
    /// <returns>枠の外側の境界の位置（座標）。枠線が見つからない場合は null。</returns>
    /// <remarks>
    /// 1. <b>クラスタ中心より画像中心側（= ウィンドウ内側）のピクセル群の中央値</b>を背景の輝度の初期値とし、<br/>
    ///    背景と異なるピクセル（枠線）を除外しながら平均を収束させる（反復平均・最大 4 回）<br/>
    ///    （上辺なら Y が大きい側・下辺なら Y が小さい側・左辺なら X が大きい側・右辺なら X が小さい側がウィンドウ内）<br/>
    ///    中央値初期化により、太い枠線（4px 等）でも白と黒の中間値にならず背景色を正確に推定できる<br/>
    /// 2. クラスタ中心の近傍（±<see cref="BandRadius"/>）で、背景と異なるピクセルのうち<br/>
    ///    <b>クラスタ中心に最も近いもの（枠線コア）</b>を求める<br/>
    /// 3. 枠線コアから<b>ウィンドウ外側方向</b>（右辺・下辺は +、左辺・上辺は −）へ背景と異なるピクセルが続く限り進み、<br/>
    ///    <b>外側の境界（排他座標の辺）</b>を返す<br/>
    ///   ・右辺・下辺（外側方向 +）: 背景に戻った位置（= 最後の非背景ピクセル + 1）が右端・下端<br/>
    ///   ・左辺・上辺（外側方向 −）: 最初の非背景（枠線コア）が左端・上端<br/>
    ///   ・帯の端まで非背景が続く場合（影・壁紙）: 枠線コア（= 影・壁紙の始まり）を境界とする<br/>
    /// 背景の色（黒・白・青等）や線の色に依存せず、輝度の変化点で枠線を検出できる。
    /// </remarks>
    private static double? FindFrameCorePosition(double[] lumas, int baseCoord, double centerCoord, double imageCenter)
    {
        const double backgroundTolerance = 15.0;
        var n = lumas.Length;
        if (n < 2)
        {
            return null;
        }

        // クラスタ中心より画像中心側（= ウィンドウ内側）のピクセル群を収集する。
        // 例: 上辺（Y=21・画像中心 Y=632）なら Y > 21 の側がウィンドウ内。
        //     探索範囲にはウィンドウ外の暗い領域も含まれるが、内側はウィンドウ内のため正しく推定できる
        var inside = new List<double>(n);
        for (var i = 0; i < n; i++)
        {
            var coord = baseCoord + i;
            var isInside = imageCenter > centerCoord ? coord >= centerCoord : coord <= centerCoord;
            var l = lumas[i];
            if (l >= 0 && isInside)
            {
                inside.Add(l);
            }
        }

        if (inside.Count < 1)
        {
            return null;
        }

        // 背景 = 反復平均（中央値で初期化）。
        // 平均のままでは白（背景）と黒（枠線）の中間になり両者を非背景と誤判定するため、
        // 中央値を初期値にして枠線（背景と異なるピクセル）を除外しながら平均を収束させる
        inside.Sort();
        var bg = inside[inside.Count / 2];
        for (var iter = 0; iter < 4; iter++)
        {
            double sum = 0;
            var count = 0;
            foreach (var l in inside)
            {
                if (Math.Abs(l - bg) <= backgroundTolerance)
                {
                    sum += l;
                    count++;
                }
            }

            if (count == 0 || count == inside.Count)
            {
                break;
            }

            bg = sum / count;
        }

        // クラスタ中心の近傍（枠線の帯）で、背景と異なるピクセルのうち中心に最も近いものを枠線コアとする
        var coreIndex = -1;
        var bestDist = double.MaxValue;
        for (var i = 0; i < n; i++)
        {
            var l = lumas[i];
            if (l < 0 || Math.Abs(baseCoord + i - centerCoord) > BandRadius)
            {
                continue;
            }

            // 背景とほぼ等しいピクセルは枠線ではない（背景領域）
            if (Math.Abs(l - bg) <= backgroundTolerance)
            {
                continue;
            }

            var dist = Math.Abs(baseCoord + i - centerCoord);
            if (dist < bestDist)
            {
                bestDist = dist;
                coreIndex = i;
            }
        }

        if (coreIndex < 0)
        {
            return null;
        }

        // 外側の境界（排他座標の辺）を返す。
        // 枠線コアからウィンドウ外側方向（右辺・下辺は +、左辺・上辺は −）へ、
        // 背景と異なるピクセルが続く限り進み、背景に戻った位置が外側の境界。
        // ・右辺・下辺（外側方向 +）: 戻った位置（= 最後の非背景ピクセル + 1）が右端・下端
        // ・左辺・上辺（外側方向 −）: 最初の非背景（枠線コア）が左端・上端
        // ・帯の端まで非背景が続く場合（影・壁紙）: 枠線コア（= 影・壁紙の始まり）を境界とする
        var firstCoreIndex = coreIndex;
        var outward = centerCoord > imageCenter ? 1 : -1;
        for (var i = coreIndex + outward; i >= 0 && i < n && Math.Abs(baseCoord + i - centerCoord) <= BandRadius; i += outward)
        {
            var l = lumas[i];
            if (l < 0 || Math.Abs(l - bg) <= backgroundTolerance)
            {
                // 背景に戻った
                return outward > 0 ? baseCoord + i : baseCoord + firstCoreIndex;
            }

            coreIndex = i;
        }

        // 帯の端まで非背景が続く（影・壁紙）: 枠線コアを境界とする
        return baseCoord + firstCoreIndex;
    }

    /// <summary>枠線の帯とみなすクラスタ中心からの距離（ピクセル）。</summary>
    /// <remarks>
    /// ウィンドウ枠の線幅（通常 1〜4px）と内縁・外縁の間隔をカバーする。<br/>
    /// 広すぎると隣接する別の線（ウィンドウ内の罫線等）を拾う恐れがある。
    /// </remarks>
    private const int BandRadius = 5;

    /// <summary>枠線コアの補正で探索する垂直方向の範囲（ピクセル）。</summary>
    /// <remarks>
    /// ウィンドウ枠の線幅（数ピクセル）と検出誤差をカバーする範囲を設定する。<br/>
    /// 狭すぎると帯全体が入らず、広すぎると隣接する別の線を拾う恐れがある。
    /// </remarks>
    private const int EdgeSearchRadius = 10;

    /// <summary>矩形の辺（コーナー座標で揃えた線分）を生成する。</summary>
    /// <param name="x1">始点 X（コーナー座標）。</param>
    /// <param name="y1">始点 Y（コーナー座標）。</param>
    /// <param name="x2">終点 X（コーナー座標）。</param>
    /// <param name="y2">終点 Y（コーナー座標）。</param>
    /// <param name="orientation">辺の向き。</param>
    /// <returns>コーナー座標で揃えた矩形の辺。</returns>
    private static LineDetectionResult MakeRectSide(int x1, int y1, int x2, int y2, LineOrientation orientation)
    {
        var length = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        return new LineDetectionResult(new AvaloniaPoint(x1, y1), new AvaloniaPoint(x2, y2), orientation, length);
    }

    /// <summary>線分に沿った Canny エッジピクセルの割合を計算する。</summary>
    /// <param name="seg">対象の線分。</param>
    /// <param name="edges">Canny エッジ画像。</param>
    /// <param name="isHorizontal">水平線分の場合は true。</param>
    /// <param name="bandRadius">垂直方向の帯の半径（ピクセル）。既定は <see cref="EdgeBandRadius"/>。</param>
    /// <returns>線分上のエッジピクセルの割合（0.0〜1.0）。線分の長さが 0 の場合は 0.0。</returns>
    /// <remarks>
    /// 線分を 1 ピクセル刻みで辿り、各点の<b>垂直方向の帯（±<paramref name="bandRadius"/>）</b>に<br/>
    /// エッジ（輝度 &gt; 0）がある割合を返す。<br/>
    /// 帯で判定することで、<see cref="SnapToEdgeByGradient"/> による 1〜2px の位置ずれを吸収する。<br/>
    /// ウィンドウ枠はエッジが連続しているため割合が高く、<br/>
    /// 文字列の水平エッジは文字間でエッジが欠けるため割合が低くなる。<br/>
    /// 帯を 0 にすると線分自体の位置のみで判定する（近接する別の線のエッジを借用しない）。
    /// </remarks>
    private static double EdgeSupportRatio(in LineSeg seg, Mat edges, bool isHorizontal, int bandRadius = EdgeBandRadius)
    {
        var length = Length(seg);
        var steps = (int)Math.Ceiling(length);
        if (steps <= 0)
        {
            return 0.0;
        }

        var edgeCount = 0;
        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            var px = (int)Math.Round(seg.X1 + (seg.X2 - seg.X1) * t);
            var py = (int)Math.Round(seg.Y1 + (seg.Y2 - seg.Y1) * t);
            if (HasEdgeInBand(edges, px, py, isHorizontal, bandRadius))
            {
                edgeCount++;
            }
        }

        return (double)edgeCount / (steps + 1);
    }

    /// <summary>座標の垂直方向の帯（水平線は Y 方向・垂直線は X 方向）にエッジピクセルが存在するか判定する。</summary>
    /// <param name="edges">Canny エッジ画像。</param>
    /// <param name="x">中心の X 座標。</param>
    /// <param name="y">中心の Y 座標。</param>
    /// <param name="isHorizontal">水平線分の場合は true。</param>
    /// <param name="radius">帯の半径（ピクセル）。</param>
    /// <returns>帯のいずれかにエッジ（輝度 &gt; 0）が存在する場合は true。</returns>
    private static bool HasEdgeInBand(Mat edges, int x, int y, bool isHorizontal, int radius)
    {
        if (isHorizontal)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                var ny = y + dy;
                if (ny < 0 || ny >= edges.Height)
                {
                    continue;
                }

                if (edges.At<byte>(ny, x) > 0)
                {
                    return true;
                }
            }
        }
        else
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var nx = x + dx;
                if (nx < 0 || nx >= edges.Width)
                {
                    continue;
                }

                if (edges.At<byte>(y, nx) > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>エッジ支持率の判定で許容する位置ずれの幅（ピクセル）。</summary>
    /// <remarks>
    /// 枠線コア補正（<see cref="SnapToEdgeByGradient"/>）による 1px 程度の位置差を吸収する。<br/>
    /// 2px に広げると近接する罫線が枠線のエッジを借用して支持率が高くなるため 1px に留める。
    /// </remarks>
    private const int EdgeBandRadius = 1;

    /// <summary>線分を垂直方向（水平線は Y・垂直線は X）でクラスタリングする。</summary>
    /// <param name="segments">同一向きの線分群。</param>
    /// <param name="isHorizontal">水平線分の場合は true。</param>
    /// <returns>垂直方向が近い線分のグループ一覧。</returns>
    private List<List<LineSeg>> ClusterByPerpendicular(List<LineSeg> segments, bool isHorizontal)
    {
        var clusters = new List<List<LineSeg>>();
        var anchors = new List<double>();

        foreach (var seg in segments.OrderBy(s => PerpCoord(s, isHorizontal)))
        {
            var perp = PerpCoord(seg, isHorizontal);
            var index = -1;

            for (var i = 0; i < clusters.Count; i++)
            {
                if (Math.Abs(anchors[i] - perp) <= ClusterTolerance)
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
    private List<LineSeg> MergeCollinear(List<LineSeg> cluster, bool isHorizontal, int gap)
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

    /// <summary>水平 / 垂直方向の範囲と垂直方向の座標から線分を生成する。</summary>
    /// <param name="start">水平方向の始点座標。</param>
    /// <param name="end">水平方向の終点座標。</param>
    /// <param name="perp">垂直方向の座標（水平線は Y・垂直線は X）。</param>
    /// <param name="isHorizontal">水平線分の場合は true。</param>
    /// <returns>生成した線分。</returns>
    private static LineSeg MakeLine(int start, int end, double perp, bool isHorizontal)
        => isHorizontal
            ? new LineSeg(start, (int)Math.Round(perp), end, (int)Math.Round(perp))
            : new LineSeg((int)Math.Round(perp), start, (int)Math.Round(perp), end);

    /// <summary>線分の長さを計算する。</summary>
    private static double Length(in LineSeg s)
        => Math.Sqrt((s.X2 - s.X1) * (s.X2 - s.X1) + (s.Y2 - s.Y1) * (s.Y2 - s.Y1));

    /// <summary>線分の垂直方向座標（水平線は Y・垂直線は X）を返す。</summary>
    private static double PerpCoord(in LineSeg s, bool isHorizontal)
        => isHorizontal ? (s.Y1 + s.Y2) / 2.0 : (s.X1 + s.X2) / 2.0;

    /// <summary>線分の水平方向の始点座標（水平線は X・垂直線は Y）を返す。</summary>
    private static int ParallelStart(in LineSeg s, bool isHorizontal)
        => isHorizontal ? Math.Min(s.X1, s.X2) : Math.Min(s.Y1, s.Y2);

    /// <summary>線分の水平方向の終点座標（水平線は X・垂直線は Y）を返す。</summary>
    private static int ParallelEnd(in LineSeg s, bool isHorizontal)
        => isHorizontal ? Math.Max(s.X1, s.X2) : Math.Max(s.Y1, s.Y2);
}
