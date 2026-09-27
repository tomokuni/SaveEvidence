using EsUtil.Feature.MarqueeCore.Decoration;
using EsUtil.SaveEvidence.FadeMaskDragRect;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Feature.MarqueeCore.Setting;
using EsUtil.Feature.LoupeCore.Setting;

namespace EsUtil.SaveEvidence.CaptureSession;

/// <summary>キャプチャ設定（<see cref="CaptureSettings"/>）から複合コントロールの設定（<see cref="FadeMaskDragRectOptions"/>）への変換。</summary>
/// <remarks>
/// 設定項目と外観（選択矩形・外側の装飾（暗転マスク・穴あけ枠・ラベル）・ルーペ・選択矩形破線）の対応を単一所有する。<br/>
/// 両 UI のホスト（セッション）が本変換を使うため、UI ごとに見た目がずれる余地がない（UI 非依存の型のみで完結する）。<br/>
/// <br/>
/// <b>本変換の対象外</b>（ホストが個別に設定する）:<br/>
/// - DPI スケーリング（<c>DomainToDipScaling</c> / <c>DisplayScaleScaling</c>）とルーペの描画スケール（<c>LoupeDipToDeviceScaling</c>）: 環境値のため。<br/>
/// - 実行時状態（選択矩形・ハイライト矩形・ルーペ中心・拡大元画像）と座標変換ポート: セッションが管理する。
/// </remarks>
public static class FadeMaskDragRectOptionsMapper
{
    /// <summary>キャプチャ設定を複合コントロールの設定へ変換する。</summary>
    /// <param name="settings">キャプチャ設定（全メンバー non-null。<c>ToNonNull</c> 済みを渡す）。</param>
    /// <returns>複合コントロールへ一括設定できる設定値。</returns>
    /// <remarks>
    /// 1. 選択矩形の設定（<see cref="MarqueeOptions"/>）を組み立てる（選択の判断に必要な項目のみ。色は含まない）。<br/>
    /// 2. 装飾の設定（<see cref="MarqueeDecoOptions"/>）を組み立てる。枠線の太さ（<see cref="CaptureSettings.BorderThickness"/>）は<br/>
    ///    装飾の外枠（暗転マスクの穴あけ枠）が単一所有する。描画の可否は明示フラグで与える<br/>
    ///    （従来の「アルファ 0 = 描かない」をフラグで表す）。<br/>
    /// 3. ルーペの設定（<see cref="LoupeOptions"/>）を組み立てる（寸法のみ）。<br/>
    /// 4. 選択矩形破線の色とルーペのオフセットを設定する（オフセットは <see cref="CaptureSettings.LoupeOffset"/> を使う）。<br/>
    /// 5. ホスト固有の付加情報（操作・情報ラベル）の外観（<see cref="FadeMaskDragRectLabelOptions"/>）を組み立てる。<br/>
    ///    文言の生成と描画はホストが行い、本変換は外観のみを写像する（表示可否はホストが判定する）。<br/>
    /// <br/>
    /// <b>色は写像しない</b>: 色は描画にだけ必要な設定であり、描画の設定（UI 層のホストが組み立てる色の値）が単一所有する。<br/>
    /// 本変換はコアの設定（表示可否・寸法）だけを組み立てる（Core → UIBase の逆流を作らないため）。<br/>
    /// <b>ラベルのフォントサイズも写像しない</b>: ラベルをどの体裁で描くかは描画の設定であり、<br/>
    /// 描画の設定（<c>MarqueePaint</c> / <c>LoupePaint</c>）が単一所有する。
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
                ShowCoordLabel = settings.ShowCoordLabel!.Value,
                // サイズラベルは選択コアが単一所有する（穴あけ矩形の左上に表示する位置と寸法のラベル）
                ShowSizeLabel = settings.SizeLabelVisible!.Value,
            },
            Deco = new MarqueeDecoOptions
            {
                // 暗転と枠線は暗転マスクの穴あけ枠と選択枠線が同じ矩形に重なるため、寸法を装飾が単一所有する
                // （色は描画にだけ必要な設定のため、描画側（UI 層のホスト）が設定する）
                // （サイズ・操作・情報ラベルは装飾の概念ではない。操作・情報ラベルはホストが自前で描く）
                OuterAppearance = new MarqueeOuterDecoAppearance
                {
                    BorderThickness = settings.BorderThickness!.Value,
                },
                // 色が透明でも描画フラグはホストが決める（従来の「アルファ 0 = 描かない」を明示フラグで表現する）
                ShowOverlay = settings.OverlayColor!.Value.IsVisible(),
                ShowOuterBorder = settings.BorderColor!.Value.IsVisible(),
            },
            Loupe = new LoupeOptions
            {
                RequestedSize = settings.LoupeSize!.Value,
                ZoomLevel = settings.LoupeZoomLevel!.Value,
                // 寸法とラベル表示の可否のみを写像する（色は描画にだけ必要な設定のため、描画側（UI 層のホスト）が設定する）
                BorderThickness = settings.LoupeFrameThickness!.Value,
                ShowCoordLabel = settings.LoupeShowCoordLabel!.Value,
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
