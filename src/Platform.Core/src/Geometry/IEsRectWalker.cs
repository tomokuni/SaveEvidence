namespace EsUtil.Platform.Core.Geometry;

/// <summary>矩形の列を確保なしで走査するための列挙子（構造体）が実装する契約。</summary>
/// <typeparam name="TSelf">実装する列挙子自身の型（構造体）。</typeparam>
/// <remarks>
/// <b>位置付け</b>: 描画のたびに生成される矩形の列を <c>IReadOnlyList&lt;EsRect&gt;</c> で返すと、<br/>
/// 要素数に比例したヒープ確保がフレームごとに発生する（市松模様のタイルのように数百〜数万件になりうる）。<br/>
/// 本契約を実装した構造体の列挙子を <c>foreach</c> または制約付きジェネリック（<see cref="TSelf"/> 制約）で<br/>
/// 受け渡せば、ボックス化もリスト確保も発生しない。<br/>
/// <br/>
/// <b>実装者の責務</b>: <c>MoveNext</c> / <c>Current</c> に加えて <c>GetEnumerator() =&gt; this</c> を公開する<br/>
/// （<c>foreach</c> がボックス化せずに構造体の列挙子を使えるようにするため）。
/// </remarks>
public interface IEsRectWalker<TSelf>
    where TSelf : struct, IEsRectWalker<TSelf>
{
    /// <summary>次の矩形へ進む。</summary>
    /// <returns>次の矩形がある場合は true。列の末尾を超えた場合は false。</returns>
    bool MoveNext();

    /// <summary>現在の矩形を取得する。</summary>
    /// <value>直近の <see cref="MoveNext"/> が true を返したときの矩形。</value>
    EsRect Current { get; }
}
