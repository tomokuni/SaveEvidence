using static EsUtil.Feature.MarqueeCore.Marquee.Limits;

namespace EsUtil.Feature.MarqueeCore.Decoration;

/// <summary>暗転マスク（オーバーレイ）の枠線の寸法をまとめた不変の設定値。</summary>
/// <remarks>
/// 枠線の太さを単一所有する（各 UI 実装は本型を 1 つ公開するだけでよく、UI ごとに既定値がずれない）。<br/>
/// <br/>
/// <b>色は含まない</b>: 色は<b>描画にだけ必要な設定</b>であり、描画の設定（<c>MarqueePaint</c>。UIBase が単一所有する）が持つ。<br/>
/// 描画の可否（暗転するか・枠線を描くか）は <c>MarqueeDecoOptions</c> の明示フラグが単一所有する<br/>
/// （アルファ 0 を「描かない」の代用にしない）。<br/>
/// <br/>
/// 状態（選択矩形・ハイライト矩形・全面暗転の有無・DPI 倍率）は本型に含めない。<br/>
/// <br/>
/// <b>安全域への補正</b>: 枠線の太さの補正は本型の生成時に適用する（<c>init</c> アクセサ）。<br/>
/// 値の設定経路（オブジェクト初期化子・<c>with</c> 式・逆シリアル化）によらず、<br/>
/// <see cref="Marquee.Limits"/> が単一所有する安全域内の値だけが保持される<br/>
/// （UI 側に補正の登録を要求すると、登録を忘れた UI だけ安全域外の値を保持する）。
/// </remarks>
public sealed record MarqueeOuterDecoAppearance
{
    /// <summary>選択枠線の太さ（DIP）を取得する。</summary>
    /// <value>枠線の太さ（DIP）。既定は 2.0。設定時は 0.5 未満なら 0.5 へ補正される。</value>
    public double BorderThickness
    {
        get => field;
        init => field = CoerceBorderThickness(value);
    } = BorderThicknessDefault;
}
