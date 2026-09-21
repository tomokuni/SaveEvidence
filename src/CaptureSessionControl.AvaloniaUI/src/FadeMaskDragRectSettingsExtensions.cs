using Estable.AvaloniaUI.FadeMaskDragRect;
using Estable.CaptureSession;

namespace Estable.AvaloniaUI.CaptureSession;

/// <summary>CaptureSettings から FadeMaskDragRectSettings への変換（複合コントロールの外観設定マッピングの一元化）。</summary>
/// <remarks>
/// 外観設定マッピングを本拡張メソッドに集約する（DRY）。<br/>
/// モード固有の差異（ルーペオフセット）は引数（<c>positionOffset</c>）で調整する。<br/>
/// <br/>
/// - Crop（プレビュー内）: <c>positionOffset = LoupeOffset</c><br/>
/// - AreaSelect（全画面）: <c>positionOffset = 20.0</c><br/>
/// <br/>
/// 実行時状態（選択矩形・ハイライト等）と座標変換ポートは本変換の対象外（セッションが管理）。<br/>
/// DPI スケーリング（<c>Scaling</c> / <c>DisplayScaleScaling</c>）とルーペの描画スケール（<c>LoupeRenderScaling</c>）も本変換の対象外であり、<br/>
/// 個別プロパティ（<c>FadeMaskDragRectControl.Scaling</c> 等）で設定する。
/// </remarks>
internal static class FadeMaskDragRectSettingsExtensions
{
    /// <summary>CaptureSettings を FadeMaskDragRectSettings へ変換する。</summary>
    /// <param name="settings">キャプチャ設定（全メンバー non-null。ToNonNull 済みを想定）。</param>
    /// <param name="positionOffset">ルーペのカーソル中心からの右下オフセット（DIP）。Crop は LoupeOffset、AreaSelect は 20.0。</param>
    /// <returns>FadeMaskDragRectSettings（外観設定の一括適用用）。</returns>
    /// <remarks>
    /// DPI スケーリング（<c>Scaling</c> / <c>DisplayScaleScaling</c>）とルーペの描画スケール（<c>RenderScaling</c>）は<br/>
    /// 本変換の対象外（コードから動的設定・DPI から自動取得）のため、個別プロパティ（<c>FadeMaskDragRectControl.Scaling</c> 等）で設定する。<br/>
    /// サイズラベルの文字色（TextColor）は SizeLabelColor をマッピングする（FadeMaskRenderer の命名に合わせる）。<br/>
    /// </remarks>
    public static FadeMaskDragRectSettings ToFadeMaskDragRectSettings(
        this CaptureSettings settings, double positionOffset = 20.0)
    {
        return new FadeMaskDragRectSettings
        {
            // DragRect 外観
            MinSelectionSize = settings.MinSelectionSize!.Value,
            BorderColor = ToAvalonia(settings.BorderColor!.Value),
            BorderThickness = settings.BorderThickness!.Value,
            HandleColor = ToAvalonia(settings.HandleColor!.Value),
            HandleSize = settings.HandleSize!.Value,
            HandleHitRadius = settings.HandleHitRadius!.Value,
            ClampToDomainBounds = settings.ClampToDomainBounds!.Value,
            MarkerColor = ToAvalonia(settings.MarkerColor!.Value),
            MarkerLength = settings.MarkerLength!.Value,
            // カーソル座標ラベル（DragRect）
            ShowCoordLabel = settings.ShowCoordLabel!.Value,
            CoordLabelColor = ToAvalonia(settings.CoordLabelColor!.Value),
            CoordLabelBackgroundColor = ToAvalonia(settings.CoordLabelBackgroundColor!.Value),
            CoordLabelFontSize = settings.CoordLabelFontSize!.Value,
            // FadeMask 外観
            OverlayColor = ToAvalonia(settings.OverlayColor!.Value),
            TextColor = ToAvalonia(settings.SizeLabelColor!.Value),
            TextBackgroundColor = ToAvalonia(settings.TextBackgroundColor!.Value),
            FontSize = settings.SizeLabelFontSize!.Value,
            ShowSizeLabel = settings.SizeLabelVisible!.Value,
            InfoLabelVisible = settings.InfoLabelVisible!.Value,
            InfoLabelColor = ToAvalonia(settings.InfoLabelColor!.Value),
            InfoLabelBackgroundColor = ToAvalonia(settings.InfoLabelBackgroundColor!.Value),
            InfoLabelFontSize = settings.InfoLabelFontSize!.Value,
            // サブライン（サイズラベルの下の 2 行目以降）
            ShowSubLines = settings.ShowSubLines!.Value,
            SubLineFontSize = settings.SubLineFontSize!.Value,
            SubLineColor = ToAvalonia(settings.SubLineColor!.Value),
            SubLineBackgroundColor = ToAvalonia(settings.SubLineBackgroundColor!.Value),
            // Loupe 外観
            RequestedSize = settings.LoupeSize!.Value,
            ZoomLevel = settings.LoupeZoomLevel!.Value,
            CrossColor = ToAvalonia(settings.LoupeCrossColor!.Value),
            CheckerSize = settings.LoupeCheckerSize!.Value,
            CheckerColorA = ToAvalonia(settings.LoupeCheckerColorA!.Value),
            CheckerColorB = ToAvalonia(settings.LoupeCheckerColorB!.Value),
            FrameColor = ToAvalonia(settings.LoupeFrameColor!.Value),
            FrameWidth = settings.LoupeFrameThickness!.Value,
            SelectionRectColorA = ToAvalonia(settings.LoupeSelectionRectColorA!.Value),
            SelectionRectColorB = ToAvalonia(settings.LoupeSelectionRectColorB!.Value),
            PositionOffset = positionOffset,
            // ルーペ座標ラベル
            LoupeShowCoordLabel = settings.LoupeShowCoordLabel!.Value,
            LoupeCoordLabelColor = ToAvalonia(settings.LoupeCoordLabelColor!.Value),
            LoupeCoordLabelBackgroundColor = ToAvalonia(settings.LoupeCoordLabelBackgroundColor!.Value),
            LoupeCoordLabelFontSize = settings.LoupeCoordLabelFontSize!.Value,
        };
    }
    /// <summary>コアの色型（<see cref="System.Drawing.Color"/>）を Avalonia の色型へ変換する。</summary>
    /// <param name="color">変換元の色（A/R/G/B）。</param>
    /// <returns>Avalonia の色。</returns>
    private static Avalonia.Media.Color ToAvalonia(System.Drawing.Color color)
        => Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
}