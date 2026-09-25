using EsUtil.Platform.Core.Geometry;

namespace EsUtil.SaveEvidence.Common.Imaging;

/// <summary>矩形の列を確保なしで走査するための列挙子（構造体）が実装する契約。</summary>
/// <typeparam name="TSelf">実装する列挙子自身の型（構造体）。</typeparam>
/// <remarks>
/// <b>位置付け</b>: <see cref="IEsCanvas"/> の複数矩形の塗り（<see cref="IEsCanvas.FillRects{TWalker}"/>）が<br/>
/// 受け取る列挙子の契約である（本契約の利用者はその 1 メソッドだけ）。<br/>
/// <b>名前が Rect を指す理由</b>: 列挙する要素が <see cref="EsRect"/> に固定されている（塗りの入力が矩形であるため）。<br/>
/// <br/>
/// <b>なぜ列挙子なのか</b>: 描画のたびに生成される矩形の列を <c>IReadOnlyList&lt;EsRect&gt;</c> で返すと、<br/>
/// 要素数に比例したヒープ確保がフレームごとに発生する（市松模様のタイルのように数百〜数万件になりうる）。<br/>
/// 本契約を実装した構造体の列挙子を <c>foreach</c> または制約付きジェネリック（<typeparamref name="TSelf"/> 制約）で<br/>
/// 受け渡せば、ボックス化もリスト確保も発生しない。<br/>
/// <br/>
/// <b>実装者の責務</b>: <c>MoveNext</c> / <c>Current</c> に加えて <c>GetEnumerator() =&gt; this</c> を公開する<br/>
/// （<c>foreach</c> がボックス化せずに構造体の列挙子を使えるようにするため）。<br/>
/// <b>列挙は 1 回だけ</b>（実装が内部に走査位置を持つため、同じ値で 2 回列挙すると 2 回目は空になる）。
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
