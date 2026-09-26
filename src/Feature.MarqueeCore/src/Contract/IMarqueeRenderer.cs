using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Feature.MarqueeCore.Drawing;


namespace EsUtil.Feature.MarqueeCore.Contract;

/// <summary>Marquee の描画内容（何を描くか）を描画面（どう描くか）へ写す実装の契約。</summary>
/// <remarks>
/// <b>適用パターン</b>: Strategy（描画の実装を差し替え可能にする）。<br/>
/// <br/>
/// <b>責務の分担</b>: コア（<see cref="Marquee"/>）は「描くかどうか・どの順に描くか・どの寸法か」を判断し、<br/>
/// 本契約の実装は「描画 API をどの順に呼ぶか」だけを担う。返す描画内容は <see cref="MarqueeDrawContent"/> であり、<br/>
/// 図形の層の順序（<see cref="MarqueeDrawContent.Layers"/>）はコアが単一所有するため、実装は順序を判断しない。<br/>
/// <br/>
/// <b>差し替えの方法</b>: 描画ポート（<see cref="IEsCanvas"/>）の実装を変えれば描画先（UI フレームワーク・描画方式）を、<br/>
/// 本契約の実装を変えれば描画手順そのものを差し替えられる。<see cref="Marquee.Renderer"/> へ設定するだけで反映される。<br/>
/// <br/>
/// <b>実装の提供元</b>: 既定実装は描画層（<c>MarqueeControl.UIBase</c> の <c>MarqueeRenderer</c>）にある。<br/>
/// 色とフォントサイズ（描画にだけ必要な設定）はその実装のインスタンス状態として渡すため、<br/>
/// 本契約にも本コアにも現れない（描画層が保持する）。<br/>
/// コアは描画層を参照できないため、本契約だけを定義して実装を注入で受ける（依存の向きは一方向に保たれる）。<br/>
/// <br/>
/// <b>テキストは値として渡す</b>: 文言と基準点は <see cref="MarqueeDrawContent.SizeLabelText"/> /<br/>
/// <see cref="MarqueeDrawContent.CoordLabelText"/> が持ち、<b>要素として置くか画像として描くかは UI 層が決める</b>。<br/>
/// テキストを描く層のフックは基底（<c>MarqueeRendererBase</c>）が用意する（既定は何もしない）。<br/>
/// 要素として置く UI の実装は当該フックを上書きしない（フォントサイズ・色は描画層の設定を参照する）。
/// </remarks>
public interface IMarqueeRenderer
{
    /// <summary>図形の層（装飾・ハンドル・始点マーカー）を描画順に描く。</summary>
    /// <param name="canvas">描画先の描画ポート。null 不可。</param>
    /// <param name="content">コアが解決した描画内容（層の順序を持つ）。</param>
    /// <remarks>
    /// テキストは描かない（UI 層が要素として置くか画像として描くかを決める）。<br/>
    /// 描画面の消去（前フレームの残像の除去）は呼び出し側が行う（描画面の寿命は UI が所有するため）。
    /// </remarks>
    void DrawShapes(IEsCanvas canvas, in MarqueeDrawContent content);
}
