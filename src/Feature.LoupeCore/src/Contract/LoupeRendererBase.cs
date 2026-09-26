using EsUtil.Feature.LoupeCore.Imaging;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Feature.LoupeCore.Drawing;


namespace EsUtil.Feature.LoupeCore.Contract;

/// <summary>ルーペの描画手順（層の順序に従った描画ポートの呼び出し）を単一所有する基底クラス。</summary>
/// <remarks>
/// <b>適用パターン</b>: Template Method（層の順序を本クラスが所有し、層ごとの描き方を派生クラスが与える）。<br/>
/// <br/>
/// <b>なぜコアに置くか</b>: 「どの層をどの順に描くか」は<b>単体のルーペにも存在する概念</b>であり、<br/>
/// UI フレームワークに依存しない（描画 API は <see cref="IEsCanvas"/> が抽象化している）。<br/>
/// コアが本クラスを所有することで、<b>描画の起動と順序がコアの単一所有</b>になり、<br/>
/// UI 層は描画面（<see cref="IEsCanvas"/>）を用意するだけでよくなる。<br/>
/// <br/>
/// <b>描画にだけ必要な設定はコアが持たない</b>: 色・フォントサイズ・角丸は<b>描画にだけ必要な設定</b>であり、コアの計算・判定に入らない。<br/>
/// したがって本クラスはこれらの値も型も持たず、それを使う層の描画を抽象メソッドまたはフックとして派生クラスへ委ねる<br/>
/// （実体は UIBase の <c>LoupePaint</c> が単一所有し、その保持者である <c>LoupeRenderer</c> が実装する）。<br/>
/// <br/>
/// <b>色を使わない層はコアが描く</b>: 拡大画像の層は色を必要としない（切り出しと描画先の計算はコアが単一所有する）<br/>
/// ため、本クラスが実装する。<br/>
/// <br/>
/// <b>テキストを描く層はフックで受ける</b>: 文言と基準点は <see cref="LoupeDrawContent.CoordLabelText"/> が持ち、UI 層が扱いを決める。<br/>
/// <b>要素（<c>TextBlock</c> 等）として置く UI は <see cref="DrawLabel"/> を上書きしない</b>（既定は何もしない）。<br/>
/// canvas へ描く実装だけが本フックを上書きする（既定の実装を置くと、要素と canvas の二重描画になるため）。<br/>
/// <br/>
/// <b>UI 非依存</b>: 描画 API は <see cref="IEsCanvas"/> の実装が担うため、UI フレームワークとウィンドウを必要としない<br/>
/// （UI 初期化なしの <c>[Fact]</c> でピクセル検証できる）。
/// </remarks>
public abstract class LoupeRendererBase : ILoupeRenderer
{
    /// <summary>ルーペの図形の層（下地・市松模様・拡大画像・十字線・外枠）を描画順に描く。</summary>
    /// <param name="canvas">描画先の描画ポート（物理ピクセル単位）。null 不可。</param>
    /// <param name="source">拡大元の 100% 画像。null の場合は拡大画像を描かない（市松模様のみ）。</param>
    /// <param name="request">描画入力（拡大画像の切り出しに使う物理ピクセルサイズ・拡大率・拡大中心）。</param>
    /// <param name="content">コアが解決した描画内容。<see cref="LoupeDrawContent.Layers"/> は null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> または <paramref name="content"/> の層が null の場合。</exception>
    /// <remarks>
    /// 本メソッドが<b>描画起動の唯一の入口</b>である（コアが描画内容を解決し、本メソッドを通して描く）。<br/>
    /// 描く値はすべて <paramref name="content"/> に解決済みであり、本クラスは計算を持たない（順に写すだけ）。<br/>
    /// テキストは <see cref="DrawLabel"/>（既定は何もしない）へ委ねる（UI 層が要素として置くか画像として描くかを決める）。<br/>
    /// 描画面の消去は下地の層（<see cref="LoupeDrawLayer.Background"/>）が行う（前フレームの残像を残さない）。
    /// </remarks>
    public void DrawShapes(
        IEsCanvas canvas, EsBitmap? source, in LoupeRenderRequest request, in LoupeDrawContent content)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(content.Layers);

        // 描画順はコア（Layers）が単一所有する。本クラスは順に写すだけで、順序を判断しない
        foreach (var layer in content.Layers)
        {
            switch (layer)
            {
                case LoupeDrawLayer.Background:
                    DrawBackground(canvas, in content);
                    break;

                case LoupeDrawLayer.Checkerboard:
                    DrawCheckerboard(canvas, in content);
                    break;

                case LoupeDrawLayer.Image:
                    DrawMagnified(canvas, source, in request);
                    break;

                case LoupeDrawLayer.Crosshair:
                    DrawCrosshair(canvas, in content);
                    break;

                case LoupeDrawLayer.Frame:
                    DrawFrame(canvas, in content);
                    break;

                case LoupeDrawLayer.Label:
                    DrawLabel(canvas, in content);
                    break;
            }
        }
    }

    /// <summary>テキスト（中心座標ラベル）を描画ポートへ描く。</summary>
    /// <param name="canvas">描画先の描画ポート（物理ピクセル単位）。</param>
    /// <param name="content">コアが解決した描画内容（テキストの文言と基準点を持つ）。</param>
    /// <remarks>
    /// <b>既定は何もしない</b>: テキストを UI の要素（<c>TextBlock</c> 等）として置く UI は、本フックを上書きしない<br/>
    /// （要素は親より後に描かれるため常に最上位になる。両方を描くと二重に表示される）。<br/>
    /// canvas へ直接描く実装だけが上書きし、<see cref="IEsCanvas.DrawLabel"/> と <see cref="IEsCanvas.MeasureText"/> を使って描く。<br/>
    /// <b>座標系の換算は実装が担う</b>（本ポートは物理ピクセル単位であり、テキストの基点は DIP である）。<br/>
    /// フォントサイズ・角丸・色は描画層の設定（<c>LoupePaint</c>）を参照する。
    /// </remarks>
    protected virtual void DrawLabel(IEsCanvas canvas, in LoupeDrawContent content)
    {
    }

    /// <summary>下地（ベース色）で描画面の全体を置き換える。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="content">コアが解決した描画内容（描画面の矩形を持つ）。</param>
    /// <remarks>
    /// 市松模様の色は半透明のため、背後の要素（プレビューの画像・画面の平均色等）を透かしたくない場合は<br/>
    /// ホストが不透明色を指定する。透明の場合も<b>描画面を置き換える</b>（前フレームの残像を残さない）。<br/>
    /// 色は本メソッドを実装する派生クラス（色の保持者）が与える。
    /// </remarks>
    protected abstract void DrawBackground(IEsCanvas canvas, in LoupeDrawContent content);

    /// <summary>市松模様を描く（明色で全面を塗った後、暗色タイルのみを上書きする）。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="content">コアが解決した描画内容（描画面の矩形と暗色タイルの列挙子を持つ）。</param>
    /// <remarks>
    /// 明色で全面を塗ってから暗色タイルだけを塗ることで描画回数を半減できる。<br/>
    /// タイルの位置と寸法はコアが換算して本値に解決済みである（本メソッドは計算しない）。<br/>
    /// 色と「描くかどうか」の判定は色の保持者が与える。
    /// </remarks>
    protected abstract void DrawCheckerboard(IEsCanvas canvas, in LoupeDrawContent content);

    /// <summary>十字線を描く。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="content">コアが解決した描画内容（十字線の矩形を持つ）。</param>
    /// <remarks>矩形はコアが解決して本値に含まれる（本メソッドは計算しない）。色は色の保持者が与える。</remarks>
    protected abstract void DrawCrosshair(IEsCanvas canvas, in LoupeDrawContent content);

    /// <summary>外枠を描く。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="content">コアが解決した描画内容（外枠の矩形を持つ）。</param>
    /// <remarks>太さの換算と矩形の導出はコアが済ませて本値に含まれる（本メソッドは計算しない）。色は色の保持者が与える。</remarks>
    protected abstract void DrawFrame(IEsCanvas canvas, in LoupeDrawContent content);

    /// <summary>ソース画像を切り出して拡大描画する（ソース／拡大中心が未設定なら何もしない）。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="source">拡大元の画像。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・拡大率・拡大中心）。</param>
    /// <remarks>
    /// 色を必要としないため本クラス（コア）が実装する。<br/>
    /// 切り出しの可否（ソースと中心が揃っているか）と切り出し手順は <see cref="LoupeImageExtractor.TryExtractRegionAt"/>（コア）へ<br/>
    /// 委譲する（破棄済みソースの扱いもコアが単一所有し、本メソッドは null なら市松模様のみを残す）。<br/>
    /// 切り出し元・描画先の矩形は <see cref="LoupeImageExtractor.ComputeMagnifiedRects"/>（コア）が単一所有する。
    /// </remarks>
    private static void DrawMagnified(IEsCanvas canvas, EsBitmap? source, in LoupeRenderRequest request)
    {
        using var region = LoupeImageExtractor.TryExtractRegionAt(source, request);
        if (region is null)
        {
            return;
        }

        var (sourceRect, destination) = LoupeImageExtractor.ComputeMagnifiedRects(region.Size, request.ZoomLevel);
        canvas.DrawImage(region, sourceRect, destination);
    }
}
