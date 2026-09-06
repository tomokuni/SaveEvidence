using Avalonia;
using Avalonia.Media;
using Estable.AvaloniaUI.Platform.Imaging;
using Xunit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary>
/// <see cref="PbBitmapAvaloniaImage"/> の単体テスト（PbBitmap を IImage として公開するアダプタ）。
/// </summary>
/// <remarks>
/// <see cref="PbBitmapAvaloniaImage"/> の <see cref="PbBitmapAvaloniaImage.Size"/> / <see cref="PbBitmapAvaloniaImage.Bitmap"/> /
/// <see cref="PbBitmapAvaloniaImage.Dispose"/> は <see cref="PbBitmap"/>（Skia のみ）と <see cref="Avalonia.Size"/> に依存し、
/// <c>WriteableBitmap</c> を生成しないため、Avalonia ヘッドレス初期化（<c>[AvaloniaFact]</c>）は不要。<br/>
/// <c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// <c>Draw</c>（Skia 直描画）はヘッドレスでは no-op のため、ここでは検証しない（描画は実機依存）。
/// </remarks>
public class PbBitmapAvaloniaImageTests
{
    // ====================================================================
    // Size・Bitmap 参照
    // ====================================================================

    [Fact]
    public void Size_内包するPbBitmapのサイズと一致する()
    {
        using var bmp = PbBitmap.Create(8, 6);
        using var image = new PbBitmapAvaloniaImage(bmp);

        Assert.Equal(new Size(8, 6), image.Size);
    }

    [Fact]
    public void Bitmap_内包する元と一致する()
    {
        using var bmp = PbBitmap.Create(8, 6);
        using var image = new PbBitmapAvaloniaImage(bmp);

        Assert.Same(bmp, image.Bitmap);
    }

    // ====================================================================
    // 所有権・破棄
    // ====================================================================

    [Fact]
    public void Dispose後にBitmapがnullになる()
    {
        using var bmp = PbBitmap.Create(8, 6);
        var image = new PbBitmapAvaloniaImage(bmp);

        image.Dispose();

        Assert.Null(image.Bitmap);
    }

    [Fact]
    public void Dispose後にSizeでObjectDisposedExceptionを投げる()
    {
        using var bmp = PbBitmap.Create(8, 6);
        using var image = new PbBitmapAvaloniaImage(bmp);

        image.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = image.Size);
    }

    [Fact]
    public void Dispose後にBitmapプロパティはnullを返す()
    {
        using var bmp = PbBitmap.Create(8, 6);
        var image = new PbBitmapAvaloniaImage(bmp);
        image.Dispose();

        // 破棄後は内包 PbBitmap を解放し、Bitmap 参照は null になる
        Assert.Null(image.Bitmap);
        Assert.True(bmp.IsDisposed);
    }

    // ====================================================================
    // ToImage 拡張
    // ====================================================================

    [Fact]
    public void ToAvaloniaImage拡張がPbBitmapAvaloniaImageを返す()
    {
        using var bmp = PbBitmap.Create(8, 6);
        using var image = (PbBitmapAvaloniaImage)bmp.ToAvaloniaImage();

        Assert.Same(bmp, image.Bitmap);
    }

    [Fact]
    public void ToImage拡張がIImageとして扱える()
    {
        using var bmp = PbBitmap.Create(8, 6);
        IImage image = bmp.ToAvaloniaImage();

        Assert.NotNull(image);
        Assert.Equal(new Size(8, 6), image.Size);
    }
}
