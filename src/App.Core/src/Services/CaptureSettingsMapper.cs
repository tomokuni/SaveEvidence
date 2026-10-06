using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Setting;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>Settings → CaptureSettings の変換を提供する。</summary>
/// <remarks>
/// CaptureSessionControl へ反映するキャプチャ設定の変換を一元化する（MainWindow から委譲）。<br/>
///
/// <c>DisplayScaleScaling</c> はコードから動的に設定されるため、<br/>
/// 設定画面の固定値は使用しない（CropSession が Provider を動的設定 / CaptureOverlayWindow.Highlight が
/// DisplayScaleScaling を動的設定）。 ここでは null（自動生成）と 0（自動 = Scaling と同値）を渡す。
/// </remarks>
internal static class CaptureSettingsMapper
{
    /// <summary>設定を CaptureSessionControl 用の CaptureSettings へ変換する。</summary>
    /// <param name="settings">アプリ設定。</param>
    /// <param name="renderScaling">描画スケール（ウィンドウの DPI スケーリング。 100% = 1.0 / 125% = 1.25 / 150% = 1.5 / 200% = 2.0）。</param>
    /// <returns>変換後の CaptureSettings。</returns>
    public static CaptureSettings From(Settings settings, double renderScaling)
    {
        // Settings → CaptureSettings に変換（全メンバーを明示的に設定。 色は Color 型で渡す）
        return new CaptureSettings(
            HideOwnerWindow: settings.HideMainWindowOnCapture,
            MinSelectionSize: settings.MinSelectionSize,
            BorderColor: ParseColor(settings.CaptureBorderColor),
            BorderThickness: settings.CaptureBorderThickness,
            OverlayColor: ParseColor(settings.CaptureOverlayColor),
            HandleColor: ParseColor(settings.HandleColor),
            HandleSize: settings.HandleSize,
            HandleHitRadius: settings.HandleHitRadius,
            ClampToSourceBounds: settings.ClampToSourceBounds,
            MarkerColor: ParseColor(settings.MarkerColor),
            MarkerLength: settings.MarkerLength,
            // カーソル座標ラベル（Marquee）
            ShowCoordLabel: settings.ShowCoordLabel,
            CoordLabelColor: ParseColor(settings.CoordLabelColor),
            CoordLabelBackgroundColor: ParseColor(settings.CoordLabelBackgroundColor),
            CoordLabelFontSize: settings.CoordLabelFontSize,
            LoupeSize: settings.LoupeSize,
            LoupeZoomLevel: settings.LoupeZoomLevel,
            LoupeOffset: settings.LoupeOffset,
            LoupeClientToPixelScaling: renderScaling,
            LoupeCrossColor: ParseColor(settings.LoupeCrossColor),
            LoupeFrameColor: ParseColor(settings.LoupeFrameColor),
            LoupeFrameThickness: settings.LoupeFrameWidth,
            LoupePasteboardColor: ParseColor(settings.LoupePasteboardColor),
            LoupeSelectionRectColorA: ParseColor(settings.LoupeSelectionRectColorA),
            LoupeSelectionRectColorB: ParseColor(settings.LoupeSelectionRectColorB),
            // ルーペ座標ラベル
            LoupeShowCoordLabel: settings.LoupeShowCoordLabel,
            LoupeCoordLabelColor: ParseColor(settings.LoupeCoordLabelColor),
            LoupeCoordLabelBackgroundColor: ParseColor(settings.LoupeCoordLabelBackgroundColor),
            LoupeCoordLabelFontSize: settings.LoupeCoordLabelFontSize,
            SizeLabelVisible: settings.SizeLabelVisible,
            SizeLabelColor: ParseColor(settings.CaptureTextColor),
            SizeLabelFontSize: settings.SizeLabelFontSize,
            // サイズラベル（1 行目）の背景色（SizeLabelColor は CaptureTextColor と共用 = ラベル設定の 1 行目）
            TextBackgroundColor: ParseColor(settings.SizeLabelBackgroundColor),
            InfoLabelVisible: settings.InfoLabelVisible,
            InfoLabelColor: ParseColor(settings.InfoLabelColor),
            InfoLabelBackgroundColor: ParseColor(settings.InfoLabelBackgroundColor),
            InfoLabelFontSize: settings.InfoLabelFontSize,
            // サブライン（サイズラベルの下の 2 行目以降）
            ShowSubLines: settings.ShowSubLines,
            SubLineFontSize: settings.SubLineFontSize,
            SubLineColor: ParseColor(settings.SubLineColor),
            SubLineBackgroundColor: ParseColor(settings.SubLineBackgroundColor),
            DisplayScaleScaling: 0);
    }

    /// <summary>設定の色文字列（<c>#AARRGGBB</c> / <c>#RRGGBB</c>）を色へ変換する。</summary>
    /// <param name="argb">設定の色文字列。 前後の空白と <c>#</c> は省略可能。</param>
    /// <returns>変換した色。 解析できない場合は透明。</returns>
    /// <remarks>
    /// 設定（<c>Settings</c>）は色と不透明度を統合した 16 進文字列で保持する。<br/>
    /// MewUI 版の <c>SettingsColorMath</c> と同じ規則（8 桁 = AARRGGBB / 6 桁 = RRGGBB・アルファ 255）。
    /// </remarks>
    private static System.Drawing.Color ParseColor(string? argb)
    {
        var text = argb?.Trim().TrimStart('#');
        if (string.IsNullOrEmpty(text))
        {
            return System.Drawing.Color.Transparent;
        }

        if (!uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var value))
        {
            return System.Drawing.Color.Transparent;
        }

        return text.Length <= 6
            ? System.Drawing.Color.FromArgb(255, (byte)(value >> 16), (byte)(value >> 8), (byte)value)
            : System.Drawing.Color.FromArgb((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }
}

