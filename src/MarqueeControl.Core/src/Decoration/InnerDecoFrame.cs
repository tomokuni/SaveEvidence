using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace Estable.Marquee.Decoration;

/// <summary>穴あけ矩形の内側の装飾の設定と描画状態を 1 つにまとめた不変の値。</summary>
/// <remarks>
/// 外側の装飾の <see cref="OuterDecoOptions"/> と対をなすが、<b>穴の決定規則は外側と同一</b>である<br/>
/// （ハイライト優先 → 選択矩形。規則は <see cref="DecoGeometry.ResolveHoleRect"/> が単一所有する）。<br/>
/// 全面暗転（<c>ShowFullOverlay</c>）は外側固有の表示ポリシーのため本値は持たない。<br/>
/// <b>可視性</b>: 外部（ホスト・UI 実装）は本値を組み立てないため内部契約とする。
/// </remarks>
internal sealed record InnerDecoOptions
{
    /// <summary>穴の内側の装飾の外観（塗り色・枠線色・太さ）を取得する。</summary>
    /// <value>外観の単一値。既定は <see cref="InnerDecoAppearance"/> の既定値（どちらも描かない）。</value>
    public InnerDecoAppearance Appearance { get; init; } = new();

    /// <summary>選択矩形（ドメイン座標）を取得する。</summary>
    /// <value>選択矩形。null（既定）の場合は選択なし。</value>
    /// <remarks><see cref="HasSelection"/> が true のときに穴あけ矩形として使う（ハイライトより優先度が低い）。</remarks>
    public EsRect? LogicalSelectionRect { get; init; }

    /// <summary>ハイライト矩形（ドメイン座標）を取得する。</summary>
    /// <value>ハイライト矩形。null（既定）の場合はハイライトなし。</value>
    /// <remarks><see cref="HasHighlight"/> が true のときに穴あけ矩形として使う（選択矩形より優先）。</remarks>
    public EsRect? LogicalHighlightRect { get; init; }

    /// <summary>選択が有効かどうかを取得する。</summary>
    /// <value>true の場合は <see cref="LogicalSelectionRect"/> を穴あけ矩形として使う。既定は false。</value>
    public bool HasSelection { get; init; }

    /// <summary>ハイライトが有効かどうかを取得する。</summary>
    /// <value>true の場合は <see cref="LogicalHighlightRect"/> を穴あけ矩形として使う（選択矩形より優先）。既定は false。</value>
    public bool HasHighlight { get; init; }
}

/// <summary>穴あけ矩形の内側の装飾の描画入力（設定・状態と、描画時に決まる値を 1 つにまとめた値）。</summary>
/// <remarks>
/// UI 実装は <c>Marquee.ResolveContent</c> を呼び、本値を直接組み立てない（内部契約）。<br/>
/// クライアント矩形は不要である（内側の装飾は穴あけ矩形そのものへ描くため、描画領域による切り分けを行わない）。
/// </remarks>
/// <param name="Options">設定と描画状態（外観・論理矩形・有効フラグ）。</param>
/// <param name="Port">論理座標 → DIP の座標変換ポート。null の場合は <paramref name="Scaling"/> を等倍スケールとして適用する。</param>
/// <param name="Scaling">ポート未指定の場合に適用する倍率（0 以下・等倍は等価変換）。</param>
internal readonly record struct InnerDecoRequest(
    InnerDecoOptions Options,
    ICoordTransform? Port,
    double Scaling);

/// <summary>穴あけ矩形の内側の装飾の 1 フレーム分の描画内容（描画に必要な値のみをコアが決定した結果）。</summary>
/// <remarks>
/// UI 層は本値の各項目を自身の描画 API へ写すだけにする（判断・計算・既定値の解決は含まない）。<br/>
/// 描画するものが無い場合（塗りなし・枠線なし）は各項目が空になるため、UI 層の分岐は不要。<br/>
/// 色は UI 非依存の <see cref="DrawingColor"/> を使用し、UI 境界で自 UI の色型へ変換する。
/// </remarks>
/// <param name="FillRect">塗る矩形（DIP。穴あけ矩形そのもの）。null の場合は塗らない。</param>
/// <param name="FillColor">塗りの色。</param>
/// <param name="BorderRects">枠線を描く矩形（DIP。穴あけ矩形の内側 4 辺）。空の場合は描かない。</param>
/// <param name="BorderColor">枠線の色。</param>
public readonly record struct InnerDecoFrame(
    EsRect? FillRect,
    DrawingColor FillColor,
    IReadOnlyList<EsRect> BorderRects,
    DrawingColor BorderColor)
{
    /// <summary>描画入力を 1 フレーム分の描画内容へ解決する。</summary>
    /// <param name="request">描画入力（外観・状態・座標変換ポート）。</param>
    /// <returns>描画内容。描画するものが無い場合は各項目が空のフレーム。</returns>
    /// <remarks>
    /// 1. 穴あけ矩形を決定する（ハイライト優先 → 選択。規則は外側の装飾と共通）。<br/>
    /// 2. 穴あけ矩形が無ければ空のフレームを返す。<br/>
    /// 3. 塗り色のアルファが 0 より大きい場合のみ塗り矩形を生成する。<br/>
    /// 4. 枠線色のアルファが 0 より大きい場合のみ枠線矩形（内側 4 辺）を生成する。<br/>
    /// <br/>
    /// 論理矩形 → DIP の変換は外側の装飾と同一の規則（選択は閉区間 +1 / ハイライトは +0）を使うため、<br/>
    /// 外側の枠線と内側の枠線が同じ矩形へ重なる。<br/>
    /// <b>可視性</b>: UI 実装は <c>Marquee.ResolveContent</c> を使うため、本メソッドは内部契約とする。
    /// </remarks>
    internal static InnerDecoFrame Resolve(in InnerDecoRequest request)
    {
        var options = request.Options;
        var appearance = options.Appearance;

        var port = DecoGeometry.ResolvePort(request.Port, request.Scaling);
        var selectionRect = options.LogicalSelectionRect is { } selection
            ? DecoGeometry.ToDipRect(selection, port)
            : (EsRect?)null;
        var highlightRect = options.LogicalHighlightRect is { } highlight
            ? DecoGeometry.ToDipRect(highlight, port, closedInterval: false)
            : (EsRect?)null;
        var holeRect = DecoGeometry.ResolveHoleRect(
            options.HasHighlight, highlightRect, options.HasSelection, selectionRect);

        if (holeRect is not { } hole)
        {
            return new InnerDecoFrame(null, appearance.FillColor, [], appearance.BorderColor);
        }

        var fillRect = appearance.FillColor.A > 0 ? hole : (EsRect?)null;
        var borderRects = appearance.BorderColor.A > 0
            ? DecoGeometry.GetBorderRects(hole, appearance.BorderThickness)
            : [];

        return new InnerDecoFrame(fillRect, appearance.FillColor, borderRects, appearance.BorderColor);
    }
}
