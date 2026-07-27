using Avalonia;
using Avalonia.Media;

namespace Estable.AvaloniaUI.Loupe.Rendering;

/// <summary>ルーペの背景を描画します。</summary>
internal static class LoupeBackgroundRenderer
{
    public static void DrawCheckerboard(DrawingContext context, int pixelSize, int checkerSize, IBrush? colorA, IBrush? colorB)
    {
        var size = Math.Max(1, checkerSize);
        var light = colorA ?? Brushes.Transparent;
        var dark = colorB ?? Brushes.Transparent;

        for (var y = 0; y < pixelSize; y += size)
        {
            for (var x = 0; x < pixelSize; x += size)
            {
                var brush = ((x / size + y / size) & 1) == 0 ? light : dark;
                context.DrawRectangle(brush, null, new Rect(x, y, size, size));
            }
        }
    }
}
