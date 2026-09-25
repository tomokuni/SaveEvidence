using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Feature.MarqueeCore;

/// <summary>Marquee の描画内容（何を描くか）を描画面（どう描くか）へ写す実装の契約。</summary>
/// <remarks>
/// <b>適用パターン</b>: Strategy（描画の実装を差し替え可能にする）。<br/>
/// <br/>
/// <b>責務の分担</b>: コア（<see cref="Marquee"/>）は「描くかどうか・どの順に描くか・どの色と寸法か」を判断し、<br/>
/// 本契約の実装は「描画 API をどの順に呼ぶか」だけを担う。返す描画内容は <see cref="MarqueeDrawContent"/> であり、<br/>
/// 層の順序（<see cref="MarqueeDrawContent.Layers"/>）はコアが単一所有するため、実装は順序を判断しない。<br/>
/// <br/>
/// <b>差し替えの方法</b>: 描画ポート（<see cref="IEsCanvas"/>）の実装を変えれば描画先（UI フレームワーク・描画方式）を、<br/>
/// 本契約の実装を変えれば描画手順そのものを差し替えられる。<see cref="Marquee.Renderer"/> へ設定するだけで反映される。<br/>
/// <br/>
/// <b>実装の提供元</b>: 既定実装は描画層（<c>MarqueeControl.Rendering</c> の <c>MarqueeRenderer.Default</c>）にある。<br/>
/// コアは描画層を参照できないため、本契約だけを定義して実装を注入で受ける（依存の向きは一方向に保たれる）。<br/>
/// <br/>
/// <b>ラベルは UI 要素が描く</b>: ラベル（サイズラベル・カーソル座標ラベル）は <see cref="ResolveLabels"/> で配置指示を返し、<br/>
/// UI の要素（<c>Border</c> + <c>TextBlock</c> 等）が描く（要素は親より後に描かれるため常に最上位になる）。<br/>
/// 描画実装はラベルの層（<see cref="MarqueeDrawLayer.SizeLabel"/> / <see cref="MarqueeDrawLayer.CoordLabel"/>）を描かない。
/// </remarks>
public interface IMarqueeRenderer
{
    /// <summary>図形の層（装飾・ハンドル・始点マーカー）を描画順に描く。</summary>
    /// <param name="canvas">描画先の描画ポート。null 不可。</param>
    /// <param name="content">コアが解決した描画内容（層の順序を持つ）。</param>
    /// <remarks>
    /// ラベルの層は描かない（UI が要素として配置するため）。<br/>
    /// 描画面の消去（前フレームの残像の除去）は呼び出し側が行う（描画面の寿命は UI が所有するため）。
    /// </remarks>
    void DrawShapes(IEsCanvas canvas, in MarqueeDrawContent content);

    /// <summary>ラベル要素の配置指示を描画順に解決する（UI 要素として配置する UI 用）。</summary>
    /// <param name="content">コアが解決した描画内容（文言・外観・層の順序を持つ）。</param>
    /// <param name="measureText">テキストの実寸を測る関数（描画 API 依存のため UI 層が与える）。null 不可。</param>
    /// <returns>配置するラベル要素の一覧（サイズラベル → カーソル座標ラベルの順）。どちらも無い場合は空。</returns>
    /// <remarks>
    /// 外観（色・フォントサイズ・余白）と文言はコアが単一所有する（実装は実寸の反映のみを行う）。<br/>
    /// UI は要素（<c>Border</c> + <c>TextBlock</c> 等）へ値を写すだけで、配置の計算を持たない。
    /// </remarks>
    IReadOnlyList<EsLabelElement> ResolveLabels(in MarqueeDrawContent content, Func<string, double, EsSize> measureText);
}
