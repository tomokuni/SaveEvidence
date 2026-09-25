namespace EsUtil.SaveEvidence.Common.Imaging;

/// <summary>ラベルのフォントサイズの安全域と補正を単一所有する（純粋関数）。</summary>
/// <remarks>
/// 「ラベルのフォントサイズは 6〜48 DIP」「非数（NaN / ∞）は呼び出し側が定める既定値」という規則を<br/>
/// 本クラスが単一所有する。ラベルを持つコントロールが個別に補正を書くと、<br/>
/// 片方だけが非数を素通ししてフォントが描画されなくなる（値の設定経路によっても結果が変わる）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class EsLabelMetrics
{
    /// <summary>ラベルのフォントサイズの最小値（DIP）。</summary>
    public const double FontSizeMin = 6.0;

    /// <summary>ラベルのフォントサイズの最大値（DIP）。</summary>
    public const double FontSizeMax = 48.0;

    /// <summary>ラベルのフォントサイズを安全域（6〜48）へ補正する。</summary>
    /// <param name="value">補正する値（DIP）。</param>
    /// <param name="defaultValue">非数（NaN / ∞）の場合に使う既定値（DIP）。</param>
    /// <returns>6〜48 に補正した値。非数の場合は <paramref name="defaultValue"/>。</returns>
    /// <remarks>
    /// ラベルごとに既定値だけが異なるため、既定値は引数で受ける<br/>
    /// （ラベルごとに補正式を書くと、片方だけが非数を素通しする余地が生じる）。
    /// </remarks>
    public static double CoerceFontSize(double value, double defaultValue)
        => double.IsFinite(value) ? Math.Clamp(value, FontSizeMin, FontSizeMax) : defaultValue;
}
