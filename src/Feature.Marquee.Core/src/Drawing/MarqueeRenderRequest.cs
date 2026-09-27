using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Marquee.Core.Setting;

namespace EsUtil.Feature.Marquee.Core.Drawing;

/// <summary>1 フレーム分の描画の幾何を解決するための入力（設定・状態と、描画時に決まる値を 1 つにまとめた値）。</summary>
/// <param name="Options">選択矩形の設定（ハンドルの寸法・十字マーカーの長さ・対話パラメータ）。</param>
/// <param name="ClientSize">描画領域のサイズ（DIP。原点は常に 0）。</param>
/// <param name="SelectionRect">選択矩形（ドメイン座標）。選択なしの場合は null（<b>null が「選択なし」を意味する</b>）。</param>
/// <param name="MarkerActive">始点マーカー（十字線）を描くかどうか（nudge 中かつ十字線が可視の場合に true）。</param>
/// <param name="MarkerPosition">始点マーカーの中心（ドメイン座標）。</param>
/// <param name="MousePosition">直近のマウス位置（ドメイン座標。カーソル座標ラベルの基準）。</param>
/// <param name="Port">論理座標 → DIP の座標変換ポート。null の場合は <paramref name="DomainToDipScaling"/> を等倍スケールとして適用する。</param>
/// <param name="DomainToDipScaling">ポート未指定の場合に適用する倍率（0 以下・等倍は等価変換）。</param>
/// <remarks>
/// 設定と状態の単一値（<see cref="Options"/>）に、描画時にしか決まらない値（描画領域のサイズ・座標変換ポート・倍率）を加えたものである。<br/>
/// <b>UI 層は本値を自分で組み立てない</b>: セッションの状態を写す組み立て順は <see cref="Marquee.CreateRequest"/> が単一所有する。<br/>
/// UI 層は本値を <see cref="Marquee.ResolveGeometry"/> へ渡すだけで、描画に必要な値の意味・単位を持たない。<br/>
/// <b>本値から導出する値は本値が単一所有する</b>: 座標変換ポートの決定（<see cref="ResolvedPort"/>）、穴あけ矩形の決定（<see cref="Hole"/> /<br/>
/// <see cref="ResolveHoleRectDip"/>）、論理座標の矩形を DIP 矩形へ変換する手順（<see cref="ToDipRect"/> / <see cref="ToDomainRect"/>）は本値のメンバーが持つ。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public readonly record struct MarqueeRenderRequest(
    MarqueeOptions Options,
    EsSize ClientSize,
    EsRect? SelectionRect,
    bool MarkerActive,
    EsPoint MarkerPosition,
    EsPoint MousePosition,
    ICoordTransform? Port,
    double DomainToDipScaling)
{
    /// <summary>使用する座標変換ポートを取得する（ポート優先・未指定なら倍率による等倍スケール）。</summary>
    /// <value>使用する座標変換ポート。</value>
    /// <remarks>解決の規則は <see cref="ResolvePort"/> が単一所有する。</remarks>
    public ICoordTransform ResolvedPort => ResolvePort(Port, DomainToDipScaling);

    /// <summary>描画面の全体を取得する（左上を原点（0, 0）とする矩形）。</summary>
    /// <value>幅・高さが <see cref="ClientSize"/> の矩形（DIP）。</value>
    internal EsRect SurfaceBounds => new(0, 0, ClientSize.Width, ClientSize.Height);

    /// <summary>穴あけ矩形（暗転オーバーレイで穴をあける領域）を取得する。</summary>
    /// <value>穴あけ矩形。ハイライトも選択も無い場合は null。</value>
    /// <remarks>
    /// 優先順（ハイライト → 選択）と終点の扱い（選択は閉区間 +1、ハイライトは +0）の規則は<br/>
    /// <see cref="ResolveHoleRect"/> が単一所有する（本値は設定と状態を渡すだけである）。
    /// </remarks>
    internal MarqueeHoleRect? Hole => ResolveHoleRect(HighlightRect, SelectionRect);

    /// <summary>ハイライト矩形（ホストが与える実行時状態・ドメイン座標）を取得または設定する。</summary>
    /// <value>ハイライト矩形（null は「ハイライトしない」）。選択矩形より優先される。</value>
    /// <remarks>選択矩形と異なり選択コントロールの操作では変化しない（ホストが与える）。</remarks>
    public EsRect? HighlightRect { get; init; }

    /// <summary>穴あけ矩形を DIP 座標で取得する。</summary>
    /// <param name="port">解決済みの座標変換ポート（1 フレームに 1 回だけ解決した値を渡す）。</param>
    /// <returns>穴あけ矩形（DIP）。ハイライトも選択も無い場合は null。</returns>
    /// <remarks>
    /// 装飾の枠線・内側の塗り・サイズラベルが同じ穴あけ矩形を使うため、<br/>
    /// 呼び出し側は本メソッドを 1 回だけ呼び、結果を各項目へ渡す（各所が個別に変換すると矩形がずれる）。<br/>
    /// 選ばれた矩形だけを DIP 変換する（両方を変換して片方を捨てない）。
    /// </remarks>
    internal EsRect? ResolveHoleRectDip(ICoordTransform port)
        => Hole is { } hole ? ToDipRect(hole.Rect, port, hole.ClosedInterval) : null;

    /// <summary>設定・状態と描画時に決まる値から描画の入力を組み立てる。</summary>
    /// <param name="options">選択矩形の設定。</param>
    /// <param name="clientSize">描画領域のサイズ（DIP。原点は常に 0）。</param>
    /// <param name="selectionRect">選択矩形（ドメイン座標）。選択なしの場合は null。</param>
    /// <param name="markerActive">始点マーカー（十字線）を描くかどうか。</param>
    /// <param name="markerPosition">始点マーカーの中心（ドメイン座標）。</param>
    /// <param name="mousePosition">直近のマウス位置（ドメイン座標）。</param>
    /// <param name="port">論理座標 → DIP の座標変換ポート。null の場合は倍率を使う。</param>
    /// <param name="domainToDipScaling">ポート未指定の場合に適用する倍率。</param>
    /// <param name="highlightRect">ハイライト矩形（ドメイン座標）。null はハイライトなし。</param>
    /// <returns>描画の入力。</returns>
    /// <remarks>
    /// セッションの状態を写す組み立て順は <see cref="Marquee.CreateRequest"/> が単一所有し、<br/>
    /// 本メソッドは値の組を 1 箇所に閉じる（UI 層が引数の並び順を知らずに済み、入力型の生成元が入力型にある）。
    /// </remarks>
    public static MarqueeRenderRequest Create(
        MarqueeOptions options,
        EsSize clientSize,
        EsRect? selectionRect,
        bool markerActive,
        EsPoint markerPosition,
        EsPoint mousePosition,
        ICoordTransform? port,
        double domainToDipScaling,
        EsRect? highlightRect = null)
        => new(options, clientSize, selectionRect, markerActive, markerPosition, mousePosition, port, domainToDipScaling)
        {
            HighlightRect = highlightRect,
        };

    /// <summary>座標変換ポートを決定する（ポート優先・未指定なら倍率による等倍スケール）。</summary>
    /// <param name="port">座標変換ポート。指定されている場合はそれをそのまま使う。</param>
    /// <param name="domainToDipScaling">倍率。ポート未指定の場合に等倍スケールとして適用する。</param>
    /// <returns>使用する座標変換ポート。</returns>
    /// <remarks>
    /// 「ポートがあればそれを使い、無ければ倍率（0 以下・等倍は等価変換）を使う」という規則を単一所有する。<br/>
    /// 各 UI はポートと倍率をそのまま渡すだけで、どちらを使うかを判断しない（両 UI で同じ結果になる）。
    /// </remarks>
    internal static ICoordTransform ResolvePort(ICoordTransform? port, double domainToDipScaling)
    {
        if (port is { } specified)
        {
            return specified;
        }

        return DpiScaler.IsValidScaling(domainToDipScaling) && domainToDipScaling != 1.0
            ? CoordTransform.Scale(domainToDipScaling)
            : CoordTransform.Identity;
    }

    /// <summary>論理座標の矩形をポート経由で DIP 矩形へ変換する。</summary>
    /// <param name="logical">論理座標（ドメイン）の矩形。</param>
    /// <param name="port">解決済みの座標変換ポート（1 フレームに 1 回だけ解決した値を渡す）。</param>
    /// <param name="closedInterval">閉区間（終点 +1）として扱うか。既定は true。</param>
    /// <returns>DIP 座標の矩形。</returns>
    /// <remarks>
    /// 選択領域は閉区間（始点・終点とも含む）を意図する仕様のため、<br/>
    /// 終点（Right / Bottom）に +1 してから DIP 変換する（終点のピクセルも非暗転領域に含める）。<br/>
    /// ハイライト矩形（ホストが既に全ピクセルを含む閉区間として与える矩形）は、<br/>
    /// <paramref name="closedInterval"/> を false にして +1 しない（1px 大きく表示されるのを防ぐ）。
    /// </remarks>
    internal EsRect ToDipRect(EsRect logical, ICoordTransform port, bool closedInterval = true)
    {
        var topLeft = port.Inverse(new EsPoint(logical.X, logical.Y));
        var bottomRight = port.Inverse(closedInterval
            ? new EsPoint(logical.Right + 1, logical.Bottom + 1)
            : new EsPoint(logical.Right, logical.Bottom));

        return new EsRect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    /// <summary>DIP 矩形をドメイン座標の矩形へ逆換算する（左上と右下の順変換から組む）。</summary>
    /// <param name="dipRect">DIP 座標の矩形。</param>
    /// <param name="port">解決済みの座標変換ポート（順変換 = DIP → ドメイン）。</param>
    /// <returns>ドメイン座標の矩形。</returns>
    /// <remarks>ホストが「矩形から表示スケールを解決する」ために使う（コアはモニタを知らない）。</remarks>
    internal EsRect ToDomainRect(EsRect dipRect, ICoordTransform port)
    {
        var topLeft = port.Forward(dipRect.TopLeft);
        var bottomRight = port.Forward(dipRect.BottomRight);

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
    internal static MarqueeHoleRect? ResolveHoleRect(EsRect? highlightRect, EsRect? selectionRect)
        => highlightRect is { } highlight ? new MarqueeHoleRect(highlight, ClosedInterval: false)
            : selectionRect is { } selection ? new MarqueeHoleRect(selection, ClosedInterval: true)
            : null;
}
