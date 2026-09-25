using DrawingColor = System.Drawing.Color;
using EsUtil.SaveEvidence.Common.Imaging;


namespace EsUtil.SaveEvidence.Marquee.UIBase;

/// <summary>選択矩形コントロールの色（ハンドル・始点マーカー・ラベル・装飾）をまとめた不変の設定値。</summary>
/// <remarks>
/// <b>なぜ本層（UIBase）が持つか</b>: 色は<b>描画にだけ必要な設定</b>であり、コアの計算には入らない。したがって<br/>
/// コア（<c>Feature.MarqueeCore</c>）には置かず、描画の設定として本層が単一所有する<br/>
/// （コアは寸法・表示可否・対話パラメータを持つ <c>MarqueeOptions</c> と <c>MarqueeDecoOptions</c> を持つ）。<br/>
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
    public DrawingColor SizeLabelColor { get; init; } = EsLabelDefaults.TextColor;

    /// <summary>サイズラベルの背景色を取得する。</summary>
    /// <value>ラベルの背景色。既定は黒 60%（<c>#99000000</c>）。</value>
    public DrawingColor SizeLabelBackgroundColor { get; init; } = EsLabelDefaults.BackgroundColor;

    /// <summary>カーソル座標ラベルの文字色を取得する。</summary>
    /// <value>ラベルの文字色。既定は白。</value>
    public DrawingColor CoordLabelColor { get; init; } = EsLabelDefaults.TextColor;

    /// <summary>カーソル座標ラベルの背景色を取得する。</summary>
    /// <value>ラベルの背景色。既定は黒 60%（<c>#99000000</c>）。</value>
    public DrawingColor CoordLabelBackgroundColor { get; init; } = EsLabelDefaults.BackgroundColor;

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
}
