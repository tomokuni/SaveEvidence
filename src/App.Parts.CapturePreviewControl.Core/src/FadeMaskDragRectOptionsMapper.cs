using EsUtil.Feature.Marquee.Core.Setting;
using EsUtil.Feature.Loupe.Core.Setting;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core;

/// <summary>キャプチャ設定（<see cref="CaptureSettings"/>）から複合コントロールの設定（<see cref="FadeMaskDragRectOptions"/>）への変換。</summary>
/// <remarks>
/// 設定項目と外観（選択矩形・外側の装飾（暗転マスク・外枠・ラベル）・ルーペ・選択矩形破線）の対応を単一所有する。<br/>
/// 両 UI のホスト（セッション）が本変換を使うため、UI ごとに見た目がずれる余地がない（UI 非依存の型のみで完結する）。<br/>
/// <br/>
/// <b>本変換の対象外</b>（ホストが個別に設定する）:<br/>
/// - DPI スケーリング（<c>DipToDomainScaling</c> / <c>DisplayScaleScaling</c>）とルーペの描画スケール（<c>LoupeClientToPixelScaling</c>）: 環境値のため。<br/>
/// - 実行時状態（選択矩形・ハイライト矩形・ルーペ中心・拡大元画像）と座標変換ポート: セッションが管理する。
/// </remarks>
public static class FadeMaskDragRectOptionsMapper
{
    /// <summary>キャプチャ設定を複合コントロールの設定へ変換する。</summary>
    /// <param name="settings">キャプチャ設定（全メンバー non-null。<c>ToNonNull</c> 済みを渡す）。</param>
    /// <returns>複合コントロールへ一括設定できる設定値。</returns>
    /// <remarks>
    /// 1. 選択矩形の設定（<see cref="MarqueeOptions"/>）を組み立てる（選択の判断に必要な項目のみ。 色は含まない）。<br/>
    /// 2. ルーペの設定（<see cref="LoupeOptions"/>）を組み立てる（計算に必要な値のみ）。<br/>
    /// 4. 選択矩形破線の色とルーペのオフセットを設定する（オフセットは <see cref="CaptureSettings.LoupeOffset"/> を使う）。<br/>
    /// 5. ホスト固有の付加情報（操作・情報ラベル）の外観（<see cref="FadeMaskDragRectLabelOptions"/>）を組み立てる。<br/>
    ///    文言の生成と描画はホストが行い、本変換は外観のみを写像する（表示可否はホストが判定する）。<br/>
    /// <br/>
    /// <b>色は写像しない</b>: 色は描画にだけ必要な設定であり、描画の設定（UI 層のホストが組み立てる色の値）が単一所有する。<br/>
    /// 本変換はコアの設定（表示可否・寸法）だけを組み立てる（Core → UIBase の逆流を作らないため）。<br/>
    /// <b>描画にだけ必要な設定（色・フォント・枠線の太さ・表示可否）も写像しない</b>:<br/>
    /// これらは描画の設定（<c>MarqueeAppearance</c> / <c>LoupeAppearance</c>）が単一所有する。
    /// </remarks>
    public static FadeMaskDragRectOptions ToFadeMaskDragRectOptions(this CaptureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new FadeMaskDragRectOptions
        {
            Marquee = new MarqueeOptions
            {
                MinSelectionSize = settings.MinSelectionSize!.Value,
                HandleSize = settings.HandleSize!.Value,
                HandleHitRadius = settings.HandleHitRadius!.Value,
                ClampToDomainBounds = settings.ClampToDomainBounds!.Value,
                MarkerLength = settings.MarkerLength!.Value,
            },
            Loupe = new LoupeOptions
            {
                RequestedSize = settings.LoupeSize!.Value,
                ZoomLevel = settings.LoupeZoomLevel!.Value,
            },
            SelectionRectColorA = settings.LoupeSelectionRectColorA!.Value,
            SelectionRectColorB = settings.LoupeSelectionRectColorB!.Value,
            LoupeOffset = settings.LoupeOffset!.Value,
            // 操作・情報ラベルはホストが自前で描くため、外観（表示可否・寸法・色）だけを写像する
            Labels = new FadeMaskDragRectLabelOptions
            {
                ShowOperationLabel = settings.ShowSubLines!.Value,
                OperationFontSize = settings.SubLineFontSize!.Value,
                OperationColor = settings.SubLineColor!.Value,
                OperationBackgroundColor = settings.SubLineBackgroundColor!.Value,
                ShowInfoLabel = settings.InfoLabelVisible!.Value,
                InfoFontSize = settings.InfoLabelFontSize!.Value,
                InfoColor = settings.InfoLabelColor!.Value,
                InfoBackgroundColor = settings.InfoLabelBackgroundColor!.Value,
            },
        };
    }
}
