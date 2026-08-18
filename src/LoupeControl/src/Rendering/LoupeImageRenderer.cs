using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Estable.AvaloniaUI.Loupe.Rendering;

/// <summary>切り出し済み画像をルーペ領域へ描画します。</summary>
internal static class LoupeImageRenderer
{
    /// <summary>ソース画像をズーム倍率で拡大してルーペ領域へ描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="image">拡大元の切り出し済み画像。</param>
    /// <param name="zoomLevel">拡大率（1〜64）。</param>
    public static void Draw(DrawingContext context, Bitmap image, int zoomLevel)
    {
        var source = new Rect(0, 0, image.PixelSize.Width, image.PixelSize.Height);
        var destination = new Rect(0, 0, source.Width * zoomLevel, source.Height * zoomLevel);
        context.DrawImage(image, source, destination);
    }
}
