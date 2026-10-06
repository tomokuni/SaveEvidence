using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.FadeMaskDragRect;

/// <summary>ホスト固有の付加情報（操作ラベル・情報ラベル）の外観を 1 つにまとめた不変の値。</summary>
/// <remarks>
/// <b>操作ラベル・情報ラベルは選択矩形コントロールの概念ではない</b>（撮影オーバーレイ等へ組み込むホストが<br/>
/// 必要とする付加情報）ため、文言の生成と描画はホストが行う。 本値は複合コントロールの設定（<see cref="FadeMaskDragRectOptions"/>）<br/>
/// の一部として、ホストが描く付加情報の外観を保持する（複合コントロールの UI 実装が描画に使う）。<br/>
/// <br/>
/// <b>表示可否はホストが判定する</b>: 本値のスイッチ（<see cref="ShowOperationLabel"/> / <see cref="ShowInfoLabel"/>）に加え、<br/>
/// 文言が空（対象外のモード等）の場合は描かない（どちらも成立したときだけ描画する）。<br/>
/// <br/>
/// 色は UI 非依存の <see cref="DrawingColor"/> であり、UI 境界で自 UI の色型へ変換する。<br/>
/// 文字の実寸測定と描画は UI の描画 API に依存するため、本値は寸法と色のみを持つ。
/// </remarks>
public sealed record FadeMaskDragRectLabelOptions
{
    /// <summary>既定の外観を取得する。</summary>
    /// <value>操作ラベル・情報ラベルを表示し、ホストの既定の配色と寸法を使う値。</value>
    /// <remarks>部分変更の基点として使用する（<c>FadeMaskDragRectLabelOptions.Default with { ... }</c>）。</remarks>
    public static FadeMaskDragRectLabelOptions Default { get; } = new();

    /// <summary>操作ラベル（確定・キャンセルの操作方法）を表示するかどうかを取得する。</summary>
    /// <value>表示する場合は true。 既定は true。</value>
    /// <remarks>文言が空の場合（操作方法の定義が無いモード等）は、本値が true でも描画しない。</remarks>
    public bool ShowOperationLabel { get; init; } = true;

    /// <summary>操作ラベルのフォントサイズ（DIP）を取得する。</summary>
    /// <value>フォントサイズ（DIP）。 既定は 12。</value>
    public double OperationFontSize { get; init; } = 12.0;

    /// <summary>操作ラベルの文字色を取得する。</summary>
    /// <value>アルファを含む色。 既定は明るい灰色（<c>#FFDCDCDC</c>）。</value>
    public DrawingColor OperationColor { get; init; } = DrawingColor.FromArgb(255, 220, 220, 220);

    /// <summary>操作ラベルの背景色を取得する。</summary>
    /// <value>アルファを含む色。 既定は黒 60%（<c>#99000000</c>）。</value>
    public DrawingColor OperationBackgroundColor { get; init; } = DrawingColor.FromArgb(153, 0, 0, 0);

    /// <summary>情報ラベル（対象ウィンドウのタイトル等）を表示するかどうかを取得する。</summary>
    /// <value>表示する場合は true。 既定は true。</value>
    /// <remarks>文言が空の場合（タイトルを持たないモード等）は、本値が true でも描画しない。</remarks>
    public bool ShowInfoLabel { get; init; } = true;

    /// <summary>情報ラベルのフォントサイズ（DIP）を取得する。</summary>
    /// <value>フォントサイズ（DIP）。 既定は 14。</value>
    public double InfoFontSize { get; init; } = 14.0;

    /// <summary>情報ラベルの文字色を取得する。</summary>
    /// <value>アルファを含む色。 既定は白。</value>
    public DrawingColor InfoColor { get; init; } = DrawingColor.White;

    /// <summary>情報ラベルの背景色を取得する。</summary>
    /// <value>アルファを含む色。 既定は黒 60%（<c>#99000000</c>）。</value>
    public DrawingColor InfoBackgroundColor { get; init; } = DrawingColor.FromArgb(153, 0, 0, 0);
}
