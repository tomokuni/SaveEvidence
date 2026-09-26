namespace EsUtil.Feature.MarqueeCore.Decoration;

/// <summary>選択矩形（穴あけ矩形）の内側に描く装飾の寸法。</summary>
/// <remarks>
/// 穴の内側の枠線の太さを単一所有する。<br/>
/// <br/>
/// <b>色は含まない</b>: 色は<b>描画にだけ必要な設定</b>であり、描画の設定（<c>MarqueePaint</c>。UIBase が単一所有する）が持つ。<br/>
/// 描画の可否（塗るか・枠線を描くか）は <c>MarqueeDecoOptions</c> の明示フラグが単一所有する<br/>
/// （アルファ 0 を「描かない」の代用にしない）。<br/>
/// <br/>
/// <b>外側の枠線との関係</b>: 外側の枠線も穴の内側 4 辺へ描かれるため、両方を有効にすると同じ位置へ二重に描画される。<br/>
/// ハイライトの色を外側と分けたい場合は、描画の設定で外側の枠線を透明にして内側だけを使う（自己完結したハイライト表現になる）。<br/>
/// <br/>
/// <b>安全域への補正</b>: 枠線の太さの補正は本型の生成時に適用する（<c>init</c> アクセサ）。<br/>
/// 値の設定経路（オブジェクト初期化子・<c>with</c> 式・逆シリアル化）によらず、<br/>
/// <see cref="Marquee.Limits"/> が単一所有する安全域内の値だけが保持される<br/>
/// （UI 側に補正の登録を要求すると、登録を忘れた UI だけ安全域外の値を保持する）。
/// </remarks>
public sealed record InnerDecoAppearance
{
    /// <summary>穴あけ矩形の内側に描く枠線の太さを取得する。</summary>
    /// <value>枠線の太さ（DIP）。既定は 2.0。設定時は 0.5 未満なら 0.5 へ補正される。</value>
    public double BorderThickness
    {
        get => field;
        init => field = Marquee.Limits.CoerceBorderThickness(value);
    } = Marquee.Limits.BorderThicknessDefault;
}
