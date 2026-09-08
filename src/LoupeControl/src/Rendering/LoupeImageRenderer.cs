using Avalonia;
using Avalonia.Media;
using Estable.AvaloniaUI.Platform.Imaging;

namespace Estable.AvaloniaUI.Loupe.Rendering;

/// <summary>切り出し済み画像をルーペ領域へ描画します。</summary>
internal static class LoupeImageRenderer
{
    /// <summary>ソース画像をズーム倍率で拡大してルーペ領域へ描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="image">拡大元の切り出し済み画像（PbBitmap）。</param>
    /// <param name="zoomLevel">拡大率（1〜64）。</param>
    public static void Draw(DrawingContext context, PbBitmap image, int zoomLevel)
    {
        var sourceSize = new Size(image.Size.Width, image.Size.Height);
        var source = new Rect(0, 0, sourceSize.Width, sourceSize.Height);
        var destination = new Rect(0, 0, source.Width * zoomLevel, source.Height * zoomLevel);

        // DrawImage は IImage を要求するため、表示境界で Avalonia Bitmap へ変換する。
        // 標準 Bitmap（内部実装 ImmutableBitmap）は Lock() がなく、WriteableBitmap 変換より高速に描画できる。
        using var av = image.ToAvaloniaBitmap();
        context.DrawImage(av, source, destination);
    }
}
