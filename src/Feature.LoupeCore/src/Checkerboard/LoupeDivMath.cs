namespace EsUtil.Feature.LoupeCore.Checkerboard;

/// <summary>ルーペの区画数の計算ロジック（純粋関数）。</summary>
/// <remarks>
/// 「n を m 単位で区切ると何区画になるか（余りを 1 区画として数える）」という、<br/>
/// ルーペの寸法から個数・寸法を導く規則を単一所有する。<br/>
/// タイル数（市松模様）と切り出し領域の一辺（拡大）が同じ規則を使うため、<br/>
/// 各クラスが <c>(int)Math.Ceiling((double)a / b)</c> を書くと余り 1 単位の扱いが散る。<br/>
/// <b>可視性</b>: 利用者は本アセンブリの内部計算だけであるため internal とする。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class LoupeDivMath
{
    /// <summary>整数の切り上げ除算を行う（<paramref name="value"/> ÷ <paramref name="divisor"/> の小数点以下を切り上げ）。</summary>
    /// <param name="value">割られる値（ピクセル数・区画の総長）。0 以上。</param>
    /// <param name="divisor">割る値（1 区画の長さ）。1 以上（0 以下を渡してはならない）。</param>
    /// <returns>切り上げた商（区画数）。<paramref name="value"/> が 0 の場合は 0。</returns>
    /// <remarks>
    /// 切り捨てると端の区画が失われ、市松模様のタイルや切り出し領域の右端・下端が埋まらない。<br/>
    /// 除数は 1 以上を前提とする（0 を渡すと <see cref="double"/> の除算が無限大になる）。
    /// </remarks>
    public static int CeilDiv(int value, int divisor) => (int)Math.Ceiling((double)value / divisor);
}
