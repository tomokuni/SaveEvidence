using EsUtil.Feature.LoupeCore.Imaging;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Feature.LoupeCore;

/// <summary>ルーペの描画手順（層の順序に従った描画ポートの呼び出し）を単一所有する基底クラス。</summary>
/// <remarks>
/// <b>適用パターン</b>: Template Method（層の順序を本クラスが所有し、層ごとの描き方を派生クラスが与える）。<br/>
/// <br/>
/// <b>なぜコアに置くか</b>: 「どの層をどの順に描くか」は<b>単体のルーペにも存在する概念</b>であり、<br/>
/// UI フレームワークに依存しない（描画 API は <see cref="IEsCanvas"/> が抽象化している）。<br/>
/// コアが本クラスを所有することで、<b>描画の起動と順序がコアの単一所有</b>になり、<br/>
/// UI 層は描画面（<see cref="IEsCanvas"/>）を用意するだけでよくなる。<br/>
/// <br/>
/// <b>色はコアが持たない</b>: 色は<b>描画にだけ必要な設定</b>であり、コアの計算・判定に入らない。<br/>
/// したがって本クラスは色の値も型も持たず、色を使う層の描画を抽象メソッドとして派生クラスへ委ねる<br/>
/// （色の実体は UIBase の <c>LoupePaint</c> が単一所有し、その保持者である <c>LoupeRenderer</c> が実装する）。<br/>
/// <br/>
/// <b>色を使わない層はコアが描く</b>: 拡大画像の層は色を必要としない（切り出しと描画先の計算はコアが単一所有する）<br/>
/// ため、本クラスが実装する。<br/>
/// <br/>
/// <b>UI 非依存</b>: 描画 API は <see cref="IEsCanvas"/> の実装が担うため、UI フレームワークとウィンドウを必要としない<br/>
/// （UI 初期化なしの <c>[Fact]</c> でピクセル検証できる）。
/// </remarks>
public abstract class LoupeRendererBase : ILoupeRenderer
{
    /// <summary>ルーペの図形の層（下地・市松模様・拡大画像・十字線・外枠）を描画順に描く。</summary>
    /// <param name="canvas">描画先の描画ポート（物理ピクセル単位）。null 不可。</param>
    /// <param name="source">拡大元の 100% 画像。null の場合は拡大画像を描かない（市松模様のみ）。</param>
    /// <param name="request">描画入力（コアと UI の契約）。</param>
    /// <param name="notification">コアが解決した通知（層の順序を持つ）。<see cref="LoupeRenderNotification.Layers"/> は null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> または <paramref name="notification"/> の層が null の場合。</exception>
    /// <remarks>
    /// 本メソッドが<b>描画起動の唯一の入口</b>である（コアが描画内容を解決し、本メソッドを通して描く）。<br/>
    /// 中心座標ラベルの層（<see cref="LoupeDrawLayer.CoordLabel"/>）は描かない（UI が要素として配置する）。<br/>
    /// 描画面の消去は下地の層（<see cref="LoupeDrawLayer.Background"/>）が行う（前フレームの残像を残さない）。
    /// </remarks>
    public void DrawShapes(
        IEsCanvas canvas, EsBitmap? source, in LoupeRenderRequest request, in LoupeRenderNotification notification)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(notification.Layers);

        // 描画順はコア（Layers）が単一所有する。本クラスは順に写すだけで、順序を判断しない
        foreach (var layer in notification.Layers)
        {
            switch (layer)
            {
                case LoupeDrawLayer.Background:
                    DrawBackground(canvas, in request);
                    break;

                case LoupeDrawLayer.Checkerboard:
                    DrawCheckerboard(canvas, in request);
                    break;

                case LoupeDrawLayer.Image:
                    DrawMagnified(canvas, source, in request);
                    break;

                case LoupeDrawLayer.Crosshair:
                    DrawCrosshair(canvas, in request);
                    break;

                case LoupeDrawLayer.Frame:
                    DrawFrame(canvas, in request);
                    break;

                // 中心座標ラベルは UI が要素として配置する（本メソッドは描かない）
                case LoupeDrawLayer.CoordLabel:
                    break;
            }
        }
    }

    /// <summary>中心座標ラベルの配置指示を解決する（UI 要素として配置する UI 用）。</summary>
    /// <param name="coordLabel">コアが解決したラベル契約。描かない場合は null。</param>
    /// <param name="measureText">テキストの実寸を測る関数（描画 API 依存のため UI 層が与える）。null 不可。</param>
    /// <returns>配置するラベル要素（1 要素）。描かない場合は空。</returns>
    /// <remarks>ラベルの色は色を持つ派生クラス（UIBase の既定実装）が与える。</remarks>
    public abstract IReadOnlyList<EsLabelElement> ResolveLabels(
        LoupeCoordLabel? coordLabel, Func<string, double, EsSize> measureText);

    /// <summary>下地（ベース色）で描画面の全体を置き換える。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="request">描画入力（外形）。</param>
    /// <remarks>
    /// 市松模様の色は半透明のため、背後の要素（プレビューの画像・画面の平均色等）を透かしたくない場合は<br/>
    /// ホストが不透明色を指定する。透明の場合も<b>描画面を置き換える</b>（前フレームの残像を残さない）。<br/>
    /// 色は本メソッドを実装する派生クラス（色の保持者）が与える。
    /// </remarks>
    protected abstract void DrawBackground(IEsCanvas canvas, in LoupeRenderRequest request);

    /// <summary>市松模様を描く（明色で全面を塗った後、暗色タイルのみを上書きする）。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・寸法・スケーリング）。</param>
    /// <remarks>
    /// 明色で全面を塗ってから暗色タイルだけを塗ることで描画回数を半減できる。<br/>
    /// タイルの位置と換算はコア（<c>LoupeCheckerboard</c> / <c>LoupeMetricsScaler</c>）が単一所有する。<br/>
    /// 色と「描くかどうか」の判定は色の保持者が与える。
    /// </remarks>
    protected abstract void DrawCheckerboard(IEsCanvas canvas, in LoupeRenderRequest request);

    /// <summary>十字線を描く。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・拡大率）。</param>
    /// <remarks>矩形の計算はコア（<c>LoupeDecorationGeometry</c>）が単一所有する。色は色の保持者が与える。</remarks>
    protected abstract void DrawCrosshair(IEsCanvas canvas, in LoupeRenderRequest request);

    /// <summary>外枠を描く。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・寸法・スケーリング）。</param>
    /// <remarks>太さの補正と換算はコア（<c>LoupeMetricsScaler</c>）が単一所有する。色は色の保持者が与える。</remarks>
    protected abstract void DrawFrame(IEsCanvas canvas, in LoupeRenderRequest request);

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
