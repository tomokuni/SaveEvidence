using Estable.DragRect;
using Estable.FadeMask;
using Estable.FadeMaskDragRect;
using Estable.Loupe;
using DrawingColor = System.Drawing.Color;

namespace Estable.CaptureSession;

/// <summary>キャプチャ設定（<see cref="CaptureSettings"/>）から複合コントロールの設定（<see cref="FadeMaskDragRectOptions"/>）への変換。</summary>
/// <remarks>
/// 設定項目と外観（選択矩形・暗転マスク・ルーペ・選択矩形破線）の対応を単一所有する。<br/>
/// 両 UI のホスト（セッション）が本変換を使うため、UI ごとに見た目がずれる余地がない（UI 非依存の型のみで完結する）。<br/>
/// <br/>
/// <b>本変換の対象外</b>（ホストが個別に設定する）:<br/>
/// - DPI スケーリング（<c>Scaling</c> / <c>DisplayScaleScaling</c>）とルーペの描画スケール（<c>LoupeRenderScaling</c>）: 環境値のため。<br/>
/// - 実行時状態（選択矩形・ハイライト矩形・ルーペ中心・拡大元画像）と座標変換ポート: セッションが管理する。
/// </remarks>
public static class FadeMaskDragRectOptionsMapper
{
    /// <summary>キャプチャ設定を複合コントロールの設定へ変換する。</summary>
    /// <param name="settings">キャプチャ設定（全メンバー non-null。<c>ToNonNull</c> 済みを渡す）。</param>
    /// <param name="loupeBackground">ルーペの市松模様の下地色。null の場合は透明（背後の要素を透かす）。</param>
    /// <returns>複合コントロールへ一括設定できる設定値。</returns>
    /// <remarks>
    /// 1. 選択矩形の設定（<see cref="DragRectOptions"/>）を組み立てる。枠線は暗転マスクの穴あけ枠と共用するため<br/>
    ///    <see cref="FadeMaskAppearance"/> 側へ設定する（<see cref="FadeMaskDragRectOptions.ResolveDragRectOptions"/> が導出する）。<br/>
    /// 2. 暗転マスクの外観（<see cref="FadeMaskAppearance"/>）を組み立てる。サイズラベルの背景色は<br/>
    ///    <see cref="CaptureSettings.TextBackgroundColor"/> を対応させる。<br/>
    /// 3. ルーペの設定（<see cref="LoupeOptions"/>）を組み立てる。市松模様の下地色は<b>ホストが決めた色</b>を使う<br/>
    ///    （引数 <paramref name="loupeBackground"/>。撮影オーバーレイは白、プレビューの切り出しはテーマ背景色）。<br/>
    /// 4. 選択矩形破線の色とルーペのオフセットを設定する（オフセットは <see cref="CaptureSettings.LoupeOffset"/> を使う）。
    /// </remarks>
    public static FadeMaskDragRectOptions ToFadeMaskDragRectOptions(
        this CaptureSettings settings, DrawingColor? loupeBackground = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new FadeMaskDragRectOptions
        {
            DragRect = new DragRectOptions
            {
                MinSelectionSize = settings.MinSelectionSize!.Value,
                HandleColor = settings.HandleColor!.Value,
                HandleSize = settings.HandleSize!.Value,
                HandleHitRadius = settings.HandleHitRadius!.Value,
                ClampToDomainBounds = settings.ClampToDomainBounds!.Value,
                MarkerColor = settings.MarkerColor!.Value,
                MarkerLength = settings.MarkerLength!.Value,
                ShowCoordLabel = settings.ShowCoordLabel!.Value,
                CoordLabelColor = settings.CoordLabelColor!.Value,
                CoordLabelBackgroundColor = settings.CoordLabelBackgroundColor!.Value,
                CoordLabelFontSize = settings.CoordLabelFontSize!.Value,
            },
            Overlay = new FadeMaskAppearance
            {
                OverlayColor = settings.OverlayColor!.Value,
                // 枠線は選択枠線と共用する（DarkGray 等の設定値がそのまま両方に効く）
                BorderColor = settings.BorderColor!.Value,
                BorderThickness = settings.BorderThickness!.Value,
                ShowSizeLabel = settings.SizeLabelVisible!.Value,
                SizeLabelColor = settings.SizeLabelColor!.Value,
                SizeLabelBackgroundColor = settings.TextBackgroundColor!.Value,
                SizeLabelFontSize = settings.SizeLabelFontSize!.Value,
                ShowSubLines = settings.ShowSubLines!.Value,
                SubLineColor = settings.SubLineColor!.Value,
                SubLineBackgroundColor = settings.SubLineBackgroundColor!.Value,
                SubLineFontSize = settings.SubLineFontSize!.Value,
                InfoLabelVisible = settings.InfoLabelVisible!.Value,
                InfoLabelColor = settings.InfoLabelColor!.Value,
                InfoLabelBackgroundColor = settings.InfoLabelBackgroundColor!.Value,
                InfoLabelFontSize = settings.InfoLabelFontSize!.Value,
            },
            Loupe = new LoupeOptions
            {
                RequestedSize = settings.LoupeSize!.Value,
                ZoomLevel = settings.LoupeZoomLevel!.Value,
                Appearance = new LoupeAppearance
                {
                    // 市松模様の色は半透明のため、下地を塗らないと背後の要素（撮影画像・画面・プレビュー）が透ける
                    BackgroundColor = loupeBackground ?? DrawingColor.Transparent,
                    CrossColor = settings.LoupeCrossColor!.Value,
                    CheckerSize = settings.LoupeCheckerSize!.Value,
                    CheckerColorA = settings.LoupeCheckerColorA!.Value,
                    CheckerColorB = settings.LoupeCheckerColorB!.Value,
                    FrameColor = settings.LoupeFrameColor!.Value,
                    FrameWidth = settings.LoupeFrameThickness!.Value,
                    ShowCoordLabel = settings.LoupeShowCoordLabel!.Value,
                    CoordLabelColor = settings.LoupeCoordLabelColor!.Value,
                    CoordLabelBackgroundColor = settings.LoupeCoordLabelBackgroundColor!.Value,
                    CoordLabelFontSize = settings.LoupeCoordLabelFontSize!.Value,
                },
            },
            SelectionRectColorA = settings.LoupeSelectionRectColorA!.Value,
            SelectionRectColorB = settings.LoupeSelectionRectColorB!.Value,
            LoupeOffset = settings.LoupeOffset!.Value,
        };
    }
}
