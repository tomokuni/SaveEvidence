namespace EsUtil.Feature.MarqueeCore.Decoration;

/// <summary>暗転マスク（オーバーレイ）の外枠（枠線）の寸法をまとめた不変の設定値。</summary>
/// <remarks>
/// 枠線の太さを単一所有する（各 UI 実装は本型を 1 つ公開するだけでよく、UI ごとに既定値がずれない）。<br/>
/// <br/>
/// <b>色は含まない</b>: 色は<b>描画にだけ必要な設定</b>であり、描画の設定（<c>MarqueePaint</c>。UIBase が単一所有する）が持つ。<br/>
/// 描画の可否（暗転するか・枠線を描くか）は <c>MarqueeDecoOptions</c> の明示フラグが単一所有する<br/>
/// （アルファ 0 を「描かない」の代用にしない）。<br/>
/// <br/>
/// 状態（選択矩形・ハイライト矩形・全面暗転の有無・DPI 倍率）は本型に含めない。
/// </remarks>
public sealed record OuterDecoAppearance
{
    /// <summary>選択枠線の太さ（DIP）を取得する。</summary>
    /// <value>枠線の太さ（DIP）。既定は 2.0。</value>
    public double BorderThickness { get; init; } = 2.0;
}
