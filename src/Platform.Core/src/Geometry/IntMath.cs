namespace EsUtil.Platform.Core.Geometry;

/// <summary>整数の丸め計算ロジック（純粋関数）。</summary>
/// <remarks>
/// 「切り上げ除算」のように演算子では表せない整数の規則を単一所有する。<br/>
/// 各所が <c>(int)Math.Ceiling((double)a / b)</c> を書くと、余り 1 単位の扱いや除数の前提が散る<br/>
/// （同じ「何個必要か」の計算が 2 箇所で違う結果になる）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class IntMath
{
    /// <summary>整数の切り上げ除算を行う（<paramref name="value"/> ÷ <paramref name="divisor"/> の小数点以下を切り上げ）。</summary>
    /// <param name="value">割られる値。0 以上。</param>
    /// <param name="divisor">割る値。1 以上（0 以下を渡してはならない）。</param>
    /// <returns>切り上げた商。<paramref name="value"/> が 0 の場合は 0。</returns>
    /// <remarks>
    /// 「n ピクセルを m ピクセル単位で区切ると何区画になるか」の計算に使う（余りを 1 区画として数える）。<br/>
    /// 切り捨てると端の区画が失われ、市松模様や切り出し領域の右端・下端が埋まらない。
    /// </remarks>
    public static int CeilDiv(int value, int divisor) => (int)Math.Ceiling((double)value / divisor);
}
