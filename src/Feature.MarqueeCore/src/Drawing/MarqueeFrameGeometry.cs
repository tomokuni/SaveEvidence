using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.MarqueeCore.Drawing;

/// <summary>1 フレーム分の描画内容に共通する幾何計算（純粋関数）。</summary>
/// <remarks>
/// <b>フレーム全体の関心事だけを持つ</b>: 座標変換ポートの決定と、論理座標の矩形を DIP 矩形へ変換する手順、<br/>
/// および穴あけ矩形（選択またはハイライトの領域）の決定である。外側の装飾・内側の装飾・ハンドル・<br/>
/// サイズラベルがすべて同じ値を使うため、各所が個別に解決すると矩形がずれ、同じ変換の生成と評価が重複する。<br/>
/// <br/>
/// <b>装飾固有の計算は持たない</b>: 暗転領域の計算と枠線矩形の計算は <c>Decoration.DecoGeometry</c> が単一所有する<br/>
/// （本クラスは装飾を知らない）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。<br/>
/// <b>可視性</b>: 描画内容の判断はフレームの解決が担うため、本クラスは内部実装とする。
/// </remarks>
internal static class MarqueeFrameGeometry
{
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

        return DpiScaler.IsValidScaling(scaling) && scaling != 1.0
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
    /// ハイライト矩形（ホストが既に全ピクセルを含む閉区間として与える矩形）は、<br/>
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

    /// <summary>穴あけ矩形（暗転オーバーレイで穴をあける領域）と閉区間の指定を決定する。</summary>
    /// <param name="highlightRect">ハイライト矩形。null の場合はハイライトなし。</param>
    /// <param name="selectionRect">選択矩形。null の場合は選択なし。</param>
    /// <returns>穴あけ矩形と閉区間の指定。どちらも null の場合は null。</returns>
    /// <remarks>
    /// <b>矩形の有無は null が表す</b>: 有効フラグを別に持つと「フラグは true だが矩形が無い」という<br/>
    /// 意味の無い状態を表現でき、設定経路によって扱いが変わる（矩形を 1 つに絞ることで不可能にする）。<br/>
    /// <b>終点の扱いは矩形の種類で決まる</b>: 選択矩形は閉区間（終点のピクセルも含む）、<br/>
    /// ハイライト矩形はホストが既に全ピクセルを含む閉区間として与えるため +1 しない。
    /// </remarks>
    public static HoleRect? ResolveHoleRect(EsRect? highlightRect, EsRect? selectionRect)
        => highlightRect is { } highlight ? new HoleRect(highlight, ClosedInterval: false)
            : selectionRect is { } selection ? new HoleRect(selection, ClosedInterval: true)
            : null;

    /// <summary>論理座標の矩形から穴あけ矩形（DIP）を決定する。</summary>
    /// <param name="logicalHighlightRect">ハイライト矩形（論理座標）。null の場合はハイライトなし。</param>
    /// <param name="logicalSelectionRect">選択矩形（論理座標）。null の場合は選択なし。</param>
    /// <param name="port">論理座標 → DIP の座標変換ポート。null の場合は等価変換。</param>
    /// <returns>穴あけ矩形（DIP）。どちらも null の場合は null。</returns>
    /// <remarks>
    /// 優先順（ハイライト → 選択）と終点の扱い（選択は閉区間 +1、ハイライトは +0）は<br/>
    /// <see cref="ResolveHoleRect"/> が単一所有し、<b>選ばれた矩形だけを DIP 変換する</b>（両方を変換して片方を捨てない）。<br/>
    /// 装飾の枠線・内側の塗り・サイズラベルが同じ穴あけ矩形を使うため、各所が個別に変換すると矩形がずれる<br/>
    /// （呼び出し側は本メソッドを 1 回だけ呼び、結果を各項目へ渡す）。<br/>
    /// ポートの解決（<see cref="ResolvePort"/>）も呼び出し側が 1 回だけ行う（処理の多重実行を避ける）。
    /// </remarks>
    public static EsRect? ResolveHoleRectDip(
        EsRect? logicalHighlightRect, EsRect? logicalSelectionRect, ICoordTransform? port)
        => ResolveHoleRect(logicalHighlightRect, logicalSelectionRect) is { } hole
            ? ToDipRect(hole.Rect, port, hole.ClosedInterval)
            : null;
}
