using Avalonia;
using Avalonia.Media;

namespace Estable.AvaloniaUI.Loupe.Rendering;

/// <summary>ルーペの背景（市松模様）を描画します。</summary>
internal static class LoupeBackgroundRenderer
{
    /// <summary>市松模様の背景を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="checkerSize">市松模様の 1 タイルサイズ（ピクセル）。1 未満は 1 に正規化される。</param>
    /// <param name="colorA">色A（明色）。null の場合は透明。</param>
    /// <param name="colorB">色B（暗色）。null の場合は透明。</param>
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
