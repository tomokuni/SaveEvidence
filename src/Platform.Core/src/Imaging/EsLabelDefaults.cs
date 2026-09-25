using DrawingColor = System.Drawing.Color;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>ラベル（サイズ・座標）の体裁の既定値（コアの全コントロールで共有する）。</summary>
/// <remarks>
/// ラベルは「文字 + 背景」の小さなチップであり、その見た目（文字色と背景色）はコントロールを跨いで共通である<br/>
/// （同じ画面に並ぶため、片方だけ色が違うと体裁が崩れる）。値の所有者を本クラスに集約し、<br/>
/// 各コントロールの設定型は本値を参照する（同じ色のリテラルを各設定型へ書かない）。<br/>
/// <br/>
/// <b>フォントサイズも共有する</b>: ラベルの可読性の基準（文字の大きさ）はコントロール固有の設計ではなく、<br/>
/// 同じ画面での見た目の基準である（サイズラベルだけ大きくする場合は各設定型が明示的に上書きする）。<br/>
/// 安全域（最小・最大）は <see cref="EsLabelMetrics"/> が単一所有する。
/// </remarks>
public static class EsLabelDefaults
{
    /// <summary>ラベルの文字色の既定値（白）。</summary>
    /// <value>アルファを含む色。既定は白（不透明）。</value>
    public static readonly DrawingColor TextColor = DrawingColor.White;

    /// <summary>ラベルの背景色の既定値（黒 60%）。</summary>
    /// <value>アルファを含む色。既定は黒 60%（#99000000）。</value>
    /// <remarks>背景はチップとして文字を読ませるための帯であり、下の画像を完全には隠さない濃度にする。</remarks>
    public static readonly DrawingColor BackgroundColor = DrawingColor.FromArgb(153, 0, 0, 0);

    /// <summary>ラベルのフォントサイズの既定値（DIP）。</summary>
    /// <value>フォントサイズ（DIP）。既定は 12.0。</value>
    public const double FontSize = 12.0;
}
