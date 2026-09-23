using System.Drawing;

namespace Estable.CaptureSession;

/// <summary>キャプチャの外観および拡大鏡の設定値を保持するレコード型。</summary>
/// <param name="HideOwnerWindow">キャプチャ時にオーナーウィンドウを非表示にするか。</param>
/// <param name="LoupeVisible">ルーペの表示状態。</param>
/// <param name="HideBeforeCaptureDelay">ウィンドウ非表示後の DWM 反映待機時間。</param>
/// <param name="MinSelectionSize">選択矩形の最小サイズ。単位はモードにより異なる: AreaSelect では物理ピクセル、Crop では論理座標（元画像ピクセル）。</param>
/// <param name="BorderColor">選択枠線の色（不透明度は色のアルファに統合）。</param>
/// <param name="BorderThickness">選択枠線の太さ（DIP）。</param>
/// <param name="OverlayColor">暗転オーバーレイの色（不透明度は色のアルファに統合）。</param>
/// <param name="HandleColor">ドラッグハンドルの色（DragRectControl.HandleColor）。</param>
/// <param name="HandleSize">ドラッグハンドルの一辺サイズ（DIP・DragRectControl.HandleSize）。</param>
/// <param name="HandleHitRadius">ハンドルヒットテスト半径（DIP・DragRectControl.HandleHitRadius）。</param>
/// <param name="ClampToDomainBounds">画像外の選択を禁止するか（DragRectControl.ClampToDomainBounds）。</param>
/// <param name="MarkerColor">nudge 中の十字マーカーの色（DragRectControl.MarkerColor）。</param>
/// <param name="MarkerLength">nudge 中の十字マーカーの長さ（全長・DIP・DragRectControl.MarkerLength）。</param>
/// <param name="LoupeSize">ルーペの一辺のサイズ（DIP）。</param>
/// <param name="LoupeZoomLevel">ルーペ拡大倍率。</param>
/// <param name="LoupeOffset">ルーペのカーソルからのオフセット（DIP）。</param>
/// <param name="LoupeRenderScaling">ルーペの DIP→物理ピクセル倍率（LoupeControl.RenderScaling・0.1〜8.0）。</param>
/// <param name="LoupeCrossColor">ルーペの十字線の色（不透明度は色のアルファに統合）。</param>
/// <param name="LoupeFrameColor">ルーペの枠の色。</param>
/// <param name="LoupeFrameThickness">ルーペの枠の太さ（ピクセル）。</param>
/// <param name="LoupeCheckerSize">ルーペの市松模様の一辺のサイズ（ピクセル）。</param>
/// <param name="LoupeCheckerColorA">ルーペの市松模様の色 A。</param>
/// <param name="LoupeCheckerColorB">ルーペの市松模様の色 B。</param>
/// <param name="LoupeSelectionRectColorA">ルーペ画像上の選択矩形破線の色 A（FadeMaskDragRectOptions.SelectionRectColorA・不透明度はアルファに統合）。</param>
/// <param name="LoupeSelectionRectColorB">ルーペ画像上の選択矩形破線の色 B（FadeMaskDragRectOptions.SelectionRectColorB・不透明度はアルファに統合）。</param>
/// <param name="LoupeShowCoordLabel">ルーペの上側に中心座標ラベル（"xy (x, y)"）を表示するか。</param>
/// <param name="LoupeCoordLabelColor">ルーペ座標ラベルの文字色。</param>
/// <param name="LoupeCoordLabelBackgroundColor">ルーペ座標ラベルの背景色（不透明度は色のアルファに統合）。</param>
/// <param name="LoupeCoordLabelFontSize">ルーペ座標ラベルのフォントサイズ。</param>
/// <param name="SizeLabelVisible">サイズラベルの表示状態。</param>
/// <param name="SizeLabelColor">サイズラベルの文字色。</param>
/// <param name="SizeLabelFontSize">サイズラベルのフォントサイズ。</param>
/// <param name="SizeLabelOverride">サイズラベルのテキスト明示指定（DragRectControl.SizeLabelOverrideProvider。null = 自動生成）。</param>
/// <param name="TextBackgroundColor">サイズラベル（1 行目）の背景色（不透明度は色のアルファに統合）。</param>
/// <param name="InfoLabelVisible">情報ラベルの表示状態。</param>
/// <param name="InfoLabelColor">情報ラベルの文字色。</param>
/// <param name="InfoLabelBackgroundColor">情報ラベル（3 行目）の背景色（不透明度は色のアルファに統合）。</param>
/// <param name="InfoLabelFontSize">情報ラベルのフォントサイズ。</param>
/// <param name="ShowSubLines">サブライン（サイズラベルの下の 2 行目以降）の表示状態。</param>
/// <param name="SubLineFontSize">サブラインのフォントサイズ。</param>
/// <param name="SubLineColor">サブラインの文字色。</param>
/// <param name="SubLineBackgroundColor">サブライン（2 行目）の背景色（不透明度は色のアルファに統合）。</param>
/// <param name="DisplayScaleScaling">サイズラベル表示用のスケーリング（DragRectControl.DisplayScaleScaling・0 以下は Scaling と同値）。</param>
/// <remarks>
/// 全メンバーは nullable とする。<br/>
/// - 初期設定として使用する場合: null は CaptureSessionControl.AvaloniaUI のデフォルト値として扱う。<br/>
/// - 既存インスタンスへの設定適用（Settings プロパティの setter）の場合: null は現在の設定値を維持する（部分更新）。<br/>
/// 内包コンポーネント（DragRectControl / LoupeControl）が公開する外観設定を網羅する。<br/>
/// 実行時状態（選択矩形・ハイライト矩形等）と Source / CenterPoint 等の入力は本型の対象外（セッションが管理）。
/// </remarks>
public sealed record CaptureSettings(
    bool? HideOwnerWindow = null,
    bool? LoupeVisible = null,
    TimeSpan? HideBeforeCaptureDelay = null,
    int? MinSelectionSize = null,
    Color? BorderColor = null,
    double? BorderThickness = null,
    Color? OverlayColor = null,
    Color? HandleColor = null,
    double? HandleSize = null,
    double? HandleHitRadius = null,
    bool? ClampToDomainBounds = null,
    Color? MarkerColor = null,
    double? MarkerLength = null,
    bool? ShowCoordLabel = null,
    Color? CoordLabelColor = null,
    Color? CoordLabelBackgroundColor = null,
    double? CoordLabelFontSize = null,
    int? LoupeSize = null,
    int? LoupeZoomLevel = null,
    double? LoupeOffset = null,
    double? LoupeRenderScaling = null,
    Color? LoupeCrossColor = null,
    Color? LoupeFrameColor = null,
    int? LoupeFrameThickness = null,
    int? LoupeCheckerSize = null,
    Color? LoupeCheckerColorA = null,
    Color? LoupeCheckerColorB = null,
    Color? LoupeSelectionRectColorA = null,
    Color? LoupeSelectionRectColorB = null,
    bool? LoupeShowCoordLabel = null,
    Color? LoupeCoordLabelColor = null,
    Color? LoupeCoordLabelBackgroundColor = null,
    double? LoupeCoordLabelFontSize = null,
    bool? SizeLabelVisible = null,
    Color? SizeLabelColor = null,
    double? SizeLabelFontSize = null,
    string? SizeLabelOverride = null,
    Color? TextBackgroundColor = null,
    bool? InfoLabelVisible = null,
    Color? InfoLabelColor = null,
    Color? InfoLabelBackgroundColor = null,
    double? InfoLabelFontSize = null,
    bool? ShowSubLines = null,
    double? SubLineFontSize = null,
    Color? SubLineColor = null,
    Color? SubLineBackgroundColor = null,
    double? DisplayScaleScaling = null)
{
    /// <summary>設定値を検証する。</summary>
    /// <remarks>null のメンバーは「設定値維持」を表すため検証対象外。</remarks>
    /// <exception cref="ArgumentOutOfRangeException">数値メンバーが許容範囲外の場合。</exception>
    public void Validate()
    {
        if (MinSelectionSize is < 1) throw new ArgumentOutOfRangeException(nameof(MinSelectionSize), "MinSelectionSize は 1 以上でなければなりません。");
        if (HandleSize is < 1) throw new ArgumentOutOfRangeException(nameof(HandleSize), "HandleSize は 1 以上でなければなりません。");
        if (HandleHitRadius is < 1) throw new ArgumentOutOfRangeException(nameof(HandleHitRadius), "HandleHitRadius は 1 以上でなければなりません。");
        if (MarkerLength is < 1) throw new ArgumentOutOfRangeException(nameof(MarkerLength), "MarkerLength は 1 以上でなければなりません。");
        if (LoupeSize is < 1) throw new ArgumentOutOfRangeException(nameof(LoupeSize), "LoupeSize は 1 以上でなければなりません。");
        if (LoupeZoomLevel is < 1) throw new ArgumentOutOfRangeException(nameof(LoupeZoomLevel), "LoupeZoomLevel は 1 以上でなければなりません。");
        if (LoupeFrameThickness is < 1) throw new ArgumentOutOfRangeException(nameof(LoupeFrameThickness), "LoupeFrameThickness は 1 以上でなければなりません。");
        if (LoupeCheckerSize is < 1) throw new ArgumentOutOfRangeException(nameof(LoupeCheckerSize), "LoupeCheckerSize は 1 以上でなければなりません。");
        if (BorderThickness is < 0) throw new ArgumentOutOfRangeException(nameof(BorderThickness), "BorderThickness は 0 以上でなければなりません。");
        if (LoupeOffset is < 0) throw new ArgumentOutOfRangeException(nameof(LoupeOffset), "LoupeOffset は 0 以上でなければなりません。");
        if (LoupeRenderScaling is < 0.1 or > 8.0) throw new ArgumentOutOfRangeException(nameof(LoupeRenderScaling), "LoupeRenderScaling は 0.1〜8.0 でなければなりません。");
        if (HideBeforeCaptureDelay is { } d && d < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(HideBeforeCaptureDelay), "HideBeforeCaptureDelay は負の時間を指定できません。");
    }

    /// <summary>null のメンバーを CaptureSessionControl.AvaloniaUI デフォルト値で埋めた non-null 版の設定を生成する。</summary>
    /// <returns>全メンバーが non-null の設定。</returns>
    /// <remarks>null は「部分更新のための入力」としてのみ使用され、適用時は本メソッドでデフォルト値に置き換える。</remarks>
    public CaptureSettings ToNonNull()
        => new(
            HideOwnerWindow: HideOwnerWindow ?? true,
            LoupeVisible: LoupeVisible ?? true,
            HideBeforeCaptureDelay: HideBeforeCaptureDelay ?? TimeSpan.FromMilliseconds(50),
            MinSelectionSize: MinSelectionSize ?? 6,
            BorderColor: BorderColor ?? Color.FromArgb(0xFF, 169, 169, 169),
            BorderThickness: BorderThickness ?? 2.0,
            OverlayColor: OverlayColor ?? Color.FromArgb(128, 0, 0, 0),
            HandleColor: HandleColor ?? Color.White,
            HandleSize: HandleSize ?? 10.0,
            HandleHitRadius: HandleHitRadius ?? 12.0,
            ClampToDomainBounds: ClampToDomainBounds ?? true,
            MarkerColor: MarkerColor ?? Color.White,
            MarkerLength: MarkerLength ?? 16.0,
            ShowCoordLabel: ShowCoordLabel ?? false,
            CoordLabelColor: CoordLabelColor ?? Color.White,
            CoordLabelBackgroundColor: CoordLabelBackgroundColor ?? Color.FromArgb(153, 0, 0, 0),
            CoordLabelFontSize: CoordLabelFontSize ?? 12.0,
            LoupeSize: LoupeSize ?? 160,
            LoupeZoomLevel: LoupeZoomLevel ?? 4,
            LoupeOffset: LoupeOffset ?? 20.0,
            LoupeRenderScaling: LoupeRenderScaling ?? 1.0,
            LoupeCrossColor: LoupeCrossColor ?? Color.FromArgb(102, 255, 0, 0),
            LoupeFrameColor: LoupeFrameColor ?? Color.LightGray,
            LoupeFrameThickness: LoupeFrameThickness ?? 2,
            LoupeCheckerSize: LoupeCheckerSize ?? 6,
            LoupeCheckerColorA: LoupeCheckerColorA ?? Color.FromArgb(0x4C, 128, 128, 128),
            LoupeCheckerColorB: LoupeCheckerColorB ?? Color.FromArgb(0x4C, 169, 169, 169),
            LoupeSelectionRectColorA: LoupeSelectionRectColorA ?? Color.White,
            LoupeSelectionRectColorB: LoupeSelectionRectColorB ?? Color.Black,
            LoupeShowCoordLabel: LoupeShowCoordLabel ?? true,
            LoupeCoordLabelColor: LoupeCoordLabelColor ?? Color.White,
            LoupeCoordLabelBackgroundColor: LoupeCoordLabelBackgroundColor ?? Color.FromArgb(153, 0, 0, 0),
            LoupeCoordLabelFontSize: LoupeCoordLabelFontSize ?? 12.0,
            SizeLabelVisible: SizeLabelVisible ?? true,
            SizeLabelColor: SizeLabelColor ?? Color.White,
            SizeLabelFontSize: SizeLabelFontSize ?? 14.0,
            SizeLabelOverride: SizeLabelOverride,
            TextBackgroundColor: TextBackgroundColor ?? Color.FromArgb(153, 0, 0, 0),
            InfoLabelVisible: InfoLabelVisible ?? true,
            InfoLabelColor: InfoLabelColor ?? Color.White,
            InfoLabelBackgroundColor: InfoLabelBackgroundColor ?? Color.FromArgb(153, 0, 0, 0),
            InfoLabelFontSize: InfoLabelFontSize ?? 14.0,
            ShowSubLines: ShowSubLines ?? true,
            SubLineFontSize: SubLineFontSize ?? 12.0,
            SubLineColor: SubLineColor ?? Color.FromArgb(255, 220, 220, 220),
            SubLineBackgroundColor: SubLineBackgroundColor ?? Color.FromArgb(153, 0, 0, 0),
            DisplayScaleScaling: DisplayScaleScaling ?? 0.0);
}

