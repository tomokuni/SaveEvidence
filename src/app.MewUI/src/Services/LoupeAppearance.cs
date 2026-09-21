using Estable.MewUI.FadeMaskDragRect;
using Estable.MewUI.Loupe;
using Estable.SaveEvidence.Models;
using SaveEvidence.MewUI.Views;
using MewUIColor = Aprillz.MewUI.Color;
using DrawingColor = System.Drawing.Color;

namespace SaveEvidence.MewUI.Services;

/// <summary>設定の「ルーペ」項目を <see cref="LoupeControl"/> へ反映する。</summary>
/// <remarks>
/// ルーペは「プレビューの常時表示」「撮影オーバーレイ」「プレビューの切り出し」の 3 箇所に登場し、<br/>
/// いずれも同じ設定を同じ対応で反映する必要がある。対応表を 1 箇所に集約し、UI ごとのずれを防ぐ。<br/>
/// <b>既定色</b>: 設定の色は <c>#AARRGGBB</c> の文字列で、解析できない場合は既定色へフォールバックする<br/>
/// （コントロール側の既定値と同じ色を渡す）。<br/>
/// <b>サイズ・位置</b>: 表示位置（オフセット）はホスト側の配置計算が扱うため、本クラスでは扱わない。
/// </remarks>
internal static class LoupeAppearance
{
    /// <summary>設定のルーペ項目をルーペコントロールへ反映する。</summary>
    /// <param name="loupe">対象のルーペコントロール。</param>
    /// <param name="settings">設定。</param>
    /// <remarks>
    /// 1. 拡大率・サイズ（一辺）を反映する。<br/>
    /// 2. 十字線・外枠の色と太さを反映する（不正な色は既定色）。<br/>
    /// 3. 市松模様（サイズ・色 A / 色 B）を反映する（プレビューと共通の設定を使う）。<br/>
    /// 4. 市松模様の下地（テーマのページ背景色）を反映する（色 A / B は半透明のため、下地がないと後ろが透ける）。<br/>
    /// 5. 中心座標ラベル（表示・文字色・背景色・文字サイズ）を反映する。
    /// </remarks>
    public static void Apply(LoupeControl loupe, Settings settings)
    {
        loupe.ZoomLevel = settings.LoupeZoomLevel;
        loupe.RequestedSize = settings.LoupeSize;

        loupe.CrossColor = SettingsColorMath.ToMewUIColor(settings.LoupeCrossColor, MewUIColor.FromArgb(102, 255, 0, 0));
        loupe.FrameColor = SettingsColorMath.ToMewUIColor(settings.LoupeFrameColor, MewUIColor.FromArgb(255, 211, 211, 211));
        loupe.FrameWidth = settings.LoupeFrameWidth;

        loupe.CheckerSize = settings.PreviewCheckerSize;
        loupe.CheckerColorA = SettingsColorMath.ToMewUIColor(settings.PreviewCheckerColorA, MewUIColor.Transparent);
        loupe.CheckerColorB = SettingsColorMath.ToMewUIColor(settings.PreviewCheckerColorB, MewUIColor.Transparent);
        loupe.BackgroundColor = AppStyles.CurrentPageBackground;

        loupe.ShowCoordLabel = settings.LoupeShowCoordLabel;
        loupe.CoordLabelColor = SettingsColorMath.ToMewUIColor(settings.LoupeCoordLabelColor, MewUIColor.White);
        loupe.CoordLabelBackgroundColor = SettingsColorMath.ToMewUIColor(
            settings.LoupeCoordLabelBackgroundColor, MewUIColor.FromArgb(153, 0, 0, 0));
        loupe.CoordLabelFontSize = settings.LoupeCoordLabelFontSize;
    }

    /// <summary>ルーペ画像上の選択矩形破線の色を選択コントロールへ反映する。</summary>
    /// <param name="selector">対象の選択コントロール（ルーペを含む）。</param>
    /// <param name="settings">設定。</param>
    /// <remarks>
    /// 破線はルーペコントロールではなく選択コントロール（ホスト側）が描画するため、<br/>
    /// <see cref="Apply"/> とは別メソッドとし、同じ設定グループ（ルーペ）の反映を本クラスへ集約する（呼び出し側の重複を防ぐ）。
    /// </remarks>
    public static void ApplySelectionOverlay(FadeMaskDragRectControl selector, Settings settings)
    {
        selector.SelectionOverlayColorA = SettingsColorMath.ToDrawingColor(settings.LoupeSelectionRectColorA, DrawingColor.White);
        selector.SelectionOverlayColorB = SettingsColorMath.ToDrawingColor(settings.LoupeSelectionRectColorB, DrawingColor.Black);
    }
}
