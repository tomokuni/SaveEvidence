using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Loupe.UIBase;

/// <summary>ルーペの色（下地・市松模様・外枠・十字線・中心座標ラベル）をまとめた不変の設定値。</summary>
/// <remarks>
/// <b>なぜ本層（UIBase）が持つか</b>: 色は<b>描画にだけ必要な設定</b>である（コアの計算には一切入らず、<br/>
/// 「描くかどうか」の判定に使うのはアルファだけ）ため、コア（<c>Feature.LoupeCore</c>）には置かない。<br/>
/// コアは寸法（<c>LoupeMetrics</c>）を持ち、色は本型が単一所有する。<br/>
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
