using EsUtil.Platform.Core.Geometry;
using Estable.Marquee.Decoration;

namespace Estable.Marquee;

/// <summary>選択矩形コントロールの 1 フレーム分の描画内容（選択矩形・装飾・ラベル）を解決する静的クラス。</summary>
/// <remarks>
/// <b>描画順はコアが単一所有する</b>: 本クラスが <see cref="MarqueeDrawContent.Layers"/> を組み立てるため、<br/>
/// UI 層は層の順序・有無を判断しない（ラベルは必ず最後 = 常に最上位）。<br/>
/// <br/>
/// <b>描くかどうかの判断はすべて本クラスが持つ</b>: 設定（非表示）・選択なし・nudge 中・透明色・穴あけ矩形なしなどを解決し、<br/>
/// UI 層は値を持つ項目を反映するだけでよい。<br/>
/// <br/>
/// <b>テキストの実寸は描画 API 依存</b>: コアは「どこへ何を描くか」だけを返し、実測と要素の配置は UI 層が行う。<br/>
/// <br/>
/// <b>可視性</b>: 本クラスは公開ファサード <see cref="Marquee"/> の内部実装であるため internal とする<br/>
/// （UI 層・ホストは <see cref="Marquee.ResolveContent"/> を通して描画内容を得る）。
/// </remarks>
internal static class MarqueeFrame
{
    /// <summary>1 フレーム分の描画内容を解決する。</summary>
    /// <param name="request">描画入力（設定・状態と描画時に決まる値）。</param>
    /// <returns>描画内容（描くものが無い項目は空・null）。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. 装飾（暗転マスク・穴あけ枠・内側の塗り）を解決する。<br/>
    /// 2. 選択矩形そのもの（ハンドル・始点マーカー）を解決する。<br/>
    /// 3. ラベル（サイズ・カーソル座標）を解決する。<br/>
    /// 4. 描く層だけを順序どおりに並べる（ラベルは最後 = 常に最上位）。<br/>
    /// <br/>
    /// 描画順は <see cref="MarqueeDrawLayer"/> の宣言順ではなく<b>本メソッドの意図</b>で決める<br/>
    /// （列挙に層を追加しても描画順が勝手に変わらないようにするため、明示的に組み立てる）。
    /// </remarks>
    public static MarqueeDrawContent ResolveContent(in MarqueeFrameRequest request)
    {
        var hasSelection = request.HasSelection;
        var selectionRect = request.SelectionRect;

        var decoration = OuterDecoFrame.Resolve(new OuterDecoRequest(
            new OuterDecoOptions
            {
                Appearance = request.DecoOptions.OuterAppearance,
                ShowFullOverlay = request.DecoOptions.OuterShowFullOverlay,
                LogicalSelectionRect = selectionRect,
                HasSelection = hasSelection,
                LogicalHighlightRect = request.DecoOptions.OuterHighlightRect,
                HasHighlight = request.DecoOptions.OuterHasHighlight,
            },
            request.ClientSize,
            request.Port,
            request.Scaling));

        var innerDecoration = InnerDecoFrame.Resolve(new InnerDecoRequest(
            new InnerDecoOptions
            {
                Appearance = request.DecoOptions.InnerAppearance,
                LogicalSelectionRect = selectionRect,
                HasSelection = hasSelection,
                LogicalHighlightRect = request.DecoOptions.OuterHighlightRect,
                HasHighlight = request.DecoOptions.OuterHasHighlight,
            },
            request.Port,
            request.Scaling));

        var handles = ResolveHandles(request);
        var marker = ResolveMarker(request);

        // 穴あけ矩形は装飾・内側の装飾・サイズラベルで同じ値を使うため 1 回だけ計算する
        var hole = DecoGeometry.ResolveHoleRectDip(
            request.DecoOptions.OuterHasHighlight,
            request.DecoOptions.OuterHighlightRect,
            request.HasSelection,
            request.SelectionRect,
            request.Port,
            request.Scaling);

        var sizeLabel = ResolveSizeLabel(request, hole);
        var coordLabel = ResolveCoordLabel(request);

        return new MarqueeDrawContent(
            decoration,
            innerDecoration,
            hole,
            handles,
            marker,
            sizeLabel,
            coordLabel,
            ResolveLayers(hasSelection, marker, sizeLabel, coordLabel));
    }

    /// <summary>描く層だけを描画順に並べる。</summary>
    /// <param name="hasSelection">選択が有効かどうか。</param>
    /// <param name="marker">始点マーカー。null の場合は層を含めない。</param>
    /// <param name="sizeLabel">サイズラベル。null の場合は層を含めない。</param>
    /// <param name="coordLabel">カーソル座標ラベル。null の場合は層を含めない。</param>
    /// <returns>描く層の一覧（先頭から順に処理する）。</returns>
    /// <remarks>
    /// 装飾は常に含める（描くものが無い場合は各項目が空であり、UI 層の分岐を増やさない）。<br/>
    /// ラベルは必ず末尾へ置き、常に最上位に表示する。
    /// </remarks>
    private static IReadOnlyList<MarqueeDrawLayer> ResolveLayers(
        bool hasSelection, MarqueeMarker? marker, MarqueeSizeLabel? sizeLabel, MarqueeCoordLabel? coordLabel)
    {
        var layers = new List<MarqueeDrawLayer>(5) { MarqueeDrawLayer.Decoration };

        if (hasSelection)
        {
            layers.Add(MarqueeDrawLayer.Handles);
        }

        if (marker is not null)
        {
            layers.Add(MarqueeDrawLayer.Marker);
        }

        // ラベルは常に最上位（選択矩形・マーカーより後に描く）
        if (sizeLabel is not null)
        {
            layers.Add(MarqueeDrawLayer.SizeLabel);
        }

        if (coordLabel is not null)
        {
            layers.Add(MarqueeDrawLayer.CoordLabel);
        }

        return layers;
    }

    /// <summary>ドラッグハンドルの描画内容を解決する。</summary>
    /// <param name="request">描画入力。</param>
    /// <returns>ハンドルの描画内容。非表示の設定または選択なしの場合は null。</returns>
    /// <remarks>ハンドル枠の色は独立した設定値である（装飾の枠線色とは別の値であり、ホストが同じ色を割り当てる）。</remarks>
    private static MarqueeHandles? ResolveHandles(in MarqueeFrameRequest request)
    {
        var options = request.Options;
        if (!options.ShowHandles || !request.HasSelection || request.SelectionRect is not { } rect)
        {
            return null;
        }

        return new MarqueeHandles(
            ToDipRect(rect, request),
            options.HandleSize,
            MarqueeAppearance.ResolveHandleColor(options.HandleColor),
            options.HandleBorderColor);
    }

    /// <summary>始点マーカー（十字線）の描画内容を解決する。</summary>
    /// <param name="request">描画入力。</param>
    /// <returns>マーカーの描画内容。nudge 中でない（または十字線が可視でない）場合は null。</returns>
    private static MarqueeMarker? ResolveMarker(in MarqueeFrameRequest request)
    {
        if (!request.MarkerActive)
        {
            return null;
        }

        var options = request.Options;
        return new MarqueeMarker(
            ToDip(request.MarkerPosition, request),
            options.MarkerColor,
            MarqueeAppearance.MarkerThickness,
            options.MarkerLength / 2);
    }

    /// <summary>サイズラベルの描画内容を解決する。</summary>
    /// <param name="request">描画入力。</param>
    /// <param name="hole">穴あけ矩形（DIP）。選択もハイライトも無い場合は null。</param>
    /// <returns>サイズラベルの描画内容。非表示の設定または穴あけ矩形なしの場合は null。</returns>
    /// <remarks>
    /// 基準点は穴あけ矩形の左上である（装飾と同じ規則で決まる）。<br/>
    /// 文言はプロバイダ優先であり、未指定・空文字の場合はコアが矩形から自動生成する。
    /// </remarks>
    private static MarqueeSizeLabel? ResolveSizeLabel(in MarqueeFrameRequest request, EsRect? hole)
    {
        if (!request.Options.ShowSizeLabel || hole is not { } holeRect)
        {
            return null;
        }

        var options = request.Options;
        var overrideText = request.SizeLabelOverrideProvider?.Invoke();
        var text = string.IsNullOrEmpty(overrideText)
            ? MarqueeSizeLabel.Format(holeRect, request.DisplayScaleScaling)
            : overrideText;

        return new MarqueeSizeLabel(
            text, holeRect.TopLeft, options.SizeLabelFontSize, options.SizeLabelColor, options.SizeLabelBackgroundColor);
    }

    /// <summary>カーソル座標ラベルの描画内容を解決する。</summary>
    /// <param name="request">描画入力。</param>
    /// <returns>座標ラベルの描画内容。非表示の設定の場合は null。</returns>
    /// <remarks>文言はコア（<see cref="CoordinateFormatter"/>）が文字列化し、配置は描画時に実寸から決まる。</remarks>
    private static MarqueeCoordLabel? ResolveCoordLabel(in MarqueeFrameRequest request)
    {
        var options = request.Options;
        if (!options.ShowCoordLabel)
        {
            return null;
        }

        return new MarqueeCoordLabel(
            CoordinateFormatter.Format(request.MousePosition),
            ToDip(request.MousePosition, request),
            request.ClientSize,
            options.CoordLabelFontSize,
            options.CoordLabelColor,
            options.CoordLabelBackgroundColor);
    }

    /// <summary>論理座標の点を DIP へ変換する。</summary>
    /// <param name="point">論理座標の点。</param>
    /// <param name="request">描画入力（座標変換ポート・倍率）。</param>
    /// <returns>DIP 座標の点。</returns>
    private static EsPoint ToDip(EsPoint point, in MarqueeFrameRequest request)
        => DecoGeometry.ResolvePort(request.Port, request.Scaling).Inverse(point);

    /// <summary>論理座標の矩形を DIP へ変換する（閉区間として終点を +1 する）。</summary>
    /// <param name="rect">論理座標の矩形。</param>
    /// <param name="request">描画入力（座標変換ポート・倍率）。</param>
    /// <returns>DIP 座標の矩形。</returns>
    private static EsRect ToDipRect(EsRect rect, in MarqueeFrameRequest request)
        => DecoGeometry.ToDipRect(rect, DecoGeometry.ResolvePort(request.Port, request.Scaling));
}
