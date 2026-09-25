using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.CaptureSession;

/// <summary>モニターの表示倍率（サイズラベルの DisplayScale）を解決する純粋関数。</summary>
/// <remarks>
/// 仮想スクリーン全体を 1 枚のウィンドウで覆うとウィンドウの倍率は 1 つに定まるため、<br/>
/// モニターごとの実倍率は「対象矩形がどのモニターに含まれるか」から解決する必要がある。<br/>
/// 判定点（矩形の中心）と「含まれるモニターが無い場合のフォールバック」の規則を本クラスが単一所有する<br/>
/// （UI ごとに判定点がずれると、同じ矩形で表示倍率が食い違う）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class MonitorScalingMath
{
    /// <summary>モニター矩形とその表示倍率の組。</summary>
    /// <param name="Bounds">モニターの矩形（画面物理ピクセル）。</param>
    /// <param name="Scale">モニターの表示倍率（1.0 = 100%）。</param>
    public readonly record struct MonitorInfo(EsPixelRect Bounds, double Scale);

    /// <summary>画面のスケーリング一覧から正規化済みのスケーリングを解決する。</summary>
    /// <param name="scalings">画面のスケーリング一覧（UI の画面情報の順序を保つ）。</param>
    /// <returns>先頭の画面のスケーリング（未確定は基準値へ正規化）。画面が 1 枚も無い場合は基準値。</returns>
    /// <remarks>
    /// 「オーナーの画面が見つからない場合（ヘッドレステスト等）は基準値を使う」という規則を単一所有する<br/>
    /// （UI が「一覧が空なら 0 を渡して正規化する」という二段の判定を書かない）。
    /// </remarks>
    public static double ResolveScaling(IEnumerable<double> scalings)
    {
        foreach (var scaling in scalings)
        {
            return DpiScaler.NormalizeScaling(scaling);
        }

        return DpiScaler.StandardScaling;
    }

    /// <summary>モニターの一覧を判定用に整える（表示倍率が未確定のモニターを除外する）。</summary>
    /// <param name="monitors">モニターの矩形と表示倍率の組（取得元の UI API の順序を保つ）。</param>
    /// <returns>表示倍率が確定しているモニターのみの一覧。</returns>
    /// <remarks>
    /// 倍率が未確定（0 以下）のモニターを判定に使うと、そのモニター上の選択で表示倍率が 0 倍として扱われる。<br/>
    /// 「未確定は除外する」という規則を単一所有する（UI ごとに <c>IsValidScaling</c> で絞るかどうかが食い違っていた）。<br/>
    /// セッション中はモニター構成が変わらないため、呼び出し側は 1 回だけ呼んで結果を再利用する。
    /// </remarks>
    public static IReadOnlyList<(EsPixelRect Bounds, double Scale)> Collect(
        IEnumerable<(EsPixelRect Bounds, double Scale)> monitors)
    {
        var collected = new List<(EsPixelRect Bounds, double Scale)>();
        foreach (var monitor in monitors)
        {
            if (DpiScaler.IsValidScaling(monitor.Scale))
            {
                collected.Add(monitor);
            }
        }

        return collected;
    }

    /// <summary>対象矩形の中心を含むモニターの表示倍率を解決する。</summary>
    /// <param name="target">対象の矩形（画面物理ピクセル）。</param>
    /// <param name="monitors">モニターの一覧（矩形と表示倍率の組）。</param>
    /// <param name="fallback">含まれるモニターが無い場合に使う倍率。</param>
    /// <returns>対象矩形の中心を含むモニターの表示倍率。無い場合は <paramref name="fallback"/>。</returns>
    /// <remarks>
    /// 判定点は矩形の<b>中心</b>とする（矩形は 1 つのモニターに収まるのが通常であり、<br/>
    /// 左上を基準にすると枠を跨いだ瞬間に隣のモニターの倍率になる）。<br/>
    /// 中心がどのモニターにも含まれない場合（モニター構成が変わった等）は <paramref name="fallback"/> を返す。
    /// </remarks>
    public static double ResolveForRect(
        EsPixelRect target, IReadOnlyList<(EsPixelRect Bounds, double Scale)> monitors, double fallback)
    {
        if (monitors.Count == 0)
        {
            return fallback;
        }

        var centerX = target.X + (target.Width / 2.0);
        var centerY = target.Y + (target.Height / 2.0);

        foreach (var (bounds, scale) in monitors)
        {
            if (centerX >= bounds.X && centerX < bounds.Right && centerY >= bounds.Y && centerY < bounds.Bottom)
            {
                return scale;
            }
        }

        return fallback;
    }

    /// <summary>ドメイン座標（画像ピクセル）の矩形から、その中心を含むモニターの表示倍率を解決する。</summary>
    /// <param name="domainRect">対象の矩形（ドメイン座標 = 画像ピクセル）。</param>
    /// <param name="origin">ドメイン座標の原点（画面物理ピクセル。ウィンドウ位置または仮想スクリーン原点）。</param>
    /// <param name="monitors">モニターの一覧（矩形と表示倍率の組）。</param>
    /// <param name="fallback">含まれるモニターが無い場合に使う倍率。</param>
    /// <returns>対象矩形の中心を含むモニターの表示倍率。無い場合は <paramref name="fallback"/>。</returns>
    /// <remarks>
    /// ドメイン座標を画面物理座標へ戻す（原点の加算）前処理を本クラスが単一所有する<br/>
    /// （各 UI が原点の加算と丸めを書くと、UI ごとに判定点が 1 ピクセルずれる）。
    /// </remarks>
    public static double ResolveForDomainRect(
        EsRect domainRect, EsPixelPoint origin,
        IReadOnlyList<(EsPixelRect Bounds, double Scale)> monitors, double fallback)
        => ResolveForRect(
            new EsPixelRect(
                (int)Math.Round(domainRect.X, MidpointRounding.AwayFromZero) + origin.X,
                (int)Math.Round(domainRect.Y, MidpointRounding.AwayFromZero) + origin.Y,
                (int)Math.Round(domainRect.Width, MidpointRounding.AwayFromZero),
                (int)Math.Round(domainRect.Height, MidpointRounding.AwayFromZero)),
            monitors, fallback);
}
