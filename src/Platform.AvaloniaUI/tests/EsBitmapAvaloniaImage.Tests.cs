using Avalonia;
using Avalonia.Media;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.SaveEvidence.Platform.AvaloniaUI.Tests;

/// <summary>
/// <see cref="EsBitmapAvaloniaImage"/> の単体テスト（EsBitmap を IImage として公開するアダプタ）。
/// </summary>
/// <remarks>
/// <see cref="EsBitmapAvaloniaImage"/> の <see cref="EsBitmapAvaloniaImage.Size"/> / <see cref="EsBitmapAvaloniaImage.Bitmap"/> /
/// <see cref="EsBitmapAvaloniaImage.Dispose"/> は <see cref="EsBitmap"/>（Skia のみ）と <see cref="Avalonia.Size"/> に依存し、
/// <c>WriteableBitmap</c> を生成しないため、Avalonia ヘッドレス初期化（<c>[AvaloniaFact]</c>）は不要。<br/>
/// <c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// <c>Draw</c>（Skia 直描画）はヘッドレスでは no-op のため、ここでは検証しない（描画は実機依存）。
/// </remarks>
public class EsBitmapAvaloniaImageTests
{
    // ====================================================================
    // Size・Bitmap 参照
    // ====================================================================

    [Fact]
    internal void Size_内包するEsBitmapのサイズと一致する()
    {
        using var bmp = EsBitmap.Create(8, 6);
        using var image = new EsBitmapAvaloniaImage(bmp);

        Assert.Equal(new Size(8, 6), image.Size);
    }

    [Fact]
    internal void Bitmap_内包する元と一致する()
    {
        using var bmp = EsBitmap.Create(8, 6);
        using var image = new EsBitmapAvaloniaImage(bmp);

        Assert.Same(bmp, image.Bitmap);
    }

    // ====================================================================
    // 所有権・破棄
    // ====================================================================

    [Fact]
    internal void Dispose後にBitmapがnullになる()
    {
        using var bmp = EsBitmap.Create(8, 6);
        var image = new EsBitmapAvaloniaImage(bmp);

        image.Dispose();

        Assert.Null(image.Bitmap);
    }

    [Fact]
    internal void Dispose後にSizeでObjectDisposedExceptionを投げる()
    {
        using var bmp = EsBitmap.Create(8, 6);
        using var image = new EsBitmapAvaloniaImage(bmp);

        image.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = image.Size);
    }

    [Fact]
    internal void Dispose後にBitmapプロパティはnullを返す()
    {
        using var bmp = EsBitmap.Create(8, 6);
        var image = new EsBitmapAvaloniaImage(bmp);
        image.Dispose();

        // 破棄後は内包 EsBitmap を解放し、Bitmap 参照は null になる
        Assert.Null(image.Bitmap);
        Assert.True(bmp.IsDisposed);
    }

    // ====================================================================
    // ToImage 拡張
    // ====================================================================

    [Fact]
    internal void ToAvaloniaImage拡張がEsBitmapAvaloniaImageを返す()
    {
        using var bmp = EsBitmap.Create(8, 6);
        using var image = (EsBitmapAvaloniaImage)bmp.ToAvaloniaImage();

        Assert.Same(bmp, image.Bitmap);
    }

    [Fact]
    internal void ToImage拡張がIImageとして扱える()
    {
        using var bmp = EsBitmap.Create(8, 6);
        IImage image = bmp.ToAvaloniaImage();

        Assert.NotNull(image);
        Assert.Equal(new Size(8, 6), image.Size);
    }
}
