using DrawingColor = System.Drawing.Color;

namespace Estable.Marquee.Decoration;

/// <summary>暗転マスク（オーバーレイ）の外観をまとめた不変の設定値。</summary>
/// <remarks>
/// 暗転・枠線・サイズラベル・サブライン・情報ラベルの外観を 1 つの値として所有する。<br/>
/// 各 UI 実装は本型を 1 つ公開するだけで外観を設定でき、UI ごとに既定値がずれない。<br/>
/// <br/>
/// 状態（選択矩形・ハイライト矩形・全面暗転の有無・DPI 倍率）は本型に含めない。<br/>
/// 外観の値のみを持ち、<see cref="BuildSubLines"/> がラベルの行を組み立てる（文言はホストが与え、行の組み立ては <see cref="OuterDecoLabels"/> が単一所有する）。<br/>
/// 色は UI 非依存の <see cref="DrawingColor"/> を使用し、各 UI の色型への変換は境界で行う。
/// </remarks>
public sealed record OuterDecoAppearance
{
    // ====================================================================
    // 暗転・枠線
    // ====================================================================

    /// <summary>暗転オーバーレイの色を取得する。</summary>
    /// <value>アルファを含む色。既定は黒 50%（#80000000）。アルファ 0 の場合は暗転しない。</value>
    public DrawingColor OverlayColor { get; init; } = DrawingColor.FromArgb(128, 0, 0, 0);

    /// <summary>選択枠線の色を取得する。</summary>
    /// <value>アルファを含む色。既定はダークグレー（#FFA9A9A9）。</value>
    public DrawingColor BorderColor { get; init; } = DrawingColor.FromArgb(255, 169, 169, 169);

    /// <summary>選択枠線の太さ（DIP）を取得する。</summary>
    /// <value>枠線の太さ（DIP）。既定は 2.0。</value>
    public double BorderThickness { get; init; } = 2.0;

    // ====================================================================
    // サイズラベル（1 行目）
    // ====================================================================

    /// <summary>サイズラベル（1 行目）を表示するかどうかを取得する。</summary>
    /// <value>true の場合は穴あけ矩形の左上にサイズラベルを描画する。既定は true。</value>
    public bool ShowSizeLabel { get; init; } = true;

    /// <summary>サイズラベルの文字色を取得する。</summary>
    /// <value>アルファを含む色。既定は白。</value>
    public DrawingColor SizeLabelColor { get; init; } = DrawingColor.White;

    /// <summary>サイズラベルの背景色を取得する。</summary>
    /// <value>アルファを含む色。既定は黒 60%（#99000000）。</value>
    public DrawingColor SizeLabelBackgroundColor { get; init; } = DrawingColor.FromArgb(153, 0, 0, 0);

    /// <summary>サイズラベルのフォントサイズ（DIP）を取得する。</summary>
    /// <value>フォントサイズ（DIP）。既定は 14.0。</value>
    public double SizeLabelFontSize { get; init; } = 14.0;

    // ====================================================================
    // サブライン（2 行目 = 操作ラベル）
    // ====================================================================

    /// <summary>操作ラベル（2 行目）を表示するかどうかを取得する。</summary>
    /// <value>true の場合は操作ラベル（ホストが与えた文言）を表示する。既定は true。</value>
    public bool ShowSubLines { get; init; } = true;

    /// <summary>操作ラベルの文字色を取得する。</summary>
    /// <value>アルファを含む色。既定は薄いグレー（#FFDCDCDC）。</value>
    public DrawingColor SubLineColor { get; init; } = DrawingColor.FromArgb(255, 220, 220, 220);

    /// <summary>操作ラベルの背景色を取得する。</summary>
    /// <value>アルファを含む色。既定は黒 60%（#99000000）。</value>
    public DrawingColor SubLineBackgroundColor { get; init; } = DrawingColor.FromArgb(153, 0, 0, 0);

    /// <summary>操作ラベルのフォントサイズ（DIP）を取得する。</summary>
    /// <value>フォントサイズ（DIP）。既定は 14.0。</value>
    public double SubLineFontSize { get; init; } = 14.0;

    // ====================================================================
    // 情報ラベル（3 行目 = ウィンドウタイトル）
    // ====================================================================

    /// <summary>情報ラベル（3 行目）を表示するかどうかを取得する。</summary>
    /// <value>true の場合は情報ラベル（ホストが与えた文言）を表示する。既定は true。</value>
    /// <remarks>表示の最終判定は <see cref="OuterDecoLabels.ShouldShowInfoLabel"/>（表示が有効かつ文言が非空）が行う。</remarks>
    public bool InfoLabelVisible { get; init; } = true;

    /// <summary>情報ラベルの文字色を取得する。</summary>
    /// <value>アルファを含む色。既定は白。</value>
    public DrawingColor InfoLabelColor { get; init; } = DrawingColor.White;

    /// <summary>情報ラベルの背景色を取得する。</summary>
    /// <value>アルファを含む色。既定は黒 60%（#99000000）。</value>
    public DrawingColor InfoLabelBackgroundColor { get; init; } = DrawingColor.FromArgb(153, 0, 0, 0);

    /// <summary>情報ラベルのフォントサイズ（DIP）を取得する。</summary>
    /// <value>フォントサイズ（DIP）。既定は 14.0。</value>
    public double InfoLabelFontSize { get; init; } = 14.0;

    /// <summary>操作ラベル（2 行目）の外観を取得する。</summary>
    /// <value>操作ラベルのフォントサイズ・文字色・背景色。</value>
    private OuterDecoLabelStyle OperationStyle => new(SubLineFontSize, SubLineColor, SubLineBackgroundColor);

    /// <summary>情報ラベル（3 行目）の外観を取得する。</summary>
    /// <value>情報ラベルのフォントサイズ・文字色・背景色。</value>
    private OuterDecoLabelStyle InfoStyle => new(InfoLabelFontSize, InfoLabelColor, InfoLabelBackgroundColor);

    /// <summary>ラベル背景の既定色（黒 50%）を取得する。</summary>
    /// <value>アルファ 0 の背景色を指定した場合に使用する色。</value>
    /// <remarks>透明（アルファ 0）の背景は「未指定」とみなし、文字が読めるよう黒 50% を敷く。</remarks>
    internal static DrawingColor DefaultLabelBackground { get; } = DrawingColor.FromArgb(128, 0, 0, 0);

    /// <summary>ラベル背景の実効色を取得する。</summary>
    /// <param name="color">指定された背景色。</param>
    /// <returns>実効の背景色。アルファ 0 の場合は <see cref="DefaultLabelBackground"/>（黒 50%）。</returns>
    /// <remarks>背景色の解決規則（アルファ 0 を黒 50% として扱う）を本メソッドが単一所有する（両 UI で同一）。</remarks>
    internal static DrawingColor ResolveLabelBackground(DrawingColor color)
        => color.A == 0 ? DefaultLabelBackground : color;

    /// <summary>サイズラベルの下に表示するサブライン（2 行目以降）を組み立てる。</summary>
    /// <param name="operationText">操作ラベル（2 行目）の文言。null または空文字の場合は表示しない。</param>
    /// <param name="infoText">情報ラベル（3 行目）の文言。null または空文字の場合は表示しない。</param>
    /// <returns>サブラインの一覧（上から順）。表示する行がない場合は空。</returns>
    /// <remarks>
    /// 1. 2 行目 = 操作ラベル。<see cref="ShowSubLines"/> と文言の非空で表示可否が決まる。<br/>
    /// 2. 3 行目 = 情報ラベル。<see cref="InfoLabelVisible"/> と文言の非空で表示可否が決まる。<br/>
    /// <br/>
    /// <b>文言はホストが与える</b>（本コアは特定の操作文言を持たない）。<br/>
    /// 行の順序・表示可否・外観の適用は <see cref="OuterDecoLabels.BuildSubLines"/> が単一所有する（両 UI で同一になる）。<br/>
    /// 本メソッドは <see cref="OuterDecoFrame"/> のフレーム解決から呼ばれる（UI 層は結果を使うだけ）。
    /// </remarks>
    internal IReadOnlyList<OuterDecoSubLine> BuildSubLines(
        string? operationText,
        string? infoText)
        => OuterDecoLabels.BuildSubLines(operationText, ShowSubLines, InfoLabelVisible, infoText, OperationStyle, InfoStyle);
}
