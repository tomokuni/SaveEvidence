using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.MarqueeCore.Decoration;

/// <summary>穴あけ矩形と、その矩形を扱うときの閉区間の指定。</summary>
/// <param name="Rect">穴あけ矩形（座標系は呼び出し側の契約に従う）。</param>
/// <param name="ClosedInterval">閉区間（終点 +1）として扱うか。選択矩形は true、ハイライト矩形は false。</param>
/// <remarks>
/// 「どちらの矩形を穴として優先するか」と「その矩形の終点を +1 するか」は同じ判断であるため 1 つの値として返す<br/>
/// （呼び出し側が優先順位を再判定すると、規則が 2 箇所へ分かれる）。
/// </remarks>
internal readonly record struct HoleRect(EsRect Rect, bool ClosedInterval);

/// <summary>装飾（選択矩形の外側・内側）に共通する幾何計算（純粋関数）。</summary>
/// <remarks>
/// 穴あけ矩形の決定・枠線矩形・論理座標（ドメイン）から DIP への変換を単一所有する。<br/>
/// 選択矩形の外側を描く <see cref="OuterDecoFrame"/> と内側を描く <see cref="InnerDecoFrame"/> が共用するため、<br/>
/// 「穴はどれか」「枠線はどの 4 矩形か」「閉区間の +1 をどう扱うか」の規則が 2 箇所へ分かれない。<br/>
/// 各 UI 実装は返された <see cref="EsRect"/> を自身の描画 API へ渡すだけにする。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。<br/>
/// <b>可視性</b>: 描画内容の判断は各フレームの解決が担うため、本クラスは内部実装とする
/// （UI 実装は返されたフレームを描くだけで、幾何計算を直接呼ばない）。
/// </remarks>
internal static class DecoGeometry
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

    /// <summary>選択矩形の四辺に描く枠線の矩形を計算する。</summary>
    /// <param name="rect">選択矩形（DIP 座標）。</param>
    /// <param name="thickness">枠線の太さ（DIP）。0.5 未満は 0.5 に正規化される。</param>
    /// <returns>上・下・左・右の順の枠線矩形（4 個）。</returns>
    /// <remarks>
    /// 辺の矩形の計算は <see cref="EsRectMath.ComputeInnerEdges"/> が単一所有する（ルーペの外枠と同じ式）。<br/>
    /// 太さの下限のみ本メソッドが決める（選択枠は細い線でも視認できる必要がある）。
    /// </remarks>
    public static IReadOnlyList<EsRect> GetBorderRects(EsRect rect, double thickness)
        => EsRectMath.ComputeInnerEdges(rect, Math.Max(BorderThicknessLowerBound, thickness));

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
    /// 優先順（ハイライト → 選択）と終点の扱い（選択は閉区間 +1、ハイライトは +0）は
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

    /// <summary>描画領域から穴あけ矩形を除いた暗転領域（4 帯）を計算する。</summary>
    /// <param name="clientSize">描画領域のサイズ（DIP）。</param>
    /// <param name="hole">穴あけ矩形（DIP）。null の場合は全面（1 領域）。</param>
    /// <returns>暗転して塗る矩形の一覧（上・下・左・右の順）。幅・高さが 0 以下の領域は含めない。</returns>
    /// <remarks>
    /// 暗転領域は描画先の原点（0, 0）を基準に計算する（描画 API のコンテキスト原点は常にコントロールの左上であるため、<br/>
    /// 親座標を混ぜるとマスクが原点分ずれる。原点を持たない型で受けることで取り違えを防ぐ）。<br/>
    /// 穴あけ矩形が完全に描画領域の外の場合は全面を返す（描画漏れを防ぐ）。
    /// </remarks>
    public static IReadOnlyList<EsRect> ComputeOverlayRegions(EsSize clientSize, EsRect? hole)
    {
        var full = new EsRect(0, 0, clientSize.Width, clientSize.Height);

        if (hole is not { } h)
        {
            return [full];
        }

        // 穴あけ矩形を描画領域内へクランプする（領域外の部分は暗転しない）
        var left = Math.Clamp(h.X, 0, clientSize.Width);
        var top = Math.Clamp(h.Y, 0, clientSize.Height);
        var right = Math.Clamp(h.Right, 0, clientSize.Width);
        var bottom = Math.Clamp(h.Bottom, 0, clientSize.Height);

        if (right <= left || bottom <= top)
        {
            // 穴あけ矩形が描画領域と交差しない: 全面を暗転する
            return [full];
        }

        var regions = new List<EsRect>(4);
        AddRegion(regions, new EsRect(0, 0, clientSize.Width, top));                                                 // 上
        AddRegion(regions, new EsRect(0, bottom, clientSize.Width, clientSize.Height - bottom));                      // 下
        AddRegion(regions, new EsRect(0, top, left, bottom - top));                                                   // 左
        AddRegion(regions, new EsRect(right, top, clientSize.Width - right, bottom - top));                           // 右
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
