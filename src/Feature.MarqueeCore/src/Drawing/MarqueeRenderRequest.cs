using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.MarqueeCore.Setting;

namespace EsUtil.Feature.MarqueeCore.Drawing;

/// <summary>1 フレーム分の描画内容を解決するための入力（設定・状態と、描画時に決まる値を 1 つにまとめた値）。</summary>
/// <param name="Options">選択矩形の設定（ハンドル・十字マーカー・カーソル座標ラベルの外観）。</param>
/// <param name="DecoOptions">装飾（暗転マスク・穴あけ枠・内側の塗り）の設定。</param>
/// <param name="ClientSize">描画領域のサイズ（DIP。原点は常に 0）。</param>
/// <param name="SelectionRect">選択矩形（ドメイン座標）。選択なしの場合は null（<b>null が「選択なし」を意味する</b>）。</param>
/// <param name="MarkerActive">始点マーカー（十字線）を描くかどうか（nudge 中かつ十字線が可視の場合に true）。</param>
/// <param name="MarkerPosition">始点マーカーの中心（ドメイン座標）。</param>
/// <param name="MousePosition">直近のマウス位置（ドメイン座標。カーソル座標ラベルの基準）。</param>
/// <param name="Port">論理座標 → DIP の座標変換ポート。null の場合は <paramref name="DomainToDipScaling"/> を等倍スケールとして適用する。</param>
/// <param name="DomainToDipScaling">ポート未指定の場合に適用する倍率（0 以下・等倍は等価変換）。</param>
/// <param name="SizeLabelOverrideProvider">サイズラベルの文言プロバイダ。null 可（未指定・空文字の場合は自動生成する）。</param>
/// <param name="DisplayScaleScaling">サイズラベルの自動生成に使う表示スケーリング（0 以下は等倍）。</param>
/// <param name="SizeLabelDisplayScalingProvider">サイズラベルの表示スケーリングを矩形ごとに解決するプロバイダ（引数はドメイン座標の矩形）。null 可（その場合は <paramref name="DisplayScaleScaling"/> を使う）。</param>
/// <remarks>
/// 設定と状態の単一値（<see cref="Options"/> / <see cref="DecoOptions"/>）に、描画時にしか決まらない値<br/>
/// （描画領域のサイズ・座標変換ポート・倍率・文言プロバイダ）を加えたものである。<br/>
/// <b>UI 層は本値を自分で組み立てない</b>: セッションの状態を写す組み立て順は <see cref="Marquee.CreateRequest"/> が単一所有する。<br/>
/// UI 層は本値を <see cref="Marquee.ResolveContent"/> へ渡すだけで、描画に必要な値の意味・単位を持たない。<br/>
/// <br/>
/// <b>本値から導出する値は本値が単一所有する</b>: 座標変換ポートの決定と、論理座標の矩形を DIP 矩形へ変換する手順、<br/>
/// および穴あけ矩形（選択またはハイライトの領域）の決定は、本値のメンバー（<see cref="ResolvedPort"/> と各メソッド）が持つ。<br/>
/// 外側の装飾・内側の装飾・ハンドル・サイズラベルが同じ値を使うため、各所が個別に解決すると矩形がずれ、<br/>
/// 同じ変換の生成と評価が重複する（フレームの解決が 1 回だけ解決して各所へ渡す）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public readonly record struct MarqueeRenderRequest(
    MarqueeOptions Options,
    Decoration.MarqueeDecoOptions DecoOptions,
    EsSize ClientSize,
    EsRect? SelectionRect,
    bool MarkerActive,
    EsPoint MarkerPosition,
    EsPoint MousePosition,
    ICoordTransform? Port,
    double DomainToDipScaling,
    Func<string?>? SizeLabelOverrideProvider,
    double DisplayScaleScaling,
    Func<EsRect, double>? SizeLabelDisplayScalingProvider = null)
{
    /// <summary>設定・状態と描画時に決まる値から描画入力を組み立てる。</summary>
    /// <param name="options">選択矩形の設定。</param>
    /// <param name="decoOptions">装飾の設定。</param>
    /// <param name="clientSize">描画領域のサイズ（DIP。原点は常に 0）。</param>
    /// <param name="selectionRect">選択矩形（ドメイン座標）。選択なしの場合は null。</param>
    /// <param name="markerActive">始点マーカー（十字線）を描くかどうか。</param>
    /// <param name="markerPosition">始点マーカーの中心（ドメイン座標）。</param>
    /// <param name="mousePosition">直近のマウス位置（ドメイン座標）。</param>
    /// <param name="port">論理座標 → DIP の座標変換ポート。null の場合は倍率を使う。</param>
    /// <param name="domainToDipScaling">ポート未指定の場合に適用する倍率。</param>
    /// <param name="sizeLabelOverrideProvider">サイズラベルの文言プロバイダ。null 可。</param>
    /// <param name="displayScaleScaling">サイズラベルの自動生成に使う表示スケーリング。</param>
    /// <param name="sizeLabelDisplayScalingProvider">表示スケーリングを矩形ごとに解決するプロバイダ。null 可。</param>
    /// <returns>描画入力。</returns>
    /// <remarks>
    /// セッションの状態を写す組み立て順は <see cref="Marquee.CreateRequest"/> が単一所有し、<br/>
    /// 本メソッドは値の組を 1 箇所に閉じる（UI 層が引数の並び順を知らずに済み、入力型の生成元が入力型にある）。
    /// </remarks>
    public static MarqueeRenderRequest Create(
        MarqueeOptions options,
        Decoration.MarqueeDecoOptions decoOptions,
        EsSize clientSize,
        EsRect? selectionRect,
        bool markerActive,
        EsPoint markerPosition,
        EsPoint mousePosition,
        ICoordTransform? port,
        double domainToDipScaling,
        Func<string?>? sizeLabelOverrideProvider,
        double displayScaleScaling,
        Func<EsRect, double>? sizeLabelDisplayScalingProvider = null)
        => new(
            options,
            decoOptions,
            clientSize,
            selectionRect,
            markerActive,
            markerPosition,
            mousePosition,
            port,
            domainToDipScaling,
            sizeLabelOverrideProvider,
            displayScaleScaling,
            sizeLabelDisplayScalingProvider);

    /// <summary>使用する座標変換ポートを取得する（ポート優先・未指定なら倍率による等倍スケール）。</summary>
    /// <value>使用する座標変換ポート。</value>
    /// <remarks>解決の規則は <see cref="ResolvePort"/> が単一所有する。</remarks>
    internal ICoordTransform ResolvedPort => ResolvePort(Port, DomainToDipScaling);

    /// <summary>座標変換ポートを決定する（ポート優先・未指定なら倍率による等倍スケール）。</summary>
    /// <param name="port">座標変換ポート。指定されている場合はそれをそのまま使う。</param>
    /// <param name="scaling">倍率。ポート未指定の場合に等倍スケールとして適用する。</param>
    /// <returns>使用する座標変換ポート。</returns>
    /// <remarks>
    /// 「ポートがあればそれを使い、無ければ倍率（0 以下・等倍は等価変換）を使う」という規則を単一所有する。<br/>
    /// 各 UI はポートと倍率をそのまま渡すだけで、どちらを使うかを判断しない（両 UI で同じ結果になる）。
    /// </remarks>
    internal static ICoordTransform ResolvePort(ICoordTransform? port, double scaling)
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
    internal static EsRect ToDipRect(EsRect logical, ICoordTransform? port = null, bool closedInterval = true)
    {
        var transform = port.OrIdentity();
        var topLeft = transform.Inverse(new EsPoint(logical.X, logical.Y));
        var bottomRight = transform.Inverse(closedInterval
            ? new EsPoint(logical.Right + 1, logical.Bottom + 1)
            : new EsPoint(logical.Right, logical.Bottom));

        return new EsRect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    /// <summary>DIP 矩形をドメイン座標の矩形へ逆換算する（左上と右下の順変換から組む）。</summary>
    /// <param name="dipRect">DIP 座標の矩形。</param>
    /// <param name="port">座標変換ポート（順変換 = DIP → ドメイン）。</param>
    /// <returns>ドメイン座標の矩形。</returns>
    /// <remarks>ホストが「矩形から表示スケールを解決する」ために使う（コアはモニタを知らない）。</remarks>
    internal static EsRect ToDomainRect(EsRect dipRect, ICoordTransform port)
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
    internal static EsRect? ResolveHoleRectDip(
        EsRect? logicalHighlightRect, EsRect? logicalSelectionRect, ICoordTransform? port)
        => ResolveHoleRect(logicalHighlightRect, logicalSelectionRect) is { } hole
            ? ToDipRect(hole.Rect, port, hole.ClosedInterval)
            : null;
}
