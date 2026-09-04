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

        // 切り出し領域の一辺（ブロック数）は切り出しサイズの計算を単一所有する LoupeImageExtractor に委譲する
        var blocks = LoupeImageExtractor.ComputeCaptureSize(pixelSize, zoomLevel);
        var start = blocks / 2 * zoomLevel;
        // 不透明度は color（ブラシ）のアルファに統合済み。そのまま描画する。
        context.DrawRectangle(color, null, new Rect(start, 0, zoomLevel, pixelSize));
        context.DrawRectangle(color, null, new Rect(0, start, pixelSize, zoomLevel));
    }

    /// <summary>外枠を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="color">外枠の色（不透明度は色のアルファに統合）。null の場合は描画しない。</param>
    /// <param name="width">外枠の太さ（ピクセル）。1 〜 ルーペサイズに正規化される。</param>
    public static void DrawFrame(DrawingContext context, int pixelSize, IBrush? color, int width)
    {
        if (color is null) return;

        var frame = Math.Clamp(width, 1, pixelSize);
        var inner = Math.Max(0, pixelSize - frame * 2);
        context.DrawRectangle(color, null, new Rect(0, 0, pixelSize, frame));
        context.DrawRectangle(color, null, new Rect(0, pixelSize - frame, pixelSize, frame));
        context.DrawRectangle(color, null, new Rect(0, frame, frame, inner));
        context.DrawRectangle(color, null, new Rect(pixelSize - frame, frame, frame, inner));
    }
}
