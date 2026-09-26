using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Feature.MarqueeCore.Drawing;


namespace EsUtil.Feature.MarqueeCore.Contract;

/// <summary>選択矩形コントロールの描画手順（層の順序に従った描画ポートの呼び出し）を単一所有する基底クラス。</summary>
/// <remarks>
/// <b>適用パターン</b>: Template Method（層の順序を本クラスが所有し、層ごとの描き方を派生クラスが与える）。<br/>
/// <br/>
/// <b>なぜコアに置くか</b>: 「どの層をどの順に描くか」は<b>単体の選択矩形コントロールにも存在する概念</b>であり、<br/>
/// UI フレームワークに依存しない（描画 API は <see cref="IEsCanvas"/> が抽象化している）。<br/>
/// コアが本クラスを所有することで、<b>描画の起動と順序がコアの単一所有</b>になり、<br/>
/// UI 層は描画面（<see cref="IEsCanvas"/>）を用意するだけでよくなる。<br/>
/// <br/>
/// <b>描画にだけ必要な設定はコアが持たない</b>: 色は<b>描画にだけ必要な設定</b>であり、コアの計算・判定に入らない。<br/>
/// したがって本クラスは色の値も型も持たず、色を使う層の描画を抽象メソッドとして派生クラスへ委ねる<br/>
/// （色の実体は UIBase の <c>MarqueePaint</c> が単一所有し、その保持者である <c>MarqueeRenderer</c> が実装する）。<br/>
/// フォントサイズも同じ扱いであり、値は描画の設定が所有し、本クラスは <see cref="IMarqueeRenderer"/> の<br/>
/// プロパティとして UI 層へ公開する（UI 層はテキストを要素として置くか画像として描くかを決める）。<br/>
/// <br/>
/// <b>層の順序はコアが単一所有する</b>: <see cref="MarqueeDrawContent.Layers"/> の順に層を処理する。<br/>
/// <b>テキストは描かない</b>: 文言と基準点は <see cref="MarqueeDrawContent.SizeLabelText"/> /<br/>
/// <see cref="MarqueeDrawContent.CoordLabelText"/> が持ち、UI 層が扱いを決める（要素は親より後に描かれるため常に最上位になる）。<br/>
/// <br/>
/// <b>UI 非依存</b>: 描画 API は <see cref="IEsCanvas"/> の実装（例: Skia は <c>SkiaEsCanvas</c>）が担うため、<br/>
/// 本クラスは UI フレームワークとウィンドウを必要としない（UI 初期化なしの <c>[Fact]</c> でピクセル検証できる）。
/// </remarks>
public abstract class MarqueeRendererBase : IMarqueeRenderer
{
    /// <inheritdoc/>
    public abstract double SizeLabelFontSize { get; }

    /// <inheritdoc/>
    public abstract double CoordLabelFontSize { get; }

    /// <summary>図形の層（装飾・ハンドル・始点マーカー）を描画順に描く。</summary>
    /// <param name="canvas">描画先の描画ポート。null 不可。</param>
    /// <param name="content">コアが解決した描画内容（図形の層の順序を持つ）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> が null の場合。</exception>
    /// <remarks>
    /// 本メソッドが<b>描画起動の唯一の入口</b>である（コアが描画内容を解決し、本メソッドを通して描く）。<br/>
    /// テキストは描かない（UI 層が要素として置くか画像として描くかを決める）。描くものが無い層は値が null のため何もしない。<br/>
    /// 描画面の消去（前フレームの残像の除去）は呼び出し側が行う（描画面の寿命は UI が所有するため）。
    /// </remarks>
    public void DrawShapes(IEsCanvas canvas, in MarqueeDrawContent content)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        // 描画順はコア（Layers）が単一所有する。本クラスは順に写すだけで、順序を判断しない
        foreach (var layer in content.Layers)
        {
            switch (layer)
            {
                case MarqueeDrawLayer.Decoration:
                    DrawDecoration(canvas, in content);
                    break;

                case MarqueeDrawLayer.Handles when content.Handles is { } handles:
                    DrawHandles(canvas, in handles);
                    break;

                case MarqueeDrawLayer.Marker when content.Marker is { } marker:
                    DrawStartMarker(canvas, in marker);
                    break;
            }
        }
    }

    /// <summary>装飾（選択矩形の外側の暗転マスク・穴あけ枠と、内側の塗り・枠線）を描く。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="content">コアが解決した描画内容（外側・内側の装飾フレームを持つ）。</param>
    /// <remarks>色は本メソッドを実装する派生クラス（色の保持者）が与える。</remarks>
    protected abstract void DrawDecoration(IEsCanvas canvas, in MarqueeDrawContent content);

    /// <summary>ドラッグハンドル（塗り矩形 + 枠線）を描く。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="handles">コアが解決したハンドルの描画内容（矩形・大きさ・枠線の太さを持つ）。</param>
    /// <remarks>ハンドル位置の計算はコア（<c>SelectionGeometry</c>）が単一所有する。色は色の保持者が与える。</remarks>
    protected abstract void DrawHandles(IEsCanvas canvas, in MarqueeHandles handles);

    /// <summary>始点マーカー（十字線）を描く。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="marker">コアが解決したマーカーの描画内容（中心・太さ・半幅を持つ）。</param>
    /// <remarks>太さはコアが単一所有する。色は色の保持者が与える。</remarks>
    protected abstract void DrawStartMarker(IEsCanvas canvas, in MarqueeMarker marker);
}
