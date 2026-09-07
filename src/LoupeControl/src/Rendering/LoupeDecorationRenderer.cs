using Avalonia;
using Avalonia.Media;
using Estable.AvaloniaUI.Loupe.Imaging;

namespace Estable.AvaloniaUI.Loupe.Rendering;

/// <summary>ルーペの十字線・外枠を描画します。</summary>
internal static class LoupeDecorationRenderer
{
    /// <summary>十字線を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率（1〜64）。</param>
    /// <param name="color">十字線の色（不透明度は色のアルファに統合）。null の場合は描画しない。</param>
    public static void DrawCrosshair(DrawingContext context, int pixelSize, int zoomLevel, IBrush? color)
    {
        if (color is null) return;

        foreach (var rect in ComputeCrosshairRects(pixelSize, zoomLevel))
        {
            context.DrawRectangle(color, null, rect);
        }
    }

    /// <summary>十字線の 2 本の矩形を計算する。</summary>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率（1〜64）。</param>
    /// <returns>十字線の縦・横 2 本の矩形。</returns>
    /// <remarks>
    /// 切り出し領域の一辺（ブロック数）は切り出しサイズの計算を単一所有する
    /// <see cref="LoupeImageExtractor.ComputeCaptureSize"/> に委譲する。<br/>
    /// 純粋計算のため、UI 非依存の <c>[Fact]</c> で検証できる。
    /// </remarks>
    public static IReadOnlyList<Rect> ComputeCrosshairRects(int pixelSize, int zoomLevel)
    {
        var blocks = LoupeImageExtractor.ComputeCaptureSize(pixelSize, zoomLevel);
        var start = blocks / 2 * zoomLevel;
        return [new Rect(start, 0, zoomLevel, pixelSize), new Rect(0, start, pixelSize, zoomLevel)];
    }

    /// <summary>外枠を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="color">外枠の色（不透明度は色のアルファに統合）。null の場合は描画しない。</param>
    /// <param name="width">外枠の太さ（ピクセル）。1 〜 ルーペサイズに正規化される。</param>
    public static void DrawFrame(DrawingContext context, int pixelSize, IBrush? color, int width)
    {
        if (color is null) return;

        foreach (var rect in ComputeFrameRects(pixelSize, width))
        {
            context.DrawRectangle(color, null, rect);
        }
    }

    /// <summary>外枠の 4 辺の矩形を計算する。</summary>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="width">外枠の太さ（ピクセル）。1 〜 ルーペサイズに正規化される。</param>
    /// <returns>外枠の上・下・左・右の 4 つの矩形。</returns>
    /// <remarks>純粋計算のため、UI 非依存の <c>[Fact]</c> で検証できる。</remarks>
    public static IReadOnlyList<Rect> ComputeFrameRects(int pixelSize, int width)
    {
        var frame = Math.Clamp(width, 1, pixelSize);
        var inner = Math.Max(0, pixelSize - frame * 2);
        return
        [
            new Rect(0, 0, pixelSize, frame),
            new Rect(0, pixelSize - frame, pixelSize, frame),
            new Rect(0, frame, frame, inner),
            new Rect(pixelSize - frame, frame, frame, inner),
        ];
    }
}
