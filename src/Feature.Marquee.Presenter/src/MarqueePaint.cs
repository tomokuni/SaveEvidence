using DrawingColor = System.Drawing.Color;


namespace EsUtil.Feature.Marquee.Presenter;

/// <summary>選択矩形コントロールの色（ハンドル・始点マーカー・ラベル・装飾）とラベルの体裁をまとめた不変の設定値。</summary>
/// <remarks>
/// <b>なぜ本層（UIBase）が持つか</b>: 色は<b>描画にだけ必要な設定</b>であり、コアの計算には入らない。したがって<br/>
/// コア（<c>Feature.Marquee.Core</c>）には置かず、描画の設定として本層が単一所有する<br/>
/// （コアは表示可否・寸法・対話パラメータを持つ <c>MarqueeOptions</c> と <c>MarqueeDecoOptions</c> を持つ）。<br/>
/// <b>ラベルのフォントサイズも同じ理由で本型が持つ</b>: ラベルをどの体裁（文字要素 / 画像）で描くかは UI 層の判断であり、<br/>
/// コアは文言と位置だけを渡す（<c>MarqueeTextPlacement</c>）。<br/>
/// <br/>
/// <b>描画の可否</b>: 「描くかどうか」は本型ではなく <c>MarqueeDecoOptions</c> の明示フラグが単一所有する<br/>
/// （色のアルファを「描かない」の代用にしない）。<br/>
/// <br/>
/// <b>未指定のハンドル塗り色</b>: 全成分 0（<see cref="DrawingColor.Empty"/> に相当）を指定した場合は白へ解決する<br/>
/// （ハンドルが見えなくなるため）。解決は本型の生成時に適用する。<br/>
/// <br/>
/// <b>色の型</b>: UI 非依存の <see cref="DrawingColor"/> を使用し、各 UI の色型への変換は境界（描画ポートのアダプタ）で行う。<br/>
/// 既定値は部分変更の基点（<c>MarqueePaint.Default with { ... }</c>）として使う。
/// </remarks>
public sealed record MarqueePaint
{
    /// <summary>ハンドル枠の色の既定値（ダークグレー）。</summary>
    /// <value>アルファを含む色。装飾の枠線（穴あけ枠）と共通の見た目である。</value>
    /// <remarks>穴あけ枠と同じ矩形に重なるため、既定値を共有する（同じ色を 2 箇所へ書かない）。</remarks>
    public static readonly DrawingColor DefaultHandleBorderColor = DrawingColor.FromArgb(255, 169, 169, 169);

    /// <summary>ラベルの文字色の既定値（白）。</summary>
    /// <value>アルファを含む色。既定は白（不透明）。</value>
    /// <remarks>
    /// ラベルは文字 + 背景の小さなチップであり、その見た目はラベルの種類を跨いで共通である<br/>
    /// （サイズ・カーソル座標・ホストの付加情報が同じ画面に並ぶため、片方だけ色が違うと体裁が崩れる）。
    /// </remarks>
    private static readonly DrawingColor LabelTextColorDefault = DrawingColor.White;

    /// <summary>ラベルの背景色の既定値（黒 60%）。</summary>
    /// <value>アルファを含む色。既定は黒 60%（<c>#99000000</c>）。</value>
    /// <remarks>背景はチップとして文字を読ませるための帯であり、下の画像を完全には隠さない濃度にする。</remarks>
    private static readonly DrawingColor LabelBackgroundColorDefault = DrawingColor.FromArgb(153, 0, 0, 0);

    /// <summary>すべての色を既定値にした設定。</summary>
    /// <value>既定の色（部分変更の基点として使用する）。</value>
    public static MarqueePaint Default { get; } = new();

    /// <summary>ドラッグハンドルの塗り色を取得する。</summary>
    /// <value>ハンドルの塗り色。既定は白。未指定（全成分 0）を指定した場合は白へ解決される。</value>
    public DrawingColor HandleColor
    {
        get => field;
        init => field = ResolveHandleColor(value);
    } = DrawingColor.White;

    /// <summary>ドラッグハンドルの枠線色を取得する。</summary>
    /// <value>ハンドルの枠線色。既定はダークグレー（<c>#FFA9A9A9</c>）。</value>
    /// <remarks>装飾の穴あけ枠と同じ矩形に重なるため、見た目を揃える場合はホストが同じ色を割り当てる。</remarks>
    public DrawingColor HandleBorderColor { get; init; } = DefaultHandleBorderColor;

    /// <summary>十字マーカー（始点マーカー）の色を取得する。</summary>
    /// <value>十字マーカーの色。既定は白。</value>
    public DrawingColor MarkerColor { get; init; } = DrawingColor.White;

    /// <summary>サイズラベルの文字色を取得する。</summary>
    /// <value>ラベルの文字色。既定は白。</value>
    public DrawingColor SizeLabelColor { get; init; } = LabelTextColorDefault;

    /// <summary>サイズラベルの背景色を取得する。</summary>
    /// <value>ラベルの背景色。既定は黒 60%（<c>#99000000</c>）。</value>
    public DrawingColor SizeLabelBackgroundColor { get; init; } = LabelBackgroundColorDefault;

    /// <summary>カーソル座標ラベルの文字色を取得する。</summary>
    /// <value>ラベルの文字色。既定は白。</value>
    public DrawingColor CoordLabelColor { get; init; } = LabelTextColorDefault;

    /// <summary>カーソル座標ラベルの背景色を取得する。</summary>
    /// <value>ラベルの背景色。既定は黒 60%（<c>#99000000</c>）。</value>
    public DrawingColor CoordLabelBackgroundColor { get; init; } = LabelBackgroundColorDefault;

    /// <summary>サイズラベルのフォントサイズ（DIP）を取得する。</summary>
    /// <value>フォントサイズ（DIP）。6〜48 へ補正済み。既定は 14.0。</value>
    /// <remarks>
    /// ラベルをどの体裁で描くかは<b>描画にだけ必要な設定</b>であり、描画の設定である本型が単一所有する。<br/>
    /// コアは値を持たず、描画の契約（<c>IMarqueeRenderer</c>）を通して UI 層へ公開する。<br/>
    /// 安全域への補正（非有限値は既定値）は <see cref="CoerceLabelFontSize"/> が単一所有する。
    /// </remarks>
    public double SizeLabelFontSize
    {
        get => field;
        init => field = CoerceLabelFontSize(value, SizeLabelFontSizeDefault);
    } = SizeLabelFontSizeDefault;

    /// <summary>サイズラベルのフォントサイズの既定値（DIP）。</summary>
    /// <value>既定のフォントサイズ（DIP）。</value>
    public const double SizeLabelFontSizeDefault = 14.0;

    /// <summary>カーソル座標ラベルのフォントサイズ（DIP）を取得する。</summary>
    /// <value>フォントサイズ（DIP）。6〜48 へ補正済み。既定は 12.0。</value>
    /// <remarks>
    /// ラベルをどの体裁で描くかは<b>描画にだけ必要な設定</b>であり、描画の設定である本型が単一所有する。<br/>
    /// コアは値を持たず、描画の契約（<c>IMarqueeRenderer</c>）を通して UI 層へ公開する。<br/>
    /// 安全域への補正（非有限値は既定値）は <see cref="CoerceLabelFontSize"/> が単一所有する。
    /// </remarks>
    public double CoordLabelFontSize
    {
        get => field;
        init => field = CoerceLabelFontSize(value, CoordLabelFontSizeDefault);
    } = CoordLabelFontSizeDefault;

    /// <summary>カーソル座標ラベルのフォントサイズの既定値（DIP）。</summary>
    /// <value>既定のフォントサイズ（DIP）。</value>
    public const double CoordLabelFontSizeDefault = 12.0;

    /// <summary>ラベルのフォントサイズの最小値（DIP）。</summary>
    /// <value>ラベルのフォントサイズとして受け付ける下限。</value>
    public const double LabelFontSizeMin = 6.0;

    /// <summary>ラベルのフォントサイズの最大値（DIP）。</summary>
    /// <value>ラベルのフォントサイズとして受け付ける上限。</value>
    public const double LabelFontSizeMax = 48.0;

    /// <summary>ラベルの内側余白（DIP）を取得する。</summary>
    /// <value>背景を文字の実寸より四方へ広げる量。既定は 4.0。</value>
    /// <remarks>
    /// サイズラベルとホストの付加情報ラベルは同じ体裁で縦に積まれるため、余白を共有する<br/>
    /// （片方だけ余白が違うと積み上げの間隔が崩れる）。行送り（前の行の高さ + 内側余白 × 2）も本値から導く。
    /// </remarks>
    public double LabelPadding { get; init; } = LabelPaddingDefault;

    /// <summary>ラベルの内側余白の既定値（DIP）。</summary>
    /// <value>既定の内側余白（DIP）。</value>
    public const double LabelPaddingDefault = 4.0;

    /// <summary>暗転マスク（オーバーレイ）の色を取得する。</summary>
    /// <value>アルファを含む色。既定は黒 50%（<c>#80000000</c>）。</value>
    public DrawingColor OverlayColor { get; init; } = DrawingColor.FromArgb(128, 0, 0, 0);

    /// <summary>穴あけ枠（外側の装飾の枠線）の色を取得する。</summary>
    /// <value>アルファを含む色。既定はダークグレー（<c>#FFA9A9A9</c>。ハンドル枠と共通）。</value>
    public DrawingColor OuterBorderColor { get; init; } = DefaultHandleBorderColor;

    /// <summary>穴あけ矩形の内側の塗り色を取得する。</summary>
    /// <value>アルファを含む色。既定は透明（塗らない。描画の可否は明示フラグが決める）。</value>
    public DrawingColor InnerFillColor { get; init; } = DrawingColor.Transparent;

    /// <summary>穴あけ矩形の内側の枠線色を取得する。</summary>
    /// <value>アルファを含む色。既定は透明（描かない。描画の可否は明示フラグが決める）。</value>
    public DrawingColor InnerBorderColor { get; init; } = DrawingColor.Transparent;

    /// <summary>指定されたハンドル塗り色を描画に使う色へ解決する。</summary>
    /// <param name="color">設定された塗り色。</param>
    /// <returns>描画に使う塗り色。未指定（全成分 0）の場合は白。</returns>
    private static DrawingColor ResolveHandleColor(DrawingColor color)
        => color.ToArgb() == 0 ? DrawingColor.White : color;

    /// <summary>ラベルのフォントサイズを安全域（6〜48）へ補正する。</summary>
    /// <param name="value">補正する値（DIP）。</param>
    /// <param name="defaultValue">非数（NaN / ∞）の場合に使う既定値（DIP）。</param>
    /// <returns>6〜48 に補正した値。非数の場合は <paramref name="defaultValue"/>。</returns>
    /// <remarks>
    /// 非数を素通しするとフォントが描画されなくなるため、本型が単一所有する<br/>
    /// （ラベルごとに既定値だけが異なるため、既定値は引数で受ける）。
    /// </remarks>
    private static double CoerceLabelFontSize(double value, double defaultValue)
        => double.IsFinite(value) ? Math.Clamp(value, LabelFontSizeMin, LabelFontSizeMax) : defaultValue;
}
