using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.MarqueeCore.Decoration;

/// <summary>選択矩形（穴あけ矩形）の内側に描く装飾の外観。</summary>
/// <remarks>
/// 外側の装飾（<see cref="OuterDecoAppearance"/>）が「暗転マスク・穴あけ枠・ラベル」を持つ 1 色であり、<br/>
/// 本値は「穴の内側の塗り」と「穴の内側の枠線」を単一所有する。<br/>
/// 用途はハイライト矩形の強調（例: 検出矩形のホバーを「枠線 + 半透明の塗り」で示す）である。<br/>
/// <br/>
/// <b>既定はどちらも描かない</b>（透明）: 汎用の選択矩形コントロールであるため、塗り・内枠は用途依存であり、<br/>
/// ホストが明示的に与える。アルファ 0 は「描かない」を意味し、描画の要否は本型ではなくフレーム解決が判断する<br/>
/// （UI 層に「透明なら描かない」を書かない）。<br/>
/// <br/>
/// <b>外側の枠線との関係</b>: 外側の枠線（<see cref="OuterDecoAppearance.BorderColor"/>）は穴の内側 4 辺へ描かれるため、<br/>
/// 本値の <see cref="BorderColor"/> と同時に指定すると同じ位置へ二重に描画される。ハイライトの色を外側と分けたい場合は、<br/>
/// 外側の枠線色を透明にして本値だけを使う（自己完結したハイライト表現になる）。<br/>
/// <br/>
/// <b>色の型</b>: UI 非依存の <see cref="DrawingColor"/> を使用し、各 UI の色型への変換は境界（UI 層）で 1 行行う。
/// </remarks>
public sealed record InnerDecoAppearance
{
    /// <summary>穴あけ矩形の内側を塗る色を取得する。</summary>
    /// <value>アルファを含む色。既定は透明（塗らない）。</value>
    /// <remarks>アルファが 0 の場合は塗らない（判断はフレーム解決が行う）。</remarks>
    public DrawingColor FillColor { get; init; } = DrawingColor.Transparent;

    /// <summary>穴あけ矩形の内側に描く枠線の色を取得する。</summary>
    /// <value>アルファを含む色。既定は透明（描かない）。</value>
    /// <remarks>アルファが 0 の場合は描かない（判断はフレーム解決が行う）。</remarks>
    public DrawingColor BorderColor { get; init; } = DrawingColor.Transparent;

    /// <summary>穴あけ矩形の内側に描く枠線の太さを取得する。</summary>
    /// <value>枠線の太さ（DIP）。既定は 2.0。</value>
    /// <remarks>0.5 未満は 0.5 へ正規化される（規則は幾何計算が単一所有する）。</remarks>
    public double BorderThickness { get; init; } = 2.0;

    /// <summary>穴あけ矩形の内側を塗るかどうかを取得する。</summary>
    /// <value>塗り色のアルファが 0 でない場合は true。false の場合は塗りを省略できる。</value>
    /// <remarks>アルファ 0 を描画しない扱いにする判断は <see cref="DrawingColorExtensions.IsVisible"/> が単一所有する。</remarks>
    public bool HasFill => FillColor.IsVisible();

    /// <summary>穴あけ矩形の内側に枠線を描くかどうかを取得する。</summary>
    /// <value>枠線色のアルファが 0 でない場合は true。false の場合は枠の生成を省略できる。</value>
    /// <remarks>アルファ 0 を描画しない扱いにする判断は <see cref="DrawingColorExtensions.IsVisible"/> が単一所有する。</remarks>
    public bool HasBorder => BorderColor.IsVisible();
}
