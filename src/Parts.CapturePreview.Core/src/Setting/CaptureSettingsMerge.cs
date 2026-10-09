using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Logging;
using Microsoft.Extensions.Logging;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Setting;

/// <summary>設定（<see cref="CaptureSettings"/>）の部分更新マージを単一所有する。</summary>
/// <remarks>
/// <see cref="CaptureSettings"/> は全メンバーが nullable であり、null は「現在値の維持」を意味する。<br/>
/// マージ規則（null は現在値・非 null は上書き）は UI 非依存であるため本クラスが単一所有する<br/>
/// （UI ごとに 42 項目の <c>??</c> 連鎖を書くと、項目の追加時に片方だけ漏れる）。<br/>
/// <b>実装</b>: <c>with</c> 式で「現在値」を基点に「入力の非 null 項目」だけを上書きする（項目の列挙を 1 箇所に閉じる）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class CaptureSettingsMerge
{
    /// <summary>入力の非 null 項目を現在値へ上書きした設定を生成する。</summary>
    /// <param name="input">部分更新の入力（null の項目は現在値を維持する）。 null の場合は既定値のみの設定を返す。</param>
    /// <param name="current">現在の設定（全メンバー non-null）。 null の場合は <see cref="CaptureSettings.ToNonNull"/> の既定値を使う。</param>
    /// <returns>マージ結果（全メンバー non-null）。</returns>
    /// <remarks>
    /// 差分検出（同一値の再設定をスキップするか）は本メソッドの責務ではない（呼び出し側がレコードの値等価で判定する）。<br/>
    /// マージ結果は常に non-null であり、そのままコントロールへ配布できる。<br/>
    /// 「入力が無い場合は既定の設定を使う」という規則も単一所有する（UI が <c>??</c> を書かない）。
    /// </remarks>
    public static CaptureSettings Merge(CaptureSettings? input, CaptureSettings? current = null)
    {
        var baseline = current ?? new CaptureSettings().ToNonNull();
        if (input is null)
        {
            return baseline;
        }

        return baseline with
        {
            HideOwnerWindow = input.HideOwnerWindow ?? baseline.HideOwnerWindow,
            MinSelectionSize = input.MinSelectionSize ?? baseline.MinSelectionSize,
            BorderColor = input.BorderColor ?? baseline.BorderColor,
            BorderThickness = input.BorderThickness ?? baseline.BorderThickness,
            OverlayColor = input.OverlayColor ?? baseline.OverlayColor,
            HandleColor = input.HandleColor ?? baseline.HandleColor,
            HandleSize = input.HandleSize ?? baseline.HandleSize,
            HandleHitRadius = input.HandleHitRadius ?? baseline.HandleHitRadius,
            ClampToSourceBounds = input.ClampToSourceBounds ?? baseline.ClampToSourceBounds,
            MarkerColor = input.MarkerColor ?? baseline.MarkerColor,
            MarkerLength = input.MarkerLength ?? baseline.MarkerLength,
            ShowCoordLabel = input.ShowCoordLabel ?? baseline.ShowCoordLabel,
            CoordLabelColor = input.CoordLabelColor ?? baseline.CoordLabelColor,
            CoordLabelBackgroundColor = input.CoordLabelBackgroundColor ?? baseline.CoordLabelBackgroundColor,
            CoordLabelFontSize = input.CoordLabelFontSize ?? baseline.CoordLabelFontSize,
            LoupeSize = input.LoupeSize ?? baseline.LoupeSize,
            LoupeZoomLevel = input.LoupeZoomLevel ?? baseline.LoupeZoomLevel,
            LoupeOffset = input.LoupeOffset ?? baseline.LoupeOffset,
            LoupeClientToPixelScaling = input.LoupeClientToPixelScaling ?? baseline.LoupeClientToPixelScaling,
            LoupeCrossColor = input.LoupeCrossColor ?? baseline.LoupeCrossColor,
            LoupeFrameColor = input.LoupeFrameColor ?? baseline.LoupeFrameColor,
            LoupeFrameThickness = input.LoupeFrameThickness ?? baseline.LoupeFrameThickness,
            LoupePasteboardColor = input.LoupePasteboardColor ?? baseline.LoupePasteboardColor,
            LoupeSelectionRectColorA = input.LoupeSelectionRectColorA ?? baseline.LoupeSelectionRectColorA,
            LoupeSelectionRectColorB = input.LoupeSelectionRectColorB ?? baseline.LoupeSelectionRectColorB,
            LoupeShowCoordLabel = input.LoupeShowCoordLabel ?? baseline.LoupeShowCoordLabel,
            LoupeCoordLabelColor = input.LoupeCoordLabelColor ?? baseline.LoupeCoordLabelColor,
            LoupeCoordLabelBackgroundColor = input.LoupeCoordLabelBackgroundColor ?? baseline.LoupeCoordLabelBackgroundColor,
            LoupeCoordLabelFontSize = input.LoupeCoordLabelFontSize ?? baseline.LoupeCoordLabelFontSize,
            SizeLabelVisible = input.SizeLabelVisible ?? baseline.SizeLabelVisible,
            SizeLabelColor = input.SizeLabelColor ?? baseline.SizeLabelColor,
            SizeLabelFontSize = input.SizeLabelFontSize ?? baseline.SizeLabelFontSize,
            TextBackgroundColor = input.TextBackgroundColor ?? baseline.TextBackgroundColor,
            InfoLabelVisible = input.InfoLabelVisible ?? baseline.InfoLabelVisible,
            InfoLabelColor = input.InfoLabelColor ?? baseline.InfoLabelColor,
            InfoLabelBackgroundColor = input.InfoLabelBackgroundColor ?? baseline.InfoLabelBackgroundColor,
            InfoLabelFontSize = input.InfoLabelFontSize ?? baseline.InfoLabelFontSize,
            ShowSubLines = input.ShowSubLines ?? baseline.ShowSubLines,
            SubLineFontSize = input.SubLineFontSize ?? baseline.SubLineFontSize,
            SubLineColor = input.SubLineColor ?? baseline.SubLineColor,
            SubLineBackgroundColor = input.SubLineBackgroundColor ?? baseline.SubLineBackgroundColor,
            DisplayScaleScaling = input.DisplayScaleScaling ?? baseline.DisplayScaleScaling,
        };
    }

    /// <summary>設定の部分更新を適用し、変更された場合のみ現在値を差し替える。</summary>
    /// <param name="input">部分更新の入力（null の項目は現在値を維持する）。</param>
    /// <param name="current">現在の設定（参照が差し替わる）。</param>
    /// <param name="logger">変更ログの出力先（null の場合は出力しない）。</param>
    /// <returns>設定が変更された場合は true。 同一値の再設定（マージ結果が現在値と等しい）の場合は false。</returns>
    /// <remarks>
    /// 1. 部分更新の入力へ <see cref="CaptureSettings.Validate"/> を適用する（範囲外は例外）。<br/>
    /// 2. 現在値へマージする（null の項目は現在値を維持）。<br/>
    /// 3. マージ結果が現在値と等しい場合は <paramref name="current"/> を差し替えず false を返す（無駄な再配布を防ぐ）。<br/>
    /// 4. 変更された場合は <paramref name="current"/> を差し替えて true を返す。<br/>
    /// <br/>
    /// 差分検出と参照の差し替えを 1 箇所に閉じることで、UI 層は「設定を渡すだけ」になる。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">設定値が許容範囲外の場合。</exception>
    public static bool Apply(CaptureSettings input, ref CaptureSettings current, ILogger? logger = null)
    {
        input.Validate();
        var merged = Merge(input, current);

        // レコードの値等価で比較する（部分更新の入力が全て現在値と一致する場合は更新しない）
        if (merged == current)
        {
            return false;
        }

        current = merged;
        logger?.LogSettingApplied();
        return true;
    }
}

