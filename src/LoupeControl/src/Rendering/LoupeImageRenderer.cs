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

        // DrawImage は IImage を要求するため、表示境界で Avalonia WriteableBitmap へ変換する。
        // （PbBitmapAvaloniaImage の Skia 直アクセスは RenderTargetBitmap 描画で機能しない場合があるため、
        //   WriteableBitmap 変換で確実に描画する）
        using var av = image.ToAvaloniaWriteableBitmap();
        context.DrawImage(av, source, destination);
    }
}
