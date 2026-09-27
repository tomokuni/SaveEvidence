using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.MarqueeCore.Decoration;
using EsUtil.Feature.MarqueeCore.Selection;
using EsUtil.Feature.MarqueeCore.Drawing;
using static EsUtil.Feature.MarqueeCore.Marquee.Limits;

namespace EsUtil.Feature.MarqueeCore;

/// <summary>選択矩形コントロールの 1 フレーム分の描画内容（選択矩形・装飾・ラベル）の解決を担う部分。</summary>
/// <remarks>
/// <b>描画順はコアが単一所有する</b>: 本部分が <see cref="MarqueeDrawContent.Layers"/> を組み立てるため、<br/>
/// UI 層は層の順序・有無を判断しない（ラベルは必ず最後 = 常に最上位）。<br/>
/// <b>層にある ⇔ 描く値がある</b>（描く値の無い層は含めない。層の有無の判断を UI 層へ置かない）。<br/>
/// <br/>
/// <b>描くかどうかの判断はすべて本部分が持つ</b>: 設定（非表示・描画可否フラグ）・選択なし・nudge 中・穴あけ矩形なしなどを解決し、<br/>
/// UI 層は値を持つ項目を反映するだけでよい（<see cref="MarqueeDrawContent"/> とその要素型は描画に必要な値のみを持つ純粋データである）。<br/>
/// <br/>
/// <b>穴あけ矩形と座標変換ポートは 1 回だけ解決する</b>: 外側の装飾・内側の装飾・サイズラベルが同じ穴あけ矩形を使い、<br/>
/// すべての項目が同じ座標変換ポートを使う。各所が個別に解決すると矩形がずれ、同じ変換の生成と評価が重複する。<br/>
/// <br/>
/// <b>テキストの実寸は描画 API 依存</b>: コアは「どこへ何を描くか」だけを返し、実測と要素の配置は UI 層が行う。<br/>
/// <br/>
/// <b>可視性</b>: 描画内容の解決は公開ファサードの窓口（<see cref="ResolveContent"/>）だけであり、<br/>
/// 本部分の内部手順（ハンドル・マーカー・ラベルの解決）は本アセンブリに限定する。
/// </remarks>
public sealed partial class Marquee
{
    /// <summary>始点マーカー（十字線）の線の太さ（DIP）。</summary>
    /// <remarks>塗りの中に隠れない細さでありながら、拡大表示でも線が判別できる太さである。</remarks>
    private const double MarkerThickness = 2.0;

    /// <summary>ドラッグハンドルの枠線の太さ（DIP）。</summary>
    /// <remarks>塗りの中に隠れない細さでありながら、拡大表示でも輪郭が判別できる太さである。</remarks>
    private const double HandleBorderThickness = 1.5;

    /// <summary>カーソル座標ラベルをカーソルから離すオフセット（DIP）。</summary>
    /// <remarks>ラベルがカーソルに重ならないための余白である。</remarks>
    private const double CoordLabelOffset = 8.0;

    /// <summary>層の組合せのうち、装飾の有無を表すビット。</summary>
    private const int DecorationBit = 1;

    /// <summary>層の組合せのうち、ドラッグハンドルの有無を表すビット。</summary>
    private const int HandlesBit = 2;

    /// <summary>層の組合せのうち、始点マーカーの有無を表すビット。</summary>
    private const int MarkerBit = 4;

    /// <summary>層の組合せのうち、テキストの有無を表すビット。</summary>
    private const int TextBit = 8;

    /// <summary>層の並び（4 つの有無 = 16 通り）。</summary>
    /// <remarks>
    /// 描画のたびに一覧を組み立てないよう、全通りを事前生成して共有する<br/>
    /// （<see cref="MarqueeDrawContent.Layers"/> は読取専用の参照として渡す）。層を追加した場合は本表も更新する。
    /// </remarks>
    private static readonly IReadOnlyList<MarqueeDrawLayer>[] s_layers = CreateLayers();

    /// <summary>描く層だけを描画順に並べる。</summary>
    /// <param name="outer">外側の装飾（暗転マスク・穴あけ枠）。描く値が無い場合は層を含めない。</param>
    /// <param name="inner">内側の装飾（塗り・内枠）。描く値が無い場合は層を含めない。</param>
    /// <param name="handles">ドラッグハンドル。null の場合は層を含めない。</param>
    /// <param name="marker">始点マーカー。null の場合は層を含めない。</param>
    /// <param name="sizeLabel">サイズラベルのテキスト配置。null の場合は数えない。</param>
    /// <param name="coordLabel">カーソル座標ラベルのテキスト配置。null の場合は数えない。</param>
    /// <returns>描く層の一覧（先頭から順に処理する）。</returns>
    /// <remarks>
    /// <b>層にある ⇔ 描く値がある</b>（ルーペの層と同じ規則。描く値の無い層は含めない）。<br/>
    /// テキストは<b>どちらかの文言がある場合</b>だけ末尾へ置き、常に最上位にする<br/>
    /// （UI 要素として置く UI は本層を無視してよい。基底のフックの既定は何もしない）。<br/>
    /// 一覧はビットの組合せで事前生成したものを返す（描画のたびの確保を避ける）。
    /// </remarks>
    private static IReadOnlyList<MarqueeDrawLayer> ResolveLayers(
        MarqueeOuterDecoFrame outer,
        MarqueeInnerDecoFrame inner,
        MarqueeHandles? handles,
        MarqueeMarker? marker,
        MarqueeTextPlacement? sizeLabel,
        MarqueeTextPlacement? coordLabel)
        => s_layers[
            (outer.HasContent || inner.HasContent ? DecorationBit : 0)
            | (handles is not null ? HandlesBit : 0)
            | (marker is not null ? MarkerBit : 0)
            | (sizeLabel is not null || coordLabel is not null ? TextBit : 0)];

    /// <summary>層の全 16 通りを生成する。</summary>
    /// <returns>ビットの組合せを添字とする層の一覧。</returns>
    /// <remarks>ビットの意味は <see cref="DecorationBit"/> / <see cref="HandlesBit"/> / <see cref="MarkerBit"/> / <see cref="TextBit"/> が単一所有する。</remarks>
    private static IReadOnlyList<MarqueeDrawLayer>[] CreateLayers()
    {
        var table = new IReadOnlyList<MarqueeDrawLayer>[1 << 4];

        for (var index = 0; index < table.Length; index++)
        {
            var layers = new List<MarqueeDrawLayer>(4);

            if ((index & DecorationBit) != 0)
            {
                layers.Add(MarqueeDrawLayer.Decoration);
            }

            if ((index & HandlesBit) != 0)
            {
                layers.Add(MarqueeDrawLayer.Handles);
            }

            if ((index & MarkerBit) != 0)
            {
                layers.Add(MarqueeDrawLayer.Marker);
            }

            // テキストは常に最上位（装飾・ハンドル・マーカーより後に描く）
            if ((index & TextBit) != 0)
            {
                layers.Add(MarqueeDrawLayer.Label);
            }

            table[index] = layers;
        }

        return table;
    }

    /// <summary>外側の装飾（暗転マスク・穴あけ枠）の描画内容を解決する。</summary>
    /// <param name="request">描画入力（外観と表示ポリシー）。</param>
    /// <param name="hole">穴あけ矩形（DIP）。選択もハイライトも無い場合は null。</param>
    /// <returns>外側の装飾の描画内容。描くものが無い場合は各項目が空（<see cref="MarqueeOuterDecoFrame.HasContent"/> が false）。</returns>
    /// <remarks>
    /// 1. 穴あけ矩形も全面暗転も無い場合は空のフレームを返す。<br/>
    /// 2. 暗転を描かない設定なら暗転領域を生成しない。<br/>
    /// 3. 穴あけ矩形が無い場合（全面暗転）は枠線を持たない（穴あけ位置が無いため）。<br/>
    /// 4. 枠線を描かない設定なら枠線矩形を生成しない。<br/>
    /// <br/>
    /// 「描くかどうか」の解決は本メソッドが単一所有する（UI 層は値を持つ項目を反映するだけ）。
    /// </remarks>
    private static MarqueeOuterDecoFrame ResolveOuterDecoration(in MarqueeRenderRequest request, EsRect? hole)
    {
        var deco = request.DecoOptions;

        if (!deco.ShouldDrawOverlay(hole is not null))
        {
            return new MarqueeOuterDecoFrame([], []);
        }

        var overlayRegions = deco.ShowOverlay
            ? MarqueeDecoGeometry.ComputeOverlayRegions(request.SurfaceBounds, hole)
            : [];

        var borderRects = hole is { } holeRect && deco.ShowOuterBorder
            ? MarqueeDecoGeometry.ComputeBorderRects(holeRect, deco.OuterAppearance.BorderThickness)
            : [];

        return new MarqueeOuterDecoFrame(overlayRegions, borderRects);
    }

    /// <summary>内側の装飾（穴あけ矩形の塗り・内枠）の描画内容を解決する。</summary>
    /// <param name="request">描画入力（装飾の設定）。</param>
    /// <param name="hole">穴あけ矩形（DIP）。選択もハイライトも無い場合は null。</param>
    /// <returns>内側の装飾の描画内容。描くものが無い場合は各項目が空（<see cref="MarqueeInnerDecoFrame.HasContent"/> が false）。</returns>
    /// <remarks>
    /// 1. 穴あけ矩形が無い場合は空のフレームを返す。<br/>
    /// 2. 塗りを描かない設定なら塗り矩形を生成しない。<br/>
    /// 3. 枠線を描かない設定なら枠線矩形（内側 4 辺）を生成しない。<br/>
    /// <br/>
    /// 枠線矩形の計算は外側の装飾と共通（<see cref="MarqueeDecoGeometry.ComputeBorderRects"/>）のため、<br/>
    /// 外側の枠線と内側の枠線が同じ矩形へ重なる。<br/>
    /// 「描くかどうか」の解決は本メソッドが単一所有する（UI 層は値を持つ項目を反映するだけ）。
    /// </remarks>
    private static MarqueeInnerDecoFrame ResolveInnerDecoration(in MarqueeRenderRequest request, EsRect? hole)
    {
        var deco = request.DecoOptions;

        if (hole is not { } holeRect)
        {
            return new MarqueeInnerDecoFrame(null, []);
        }

        var fillRect = deco.ShowInnerFill ? holeRect : (EsRect?)null;
        var borderRects = deco.ShowInnerBorder
            ? MarqueeDecoGeometry.ComputeBorderRects(holeRect, deco.InnerAppearance.BorderThickness)
            : [];

        return new MarqueeInnerDecoFrame(fillRect, borderRects);
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
    private static MarqueeHandles? ResolveHandles(in MarqueeRenderRequest request, ICoordTransform port)
    {
        var options = request.Options;
        if (!options.ShowHandles || request.SelectionRect is not { } rect)
        {
            return null;
        }

        var dip = request.ToDipRect(rect, port);
        var half = options.HandleSize / 2;
        var rects = new List<EsRect>(MarqueeSelectionGeometry.DirectionalHandles.Count);
        foreach (var handle in MarqueeSelectionGeometry.DirectionalHandles)
        {
            var center = MarqueeSelectionGeometry.GetHandlePosition(dip, handle);
            rects.Add(new EsRect(center.X - half, center.Y - half, options.HandleSize, options.HandleSize));
        }

        return new MarqueeHandles(rects, HandleBorderThickness);
    }

    /// <summary>始点マーカー（十字線）の描画内容を解決する。</summary>
    /// <param name="request">描画入力。</param>
    /// <param name="port">解決済みの座標変換ポート（論理座標 → DIP）。</param>
    /// <returns>マーカーの描画内容。nudge 中でない（または十字線が可視でない）場合は null。</returns>
    /// <remarks>十字線の色は描画の設定（<c>MarqueePaint</c>）が単一所有する。</remarks>
    private static MarqueeMarker? ResolveMarker(in MarqueeRenderRequest request, ICoordTransform port)
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

    /// <summary>サイズラベルの文言と基準点を解決する。</summary>
    /// <param name="request">描画入力。</param>
    /// <param name="hole">穴あけ矩形（DIP）。選択もハイライトも無い場合は null。</param>
    /// <param name="port">解決済みの座標変換ポート（論理座標 → DIP）。</param>
    /// <returns>サイズラベルのテキスト配置。非表示の設定または穴あけ矩形なしの場合は null。</returns>
    /// <remarks>
    /// 基準点は穴あけ矩形の左上であり、アラインは左端・上端である（装飾と同じ規則で決まる）。<br/>
    /// 文言はプロバイダ優先であり、未指定・空文字の場合はコアが矩形から自動生成する<br/>
    /// （書式はコアが単一所有し、描画層・ホストは文言を作らない）。<br/>
    /// 座標の換算は <see cref="MarqueeRenderRequest.DisplayScaleScaling"/> を使い、<br/>
    /// DisplayScale の百分率だけを <see cref="MarqueeRenderRequest.SizeLabelDisplayScalingProvider"/> で矩形ごとに差し替えられる<br/>
    /// （マルチモニタで表示スケールが矩形の位置によって異なる場合、ホストが矩形からモニタのスケールを解決する）。<br/>
    /// フォントサイズ・色は描画の設定（<c>MarqueePaint</c>）が単一所有する。
    /// </remarks>
    private static MarqueeTextPlacement? ResolveSizeLabelText(
        in MarqueeRenderRequest request, EsRect? hole, ICoordTransform port)
    {
        if (!request.Options.ShouldShowSizeLabel(hole is not null) || hole is not { } holeRect)
        {
            return null;
        }

        var displayScaling = request.SizeLabelDisplayScalingProvider is { } scalingProvider
            ? scalingProvider(request.ToDomainRect(holeRect, port))
            : request.DisplayScaleScaling;

        var overrideText = request.SizeLabelOverrideProvider?.Invoke();
        var text = string.IsNullOrEmpty(overrideText)
            ? FormatSizeLabel(holeRect, request.DisplayScaleScaling, displayScaling)
            : overrideText;

        return new MarqueeTextPlacement(
            text, holeRect.TopLeft, EsHorizontalAlign.Left, EsVerticalAlign.Top, default);
    }

    /// <summary>サイズラベルの書式（文言）を生成する。</summary>
    /// <param name="dipRect">対象の矩形（DIP）。</param>
    /// <param name="scaling">座標・サイズを物理ピクセルへ換算する係数（0 以下は 100% として扱う）。</param>
    /// <param name="displayScaling">DisplayScale の百分率に使う係数（0 以下は <paramref name="scaling"/> を使う）。</param>
    /// <returns><c>"(L, T) - (R, B),  (W×H), DisplayScale: (P%)"</c> 形式の文言。</returns>
    /// <remarks>
    /// 書式はコアの内部に閉じる（ホスト・描画層は文言を作らない）。<br/>
    /// 座標・サイズは物理ピクセル（<c>DIP × scaling</c> の整数切り捨て）で表示する。<br/>
    /// 値がドメイン座標（画像ピクセル）の場合は <paramref name="scaling"/> に等倍を渡す（百分率だけを実表示スケールで表示する）。
    /// </remarks>
    private static string FormatSizeLabel(EsRect dipRect, double scaling, double displayScaling)
    {
        var s = DpiScaler.NormalizeScaling(scaling);
        var d = ResolveDisplayScale(displayScaling, s);
        var left = (int)(dipRect.X * s);
        var top = (int)(dipRect.Y * s);
        var right = (int)(dipRect.Right * s);
        var bottom = (int)(dipRect.Bottom * s);
        var width = (int)(dipRect.Width * s);
        var height = (int)(dipRect.Height * s);
        var percent = (int)(d * 100);

        return $"({left}, {top}) - ({right}, {bottom}),  ({width}×{height}), DisplayScale: ({percent}%)";
    }

    /// <summary>カーソル座標ラベルの文言と基準点を解決する。</summary>
    /// <param name="request">描画入力。</param>
    /// <param name="port">解決済みの座標変換ポート（論理座標 → DIP）。</param>
    /// <returns>座標ラベルのテキスト配置。非表示の設定の場合は null。</returns>
    /// <remarks>
    /// 文言はコア（<see cref="EsPoint.Format(int)"/>）が文字列化する。<br/>
    /// 基準点は「カーソル + <see cref="CoordLabelOffset"/>」であり、アラインは左端・上端である<br/>
    /// （ラベルがカーソルに重ならず、描画領域をはみ出す場合は領域内へクランプされる）。<br/>
    /// フォントサイズ・色は描画の設定（<c>MarqueePaint</c>）が単一所有する。
    /// </remarks>
    private static MarqueeTextPlacement? ResolveCoordLabelText(in MarqueeRenderRequest request, ICoordTransform port)
    {
        if (!request.Options.ShouldShowCoordLabel())
        {
            return null;
        }

        var cursor = port.Inverse(request.MousePosition);
        var offset = CoordLabelOffset;

        return new MarqueeTextPlacement(
            request.MousePosition.Format(),
            new EsPoint(cursor.X + offset, cursor.Y + offset),
            EsHorizontalAlign.Left,
            EsVerticalAlign.Top,
            request.ClientSize);
    }
}
