using EsUtil.Platform.Core.Imaging;
using DrawingColor = System.Drawing.Color;

namespace Estable.Marquee.Decoration;

/// <summary>暗転マスク（オーバーレイ）の外観をまとめた不変の設定値。</summary>
/// <remarks>
/// 暗転と枠線の外観を 1 つの値として所有する。<br/>
/// 各 UI 実装は本型を 1 つ公開するだけで外観を設定でき、UI ごとに既定値がずれない。<br/>
/// <br/>
/// 状態（選択矩形・ハイライト矩形・全面暗転の有無・DPI 倍率）は本型に含めない。<br/>
/// <b>ラベルの外観は本型に含まない</b>: サイズラベルは選択コア（<see cref="MarqueeOptions"/>）が、<br/>
/// 操作・情報ラベルはホストが自前で描くため、装飾は画面上の暗転と枠線のみを担う。<br/>
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
    /// <value>アルファを含む色。既定はダークグレー（#FFA9A9A9。ハンドル枠と共通）。</value>
    public DrawingColor BorderColor { get; init; } = MarqueeOptions.DefaultHandleBorderColor;

    /// <summary>選択枠線の太さ（DIP）を取得する。</summary>
    /// <value>枠線の太さ（DIP）。既定は 2.0。</value>
    public double BorderThickness { get; init; } = 2.0;

    /// <summary>暗転マスクを描画するかどうかを取得する。</summary>
    /// <value>暗転色のアルファが 0 でない場合は true。false の場合は塗りを省略できる。</value>
    /// <remarks>アルファ 0 を描画しない扱いにする判断は <see cref="DrawingColorExtensions.IsVisible"/> が単一所有する。</remarks>
    public bool HasOverlay => OverlayColor.IsVisible();

    /// <summary>選択枠線を描画するかどうかを取得する。</summary>
    /// <value>枠線色のアルファが 0 でない場合は true。false の場合は枠の生成を省略できる。</value>
    /// <remarks>アルファ 0 を描画しない扱いにする判断は <see cref="DrawingColorExtensions.IsVisible"/> が単一所有する。</remarks>
    public bool HasBorder => BorderColor.IsVisible();
}
