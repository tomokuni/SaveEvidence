using Estable.Loupe;
using Estable.Loupe.Imaging;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;

namespace Estable.MewUI.Loupe;

/// <summary>ルーペの描画（市松模様背景 + 拡大画像 + 十字線 + 外枠）を Skia で行うレンダラー。</summary>
/// <remarks>
/// 描画の順序と、描画に必要な値の単位はコアの契約（<see cref="LoupeRenderRequest"/>）が単一所有する。<br/>
/// UI 非依存コア（<see cref="LoupeAppearance"/> / <see cref="LoupeImageExtractor"/> / <see cref="LoupeDecorationGeometry"/> /<br/>
/// <see cref="LoupeCheckerboard"/>）の値と計算結果を <see cref="SKCanvas"/> へ描画するだけの層。<br/>
/// 外観値（色・太さ・タイルサイズ）は <see cref="LoupeAppearance"/> が単一所有し、本クラスは保持しない。<br/>
/// <b>文字（中心座標ラベル）は本クラスが描かない</b>: テキストの実寸測定が必要なため、描画完了時にホストへ通知する<br/>
/// （ホストが <c>SkiaText.Measure</c> と <c>SkiaLabel.Draw</c> で描く。文字の描画をホストの責務とする）。<br/>
/// Avalonia 版は中間ビットマップ（<c>RenderTargetBitmap</c>）を経由するが、MewUI 版は画面のキャンバスへ<br/>
/// 直接描画できるため、中間バッファの生成とコピーを行わない。<br/>
/// 矩形計算はコアが単一所有するため、両 UI の見た目は一致する（Avalonia 版のレンダラーと等価）。
/// </remarks>
internal static class LoupeSkiaRenderer
{
    /// <summary>ルーペを描画する（市松模様背景 + 拡大画像 + 十字線 + 外枠）。</summary>
    /// <param name="canvas">描画先のキャンバス（左上を原点とする）。</param>
    /// <param name="source">拡大元の 100% 画像。null の場合は市松模様のみ。</param>
    /// <param name="request">描画入力（コア⇔UI の契約。UI が公開プロパティから組み立てる）。</param>
    /// <remarks>
    /// 0. 下地（ベース色。透明なら描かない）を描画する。<br/>
    /// 1. 市松模様の背景を描画する。<br/>
    /// 2. ソース画像があれば切り出して拡大描画する（切り出しはコアへ委譲。範囲外は透明パディング）。<br/>
    /// 3. 十字線を描画する。<br/>
    /// 4. 外枠を描画する。<br/>
    /// 外観の DIP 値は描画時に物理ピクセルへ換算する（DPI 差で見た目を変えない）。<br/>
    /// 中心座標ラベル（<see cref="LoupeCoordLabel"/>）は文字の描画をホストが担うため、本メソッドは描かない。
    /// </remarks>
    public static void Render(SKCanvas canvas, EsBitmap? source, LoupeRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(request.Appearance);

        // 下地（ベース色）→ 市松模様の背景（タイルサイズの補正・DIP → 物理ピクセルの換算はコアが行う）
        DrawBackground(canvas, request);
        DrawCheckerboard(canvas, request);
        DrawMagnified(canvas, source, request);
        DrawCrosshair(canvas, request);
        DrawFrame(canvas, request);
    }

    /// <summary>下地（ベース色）を表面全体へ描画する。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="request">描画入力（外観）。</param>
    /// <remarks>
    /// 市松模様の色は半透明のため、背後の要素（プレビューの画像・画面の平均色等）を透かしたくない場合はホストが不透明色を指定する。<br/>
    /// 表面全体を塗り替える（透明の場合も前フレームの画素を残さない）ため、キャンバスのクリアを使う。
    /// </remarks>
    private static void DrawBackground(SKCanvas canvas, LoupeRenderRequest request)
        => canvas.Clear(request.Appearance.BackgroundColor.ToSKColor());

    /// <summary>市松模様の背景を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・外観・スケーリング）。</param>
    /// <remarks>
    /// 明色で全面を塗った後、暗色タイルのみを上書きする（描画回数を半減）。<br/>
    /// 描画の可否（アルファ 0 は描画しない）はコアの <see cref="LoupeAppearance.HasCheckerColorA"/> /
    /// <see cref="LoupeAppearance.HasCheckerColorB"/> の判定が単一所有し、本メソッドは描画だけを行う。<br/>
    /// 暗色タイルの位置（タイル数・座標）とタイルサイズの物理ピクセル換算はコアが単一所有する。
    /// </remarks>
    private static void DrawCheckerboard(SKCanvas canvas, LoupeRenderRequest request)
    {
        var appearance = request.Appearance;

        // 明色で全面を塗る（暗色タイルの下地）。透明の場合は上書きされないため省略する
        if (appearance.HasCheckerColorA)
        {
            SkiaFill.Rect(canvas, request.SurfaceBounds, appearance.CheckerColorA);
        }

        if (!appearance.HasCheckerColorB)
        {
            return;
        }

        // タイルサイズの換算と暗色タイルの位置（タイル数・座標）はコアが単一所有する（本クラスは返された矩形を塗るだけ）
        SkiaFill.Rects(
            canvas,
            LoupeCheckerboard.ComputeDarkTileRects(
                request.PixelSize, LoupeAppearanceScaler.ScaleCheckerSize(appearance, request.Scaling)),
            appearance.CheckerColorB);
    }

    /// <summary>ソース画像を切り出して拡大描画する（ソース／中心が未設定なら何もしない）。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="source">拡大元の画像。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・拡大率・拡大中心）。</param>
    /// <remarks>
    /// 切り出しの可否（ソースと中心が揃っているか）と切り出し手順は <see cref="LoupeImageExtractor.TryExtractRegionAt"/>（コア）へ委譲する<br/>
    /// （破棄済みソースの扱いもコアが単一所有し、本メソッドは null なら市松模様のみを残す）。<br/>
    /// 切り出し元・描画先の矩形はコア（<see cref="LoupeImageExtractor.ComputeMagnifiedRects"/>）が単一所有する（Avalonia 版と同一の倍率）。<br/>
    /// <c>ToSKBitmap()</c> は内部インスタンスの共有参照を返すため破棄しない。
    /// </remarks>
    private static void DrawMagnified(SKCanvas canvas, EsBitmap? source, LoupeRenderRequest request)
    {
        using var region = LoupeImageExtractor.TryExtractRegionAt(source, request);
        if (region is null)
        {
            return;
        }

        var (src, dst) = LoupeImageExtractor.ComputeMagnifiedRects(region.Size, request.ZoomLevel);

        // 拡大は最近傍（既定）で行い、画素をそのまま拡大表示する
        canvas.DrawBitmap(region.ToSKBitmap(), src.ToSKRect(), dst.ToSKRect());
    }

    /// <summary>十字線を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・拡大率・外観）。</param>
    /// <remarks>
    /// 矩形の計算はコアが単一所有し、描画の可否（アルファ 0 は描画しない）は
    /// <see cref="LoupeAppearance.HasCrosshair"/> の判定が単一所有する。
    /// </remarks>
    private static void DrawCrosshair(SKCanvas canvas, LoupeRenderRequest request)
    {
        if (!request.Appearance.HasCrosshair)
        {
            return;
        }

        SkiaFill.Rects(
            canvas,
            LoupeDecorationGeometry.ComputeCrosshairRects(request.PixelSize, request.ZoomLevel),
            request.Appearance.CrossColor);
    }

    /// <summary>外枠を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・外観・スケーリング）。</param>
    /// <remarks>
    /// 太さの補正と DIP → 物理ピクセルの換算は <see cref="LoupeAppearanceScaler"/>（コア）が単一所有し、<br/>
    /// 描画の可否（アルファ 0 は描画しない）は <see cref="LoupeAppearance.HasFrame"/> の判定が単一所有する。
    /// </remarks>
    private static void DrawFrame(SKCanvas canvas, LoupeRenderRequest request)
    {
        if (!request.Appearance.HasFrame)
        {
            return;
        }

        SkiaFill.Rects(
            canvas,
            LoupeDecorationGeometry.ComputeFrameRects(
                request.PixelSize, LoupeAppearanceScaler.ScaleFrameWidth(request.Appearance, request.Scaling)),
            request.Appearance.FrameColor);
    }
}
