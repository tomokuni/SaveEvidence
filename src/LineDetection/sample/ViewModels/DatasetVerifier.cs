using Avalonia.Media.Imaging;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core.Native.Imaging;
using System.Text.Json;

namespace EsUtil.SaveEvidence.LineDetection.Sample.ViewModels;

/// <summary>データ検証の共通ロジックを提供する静的ユーティリティ。</summary>
/// <remarks>
/// 単一検証（「データ検証」ページ）と全検証（「データ全検証」ページ）の両方から使用する<br/>
/// 検証・照合ロジックを集約する（単一所有）。
/// </remarks>
internal static class DatasetVerifier
{
    /// <summary>検証の保守用許容誤差（ドメイン座標・px）。</summary>
    /// <remarks>検証は 0px 誤差で一致判定する。</remarks>
    public const int Tolerance = 0;

    /// <summary>1 つの dataset エントリを検出し、JSON と照合した結果を返す。</summary>
    /// <param name="entry">検証対象のエントリ。</param>
    /// <param name="detector">使用する検出器。</param>
    /// <returns>検証結果（画像名・一致/不一致・4 辺の詳細）。矩形を復元できない場合は 4 辺が空の結果を返す。</returns>
    /// <remarks>
    /// 検出結果から復元した矩形を <see cref="DatasetEntry.Bounds"/>（ドメイン座標・排他座標）と<br/>
    /// 0px 誤差で照合し、一致 / 不一致を <see cref="VerifyResultItem.IsCorrect"/> に反映する。<br/>
    /// 不一致の場合は、4 辺（左 X・上 Y・右 Right・下 Bottom）それぞれの検出値・正解値・差分を<br/>
    /// 「◯px」の形式で <see cref="VerifyResultItem.DetailLines"/> に格納する。
    /// </remarks>
    public static VerifyResultItem VerifyImage(DatasetEntry entry, ILineDetector detector)
    {
        try
        {
            using var bitmap = new Bitmap(entry.PngPath);
            using var unified = bitmap.ToEsBitmap();
            var results = detector.Detect(unified);

            if (TryReconstructBounds(results) is not { } bound)
            {
                return new VerifyResultItem(entry.Name, false, ["矩形を復元できません"]);
            }

            var gt = entry.Bounds;
            var diffX = AbsDiff(bound.X, gt.X);
            var diffY = AbsDiff(bound.Y, gt.Y);
            var diffR = AbsDiff(bound.Right, gt.Right);
            var diffB = AbsDiff(bound.Bottom, gt.Bottom);

            var correct = diffX <= Tolerance
                && diffY <= Tolerance
                && diffR <= Tolerance
                && diffB <= Tolerance;

            // 4 辺それぞれを 1 行ずつにして見やすく表示する
            var detailLines = new[]
            {
                $"左X: 検出={bound.X} / 正解={gt.X}（{FormatDiff(diffX)}）",
                $"上Y: 検出={bound.Y} / 正解={gt.Y}（{FormatDiff(diffY)}）",
                $"右R: 検出={bound.Right} / 正解={gt.Right}（{FormatDiff(diffR)}）",
                $"下B: 検出={bound.Bottom} / 正解={gt.Bottom}（{FormatDiff(diffB)}）",
            };

            return new VerifyResultItem(entry.Name, correct, detailLines);
        }
        catch (Exception ex)
        {
            return new VerifyResultItem(entry.Name, false, [$"エラー（{ex.Message}）"]);
        }
    }

    /// <summary>検出結果から正解の矩形（排他座標）を復元する。</summary>
    /// <param name="results">検出結果の線分一覧。</param>
    /// <returns>復元した矩形の境界（X/Y/Right/Bottom）。復元できない場合は null。</returns>
    /// <remarks>
    /// ロジック方式は矩形を 4 辺（垂直 2 本・水平 2 本）として返すため、<br/>
    /// 垂直線の X 座標の min/max と水平線の Y 座標の min/max から外側の境界を復元する。<br/>
    /// 垂直 2 本・水平 2 本が揃っていない場合は矩形を復元できない（null）。
    /// </remarks>
    private static DatasetBounds? TryReconstructBounds(IReadOnlyList<LineDetectionResult> results)
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

        if (verticalX.Count < 2 || horizontalY.Count < 2)
        {
            return null;
        }

        return new DatasetBounds(verticalX[0], horizontalY[0], verticalX[^1], horizontalY[^1]);
    }

    /// <summary>2 つの値の差分の絶対値を返す。</summary>
    /// <param name="a">値 1。</param>
    /// <param name="b">値 2。</param>
    /// <returns>|a − b|。</returns>
    private static int AbsDiff(int a, int b) => Math.Abs(a - b);

    /// <summary>差分量（px）を表示用の文字列へ変換する。</summary>
    /// <param name="diff">差分の絶対値（px）。</param>
    /// <returns>「差異なし」または「◯px 相違」。<paramref name="diff"/> が 0 の場合は「差異なし」。</returns>
    private static string FormatDiff(int diff) => diff == 0 ? "差異なし" : $"{diff}px 相違";

    /// <summary>出力ディレクトリの dataset フォルダからエントリ一覧を読み込む。</summary>
    /// <returns>読み込んだエントリ一覧（正解 JSON がある画像のみ）。</returns>
    /// <remarks>
    /// <c>dataset</c> 配下の各 PNG に対応する JSON を探し、<c>GroundTruthWindows[0].Bounds</c> を読み込む。<br/>
    /// 対応する JSON が無い場合は一覧に含めない。フォルダが無い場合は空リスト。
    /// </remarks>
    public static IReadOnlyList<DatasetEntry> LoadDatasetItems()
    {
        var datasetDir = Path.Combine(AppContext.BaseDirectory, "dataset");
        if (!Directory.Exists(datasetDir))
        {
            return [];
        }

        var items = new List<DatasetEntry>();
        foreach (var png in Directory.EnumerateFiles(datasetDir, "*.png").OrderBy(f => f))
        {
            var name = Path.GetFileNameWithoutExtension(png);
            var jsonPath = Path.Combine(datasetDir, name + ".json");
            if (!File.Exists(jsonPath))
            {
                continue;
            }

            try
            {
                var root = JsonSerializer.Deserialize<DatasetRoot>(File.ReadAllText(jsonPath));
                if (root?.GroundTruthWindows.FirstOrDefault()?.Bounds is not { } b)
                {
                    continue;
                }

                items.Add(new DatasetEntry(name, png, new DatasetBounds(b.X, b.Y, b.Right, b.Bottom)));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"dataset 読み込み失敗: {name}（{ex.Message}）");
            }
        }

        return items;
    }
}
