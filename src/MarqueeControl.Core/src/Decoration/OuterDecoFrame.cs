using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace Estable.Marquee.Decoration;

/// <summary>暗転マスクの描画入力（設定・状態と、描画時に決まる値を 1 つにまとめた値）。</summary>
/// <remarks>
/// UI 層は本値を組み立てず、選択コア（<see cref="Marquee.ResolveContent"/>）が組み立てて
/// <see cref="OuterDecoFrame.Resolve"/> へ渡す（判断はコアが単一所有する）。<br/>
/// 色は UI 非依存の <see cref="DrawingColor"/> を使用する。
/// </remarks>
/// <param name="Options">設定と描画状態（外観・モード・論理矩形・有効フラグ）。</param>
/// <param name="ClientSize">描画領域のサイズ（DIP。原点は常に 0）。</param>
/// <param name="Port">論理座標 → DIP の座標変換ポート。null の場合は <paramref name="Scaling"/> を等倍スケールとして適用する。</param>
/// <param name="Scaling">ポート未指定の場合に適用する倍率（0 以下・等倍は等価変換）。</param>
/// <remarks>
/// <b>可視性</b>: 本値はコアの内部契約とする（UI 実装は <c>Marquee.ResolveContent</c> を呼ぶ）。
/// </remarks>
internal readonly record struct OuterDecoRequest(
    OuterDecoOptions Options,
    EsSize ClientSize,
    ICoordTransform? Port,
    double Scaling);

/// <summary>暗転マスクの 1 フレーム分の描画内容（描画に必要な値のみをコアが決定した結果）。</summary>
/// <remarks>
/// UI 層は本値の各項目を自身の描画 API へ写すだけにする（判断・計算・既定値の解決は含まない）。<br/>
/// 描画するものが無い場合（暗転なし・枠線なし）は各項目が空になるため、UI 層の分岐は不要。<br/>
/// 色は UI 非依存の <see cref="DrawingColor"/> を使用し、UI 境界で自 UI の色型へ変換する。
/// </remarks>
/// <param name="OverlayRegions">暗転して塗る領域（DIP）。空の場合は塗らない。</param>
/// <param name="OverlayColor">暗転の色。</param>
/// <param name="BorderRects">枠線を描く矩形（DIP）。空の場合は描かない。</param>
/// <param name="BorderColor">枠線の色。</param>
public readonly record struct OuterDecoFrame(
    IReadOnlyList<EsRect> OverlayRegions,
    DrawingColor OverlayColor,
    IReadOnlyList<EsRect> BorderRects,
    DrawingColor BorderColor)
{
    /// <summary>描画入力を 1 フレーム分の描画内容へ解決する。</summary>
    /// <param name="request">描画入力（外観・状態・座標変換ポート）。</param>
    /// <returns>描画内容。描画するものが無い場合は各項目が空のフレーム。</returns>
    /// <remarks>
    /// 1. 論理矩形を DIP 矩形へ変換し、穴あけ矩形を決定する（ハイライト優先 → 選択。規則は <see cref="DecoGeometry"/>）。<br/>
    /// 2. 暗転の要否を判定する（穴あけ矩形なし + 全面暗転なしなら空のフレームを返す）。<br/>
    /// 3. 暗転の色・枠線の色が透明なら、その領域・矩形を生成しない（UI 層に分岐を持たせない）。<br/>
    /// <br/>
    /// <b>ラベルは本層が持たない</b>: サイズラベルとカーソル座標ラベルはコア（<see cref="MarqueeSizeLabel"/> / <see cref="MarqueeCoordLabel"/>）が解決し、<br/>
    /// 操作・情報ラベルはホストが自前で描く（装飾は画面上の暗転と枠線のみを担う）。<br/>
    /// <b>可視性</b>: UI 実装は <see cref="Marquee.ResolveContent"/> を使うため、本メソッドは内部契約とする。
    /// </remarks>
    internal static OuterDecoFrame Resolve(in OuterDecoRequest request)
    {
        var options = request.Options;
        var appearance = options.Appearance;

        // 穴あけ矩形の決定（論理矩形 + 座標変換ポートを描画時に評価。ポート未指定は倍率または等価変換）
        var holeRect = DecoGeometry.ResolveHoleRectDip(
            options.HasHighlight, options.LogicalHighlightRect, options.HasSelection, options.LogicalSelectionRect,
            request.Port, request.Scaling);

        if (!DecoGeometry.ShouldDrawOverlay(holeRect, options.ShowFullOverlay))
        {
            return CreateEmpty(appearance);
        }

        var overlayRegions = appearance.OverlayColor.A > 0
            ? DecoGeometry.ComputeOverlayRegions(request.ClientSize, holeRect)
            : [];

        if (holeRect is not { } hole)
        {
            // 全面暗転: 穴あけ位置が無いため枠線は持たない
            return new OuterDecoFrame(overlayRegions, appearance.OverlayColor, [], appearance.BorderColor);
        }

        var borderRects = appearance.BorderColor.A > 0
            ? DecoGeometry.GetBorderRects(hole, appearance.BorderThickness)
            : [];

        return new OuterDecoFrame(overlayRegions, appearance.OverlayColor, borderRects, appearance.BorderColor);
    }

    /// <summary>描画するものが無いフレームを生成する。</summary>
    /// <param name="appearance">外観（色のみ引き継ぐ）。</param>
    /// <returns>各項目が空のフレーム。</returns>
    private static OuterDecoFrame CreateEmpty(OuterDecoAppearance appearance)
        => new([], appearance.OverlayColor, [], appearance.BorderColor);
}
