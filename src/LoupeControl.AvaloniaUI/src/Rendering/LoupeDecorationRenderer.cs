using Avalonia;
using Avalonia.Media;
using Estable.AvaloniaUI.Platform;
using Estable.Loupe;
using Estable.Platform.Geometry;

namespace Estable.AvaloniaUI.Loupe.Rendering;

/// <summary>ルーペの十字線・外枠を描画します。</summary>
/// <remarks>
/// 矩形計算は UI 非依存の <see cref="LoupeDecorationGeometry"/> が単一所有し、<br/>
/// 本クラスは <see cref="EsRect"/> を Avalonia の矩形へ変換して描画するだけにする（MewUI と共用）。
/// </remarks>
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
    /// <returns>十字線の縦・横 2 本の矩形（Avalonia 座標）。</returns>
    public static IReadOnlyList<Rect> ComputeCrosshairRects(int pixelSize, int zoomLevel)
        => [.. LoupeDecorationGeometry.ComputeCrosshairRects(pixelSize, zoomLevel).Select(static r => r.ToAvaloniaRect())];

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
    /// <returns>外枠の上・下・左・右の 4 つの矩形（Avalonia 座標）。</returns>
    public static IReadOnlyList<Rect> ComputeFrameRects(int pixelSize, int width)
        => [.. LoupeDecorationGeometry.ComputeFrameRects(pixelSize, width).Select(static r => r.ToAvaloniaRect())];
}
