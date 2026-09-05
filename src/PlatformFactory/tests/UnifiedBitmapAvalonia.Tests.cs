using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Estable.AvaloniaUI.Platform.Imaging;
using Xunit;
using Avalonia.Headless.XUnit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary>
/// <see cref="UnifiedBitmap"/> の Avalonia Bitmap 変換テスト（ヘッドレス依存・直列実行）。
/// </summary>
/// <remarks>
/// <c>Bitmap.ToUnifiedBitmap()</c> / <c>bitmap.ToAvaloniaBitmap()</c> は
/// <c>WriteableBitmap</c> の生成・ピクセルコピーを伴うため、Avalonia ヘッドレスプラットフォームが必要。<br/>
/// そのため <see cref="AvaloniaHeadlessCollection"/> 内で直列実行する（純粋計算テストは
/// <see cref="UnifiedBitmapTests"/> で並列実行）。<br/>
/// サイズ・ストライドを主に検証する（ピクセル値はヘッドレス環境で不安定なため検証しない）。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class UnifiedBitmapAvaloniaTests
{
    /// <summary>テスト用の WriteableBitmap を生成する。</summary>
    private static WriteableBitmap CreateBitmap(int width, int height)
        => new(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

    [AvaloniaFact]
    public void ToAvaloniaBitmap_サイズが一致する()
    {
        using var bmp = UnifiedBitmap.Create(8, 8);

        var result = bmp.ToAvaloniaBitmap();

        Assert.Equal(new PixelSize(8, 8), result.PixelSize);
    }

    [AvaloniaFact]
    public void ToUnifiedBitmap_サイズが一致する()
    {
        var src = CreateBitmap(8, 8);

        using var bmp = src.ToUnifiedBitmap();

        Assert.Equal(8, bmp.PixelWidth);
        Assert.Equal(8, bmp.PixelHeight);
    }

    [AvaloniaFact]
    public void Avalonia_往復変換でサイズが一致する()
    {
        // Bitmap → UnifiedBitmap → Bitmap の往復でサイズが保持される。
        var src = CreateBitmap(12, 10);

        using var bmp = src.ToUnifiedBitmap();
        var back = bmp.ToAvaloniaBitmap();

        Assert.Equal(src.PixelSize, back.PixelSize);
    }

    [AvaloniaFact]
    public void ToUnifiedBitmap_元Bitmapは変更されない()
    {
        var src = CreateBitmap(8, 8);

        using var bmp = src.ToUnifiedBitmap();

        // 元のサイズが保持される（複製のため元は変更されない）
        Assert.Equal(new PixelSize(8, 8), src.PixelSize);
    }

    [AvaloniaFact]
    public void ToUnifiedBitmap_内部ストライドが幅4倍以上になる()
    {
        // Avalonia の WriteableBitmap.RowBytes はプラットフォーム依存で幅の4バイト以上になり得る。
        // UnifiedBitmap は幅の4バイト以上を保持し、以降のピクセル演算で正しく扱う。
        var src = CreateBitmap(7, 5);

        using var bmp = src.ToUnifiedBitmap();

        Assert.Equal(7, bmp.PixelWidth);
        Assert.Equal(5, bmp.PixelHeight);
        Assert.True(bmp.Stride >= 7 * 4);
    }
}
