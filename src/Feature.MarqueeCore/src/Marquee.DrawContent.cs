using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.MarqueeCore.Content;
using EsUtil.Feature.MarqueeCore.Decoration;
using EsUtil.Feature.MarqueeCore.Selection;

namespace EsUtil.Feature.MarqueeCore;

/// <summary>選択矩形コントロールの 1 フレーム分の描画内容（選択矩形・装飾・ラベル）の解決を担う部分。</summary>
/// <remarks>
/// <b>描画順はコアが単一所有する</b>: 本部分が <see cref="MarqueeDrawContent.Layers"/> を組み立てるため、<br/>
/// UI 層は層の順序・有無を判断しない（ラベルは必ず最後 = 常に最上位）。<br/>
/// <br/>
/// <b>描くかどうかの判断はすべて本部分が持つ</b>: 設定（非表示）・選択なし・nudge 中・透明色・穴あけ矩形なしなどを解決し、<br/>
/// UI 層は値を持つ項目を反映するだけでよい（<see cref="MarqueeDrawContent"/> とその要素型は描画に必要な値のみを持つ純粋データである）。<br/>
/// <br/>
/// <b>穴あけ矩形と座標変換ポートは 1 回だけ解決する</b>: 外側の装飾・内側の装飾・サイズラベルが同じ穴あけ矩形を使い、<br/>
/// すべての項目が同じ座標変換ポートを使う。各所が個別に解決すると矩形がずれ、同じ変換の生成と評価が重複する。<br/>
/// <br/>
/// <b>テキストの実寸は描画 API 依存</b>: コアは「どこへ何を描くか」だけを返し、実測と要素の配置は UI 層が行う。<br/>
/// <br/>
/// <b>可視性</b>: 本部分は公開ファサード <see cref="Marquee"/> の内部実装であるため internal とする<br/>
/// （UI 層・ホストは <see cref="Marquee.ResolveContent"/> を通して描画内容を得る）。
/// </remarks>
public sealed partial class Marquee
{
    /// <summary>始点マーカー（十字線）の線の太さ（DIP）。</summary>
    /// <remarks>塗りの中に隠れない細さでありながら、拡大表示でも線が判別できる太さである。</remarks>
    private const double MarkerThickness = 2.0;

    /// <summary>層の組合せのうち、選択の有無を表すビット。</summary>
    private const int SelectionBit = 1;

    /// <summary>層の組合せのうち、始点マーカーの有無を表すビット。</summary>
    private const int MarkerBit = 2;

    /// <summary>層の組合せのうち、サイズラベルの有無を表すビット。</summary>
    private const int SizeLabelBit = 4;

    /// <summary>層の組合せのうち、カーソル座標ラベルの有無を表すビット。</summary>
    private const int CoordLabelBit = 8;

    /// <summary>層の並び（4 つの有無 = 16 通り）。</summary>
    /// <remarks>
    /// 描画と要素配置のたびに一覧を組み立てないよう、全通りを事前生成して共有する<br/>
    /// （<see cref="MarqueeDrawContent.Layers"/> は読取専用の参照として渡す）。層を追加した場合は本表も更新する。
    /// </remarks>
    private static readonly IReadOnlyList<MarqueeDrawLayer>[] s_layers = CreateLayers();

    /// <summary>1 フレーム分の描画内容を解決する（ファサードの描画入力から）。</summary>
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
    internal static MarqueeDrawContent ResolveFrame(in MarqueeFrameRequest request)
    {
        // 座標変換ポートは 1 フレームで 1 回だけ解決する（未指定時は倍率から等倍スケールを生成するため、各項目で解決すると生成と評価が重複する）
        var port = DecoGeometry.ResolvePort(request.Port, request.Scaling);

        // 穴あけ矩形も 1 回だけ解決する（外側の装飾・内側の装飾・サイズラベルが同じ矩形を使う）
        var hole = DecoGeometry.ResolveHoleRectDip(
            request.DecoOptions.OuterHighlightRect, request.SelectionRect, port);

        var handles = ResolveHandles(request, port);
        var marker = ResolveMarker(request, port);
        var sizeLabel = ResolveSizeLabel(request, hole);
        var coordLabel = ResolveCoordLabel(request, port);

        return new MarqueeDrawContent(
            ResolveOuterDecoration(request, hole),
            ResolveInnerDecoration(request, hole),
            hole,
            handles,
            marker,
            sizeLabel,
            coordLabel,
            ResolveLayers(handles, marker, sizeLabel, coordLabel));
    }

    /// <summary>描く層だけを描画順に並べる。</summary>
    /// <param name="handles">ドラッグハンドル。null の場合は層を含めない。</param>
    /// <param name="marker">始点マーカー。null の場合は層を含めない。</param>
    /// <param name="sizeLabel">サイズラベル。null の場合は層を含めない。</param>
    /// <param name="coordLabel">カーソル座標ラベル。null の場合は層を含めない。</param>
    /// <returns>描く層の一覧（先頭から順に処理する）。</returns>
    /// <remarks>
    /// 装飾は常に含める（描くものが無い場合は各項目が空であり、UI 層の分岐を増やさない）。<br/>
    /// ラベルは必ず末尾へ置き、常に最上位に表示する。<br/>
    /// 一覧はビットの組合せで事前生成したものを返す（描画のたびの確保を避ける）。
    /// </remarks>
    private static IReadOnlyList<MarqueeDrawLayer> ResolveLayers(
        MarqueeHandles? handles, MarqueeMarker? marker, MarqueeSizeLabel? sizeLabel, MarqueeCoordLabel? coordLabel)
        => s_layers[
            (handles is not null ? SelectionBit : 0)
            | (marker is not null ? MarkerBit : 0)
            | (sizeLabel is not null ? SizeLabelBit : 0)
            | (coordLabel is not null ? CoordLabelBit : 0)];

    /// <summary>層の全 16 通りを生成する。</summary>
    /// <returns>ビットの組合せを添字とする層の一覧。</returns>
    /// <remarks>ビットの意味は <see cref="SelectionBit"/> / <see cref="MarkerBit"/> / <see cref="SizeLabelBit"/> / <see cref="CoordLabelBit"/> が単一所有する。</remarks>
    private static IReadOnlyList<MarqueeDrawLayer>[] CreateLayers()
    {
        var table = new IReadOnlyList<MarqueeDrawLayer>[16];

        for (var index = 0; index < table.Length; index++)
        {
            var layers = new List<MarqueeDrawLayer>(5) { MarqueeDrawLayer.Decoration };

            if ((index & SelectionBit) != 0)
            {
                layers.Add(MarqueeDrawLayer.Handles);
            }

            if ((index & MarkerBit) != 0)
            {
                layers.Add(MarqueeDrawLayer.Marker);
            }

            // ラベルは常に最上位（選択矩形・マーカーより後に描く）
            if ((index & SizeLabelBit) != 0)
            {
                layers.Add(MarqueeDrawLayer.SizeLabel);
            }

            if ((index & CoordLabelBit) != 0)
            {
                layers.Add(MarqueeDrawLayer.CoordLabel);
            }

            table[index] = layers;
        }

        return table;
    }

    /// <summary>外側の装飾（暗転マスク・穴あけ枠）の描画内容を解決する。</summary>
    /// <param name="request">描画入力（外観と表示ポリシー）。</param>
    /// <param name="hole">穴あけ矩形（DIP）。選択もハイライトも無い場合は null。</param>
    /// <returns>外側の装飾の描画内容。描くものが無い場合は各項目が空。</returns>
    /// <remarks>
    /// 1. 穴あけ矩形も全面暗転も無い場合は空のフレームを返す。<br/>
    /// 2. 暗転の色が透明なら暗転領域を生成しない。<br/>
    /// 3. 穴あけ矩形が無い場合（全面暗転）は枠線を持たない（穴あけ位置が無いため）。<br/>
    /// 4. 枠線の色が透明なら枠線矩形を生成しない。<br/>
    /// <br/>
    /// 「描くかどうか」の解決は本メソッドが単一所有する（UI 層は値を持つ項目を反映するだけ）。
    /// </remarks>
    private static OuterDecoFrame ResolveOuterDecoration(in MarqueeFrameRequest request, EsRect? hole)
    {
        var deco = request.DecoOptions;

        if (!DecoGeometry.ShouldDrawOverlay(hole, deco.OuterShowFullOverlay))
        {
            return new OuterDecoFrame([], []);
        }

        var overlayRegions = deco.ShowOverlay
            ? DecoGeometry.ComputeOverlayRegions(request.ClientSize, hole)
            : [];

        var borderRects = hole is { } holeRect && deco.ShowOuterBorder
            ? DecoGeometry.GetBorderRects(holeRect, deco.OuterAppearance.BorderThickness)
            : [];

        return new OuterDecoFrame(overlayRegions, borderRects);
    }

    /// <summary>内側の装飾（穴あけ矩形の塗り・内枠）の描画内容を解決する。</summary>
    /// <param name="request">描画入力（装飾の設定）。</param>
    /// <param name="hole">穴あけ矩形（DIP）。選択もハイライトも無い場合は null。</param>
    /// <returns>内側の装飾の描画内容。描くものが無い場合は各項目が空。</returns>
    /// <remarks>
    /// 1. 穴あけ矩形が無い場合は空のフレームを返す。<br/>
    /// 2. 塗りを描かない設定なら塗り矩形を生成しない。<br/>
    /// 3. 枠線を描かない設定なら枠線矩形（内側 4 辺）を生成しない。<br/>
    /// <br/>
    /// 枠線矩形の計算は外側の装飾と共通（<see cref="DecoGeometry.GetBorderRects"/>）のため、<br/>
    /// 外側の枠線と内側の枠線が同じ矩形へ重なる。<br/>
    /// 「描くかどうか」の解決は本メソッドが単一所有する（UI 層は値を持つ項目を反映するだけ）。
    /// </remarks>
    private static InnerDecoFrame ResolveInnerDecoration(in MarqueeFrameRequest request, EsRect? hole)
    {
        var deco = request.DecoOptions;

        if (hole is not { } holeRect)
        {
            return new InnerDecoFrame(null, []);
        }

        var fillRect = deco.ShowInnerFill ? holeRect : (EsRect?)null;
        var borderRects = deco.ShowInnerBorder
            ? DecoGeometry.GetBorderRects(holeRect, deco.InnerAppearance.BorderThickness)
            : [];

        return new InnerDecoFrame(fillRect, borderRects);
    }

    /// <summary>ドラッグハンドルの描画内容を解決する。</summary>
    /// <param name="request">描画入力。</param>
    /// <param name="port">解決済みの座標変換ポート（論理座標 → DIP）。</param>
    /// <returns>ハンドルの描画内容。非表示の設定または選択なしの場合は null。</returns>
    /// <remarks>
    /// 中心位置（8 方向）と一辺サイズの適用を本メソッドが解決し、描画側へは矩形の一覧として渡す<br/>
    /// （位置の式を描画層が持つと、ハンドルのヒットテストと同じ規則が 2 箇所になる）。<br/>
    /// ハンドル枠の太さはコアが、色は描画の設定（<c>MarqueePaint</c>）が単一所有する。
    /// </remarks>
    private static MarqueeHandles? ResolveHandles(in MarqueeFrameRequest request, ICoordTransform port)
    {
        var options = request.Options;
        if (!options.ShowHandles || request.SelectionRect is not { } rect)
        {
            return null;
        }

        var dip = DecoGeometry.ToDipRect(rect, port);
        var half = options.HandleSize / 2;
        var rects = new List<EsRect>(SelectionGeometry.DirectionalHandles.Count);
        foreach (var handle in SelectionGeometry.DirectionalHandles)
        {
            var center = SelectionGeometry.GetHandlePosition(dip, handle);
            rects.Add(new EsRect(center.X - half, center.Y - half, options.HandleSize, options.HandleSize));
        }

        return new MarqueeHandles(rects, MarqueeOptions.HandleBorderThickness);
    }

    /// <summary>始点マーカー（十字線）の描画内容を解決する。</summary>
    /// <param name="request">描画入力。</param>
    /// <param name="port">解決済みの座標変換ポート（論理座標 → DIP）。</param>
    /// <returns>マーカーの描画内容。nudge 中でない（または十字線が可視でない）場合は null。</returns>
    /// <remarks>十字線の色は描画の設定（<c>MarqueePaint</c>）が単一所有する。</remarks>
    private static MarqueeMarker? ResolveMarker(in MarqueeFrameRequest request, ICoordTransform port)
    {
        if (!request.MarkerActive)
        {
            return null;
        }

        return new MarqueeMarker(
            port.Inverse(request.MarkerPosition),
            MarkerThickness,
            request.Options.MarkerLength / 2);
    }

    /// <summary>サイズラベルの描画内容を解決する。</summary>
    /// <param name="request">描画入力。</param>
    /// <param name="hole">穴あけ矩形（DIP）。選択もハイライトも無い場合は null。</param>
    /// <returns>サイズラベルの描画内容。非表示の設定または穴あけ矩形なしの場合は null。</returns>
    /// <remarks>
    /// 基準点は穴あけ矩形の左上である（装飾と同じ規則で決まる）。<br/>
    /// 文言はプロバイダ優先であり、未指定・空文字の場合はコアが矩形から自動生成する。<br/>
    /// 自動生成の 2 つの係数は、どちらも本コントロールがホストから受け取った表示スケーリングを使う<br/>
    /// （ホストが文言プロバイダを与える場合は <see cref="MarqueeSizeLabel.Format"/> を自前で呼び分けられる）。<br/>
    /// 色は描画の設定（<c>MarqueePaint</c>）が単一所有する。
    /// </remarks>
    private static MarqueeSizeLabel? ResolveSizeLabel(in MarqueeFrameRequest request, EsRect? hole)
    {
        if (!request.Options.ShowSizeLabel || hole is not { } holeRect)
        {
            return null;
        }

        var overrideText = request.SizeLabelOverrideProvider?.Invoke();
        var text = string.IsNullOrEmpty(overrideText)
            ? MarqueeSizeLabel.Format(holeRect, request.DisplayScaleScaling, request.DisplayScaleScaling)
            : overrideText;

        return new MarqueeSizeLabel(text, holeRect.TopLeft, request.Options.SizeLabelFontSize);
    }

    /// <summary>カーソル座標ラベルの描画内容を解決する。</summary>
    /// <param name="request">描画入力。</param>
    /// <param name="port">解決済みの座標変換ポート（論理座標 → DIP）。</param>
    /// <returns>座標ラベルの描画内容。非表示の設定の場合は null。</returns>
    /// <remarks>
    /// 文言はコア（<see cref="EsPoint.Format(int)"/>）が文字列化し、配置は描画時に実寸から決まる。<br/>
    /// 色は描画の設定（<c>MarqueePaint</c>）が単一所有する。
    /// </remarks>
    private static MarqueeCoordLabel? ResolveCoordLabel(in MarqueeFrameRequest request, ICoordTransform port)
    {
        if (!request.Options.ShowCoordLabel)
        {
            return null;
        }

        return new MarqueeCoordLabel(
            request.MousePosition.Format(),
            port.Inverse(request.MousePosition),
            request.ClientSize,
            request.Options.CoordLabelFontSize);
    }
}
