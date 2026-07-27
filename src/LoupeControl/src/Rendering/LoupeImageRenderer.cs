using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Estable.AvaloniaUI.Loupe.Rendering;

/// <summary>切り出し済み画像をルーペ領域へ描画します。</summary>
internal static class LoupeImageRenderer
{
    public static void Draw(DrawingContext context, Bitmap image, int zoomLevel)
    {
        var source = new Rect(0, 0, image.PixelSize.Width, image.PixelSize.Height);
        var destination = new Rect(0, 0, source.Width * zoomLevel, source.Height * zoomLevel);
        context.DrawImage(image, source, destination);
    }
}
