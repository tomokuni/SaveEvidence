using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;

namespace Estable.DragRect.Decoration;

/// <summary>暗転オーバーレイの幾何計算（純粋関数）。</summary>
/// <remarks>
/// 穴あけ矩形の決定・枠線矩形・論理座標（ドメイン）から DIP への変換を単一所有する。<br/>
/// 各 UI（Avalonia / MewUI）は返された <see cref="EsRect"/> を自身の描画 API へ渡すだけにする。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class OuterDecoOverlayGeometry
{
    /// <summary>枠線の太さの下限（DIP。0 以下の指定を無効化する）。</summary>
    private const double BorderThicknessLowerBound = 0.5;

    /// <summary>座標変換ポートを決定する（ポート優先・未指定なら倍率による等倍スケール）。</summary>
    /// <param name="port">座標変換ポート。指定されている場合はそれをそのまま使う。</param>
    /// <param name="scaling">倍率。ポート未指定の場合に等倍スケールとして適用する。</param>
    /// <returns>使用する座標変換ポート。</returns>
    /// <remarks>
    /// 「ポートがあればそれを使い、無ければ倍率（0 以下・等倍は等価変換）を使う」という規則を単一所有する。<br/>
    /// 各 UI はポートと倍率をそのまま渡すだけで、どちらを使うかを判断しない（両 UI で同じ結果になる）。
    /// </remarks>
    public static ICoordTransform ResolvePort(ICoordTransform? port, double scaling)
    {
        if (port is { } specified)
        {
            return specified;
        }

        return scaling > 0 && scaling != 1.0
            ? CoordTransform.Scale(scaling)
            : CoordTransform.Identity;
    }

    /// <summary>論理座標の矩形をポート経由で DIP 矩形へ変換する。</summary>
    /// <param name="logical">論理座標（ドメイン）の矩形。</param>
    /// <param name="port">座標変換ポート。null の場合は等価変換。</param>
    /// <param name="closedInterval">閉区間（終点 +1）として扱うか。既定は true。</param>
    /// <returns>DIP 座標の矩形。</returns>
    /// <remarks>
    /// 選択領域は閉区間（始点・終点とも含む）を意図する仕様のため、<br/>
    /// 終点（Right / Bottom）に +1 してから DIP 変換する（終点のピクセルも非暗転領域に含める）。<br/>
    /// ハイライト矩形（スクリーン・ウィンドウ境界）は最初から全ピクセルを含む閉区間表現のため、<br/>
    /// <paramref name="closedInterval"/> を false にして +1 しない（1px 大きく表示されるのを防ぐ）。
    /// </remarks>
    public static EsRect ToDipRect(EsRect logical, ICoordTransform? port = null, bool closedInterval = true)
    {
        var transform = port.OrIdentity();
        var topLeft = transform.Inverse(new EsPoint(logical.X, logical.Y));
        var bottomRight = transform.Inverse(closedInterval
            ? new EsPoint(logical.Right + 1, logical.Bottom + 1)
            : new EsPoint(logical.Right, logical.Bottom));

        return new EsRect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    /// <summary>選択矩形の四辺に描く枠線の矩形を計算する。</summary>
    /// <param name="rect">選択矩形（DIP 座標）。</param>
    /// <param name="thickness">枠線の太さ（DIP）。0.5 未満は 0.5 に正規化される。</param>
    /// <returns>上・下・左・右の順の枠線矩形（4 個）。</returns>
    /// <remarks>
    /// 各辺は矩形の内側に描く（上辺は Y、下辺は Bottom − t、左右は内側に t 幅）。<br/>
    /// 高さ・幅が負にならないよう 0 でクランプする。
    /// </remarks>
    public static IReadOnlyList<EsRect> GetBorderRects(EsRect rect, double thickness)
    {
        var t = Math.Max(BorderThicknessLowerBound, thickness);
        return
        [
            new EsRect(rect.X, rect.Y, rect.Width, t),                                                  // 上
            new EsRect(rect.X, rect.Bottom - t, rect.Width, t),                                         // 下
            new EsRect(rect.X, rect.Y + t, t, Math.Max(0, rect.Height - 2 * t)),                        // 左
            new EsRect(rect.Right - t, rect.Y + t, t, Math.Max(0, rect.Height - 2 * t)),                // 右
        ];
    }

    /// <summary>穴あけ矩形（暗転オーバーレイで穴をあける領域）を決定する。</summary>
    /// <param name="hasHighlight">ハイライトが有効か。</param>
    /// <param name="highlightRect">ハイライト矩形（DIP）。null の場合は無効。</param>
    /// <param name="hasSelection">選択が有効か。</param>
    /// <param name="selectionRect">選択矩形（DIP）。null の場合は無効。</param>
    /// <returns>穴あけ矩形。ハイライト優先・次に選択矩形。ハイライト/選択が無効の場合は null。</returns>
    public static EsRect? ResolveHoleRect(
        bool hasHighlight, EsRect? highlightRect, bool hasSelection, EsRect? selectionRect)
        => hasHighlight ? highlightRect
            : hasSelection ? selectionRect
            : null;

    /// <summary>暗転オーバーレイを描画するかどうかを判定する。</summary>
    /// <param name="holeRect">穴あけ矩形（DIP）。null の場合は全面暗転を検討する。</param>
    /// <param name="showFullOverlay">選択なしでも全面暗転を表示するか。</param>
    /// <returns>暗転オーバーレイを描画する場合は true。</returns>
    /// <remarks>
    /// 穴あけ矩形がある場合、または全面暗転表示が有効な場合は描画する。<br/>
    /// 穴あけ矩形がある場合の暗転は「クライアント矩形 − 穴あけ矩形」の 4 領域で表現する（穴あけ矩形を除いた帯状の矩形）。
    /// </remarks>
    public static bool ShouldDrawOverlay(EsRect? holeRect, bool showFullOverlay)
        => holeRect.HasValue || showFullOverlay;

    /// <summary>クライアント矩形から穴あけ矩形を除いた暗転領域（4 帯）を計算する。</summary>
    /// <param name="client">クライアント矩形（DIP）。</param>
    /// <param name="hole">穴あけ矩形（DIP）。null の場合は全面（1 領域）。</param>
    /// <returns>暗転して塗る矩形の一覧（上・下・左・右の順）。幅・高さが 0 以下の領域は含めない。</returns>
    /// <remarks>
    /// 穴あけ矩形が完全にクライアント外の場合は全面を返す（描画漏れを防ぐ）。
    /// </remarks>
    public static IReadOnlyList<EsRect> ComputeOverlayRegions(EsRect client, EsRect? hole)
    {
        if (hole is not { } h)
        {
            return [client];
        }

        // 穴あけ矩形をクライアント内へクランプする（クライアント外の部分は暗転しない）
        var left = Math.Clamp(h.X, client.X, client.Right);
        var top = Math.Clamp(h.Y, client.Y, client.Bottom);
        var right = Math.Clamp(h.Right, client.X, client.Right);
        var bottom = Math.Clamp(h.Bottom, client.Y, client.Bottom);

        if (right <= left || bottom <= top)
        {
            // 穴あけ矩形がクライアントと交差しない: 全面を暗転する
            return [client];
        }

        var regions = new List<EsRect>(4);
        AddRegion(regions, new EsRect(client.X, client.Y, client.Width, top - client.Y));                            // 上
        AddRegion(regions, new EsRect(client.X, bottom, client.Width, client.Bottom - bottom));                      // 下
        AddRegion(regions, new EsRect(client.X, top, left - client.X, bottom - top));                                // 左
        AddRegion(regions, new EsRect(right, top, client.Right - right, bottom - top));                              // 右
        return regions;
    }

    /// <summary>幅・高さが正の矩形のみを追加する。</summary>
    /// <param name="regions">追加先のリスト。</param>
    /// <param name="rect">追加する矩形。</param>
    private static void AddRegion(List<EsRect> regions, EsRect rect)
    {
        if (rect.Width > 0 && rect.Height > 0)
        {
            regions.Add(rect);
        }
    }
}
