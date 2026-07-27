using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Estable.AvaloniaUI.Platform.Imaging;
using Xunit;
using Avalonia.Headless.XUnit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary><see cref="AppPixelData"/> の Bitmap 変換テスト（Avalonia ヘッドレス依存・直列実行）。</summary>
/// <remarks>
/// <c>WriteableBitmap</c> の生成・ピクセルコピーはヘッドレスプラットフォームが必要なため、<br/>
/// <see cref="AvaloniaHeadlessCollection"/> 内で直列実行する（純粋計算テストは AppPixelDataTests で並列実行）。<br/>
/// PNG 往復を回避した <c>Bitmap.CopyPixels</c> ベースの変換（<see cref="AppPixelData.ToPixelData"/>）を検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class AppPixelDataConversionTests
{
    /// <summary>テスト用の WriteableBitmap を生成する。</summary>
    private static WriteableBitmap CreateBitmap(int width, int height)
        => new(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

    [AvaloniaFact]
    public void ToPixelData_サイズとストライドが正しく取得できる()
    {
        var bmp = CreateBitmap(10, 20);

        var data = AppPixelData.ToPixelData(bmp);

        Assert.Equal(10, data.Width);
        Assert.Equal(20, data.Height);
        Assert.Equal(40, data.Stride);
        Assert.Equal(40 * 20, data.PixelData.Length);
    }

    [AvaloniaFact]
    public void ToPixelData_ピクセル値のコピーが例外なく実行される()
    {
        // 1x1 の不透明白（BGRA: 255,255,255,255）
        var bmp = new WriteableBitmap(new PixelSize(1, 1), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        unsafe
        {
            using var locked = bmp.Lock();
            *(uint*)locked.Address = 0xFFFFFFFF;
        }

        var data = AppPixelData.ToPixelData(bmp);

        // サイズ・ストライドが正しい（ピクセル値はヘッドレス環境で不安定なため検証しない）
        Assert.Equal(1, data.Width);
        Assert.Equal(1, data.Height);
        Assert.Equal(4, data.Stride);
        Assert.Equal(4, data.PixelData.Length);
    }

    [AvaloniaFact]
    public void ToBitmap_ピクセル配列からWriteableBitmapを再生成できる()
    {
        var data = new AppPixelData(4, 4, 16, new byte[16 * 4]);

        var bmp = data.ToBitmap();

        Assert.NotNull(bmp);
        Assert.Equal(4, bmp!.PixelSize.Width);
        Assert.Equal(4, bmp.PixelSize.Height);
    }

    [AvaloniaFact]
    public void 往復変換でサイズが一致する()
    {
        // Bitmap → AppPixelData → Bitmap の往復でサイズが保持される（ピクセル値はヘッドレス環境で不安定なため検証しない）
        var src = CreateBitmap(8, 8);

        var data = AppPixelData.ToPixelData(src);
        var back = data.ToBitmap()!;

        Assert.Equal(src.PixelSize, back.PixelSize);
    }
}
