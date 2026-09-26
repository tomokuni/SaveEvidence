using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Feature.LoupeCore.Drawing;


namespace EsUtil.Feature.LoupeCore.Contract;

/// <summary>Loupe の描画内容（何を描くか）を描画面（どう描くか）へ写す実装の契約。</summary>
/// <remarks>
/// <b>適用パターン</b>: Strategy（描画の実装を差し替え可能にする）。<br/>
/// <br/>
/// <b>責務の分担</b>: コア（<see cref="Loupe"/>）は「描くかどうか・どの順に描くか・どの色と寸法か」を判断し、<br/>
/// 本契約の実装は「描画 API をどの順に呼ぶか」だけを担う。描画入力（<see cref="LoupeRenderRequest"/>）と<br/>
/// 描画完了通知（<see cref="LoupeRenderNotification"/>）を受け取り、層の順序は通知が単一所有するため<br/>
/// 実装は順序を判断しない。<br/>
/// <br/>
/// <b>差し替えの方法</b>: 描画ポート（<see cref="IEsCanvas"/>）の実装を変えれば描画先（UI フレームワーク・描画方式）を、<br/>
/// 本契約の実装を変えれば描画手順そのものを差し替えられる。<see cref="Loupe.Renderer"/> へ設定するだけで反映される。<br/>
/// <br/>
/// <b>実装の提供元</b>: 既定実装は描画層（<c>LoupeControl.UIBase</c> の <c>LoupeRenderer</c>）にあり、<br/>
/// 色はその実装のインスタンス状態として渡すため、本契約にも本コアにも色の型は現れない。<br/>
/// コアは本契約だけを定義して実装を注入で受ける（コアは描画層を参照できない）。<br/>
/// <br/>
/// <b>拡大元画像は引数で受ける</b>: 拡大元の型は UI に依存する（UI が解決する）ため、描画の契約に含めない。<br/>
/// <br/>
/// <b>テキストは値として渡す</b>: 文言と基準点は <see cref="LoupeDrawContent.CoordLabelText"/> が持ち、<br/>
/// <b>要素として置くか画像として描くかは UI 層が決める</b>。<br/>
/// <b>フォントサイズと角丸は本契約に含めない</b>: 描画にだけ必要な設定であるため、描画実装が描画の設定（<c>LoupePaint</c>）の値として公開する。<br/>
/// 本契約を実装する描画実装はテキストを描かない（canvas へ描く場合は基底のフックを上書きし、描画の設定の値を使う）。
/// </remarks>
public interface ILoupeRenderer
{
    /// <summary>図形の層（下地・市松模様・拡大画像・十字線・外枠）を描画順に描く。</summary>
    /// <param name="canvas">描画先の描画ポート（物理ピクセル単位）。null 不可。</param>
    /// <param name="source">拡大元の 100% 画像。null の場合は拡大画像を描かない（市松模様のみ）。</param>
    /// <param name="request">描画入力（拡大画像の切り出しに使う物理ピクセルサイズ・拡大率・拡大中心）。</param>
    /// <param name="content">コアが解決した描画内容（層の順序・描画面・矩形を持つ）。</param>
    /// <remarks>
    /// 描く値はすべて <paramref name="content"/> に解決済みであり、実装は計算を持たない。<br/>
    /// テキストは描かない（UI 層が要素として置くか画像として描くかを決める）。<br/>
    /// 描画面の消去は下地の層（<c>LoupeDrawLayer.Background</c>）が行う（前フレームの残像を残さない）。
    /// </remarks>
    void DrawShapes(
        IEsCanvas canvas, EsBitmap? source, in LoupeRenderRequest request, in LoupeDrawContent content);
}
