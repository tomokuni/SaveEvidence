using Estable.Loupe;
using Estable.Loupe.Imaging;
using Estable.Platform.Imaging;
using SkiaSharp;
using MewUIColor = Aprillz.MewUI.Color;

namespace Estable.MewUI.Loupe;

/// <summary>ルーペの描画（市松模様背景 + 拡大画像 + 十字線 + 外枠）を Skia で行うレンダラー。</summary>
/// <remarks>
/// UI 非依存コア（<see cref="LoupeImageExtractor"/> / <see cref="LoupeDecorationGeometry"/> /<br/>
/// <see cref="LoupeCheckerboard"/>）の計算結果を <see cref="SKCanvas"/> へ描画するだけの層。<br/>
/// Avalonia 版は中間ビットマップ（<c>RenderTargetBitmap</c>）を経由するが、MewUI 版は<br/>
/// 画面のキャンバスへ直接描画できるため、中間バッファの生成とコピーを行わない。<br/>
/// 矩形計算はコアが単一所有するため、両 UI の見た目は一致する（Avalonia 版のレンダラーと等価）。
/// </remarks>
internal static class LoupeSkiaRenderer
{
    /// <summary>ルーペを描画する（市松模様背景 + 拡大画像 + 十字線 + 外枠）。</summary>
    /// <param name="canvas">描画先のキャンバス（左上を原点とする）。</param>
    /// <param name="source">拡大元の 100% 画像。null の場合は市松模様のみ。</param>
    /// <param name="center">拡大中心（元画像ピクセル座標）。null の場合は市松模様のみ。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率（1〜64）。</param>
    /// <param name="crossColor">十字線の色（不透明度は色のアルファに統合）。null の場合は描画しない。</param>
    /// <param name="checkerSize">市松模様の 1 タイルサイズ（物理ピクセル）。1 未満は 1 に正規化される。</param>
    /// <param name="checkerColorA">市松模様の色A（明色）。null の場合は透明。</param>
    /// <param name="checkerColorB">市松模様の色B（暗色）。null の場合は透明。</param>
    /// <param name="frameColor">外枠の色。null の場合は描画しない。</param>
    /// <param name="frameWidth">外枠の太さ（物理ピクセル）。1〜ルーペサイズに正規化される。</param>
    /// <remarks>
    /// 1. 市松模様の背景を描画する。<br/>
    /// 2. ソース画像があれば切り出して拡大描画する（切り出しはコアへ委譲。範囲外は透明パディング）。<br/>
    /// 3. 十字線を描画する。<br/>
    /// 4. 外枠を描画する。
    /// </remarks>
    public static void Render(
        SKCanvas canvas,
        EsBitmap? source,
        Estable.Platform.Geometry.EsPoint? center,
        int pixelSize,
        int zoomLevel,
        MewUIColor? crossColor,
        int checkerSize,
        MewUIColor? checkerColorA,
        MewUIColor? checkerColorB,
        MewUIColor? frameColor,
        int frameWidth)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        DrawCheckerboard(canvas, pixelSize, checkerSize, checkerColorA, checkerColorB);
        DrawMagnified(canvas, source, center, pixelSize, zoomLevel);
        DrawCrosshair(canvas, pixelSize, zoomLevel, crossColor);
        DrawFrame(canvas, pixelSize, frameColor, frameWidth);
    }

    /// <summary>市松模様の背景を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="checkerSize">1 タイルサイズ（物理ピクセル）。1 未満は 1 に正規化される。</param>
    /// <param name="colorA">色A（明色）。null の場合は透明。</param>
    /// <param name="colorB">色B（暗色）。null の場合は透明。</param>
    /// <remarks>
    /// 明色で全面を塗った後、明暗判定（<see cref="LoupeCheckerboard.IsDarkTile"/>）が真のタイルのみを<br/>
    /// 暗色で上書きする（描画回数を半減）。明色が透明の場合は全面塗りを省略する。
    /// </remarks>
    private static void DrawCheckerboard(SKCanvas canvas, int pixelSize, int checkerSize, MewUIColor? colorA, MewUIColor? colorB)
    {
        var size = Math.Max(1, checkerSize);

        using var paint = new SKPaint { IsAntialias = false };

        // 色A（明色）で全面を塗る（暗色タイルの下地）。透明の場合は上書きされないため省略する
        if (colorA is { } light && light.A > 0)
        {
            paint.Color = ToSKColor(light);
            canvas.DrawRect(new SKRect(0, 0, pixelSize, pixelSize), paint);
        }

        if (colorB is not { } dark || dark.A == 0)
        {
            return;
        }

        paint.Color = ToSKColor(dark);

        var tiles = (int)Math.Ceiling((double)pixelSize / size);
        for (var row = 0; row < tiles; row++)
        {
            var y = row * size;
            for (var col = 0; col < tiles; col++)
            {
                if (LoupeCheckerboard.IsDarkTile(col, row))
                {
                    canvas.DrawRect(new SKRect(col * size, y, col * size + size, y + size), paint);
                }
            }
        }
    }

    /// <summary>ソース画像を切り出して拡大描画する（ソース／中心が未設定なら何もしない）。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="source">拡大元の画像。</param>
    /// <param name="center">拡大中心（元画像ピクセル座標）。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <remarks>
    /// 切り出しは <see cref="LoupeImageExtractor.ExtractRegionAt"/> へ委譲する（中心 → 左上の変換を単一所有）。<br/>
    /// 破棄済みのソースは市松模様のみを残して描画を打ち切る（ホストの状態と競合しても例外にしない）。<br/>
    /// <c>ToSKBitmap()</c> は内部インスタンスの共有参照を返すため破棄しない。
    /// </remarks>
    private static void DrawMagnified(
        SKCanvas canvas,
        EsBitmap? source,
        Estable.Platform.Geometry.EsPoint? center,
        int pixelSize,
        int zoomLevel)
    {
        if (source is null || center is not { } origin)
        {
            return;
        }

        var captureSize = LoupeImageExtractor.ComputeCaptureSize(pixelSize, zoomLevel);

        EsBitmap? region = null;
        try
        {
            region = LoupeImageExtractor.ExtractRegionAt(source, (int)origin.X, (int)origin.Y, captureSize);
        }
        catch (ObjectDisposedException)
        {
            // 描画中にソースが破棄された: 市松模様のみを残す
            return;
        }

        using (region)
        {
            var bitmap = region.ToSKBitmap();
            var src = new SKRect(0, 0, region.Size.Width, region.Size.Height);
            var dst = new SKRect(0, 0, src.Width * zoomLevel, src.Height * zoomLevel);

            // 拡大は最近傍（既定）で行い、画素をそのまま拡大表示する
            canvas.DrawBitmap(bitmap, src, dst);
        }
    }

    /// <summary>十字線を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <param name="color">十字線の色。null の場合は描画しない。</param>
    private static void DrawCrosshair(SKCanvas canvas, int pixelSize, int zoomLevel, MewUIColor? color)
    {
        if (color is not { } cross)
        {
            return;
        }

        FillRects(canvas, LoupeDecorationGeometry.ComputeCrosshairRects(pixelSize, zoomLevel), ToSKColor(cross));
    }

    /// <summary>外枠を描画する。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="color">外枠の色。null の場合は描画しない。</param>
    /// <param name="width">外枠の太さ（物理ピクセル）。</param>
    private static void DrawFrame(SKCanvas canvas, int pixelSize, MewUIColor? color, int width)
    {
        if (color is not { } frame)
        {
            return;
        }

        FillRects(canvas, LoupeDecorationGeometry.ComputeFrameRects(pixelSize, width), ToSKColor(frame));
    }

    /// <summary>矩形の一覧を単色で塗りつぶす。</summary>
    /// <param name="canvas">描画先のキャンバス。</param>
    /// <param name="rects">塗りつぶす矩形（コア座標）。</param>
    /// <param name="color">塗りつぶしの色。</param>
    private static void FillRects(SKCanvas canvas, IReadOnlyList<Estable.Platform.Geometry.EsRect> rects, SKColor color)
    {
        using var paint = new SKPaint { IsAntialias = false, Color = color, Style = SKPaintStyle.Fill };

        foreach (var rect in rects)
        {
            canvas.DrawRect(new SKRect((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom), paint);
        }
    }

    /// <summary>MewUI の色を Skia の色へ変換する。</summary>
    /// <param name="color">変換元の色。</param>
    /// <returns>Skia の色。</returns>
    private static SKColor ToSKColor(MewUIColor color) => new(color.R, color.G, color.B, color.A);
}
