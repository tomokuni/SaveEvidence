using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;
using EsUtil.SaveEvidence.Common.Imaging;


namespace EsUtil.SaveEvidence.Loupe.UIBase;

/// <summary>ルーペの色（下地・市松模様・外枠・十字線・中心座標ラベル）とラベルの体裁をまとめた不変の設定値。</summary>
/// <remarks>
/// <b>なぜ本層（UIBase）が持つか</b>: 色は<b>描画にだけ必要な設定</b>である（コアの計算には一切入らず、<br/>
/// 「描くかどうか」の判定に使うのはアルファだけ）ため、コア（<c>Feature.LoupeCore</c>）には置かない。<br/>
/// コアは表示可否と寸法（<c>LoupeMetrics</c>）を持ち、色と描画の体裁は本型が単一所有する。<br/>
/// <b>ラベルのフォントサイズ・角丸も同じ理由で本型が持つ</b>: ラベルをどの体裁（文字要素 / 画像）で描くかは UI 層の判断であり、<br/>
/// コアは文言と位置だけを渡す（<c>LoupeTextPlacement</c>）。<br/>
/// <br/>
/// <b>描画の可否</b>: 「アルファ 0 の色は描かない」の判定は <see cref="HasBackground"/> 等の述語が単一所有する<br/>
/// （判定の規則自体は <see cref="DrawingColorExtensions.IsVisible"/> が単一所有する）。<br/>
/// <br/>
/// <b>下地</b>: 市松模様の色は半透明であり、背後の要素を透かしたくない場合はホストが不透明色を指定する。<br/>
/// <br/>
/// <b>色の型</b>: UI 非依存の <see cref="DrawingColor"/> を使用し、各 UI の色型への変換は境界（描画ポートのアダプタ）で行う。<br/>
/// 既定値は部分変更の基点（<c>LoupePaint.Default with { ... }</c>）として使う。
/// </remarks>
public sealed record LoupePaint
{
    /// <summary>すべての色を既定値にした設定。</summary>
    /// <value>既定の色（部分変更の基点として使用する）。</value>
    public static LoupePaint Default { get; } = new();

    /// <summary>ルーペの下地（ベース）色を取得する。</summary>
    /// <value>アルファを含む色。既定は透明（背後の要素を透かす）。</value>
    /// <remarks>市松模様・拡大画像より先に全面へ塗る色。</remarks>
    public DrawingColor BackgroundColor { get; init; } = DrawingColor.Transparent;

    /// <summary>市松模様の色A（明色）を取得する。</summary>
    /// <value>アルファを含む色。既定は Gray 30%（#4C808080）。</value>
    public DrawingColor CheckerColorA { get; init; } = DrawingColor.FromArgb(0x4C, 0x80, 0x80, 0x80);

    /// <summary>市松模様の色B（暗色）を取得する。</summary>
    /// <value>アルファを含む色。既定は DarkGray 30%（#4CA9A9A9）。</value>
    public DrawingColor CheckerColorB { get; init; } = DrawingColor.FromArgb(0x4C, 0xA9, 0xA9, 0xA9);

    /// <summary>外枠の色を取得する。</summary>
    /// <value>アルファを含む色。既定は LightGray（#FFD3D3D3）。</value>
    public DrawingColor FrameColor { get; init; } = DrawingColor.FromArgb(255, 0xD3, 0xD3, 0xD3);

    /// <summary>十字線の色を取得する。</summary>
    /// <value>アルファを含む色。既定は赤 40%（#66FF0000）。</value>
    public DrawingColor CrossColor { get; init; } = DrawingColor.FromArgb(102, 255, 0, 0);

    /// <summary>中心座標ラベルの文字色を取得する。</summary>
    /// <value>アルファを含む色。既定は白。</value>
    public DrawingColor CoordLabelColor { get; init; } = EsLabelDefaults.TextColor;

    /// <summary>中心座標ラベルの背景色を取得する。</summary>
    /// <value>アルファを含む色。既定は黒 60%（#99000000）。</value>
    public DrawingColor CoordLabelBackgroundColor { get; init; } = EsLabelDefaults.BackgroundColor;

    /// <summary>中心座標ラベルのフォントサイズ（DIP）を取得する。</summary>
    /// <value>フォントサイズ（DIP）。6〜48 へ補正済み。既定は 12.0。</value>
    /// <remarks>
    /// ラベルをどの体裁で描くかは<b>描画にだけ必要な設定</b>であり、描画の設定である本型が単一所有する。<br/>
    /// コアは値を持たず、描画の契約（<c>ILoupeRenderer</c>）を通して UI 層へ公開する。<br/>
    /// 安全域への補正（非有限値は既定値）は <see cref="EsLabelMetrics"/> が単一所有する。
    /// </remarks>
    public double CoordLabelFontSize
    {
        get => field;
        init => field = EsLabelMetrics.CoerceFontSize(value, CoordLabelFontSizeDefault);
    } = CoordLabelFontSizeDefault;

    /// <summary>中心座標ラベルのフォントサイズの既定値（DIP）。</summary>
    /// <value>既定のフォントサイズ（DIP）。</value>
    public const double CoordLabelFontSizeDefault = EsLabelDefaults.FontSize;

    /// <summary>中心座標ラベルの背景の角丸半径（DIP）を取得する。</summary>
    /// <value>角丸半径（DIP）。0〜64 へ補正済み（0 は四角形）。既定は 2。</value>
    /// <remarks>ラベルの体裁（描画にだけ必要な設定）であるため、描画の設定である本型が単一所有する。</remarks>
    public int CoordLabelCornerRadius
    {
        get => field;
        init => field = Math.Clamp(value, CoordLabelCornerRadiusMin, CoordLabelCornerRadiusMax);
    } = CoordLabelCornerRadiusDefault;

    /// <summary>中心座標ラベルの角丸半径の最小値（DIP。0 は四角形）。</summary>
    public const int CoordLabelCornerRadiusMin = 0;

    /// <summary>中心座標ラベルの角丸半径の最大値（DIP）。</summary>
    public const int CoordLabelCornerRadiusMax = 64;

    /// <summary>中心座標ラベルの角丸半径の既定値（DIP）。</summary>
    public const int CoordLabelCornerRadiusDefault = 2;

    /// <summary>下地を塗るかどうかを取得する。</summary>
    /// <value>下地のアルファが 0 でない場合は true。</value>
    /// <remarks>アルファ 0 を描画しない扱いにする判定は <see cref="DrawingColorExtensions.IsVisible"/> が単一所有する（UI 層に持たせない）。</remarks>
    public bool HasBackground => BackgroundColor.IsVisible();

    /// <summary>市松模様の明色で下地を塗るかどうかを取得する。</summary>
    /// <value>色Aのアルファが 0 でない場合は true。</value>
    /// <remarks>アルファ 0 を描画しない扱いにする判定は <see cref="DrawingColorExtensions.IsVisible"/> が単一所有する（UI 層に持たせない）。</remarks>
    public bool HasCheckerColorA => CheckerColorA.IsVisible();

    /// <summary>市松模様の暗色タイルを描画するかどうかを取得する。</summary>
    /// <value>色Bのアルファが 0 でない場合は true。</value>
    /// <remarks>アルファ 0 を描画しない扱いにする判定は <see cref="DrawingColorExtensions.IsVisible"/> が単一所有する（UI 層に持たせない）。</remarks>
    public bool HasCheckerColorB => CheckerColorB.IsVisible();

    /// <summary>外枠を描画するかどうかを取得する。</summary>
    /// <value>外枠の色のアルファが 0 でない場合は true。</value>
    /// <remarks>アルファ 0 を描画しない扱いにする判定は <see cref="DrawingColorExtensions.IsVisible"/> が単一所有する（UI 層に持たせない）。</remarks>
    public bool HasFrame => FrameColor.IsVisible();

    /// <summary>十字線を描画するかどうかを取得する。</summary>
    /// <value>十字線の色のアルファが 0 でない場合は true。</value>
    /// <remarks>アルファ 0 を描画しない扱いにする判定は <see cref="DrawingColorExtensions.IsVisible"/> が単一所有する（UI 層に持たせない）。</remarks>
    public bool HasCrosshair => CrossColor.IsVisible();
}
