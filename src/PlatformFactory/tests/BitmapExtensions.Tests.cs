using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Estable.AvaloniaUI.Platform.Imaging;
using Xunit;
using Avalonia.Headless.XUnit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary>
/// <see cref="BitmapExtensions"/> の単体テスト（実行・サイズ検証。ピクセル値はヘッドレス環境で不安定なため検証しない）。
/// </summary>
[Collection("AvaloniaHeadless")]
public class BitmapExtensionsTests
{
    /// <summary>テスト用 Bitmap を生成する。</summary>
    private static WriteableBitmap CreateBitmap(int width, int height)
    {
        return new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
    }

    [AvaloniaFact]
    public void CopyRegionWithPadding_範囲内を例外なくコピーする()
    {
        var src = CreateBitmap(8, 8);

        var dst = new byte[4 * 4 * 4];
        unsafe
        {
            fixed (byte* p = dst)
            {
                src.CopyRegionWithPadding(new PixelRect(2, 2, 4, 4), (IntPtr)p, 16, dst.Length, 0, 0);
            }
        }
        // 例外なく実行されたことを確認
        Assert.True(true);
    }

    [AvaloniaFact]
    public void CopyRegionWithPadding_完全に範囲外なら何もコピーしない()
    {
        var src = CreateBitmap(8, 8);

        var dst = new byte[4 * 4 * 4];
        unsafe
        {
            fixed (byte* p = dst)
            {
                // ソース範囲外（-10,-10 から 4x4）→ interW/H <= 0 で早期 return
                src.CopyRegionWithPadding(new PixelRect(-10, -10, 4, 4), (IntPtr)p, 16, dst.Length, 0, 0);
            }
        }
        // 例外なく実行されたことを確認（バッファはゼロのまま）
        Assert.All(dst, b => Assert.Equal(0, b));
    }

    [AvaloniaFact]
    public void ExtractRegion_指定矩形を切り出す()
    {
        var src = CreateBitmap(16, 16);

        var result = src.ExtractRegion(new PixelRect(2, 3, 4, 5));

        Assert.NotNull(result);
        Assert.Equal(new PixelSize(4, 5), result.PixelSize);
    }

    [AvaloniaFact]
    public void ExtractRegion_範囲外を透明パディングで切り出す()
    {
        var src = CreateBitmap(8, 8);

        // ソースをはみ出す矩形（端でクリップ + 透明パディング）
        var result = src.ExtractRegion(new PixelRect(6, 6, 4, 4));

        Assert.NotNull(result);
        Assert.Equal(new PixelSize(4, 4), result.PixelSize);
    }

    [AvaloniaFact]
    public void ExtractRegion_サイズ0以下の矩形は例外を投げる()
    {
        var src = CreateBitmap(8, 8);

        Assert.Throws<ArgumentOutOfRangeException>(() => src.ExtractRegion(new PixelRect(0, 0, 0, 4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => src.ExtractRegion(new PixelRect(0, 0, 4, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => src.ExtractRegion(new PixelRect(0, 0, -1, 4)));
    }
}
