using Estable.AvaloniaUI.FadeMaskDragRect;

namespace Estable.AvaloniaUI.SnipCaptureUI;

/// <summary>CaptureSettings から FadeMaskDragRectSettings への変換（複合コントロールの外観設定マッピングの一元化）。</summary>
/// <remarks>
/// 外観設定マッピングを本拡張メソッドに集約する（DRY）。<br/>
/// モード固有の差異（スケーリング・ルーペオフセット）は引数で調整する。<br/>
/// <br/>
/// - Crop（プレビュー内）: <c>scaling = 1.0</c> / <c>positionOffset = LoupeOffset</c><br/>
/// - AreaSelect（全画面）: <c>scaling = DPI スケーリング</c> / <c>positionOffset = 20.0</c><br/>
/// <br/>
/// 実行時状態（選択矩形・ハイライト等）と座標変換ポートは本変換の対象外（セッションが管理）。
/// </remarks>
internal static class FadeMaskDragRectSettingsExtensions
{
    /// <summary>CaptureSettings を FadeMaskDragRectSettings へ変換する。</summary>
    /// <param name="settings">キャプチャ設定（全メンバー non-null。ToNonNull 済みを想定）。</param>
    /// <param name="scaling">DPI スケーリング係数（ドメイン → DIP 変換の基準倍率）。Crop は 1.0、AreaSelect はウィンドウのスケーリング。</param>
    /// <param name="positionOffset">ルーペのカーソル中心からの右下オフセット（DIP）。Crop は LoupeOffset、AreaSelect は 20.0。</param>
    /// <returns>FadeMaskDragRectSettings（外観設定の一括適用用）。</returns>
    /// <remarks>
    /// DisplayScaleScaling は設定値（0 以下の場合は scaling と同値）をそのまま使用する<br/>
    /// （FadeMaskRenderer.DisplayScaleScaling が 0 以下を Scaling と同値として扱う）。<br/>
    /// サイズラベルの文字色（TextColor）は SizeLabelColor をマッピングする（FadeMaskRenderer の命名に合わせる）。<br/>
    /// </remarks>
    public static FadeMaskDragRectSettings ToFadeMaskDragRectSettings(
        this CaptureSettings settings, double scaling = 1.0, double positionOffset = 20.0)
    {
        return new FadeMaskDragRectSettings
        {
            // DragRect 外観
            MinSelectionSize = settings.MinSelectionSize!.Value,
            BorderColor = settings.BorderColor!.Value,
            BorderThickness = settings.BorderThickness!.Value,
            HandleColor = settings.HandleColor!.Value,
            HandleSize = settings.HandleSize!.Value,
            HandleHitRadius = settings.HandleHitRadius!.Value,
            ClampToDomainBounds = settings.ClampToDomainBounds!.Value,
            MarkerColor = settings.MarkerColor!.Value,
            MarkerLength = settings.MarkerLength!.Value,
            // カーソル座標ラベル（DragRect）
            ShowCoordLabel = settings.ShowCoordLabel!.Value,
            CoordLabelColor = settings.CoordLabelColor!.Value,
            CoordLabelBackgroundColor = settings.CoordLabelBackgroundColor!.Value,
            CoordLabelFontSize = settings.CoordLabelFontSize!.Value,
            // FadeMask 外観
            Scaling = scaling,
            DisplayScaleScaling = settings.DisplayScaleScaling!.Value > 0 ? settings.DisplayScaleScaling!.Value : scaling,
            OverlayColor = settings.OverlayColor!.Value,
            TextColor = settings.SizeLabelColor!.Value,
            TextBackgroundColor = settings.TextBackgroundColor!.Value,
            FontSize = settings.SizeLabelFontSize!.Value,
            ShowSizeLabel = settings.SizeLabelVisible!.Value,
            InfoLabelVisible = settings.InfoLabelVisible!.Value,
            InfoLabelColor = settings.InfoLabelColor!.Value,
            InfoLabelBackgroundColor = settings.InfoLabelBackgroundColor!.Value,
            InfoLabelFontSize = settings.InfoLabelFontSize!.Value,
            // サブライン（サイズラベルの下の 2 行目以降）
            ShowSubLines = settings.ShowSubLines!.Value,
            SubLineFontSize = settings.SubLineFontSize!.Value,
            SubLineColor = settings.SubLineColor!.Value,
            SubLineBackgroundColor = settings.SubLineBackgroundColor!.Value,
            // Loupe 外観
            RequestedSize = settings.LoupeSize!.Value,
            ZoomLevel = settings.LoupeZoomLevel!.Value,
            RenderScaling = settings.LoupeRenderScaling!.Value,
            CrossColor = settings.LoupeCrossColor!.Value,
            CheckerSize = settings.LoupeCheckerSize!.Value,
            CheckerColorA = settings.LoupeCheckerColorA!.Value,
            CheckerColorB = settings.LoupeCheckerColorB!.Value,
            FrameColor = settings.LoupeFrameColor!.Value,
            FrameWidth = settings.LoupeFrameThickness!.Value,
            SelectionRectColorA = settings.LoupeSelectionRectColorA!.Value,
            SelectionRectColorB = settings.LoupeSelectionRectColorB!.Value,
            PositionOffset = positionOffset,
            // ルーペ座標ラベル
            LoupeShowCoordLabel = settings.LoupeShowCoordLabel!.Value,
            LoupeCoordLabelColor = settings.LoupeCoordLabelColor!.Value,
            LoupeCoordLabelBackgroundColor = settings.LoupeCoordLabelBackgroundColor!.Value,
            LoupeCoordLabelFontSize = settings.LoupeCoordLabelFontSize!.Value,
        };
    }
}
