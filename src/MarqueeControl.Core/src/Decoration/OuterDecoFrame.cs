using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using DrawingColor = System.Drawing.Color;

namespace Estable.Marquee.Decoration;

/// <summary>暗転マスクの描画入力（設定・状態と、描画時に決まる値を 1 つにまとめた値）。</summary>
/// <remarks>
/// UI 層は自 UI の公開プロパティから本値を組み立てて <see cref="OuterDecoFrame.Resolve"/> へ渡すだけで、<br/>
/// 「どの矩形を暗転するか」「どのラベルを描くか」の判断を持たない（判断はコアが単一所有する）。<br/>
/// 設定と描画状態は <see cref="OuterDecoOptions"/>（公開値）が単一所有し、本値はそれに<b>描画時に決まる値</b><br/>
/// （クライアント矩形・座標変換ポート・倍率・文言プロバイダ）を加えたものである。<br/>
/// 色は UI 非依存の <see cref="DrawingColor"/> を使用する。
/// </remarks>
/// <param name="Options">設定と描画状態（外観・モード・論理矩形・有効フラグ）。</param>
/// <param name="Client">クライアント矩形（DIP）。</param>
/// <param name="Port">論理座標 → DIP の座標変換ポート。null の場合は <paramref name="Scaling"/> を等倍スケールとして適用する。</param>
/// <param name="Scaling">ポート未指定の場合に適用する倍率（0 以下・等倍は等価変換）。</param>
/// <param name="SizeLabelOverrideProvider">サイズラベルの文言プロバイダ。null 可（未指定・空文字の場合は自動生成する）。</param>
/// <param name="OperationTextProvider">操作ラベル（2 行目）の文言プロバイダ。null 可（未指定・空文字の場合は行を描かない。ホストが与える）。</param>
/// <param name="InfoTextProvider">情報ラベル（3 行目）の文言プロバイダ。null 可（未指定・空文字の場合は行を描かない。ホストが与える）。</param>
/// <param name="DisplayScaleScaling">サイズラベルの自動生成に使うスケーリング（0 以下は等倍）。</param>
/// <remarks>
/// 設定と描画状態（<see cref="OuterDecoOptions"/>）は <see cref="MarqueeFrame"/> が組み立てるため、本値は内部契約とする<br/>
/// （UI 実装は <see cref="MarqueeFrame.Resolve"/> を呼び、本値を直接組み立てない）。
/// </remarks>
internal readonly record struct OuterDecoRequest(
    OuterDecoOptions Options,
    EsRect Client,
    ICoordTransform? Port,
    double Scaling,
    Func<string?>? SizeLabelOverrideProvider,
    Func<string?>? OperationTextProvider,
    Func<string?>? InfoTextProvider,
    double DisplayScaleScaling);

/// <summary>暗転マスクの 1 フレーム分の描画内容（描画に必要な値のみをコアが決定した結果）。</summary>
/// <remarks>
/// UI 層は本値の各項目を自身の描画 API へ写すだけにする（判断・計算・既定値の解決は含まない）。<br/>
/// 描画するものが無い場合（暗転なし・枠線なし・ラベルなし）は各項目が空になるため、UI 層の分岐は不要。<br/>
/// 色は UI 非依存の <see cref="DrawingColor"/> を使用し、UI 境界で自 UI の色型へ変換する。
/// </remarks>
/// <param name="OverlayRegions">暗転して塗る領域（DIP）。空の場合は塗らない。</param>
/// <param name="OverlayColor">暗転の色。</param>
/// <param name="BorderRects">枠線を描く矩形（DIP）。空の場合は描かない。</param>
/// <param name="BorderColor">枠線の色。</param>
/// <param name="LabelAnchor">ラベルの基準矩形（穴あけ矩形・DIP）。null の場合はラベルを描かない。</param>
/// <param name="SizeLabelText">サイズラベル（1 行目）の文言。null の場合は描かない（常に非空か null）。</param>
/// <param name="SizeLabelStyle">サイズラベルの外観（背景の実効色を含む）。</param>
/// <param name="SubLines">サイズラベルの下に積む行（2 行目以降）。空の場合は描かない。</param>
public readonly record struct OuterDecoFrame(
    IReadOnlyList<EsRect> OverlayRegions,
    DrawingColor OverlayColor,
    IReadOnlyList<EsRect> BorderRects,
    DrawingColor BorderColor,
    EsRect? LabelAnchor,
    string? SizeLabelText,
    OuterDecoLabelStyle SizeLabelStyle,
    IReadOnlyList<OuterDecoSubLine> SubLines)
{
    /// <summary>描画入力を 1 フレーム分の描画内容へ解決する。</summary>
    /// <param name="request">描画入力（外観・状態・座標変換ポート）。</param>
    /// <returns>描画内容。描画するものが無い場合は各項目が空のフレーム。</returns>
    /// <remarks>
    /// 1. 論理矩形を DIP 矩形へ変換する（選択は閉区間 +1、ハイライトは +0。規則はコアが単一所有する）。<br/>
    /// 2. 穴あけ矩形を決定する（ハイライト優先 → 選択）。<br/>
    /// 3. 暗転の要否を判定する（穴あけ矩形なし + 全面暗転なしなら空のフレームを返す）。<br/>
    /// 4. 暗転の色・枠線の色が透明なら、その領域・矩形を生成しない（UI 層に分岐を持たせない）。<br/>
    /// 5. 穴あけ矩形がある場合のみ、サイズラベルの文言（プロバイダ未指定・空文字なら自動生成）と<br/>
    ///    サブライン（<see cref="OuterDecoAppearance.BuildSubLines"/>）を組み立てる。<br/>
    /// <br/>
    /// ラベル背景の実効色（アルファ 0 を黒 50% として扱う規則）も本メソッドが解決する。<br/>
    /// <b>可視性</b>: UI 実装は <see cref="MarqueeFrame.Resolve"/> を使うため、本メソッドは内部契約とする。
    /// </remarks>
    internal static OuterDecoFrame Resolve(in OuterDecoRequest request)
    {
        var options = request.Options;
        var appearance = options.Appearance;

        // 穴あけ矩形の決定（論理矩形 + 座標変換ポートを描画時に評価。ポート未指定は倍率または等価変換）
        // 選択矩形は閉区間（+1）で DIP 変換する。ハイライト矩形（既に全ピクセルを含む閉区間表現）は +1 しない。
        var port = OuterDecoOverlayGeometry.ResolvePort(request.Port, request.Scaling);
        var selectionRect = options.LogicalSelectionRect is { } selection
            ? OuterDecoOverlayGeometry.ToDipRect(selection, port)
            : (EsRect?)null;
        var highlightRect = options.LogicalHighlightRect is { } highlight
            ? OuterDecoOverlayGeometry.ToDipRect(highlight, port, closedInterval: false)
            : (EsRect?)null;
        var holeRect = OuterDecoOverlayGeometry.ResolveHoleRect(
            options.HasHighlight, highlightRect, options.HasSelection, selectionRect);

        if (!OuterDecoOverlayGeometry.ShouldDrawOverlay(holeRect, options.ShowFullOverlay))
        {
            return CreateEmpty(appearance);
        }

        var overlayRegions = appearance.OverlayColor.A > 0
            ? OuterDecoOverlayGeometry.ComputeOverlayRegions(request.Client, holeRect)
            : [];

        if (holeRect is not { } hole)
        {
            // 全面暗転: 穴あけ位置が無いため枠線・ラベルは持たない
            return new OuterDecoFrame(overlayRegions, appearance.OverlayColor, [], appearance.BorderColor, null, null, default, []);
        }

        var borderRects = appearance.BorderColor.A > 0
            ? OuterDecoOverlayGeometry.GetBorderRects(hole, appearance.BorderThickness)
            : [];

        // サイズラベルのプロバイダは表示時のみ評価する（非表示時の無駄な呼び出しを回避）
        var sizeLabelText = appearance.ShowSizeLabel
            ? ResolveSizeLabelText(request, hole)
            : null;
        var sizeLabelStyle = new OuterDecoLabelStyle(
            appearance.SizeLabelFontSize,
            appearance.SizeLabelColor,
            OuterDecoAppearance.ResolveLabelBackground(appearance.SizeLabelBackgroundColor));

        return new OuterDecoFrame(
            overlayRegions,
            appearance.OverlayColor,
            borderRects,
            appearance.BorderColor,
            hole,
            sizeLabelText,
            sizeLabelStyle,
            appearance.BuildSubLines(
                request.OperationTextProvider?.Invoke(),
                request.InfoTextProvider?.Invoke()));
    }

    /// <summary>サイズラベル（1 行目）の文言を決定する。</summary>
    /// <param name="request">描画入力。</param>
    /// <param name="holeRect">穴あけ矩形（DIP）。</param>
    /// <returns>プロバイダの戻り値（非空の場合）。未指定・空文字の場合は矩形から自動生成した文言（常に非空）。</returns>
    /// <remarks>
    /// プロバイダは「ホスト固有の文言（元画像座標・表示倍率等）」を与えるための入口であり、<br/>
    /// 未指定・空文字の場合は矩形と <see cref="OuterDecoRequest.DisplayScaleScaling"/> から自動生成する（両 UI 共通）。<br/>
    /// したがって本メソッドは常に非空の文言を返す（呼び出し側の空文字判定は不要）。
    /// </remarks>
    private static string ResolveSizeLabelText(in OuterDecoRequest request, EsRect holeRect)
    {
        var overrideText = request.SizeLabelOverrideProvider?.Invoke();
        return string.IsNullOrEmpty(overrideText)
            ? OuterDecoLabels.FormatSizeLabel(holeRect, request.DisplayScaleScaling)
            : overrideText;
    }

    /// <summary>描画するものが無いフレームを生成する。</summary>
    /// <param name="appearance">外観（色のみ引き継ぐ）。</param>
    /// <returns>各項目が空のフレーム。</returns>
    private static OuterDecoFrame CreateEmpty(OuterDecoAppearance appearance)
        => new([], appearance.OverlayColor, [], appearance.BorderColor, null, null, default, []);
}
