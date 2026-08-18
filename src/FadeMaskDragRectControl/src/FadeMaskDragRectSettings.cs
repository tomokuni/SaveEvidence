using Avalonia.Media;
using Estable.AvaloniaUI.Platform;

namespace Estable.AvaloniaUI.FadeMaskDragRect;

/// <summary>FadeMaskDragRectControl の外観設定（DragRect / FadeMask / Loupe の外観を一括設定するレコード型）。</summary>
/// <remarks>
/// ホスト（上位 AP）は自プロジェクトの設定型（CaptureSettings 等）から本型へ変換して<br/>
/// <see cref="FadeMaskDragRectControl.AppearanceSettings"/> に設定する（複合コントロールはホストの設定型に依存しない）。<br/>
/// 全メンバーは nullable とする。<br/>
/// - 初期設定として使用する場合: null は既定値（<see cref="ToNonNull"/> のデフォルト値）として扱う。<br/>
/// - 既存インスタンスへの設定適用（AppearanceSettings の setter）の場合: null は現在の設定値を維持する（部分更新）。<br/>
/// <br/>
/// 内包コントロール（DragRectControl / FadeMaskControl / LoupeControl）が公開する外観設定を網羅する。<br/>
/// 実行時状態（選択矩形・ハイライト矩形・ルーペ中心・ルーペソース等）と座標変換ポートは本型の対象外（ホストが管理）。
/// </remarks>
public sealed record FadeMaskDragRectSettings
{
    // ====================================================================
    // DragRect 外観（DragRectControl の公開外観設定）
    // ====================================================================

    /// <summary>選択矩形の最小サイズ（ドメイン座標）。</summary>
    public double? MinSelectionSize { get; init; }

    /// <summary>選択枠線の色（不透明度は色のアルファに統合）。</summary>
    public Color? BorderColor { get; init; }

    /// <summary>選択枠線の太さ（DIP）。</summary>
    public double? BorderThickness { get; init; }

    /// <summary>ドラッグハンドルの色。</summary>
    public Color? HandleColor { get; init; }

    /// <summary>ドラッグハンドルの一辺サイズ（DIP）。</summary>
    public double? HandleSize { get; init; }

    /// <summary>ドラッグハンドルのヒットテスト半径（DIP）。</summary>
    public double? HandleHitRadius { get; init; }

    /// <summary>画像外の選択を禁止するか（ドメイン境界内へクリップ）。</summary>
    public bool? ClampToDomainBounds { get; init; }

    /// <summary>nudge 中の十字マーカーの色。</summary>
    public Color? MarkerColor { get; init; }

    /// <summary>nudge 中の十字マーカーの長さ（全長・DIP）。</summary>
    public double? MarkerLength { get; init; }

    /// <summary>カーソルの右側にカーソルのドメイン座標ラベル（"xy (x, y)"）を表示するか。</summary>
    public bool? ShowCoordLabel { get; init; }

    /// <summary>カーソル座標ラベルの文字色。</summary>
    public Color? CoordLabelColor { get; init; }

    /// <summary>カーソル座標ラベルの背景色（不透明度は色のアルファに統合）。</summary>
    public Color? CoordLabelBackgroundColor { get; init; }

    /// <summary>カーソル座標ラベルのフォントサイズ（DIP）。</summary>
    public double? CoordLabelFontSize { get; init; }

    // ====================================================================
    // FadeMask 外観（FadeMaskRenderer の描画設定）
    // ====================================================================

    /// <summary>DPI スケーリング係数（ドメイン → DIP 変換の基準倍率）。</summary>
    public double? Scaling { get; init; }

    /// <summary>サイズラベル表示用の DPI スケーリング係数（マルチスクリーン補正。0 以下は Scaling と同値）。</summary>
    public double? DisplayScaleScaling { get; init; }

    /// <summary>暗転オーバーレイの色（不透明度は色のアルファに統合。既定は黒 50%）。</summary>
    public Color? OverlayColor { get; init; }

    /// <summary>サイズラベルテキストの色。</summary>
    public Color? TextColor { get; init; }

    /// <summary>サイズラベルのフォントサイズ（DIP）。</summary>
    public double? FontSize { get; init; }

    /// <summary>サイズラベルを表示するか。</summary>
    public bool? ShowSizeLabel { get; init; }

    /// <summary>情報ラベルの表示状態（ScreenSelect / WindowSelect のハイライト時の説明ラベル）。</summary>
    public bool? InfoLabelVisible { get; init; }

    /// <summary>情報ラベルの文字色。</summary>
    public Color? InfoLabelColor { get; init; }

    /// <summary>情報ラベルのフォントサイズ（DIP）。</summary>
    public double? InfoLabelFontSize { get; init; }

    /// <summary>サブライン（サイズラベルの下の 2 行目以降）の表示有無。</summary>
    public bool? ShowSubLines { get; init; }

    /// <summary>サブラインのフォントサイズ（DIP）。</summary>
    public double? SubLineFontSize { get; init; }

    /// <summary>サブラインの文字色。</summary>
    public Color? SubLineColor { get; init; }

    /// <summary>サブライン（サイズラベルの下の 2 行目以降）の背景色（不透明度は色のアルファに統合）。</summary>
    public Color? SubLineBackgroundColor { get; init; }

    /// <summary>情報ラベルの背景色（不透明度は色のアルファに統合）。</summary>
    public Color? InfoLabelBackgroundColor { get; init; }

    /// <summary>サイズラベル（1 行目）の背景色（不透明度は色のアルファに統合）。</summary>
    public Color? TextBackgroundColor { get; init; }

    // ====================================================================
    // Loupe 外観（LoupeControl の公開外観設定）
    // ====================================================================

    /// <summary>ルーペの一辺のサイズ（DIP）。</summary>
    public int? RequestedSize { get; init; }

    /// <summary>ルーペ拡大倍率。</summary>
    public int? ZoomLevel { get; init; }

    /// <summary>ルーペの DIP → 物理ピクセル倍率（0.1〜8.0）。</summary>
    public double? RenderScaling { get; init; }

    /// <summary>ルーペの十字線の色（不透明度は色のアルファに統合。既定は赤 40%）。</summary>
    public Color? CrossColor { get; init; }

    /// <summary>ルーペの市松模様の一辺のサイズ（ピクセル）。</summary>
    public int? CheckerSize { get; init; }

    /// <summary>ルーペの市松模様の色 A。</summary>
    public Color? CheckerColorA { get; init; }

    /// <summary>ルーペの市松模様の色 B。</summary>
    public Color? CheckerColorB { get; init; }

    /// <summary>ルーペの枠の色。</summary>
    public Color? FrameColor { get; init; }

    /// <summary>ルーペの枠の太さ（ピクセル）。</summary>
    public int? FrameWidth { get; init; }

    /// <summary>ルーペ画像上の選択矩形破線の色 A（明色・不透明度はアルファに統合）。</summary>
    public Color? SelectionRectColorA { get; init; }

    /// <summary>ルーペ画像上の選択矩形破線の色 B（暗色・不透明度はアルファに統合）。</summary>
    public Color? SelectionRectColorB { get; init; }

    /// <summary>ルーペのカーソル中心からの右下オフセット（DIP）。</summary>
    public double? PositionOffset { get; init; }

    /// <summary>ルーペの上側に中心座標ラベル（"xy (x, y)"）を表示するか。</summary>
    public bool? LoupeShowCoordLabel { get; init; }

    /// <summary>ルーペ座標ラベルの文字色。</summary>
    public Color? LoupeCoordLabelColor { get; init; }

    /// <summary>ルーペ座標ラベルの背景色（不透明度は色のアルファに統合）。</summary>
    public Color? LoupeCoordLabelBackgroundColor { get; init; }

    /// <summary>ルーペ座標ラベルのフォントサイズ（DIP）。</summary>
    public double? LoupeCoordLabelFontSize { get; init; }

    // ====================================================================
    // 既定値への解決
    // ====================================================================

    /// <summary>null のメンバーを既定値で埋めた non-null 版の設定を生成する。</summary>
    /// <returns>全メンバーが non-null の設定（値は null 許容型のまま）。</returns>
    /// <remarks>null は「部分更新のための入力」としてのみ使用され、適用時は本メソッドの既定値に置き換える。</remarks>
    public FadeMaskDragRectSettings ToNonNull()
        => new()
        {
            // DragRect 外観
            MinSelectionSize = MinSelectionSize ?? 6.0,
            BorderColor = BorderColor ?? Color.FromAColor(0xFF, Colors.DarkGray),
            BorderThickness = BorderThickness ?? 2.0,
            HandleColor = HandleColor ?? Colors.White,
            HandleSize = HandleSize ?? 10.0,
            HandleHitRadius = HandleHitRadius ?? 12.0,
            ClampToDomainBounds = ClampToDomainBounds ?? true,
            MarkerColor = MarkerColor ?? Colors.White,
            MarkerLength = MarkerLength ?? 16.0,
            // カーソル座標ラベル（DragRect）
            ShowCoordLabel = ShowCoordLabel ?? false,
            CoordLabelColor = CoordLabelColor ?? Colors.White,
            CoordLabelBackgroundColor = CoordLabelBackgroundColor ?? Color.FromAColor(153, Colors.Black),
            CoordLabelFontSize = CoordLabelFontSize ?? 12.0,
            // FadeMask 外観
            Scaling = Scaling ?? 1.0,
            DisplayScaleScaling = DisplayScaleScaling ?? 1.0,
            // 暗転オーバーレイの既定は黒 50%（アルファ込み）
            OverlayColor = OverlayColor ?? Color.FromAColor(128, Colors.Black),
            TextColor = TextColor ?? Colors.White,
            TextBackgroundColor = TextBackgroundColor ?? Color.FromAColor(153, Colors.Black),
            FontSize = FontSize ?? 14.0,
            ShowSizeLabel = ShowSizeLabel ?? true,
            InfoLabelVisible = InfoLabelVisible ?? true,
            InfoLabelColor = InfoLabelColor ?? Colors.White,
            InfoLabelBackgroundColor = InfoLabelBackgroundColor ?? Color.FromAColor(153, Colors.Black),
            InfoLabelFontSize = InfoLabelFontSize ?? 14.0,
            // サブライン（サイズラベルの下の 2 行目以降）
            ShowSubLines = ShowSubLines ?? true,
            SubLineFontSize = SubLineFontSize ?? 12.0,
            SubLineColor = SubLineColor ?? Colors.Gainsboro,
            SubLineBackgroundColor = SubLineBackgroundColor ?? Color.FromAColor(153, Colors.Black),
            // Loupe 外観（既定は Settings / LoupeControl の既定と一致させる）
            RequestedSize = RequestedSize ?? 160,
            ZoomLevel = ZoomLevel ?? 4,
            RenderScaling = RenderScaling ?? 1.0,
            CrossColor = CrossColor ?? Color.FromAColor(102, Colors.Red),
            CheckerSize = CheckerSize ?? 6,
            CheckerColorA = CheckerColorA ?? Color.FromAColor(0x4C, Colors.Gray),
            CheckerColorB = CheckerColorB ?? Color.FromAColor(0x4C, Colors.DarkGray),
            FrameColor = FrameColor ?? Colors.LightGray,
            FrameWidth = FrameWidth ?? 2,
            SelectionRectColorA = SelectionRectColorA ?? Colors.White,
            SelectionRectColorB = SelectionRectColorB ?? Colors.Black,
            PositionOffset = PositionOffset ?? 20.0,
            // ルーペ座標ラベル
            LoupeShowCoordLabel = LoupeShowCoordLabel ?? true,
            LoupeCoordLabelColor = LoupeCoordLabelColor ?? Colors.White,
            LoupeCoordLabelBackgroundColor = LoupeCoordLabelBackgroundColor ?? Color.FromAColor(153, Colors.Black),
            LoupeCoordLabelFontSize = LoupeCoordLabelFontSize ?? 12.0,
        };
}
