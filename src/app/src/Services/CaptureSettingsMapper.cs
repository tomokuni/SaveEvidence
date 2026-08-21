using app.Models;
using Avalonia.Media;
using Estable.AvaloniaUI.SnipCaptureUI;

namespace app.Services;

/// <summary>Settings → CaptureSettings の変換を提供する。</summary>
/// <remarks>
/// SnipCapture へ反映するキャプチャ設定の変換を一元化する（MainWindow から委譲）。<br/>
/// <c>WindowCaptureMode</c> は渡さない（キャプチャの実処理は仮想スクリーン全体から切り出す方式のため、<br/>
/// 常に ScreenCopy で動作する。SnipCaptureUI 側のデフォルトに委ねる）。<br/>
/// <c>SizeLabelOverride</c> / <c>DisplayScaleScaling</c> はコードから動的に設定されるため、<br/>
/// 設定画面の固定値は使用しない（CropSession が Provider を動的設定 / OverlayWindow.Highlight が
/// DisplayScaleScaling を動的設定）。ここでは null（自動生成）と 0（自動 = Scaling と同値）を渡す。
/// </remarks>
internal static class CaptureSettingsMapper
{
    /// <summary>設定を SnipCapture 用の CaptureSettings へ変換する。</summary>
    /// <param name="settings">アプリ設定。</param>
    /// <param name="renderScaling">描画スケール（ウィンドウの DPI スケーリング。100% = 1.0 / 125% = 1.25 / 150% = 1.5 / 200% = 2.0）。</param>
    /// <returns>変換後の CaptureSettings。</returns>
    public static CaptureSettings From(Settings settings, double renderScaling)
    {
        // Settings → CaptureSettings に変換（全メンバーを明示的に設定。色は Color 型で渡す）
        return new CaptureSettings(
            HideOwnerWindow: settings.HideMainWindowOnCapture,
            LoupeVisible: settings.LoupeModeValue != 0,
            HideBeforeCaptureDelay: TimeSpan.FromMilliseconds(settings.HideBeforeCaptureDelayMs),
            MinSelectionSize: settings.MinSelectionSize,
            BorderColor: Color.Parse(settings.CaptureBorderColor),
            BorderThickness: settings.CaptureBorderThickness,
            OverlayColor: Color.Parse(settings.CaptureOverlayColor),
            HandleColor: Color.Parse(settings.HandleColor),
            HandleSize: settings.HandleSize,
            HandleHitRadius: settings.HandleHitRadius,
            ClampToDomainBounds: settings.ClampToDomainBounds,
            MarkerColor: Color.Parse(settings.MarkerColor),
            MarkerLength: settings.MarkerLength,
            // カーソル座標ラベル（DragRect）
            ShowCoordLabel: settings.ShowCoordLabel,
            CoordLabelColor: Color.Parse(settings.CoordLabelColor),
            CoordLabelBackgroundColor: Color.Parse(settings.CoordLabelBackgroundColor),
            CoordLabelFontSize: settings.CoordLabelFontSize,
            LoupeSize: settings.LoupeSize,
            LoupeZoomLevel: settings.LoupeZoomLevel,
            LoupeOffset: settings.LoupeOffset,
            LoupeRenderScaling: renderScaling,
            LoupeCrossColor: Color.Parse(settings.LoupeCrossColor),
            LoupeFrameColor: Color.Parse(settings.LoupeFrameColor),
            LoupeFrameThickness: settings.LoupeFrameWidth,
            LoupeCheckerSize: settings.PreviewCheckerSize,
            LoupeCheckerColorA: Color.Parse(settings.PreviewCheckerColorA),
            LoupeCheckerColorB: Color.Parse(settings.PreviewCheckerColorB),
            LoupeSelectionRectColorA: Color.Parse(settings.LoupeSelectionRectColorA),
            LoupeSelectionRectColorB: Color.Parse(settings.LoupeSelectionRectColorB),
            // ルーペ座標ラベル
            LoupeShowCoordLabel: settings.LoupeShowCoordLabel,
            LoupeCoordLabelColor: Color.Parse(settings.LoupeCoordLabelColor),
            LoupeCoordLabelBackgroundColor: Color.Parse(settings.LoupeCoordLabelBackgroundColor),
            LoupeCoordLabelFontSize: settings.LoupeCoordLabelFontSize,
            SizeLabelVisible: settings.SizeLabelVisible,
            SizeLabelColor: Color.Parse(settings.CaptureTextColor),
            SizeLabelFontSize: settings.SizeLabelFontSize,
            SizeLabelOverride: null,
            // サイズラベル（1 行目）の背景色（SizeLabelColor は CaptureTextColor と共用 = ラベル設定の 1 行目）
            TextBackgroundColor: Color.Parse(settings.SizeLabelBackgroundColor),
            InfoLabelVisible: settings.InfoLabelVisible,
            InfoLabelColor: Color.Parse(settings.InfoLabelColor),
            InfoLabelBackgroundColor: Color.Parse(settings.InfoLabelBackgroundColor),
            InfoLabelFontSize: settings.InfoLabelFontSize,
            // サブライン（サイズラベルの下の 2 行目以降）
            ShowSubLines: settings.ShowSubLines,
            SubLineFontSize: settings.SubLineFontSize,
            SubLineColor: Color.Parse(settings.SubLineColor),
            SubLineBackgroundColor: Color.Parse(settings.SubLineBackgroundColor),
            DisplayScaleScaling: 0);
    }
}
