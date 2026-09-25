using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Estable.Loupe.Imaging;

namespace Estable.Loupe.Rendering;

/// <summary>ルーペの描画内容を描画ポートへ写すレンダラ（UI フレームワーク非依存）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Strategy の既定実装（<see cref="ILoupeRenderer"/> を実装し、<see cref="Default"/> で共有する）。<br/>
/// <br/>
/// <b>位置付け</b>: 「どう描くか」を単一所有する描画側の中核である。コアが決めた描画入力<br/>
/// （<see cref="LoupeRenderRequest"/>）と層の順序（<see cref="LoupeRenderNotification.Layers"/>）を受け取り、<br/>
/// 描画ポート（<see cref="IEsCanvas"/>）へ写すだけである（外観値・判断を保持しない）。<br/>
/// ファサード（<see cref="Loupe.Renderer"/>）へ設定して使う（差し替える場合は <see cref="ILoupeRenderer"/> を実装した別の型を設定する）。<br/>
/// <br/>
/// <b>前提</b>: 描く単位は<b>物理ピクセル</b>である（<see cref="LoupeRenderRequest.PixelSize"/> が作る正方形が描画面）。<br/>
/// DIP → 物理ピクセルの換算はコア（<see cref="LoupeAppearanceScaler" /> / <see cref="LoupePositionCalculator" />）が済ませている。<br/>
/// <br/>
/// <b>ラベルは UI 要素が描く</b>: UI 要素として配置する UI は <see cref="DrawShapes"/> で図形だけを描き、<see cref="ResolveLabels"/> で<br/>
/// 配置指示を得る（要素は親より後に描かれるため常に最上位になる）。<br/>
/// <br/>
/// <b>UI 非依存</b>: 描画 API は <see cref="IEsCanvas"/> の実装が担うため、UI フレームワークとウィンドウを必要としない<br/>
/// （UI 初期化なしの <c>[Fact]</c> でピクセル検証できる）。
/// </remarks>
public sealed class LoupeRenderer : ILoupeRenderer
{
    /// <summary>既定の描画実装（唯一のインスタンス）。</summary>
    /// <value>本クラスの唯一のインスタンス。状態を持たないためファサード間で共有できる。</value>
    public static LoupeRenderer Default { get; } = new();

    /// <summary>唯一のインスタンスだけを生成できるようにする（<see cref="Default"/> を使う）。</summary>
    /// <remarks>状態を持たないため複数インスタンスを作る意味が無い（生成を閉じて単一所有を守る）。</remarks>
    private LoupeRenderer()
    {
    }

    /// <summary>ルーペの図形の層（下地・市松模様・拡大画像・十字線・外枠）を描画順に描く。</summary>
    /// <param name="canvas">描画先の描画ポート（物理ピクセル単位）。null 不可。</param>
    /// <param name="source">拡大元の 100% 画像。null の場合は拡大画像を描かない（市松模様のみ）。</param>
    /// <param name="request">描画入力（コアと UI の契約）。</param>
    /// <param name="notification">コアが解決した通知（層の順序を持つ）。<see cref="LoupeRenderNotification.Layers"/> は null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> または <paramref name="notification"/> の層が null の場合。</exception>
    /// <remarks>
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
                    DrawBackground(canvas, request);
                    break;

                case LoupeDrawLayer.Checkerboard:
                    DrawCheckerboard(canvas, request);
                    break;

                case LoupeDrawLayer.Image:
                    DrawMagnified(canvas, source, request);
                    break;

                case LoupeDrawLayer.Crosshair:
                    DrawCrosshair(canvas, request);
                    break;

                case LoupeDrawLayer.Frame:
                    DrawFrame(canvas, request);
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
    /// <exception cref="ArgumentNullException"><paramref name="measureText"/> が null の場合。</exception>
    /// <remarks>実寸 → 配置矩形の写像は <see cref="LoupeLabelPlan"/> が単一所有する（本メソッドは委譲するだけである）。</remarks>
    public IReadOnlyList<EsLabelElement> ResolveLabels(
        LoupeCoordLabel? coordLabel, Func<string, double, EsSize> measureText)
        => LoupeLabelPlan.Resolve(coordLabel, measureText);

    /// <summary>下地（ベース色）で描画面の全体を置き換える。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="request">描画入力（外観）。</param>
    /// <remarks>
    /// 市松模様の色は半透明のため、背後の要素（プレビューの画像・画面の平均色等）を透かしたくない場合は<br/>
    /// ホストが不透明色を指定する。透明の場合も<b>描画面を置き換える</b>（前フレームの残像を残さない）。
    /// </remarks>
    private static void DrawBackground(IEsCanvas canvas, in LoupeRenderRequest request)
        => canvas.Clear(request.SurfaceBounds, request.Appearance.BackgroundColor);

    /// <summary>市松模様を描く（明色で全面を塗った後、暗色タイルのみを上書きする）。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・外観・スケーリング）。</param>
    /// <remarks>
    /// 明色で全面を塗ってから暗色タイルだけを塗ることで描画回数を半減できる。<br/>
    /// 描画の可否（アルファ 0 は描かない）はコア（<see cref="LoupeAppearance.HasCheckerColorA"/> /<br/>
    /// <see cref="LoupeAppearance.HasCheckerColorB"/>）が単一所有し、タイルの位置と換算もコアが単一所有する。
    /// </remarks>
    private static void DrawCheckerboard(IEsCanvas canvas, in LoupeRenderRequest request)
    {
        var appearance = request.Appearance;

        if (appearance.HasCheckerColorA)
        {
            canvas.FillRect(request.SurfaceBounds, appearance.CheckerColorA);
        }

        if (!appearance.HasCheckerColorB)
        {
            return;
        }

        // タイル数は描画先の大きさに比例して増えるため、リストを確保しない列挙子をそのまま渡す
        canvas.FillRects(
            LoupeCheckerboard.ComputeDarkTiles(
                request.PixelSize, LoupeAppearanceScaler.ScaleCheckerSize(appearance, request.Scaling)),
            appearance.CheckerColorB);
    }

    /// <summary>ソース画像を切り出して拡大描画する（ソース／拡大中心が未設定なら何もしない）。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="source">拡大元の画像。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・拡大率・拡大中心）。</param>
    /// <remarks>
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

    /// <summary>十字線を描く。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・拡大率・外観）。</param>
    /// <remarks>
    /// 矩形の計算はコア（<see cref="LoupeDecorationGeometry"/>）が、描画の可否は<br/>
    /// <see cref="LoupeAppearance.HasCrosshair"/>（コア）が単一所有する。
    /// </remarks>
    private static void DrawCrosshair(IEsCanvas canvas, in LoupeRenderRequest request)
    {
        if (!request.Appearance.HasCrosshair)
        {
            return;
        }

        canvas.FillRects(
            LoupeDecorationGeometry.ComputeCrosshairRects(request.PixelSize, request.ZoomLevel),
            request.Appearance.CrossColor);
    }

    /// <summary>外枠を描く。</summary>
    /// <param name="canvas">描画先の描画ポート。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・外観・スケーリング）。</param>
    /// <remarks>
    /// 太さの補正と DIP → 物理ピクセルの換算は <see cref="LoupeAppearanceScaler"/>（コア）が、<br/>
    /// 描画の可否は <see cref="LoupeAppearance.HasFrame"/>（コア）が単一所有する。
    /// </remarks>
    private static void DrawFrame(IEsCanvas canvas, in LoupeRenderRequest request)
    {
        if (!request.Appearance.HasFrame)
        {
            return;
        }

        canvas.FillRects(
            LoupeDecorationGeometry.ComputeFrameRects(
                request.PixelSize, LoupeAppearanceScaler.ScaleFrameWidth(request.Appearance, request.Scaling)),
            request.Appearance.FrameColor);
    }
}
