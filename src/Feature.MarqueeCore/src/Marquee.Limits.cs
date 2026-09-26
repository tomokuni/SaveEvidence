using EsUtil.Platform.Core.Coordinate;

namespace EsUtil.Feature.MarqueeCore;

public sealed partial class Marquee
{
    /// <summary>選択矩形コントロールの数値パラメータの既定値と、未設定時の解決規則（ファサードが公開する設定のスキーマ）。</summary>
    /// <remarks>
    /// <b>位置付け</b>: ホストが設定する倍率の既定値と、未設定（0 以下）をどう扱うかの規則を単一所有する。<br/>
    /// <b>ファサードの入れ子である理由</b>: 利用者（UI 実装）はプロパティの既定値を登録するため本値を参照する。<br/>
    /// ファサード（<see cref="Marquee"/>）の入れ子とすることで、UI がファサード以外のコア型を直接呼ぶ経路を作らない<br/>
    /// （利用者は選択矩形の数値を得るのに <c>Marquee</c> だけを参照すればよい）。<br/>
    /// <b>規則の所在</b>: 倍率が未設定（0 以下・等倍）の場合に等価変換とする規則は <c>Drawing.MarqueeRenderRequest.ResolvePort</c> が、<br/>
    /// 表示スケールが未設定（0 以下）の場合に倍率を使う規則は <see cref="ResolveDisplayScale"/> が単一所有する。<br/>
    /// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
    /// </remarks>
    public static class Limits
    {
        /// <summary>ポート未指定時に適用する倍率の既定値（ドメイン座標と DIP が 1:1）。</summary>
        /// <value>1.0（等倍）。</value>
        /// <remarks>各 UI の依存関係プロパティ（<c>Scaling</c>）の既定値も本値を参照する（既定値の二重所有を防ぐ）。</remarks>
        public const double DomainToDipScalingDefault = 1.0;

        /// <summary>枠線の太さの最小値（DIP。0 以下の指定を無効化する）。</summary>
        /// <value>0.5（選択枠は細い線でも視認できる必要がある）。</value>
        public const double BorderThicknessMin = 0.5;

        /// <summary>枠線の太さの既定値（DIP）。</summary>
        /// <value>2.0。</value>
        /// <remarks>装飾の外観（<c>OuterDecoAppearance</c> / <c>InnerDecoAppearance</c>）の既定値も本値を参照する（既定値の二重所有を防ぐ）。</remarks>
        public const double BorderThicknessDefault = 2.0;

        /// <summary>枠線の太さを安全域へ補正する（最小値未満は最小値）。</summary>
        /// <param name="value">補正する値（DIP）。</param>
        /// <returns>安全域に補正した値。</returns>
        /// <remarks>
        /// 補正は装飾の外観（<c>OuterDecoAppearance</c> / <c>InnerDecoAppearance</c>）の生成時に適用する<br/>
        /// （設定の経路によらず、範囲内の値だけが保持される）。
        /// </remarks>
        public static double CoerceBorderThickness(double value) => Math.Max(BorderThicknessMin, value);

        /// <summary>表示スケールの係数を解決する（0 以下の場合は座標換算の係数を使う）。</summary>
        /// <param name="displayScaling">指定された表示スケール。0 以下は未指定として扱う。</param>
        /// <param name="scaling">座標換算の係数（1.0 未満は 1.0 として扱う）。</param>
        /// <returns>解決した表示スケール。</returns>
        /// <remarks>「0 以下は未指定」という規則を単一所有する（UI 層に同じ三項演算を書かせない）。</remarks>
        internal static double ResolveDisplayScale(double displayScaling, double scaling)
            => displayScaling > 0 ? displayScaling : DpiScaler.NormalizeScaling(scaling);
    }
}
