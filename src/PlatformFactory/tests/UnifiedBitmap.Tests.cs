using Avalonia;
using Estable.AvaloniaUI.Platform.Imaging;
using SkiaSharp;
using Xunit;
using AvaloniaColor = Avalonia.Media.Color;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary>
/// <see cref="UnifiedBitmap"/> の単体テスト。
/// </summary>
/// <remarks>
/// <see cref="UnifiedBitmap"/> は静的共有状態を持たないため、本テストは Avalonia ヘッドレス初期化に依存しない
/// （<c>[Collection]</c> により並列実行可能）。<br/>
/// Avalonia Bitmap の変換（<see cref="UnifiedBitmap.ToAvaloniaBitmap"/> / <see cref="UnifiedBitmap.FromAvaloniaBitmap"/>）は
/// ヘッドレス環境が必要なため、別テスト（<see cref="UnifiedBitmapAvaloniaTests"/>）で検証する。
/// </remarks>
[Collection("UnifiedBitmapNonAvalonia")]
public class UnifiedBitmapTests
{
    /// <summary>指定サイズの空のビットマップを生成する。</summary>
    private static UnifiedBitmap Create(int w = 4, int h = 4) => UnifiedBitmap.Create(w, h);

    /// <summary>単色の AppPixelData（BGRA8888）を生成する。</summary>
    private static AppPixelData CreateSolidPixelData(int width, int height, byte b, byte g, byte r, byte a = 255)
    {
        var stride = width * AppPixelData.BytesPerPixel;
        var pixels = new byte[stride * height];
        for (var i = 0; i < width * height; i++)
        {
            pixels[i * 4] = b;
            pixels[i * 4 + 1] = g;
            pixels[i * 4 + 2] = r;
            pixels[i * 4 + 3] = a;
        }
        return new AppPixelData(width, height, stride, pixels);
    }

    // ====================================================================
    // 生成・基本プロパティ
    // ====================================================================

    [Fact]
    public void Create_指定サイズで生成する()
    {
        using var bmp = Create(6, 8);
        Assert.Equal(6, bmp.PixelWidth);
        Assert.Equal(8, bmp.PixelHeight);
        Assert.True(bmp.Stride >= 6 * 4);
        Assert.False(bmp.IsDisposed);
    }

    [Fact]
    public void Create_破棄後はIsDisposedが真になる()
    {
        var bmp = Create();
        bmp.Dispose();
        Assert.True(bmp.IsDisposed);
    }

    [Fact]
    public void Dispose_二重破棄しても例外にならない()
    {
        var bmp = Create();
        bmp.Dispose();
        bmp.Dispose();
        Assert.True(bmp.IsDisposed);
    }

    // ====================================================================
    // ピクセル操作
    // ====================================================================

    [Fact]
    public void SetPixelBgra_設定したピクセルを取得できる()
    {
        using var bmp = Create();
        bmp.SetPixelBgra(1, 2, b: 10, g: 20, r: 30, a: 255);
        var px = bmp.GetPixelBgra(1, 2);
        Assert.Equal((byte)10, px.B);
        Assert.Equal((byte)20, px.G);
        Assert.Equal((byte)30, px.R);
        Assert.Equal((byte)255, px.A);
    }

    [Fact]
    public void GetPixelBgra_未初期化領域のピクセルを取得できる()
    {
        using var bmp = Create();
        var px = bmp.GetPixelBgra(0, 0);
        Assert.Equal((byte)0, px.B);
        Assert.Equal((byte)0, px.G);
        Assert.Equal((byte)0, px.R);
        Assert.Equal((byte)0, px.A);
    }

    [Fact]
    public void Clear_全ピクセルが指定色になる()
    {
        using var bmp = Create();
        bmp.Clear(new SKColor(255, 0, 0, 255)); // 赤・不透明
        var px = bmp.GetPixelBgra(3, 3);
        Assert.Equal((byte)0, px.B);
        Assert.Equal((byte)0, px.G);
        Assert.Equal((byte)255, px.R);
        Assert.Equal((byte)255, px.A);
    }

    [Fact]
    public void Clear_アルファ乗算済みの色で塗りつぶす()
    {
        using var bmp = Create();
        // 50% アルファの赤（SRGB で R=255*A=128 → 乗算後 128）
        bmp.Clear(new SKColor(255, 0, 0, 128));
        var px = bmp.GetPixelBgra(0, 0);
        Assert.Equal((byte)128, px.A);
    }

    [Fact]
    public void Clone_独立した複製を生成する()
    {
        using var bmp = Create();
        bmp.SetPixelBgra(0, 0, b: 1, g: 2, r: 3, a: 255);
        using var clone = bmp.Clone();
        clone.SetPixelBgra(0, 0, b: 9, g: 8, r: 7, a: 255);
        Assert.Equal((byte)1, bmp.GetPixelBgra(0, 0).B);
        Assert.Equal((byte)9, clone.GetPixelBgra(0, 0).B);
    }

    // ====================================================================
    // アルファ喪失の防止（BGRA8888・Premul に統一し、alpha=0 を強制しない）
    // ====================================================================

    [Fact]
    public void SetPixelBgra_部分透明アルファを保持する()
    {
        using var bmp = Create();
        // 50% アルファの青（BGRA: B=255, G=0, R=0, A=128）
        bmp.SetPixelBgra(2, 3, b: 255, g: 0, r: 0, a: 128);
        var px = bmp.GetPixelBgra(2, 3);
        Assert.Equal((byte)255, px.B);
        Assert.Equal((byte)0, px.G);
        Assert.Equal((byte)0, px.R);
        Assert.Equal((byte)128, px.A); // アルファが喪失（0 に強制）されない
    }

    [Fact]
    public void SetPixelBgra_完全透明アルファも保持する()
    {
        using var bmp = Create();
        bmp.SetPixelBgra(0, 0, b: 10, g: 20, r: 30, a: 0);
        var px = bmp.GetPixelBgra(0, 0);
        Assert.Equal((byte)0, px.A);
        Assert.Equal((byte)10, px.B);
        Assert.Equal((byte)20, px.G);
        Assert.Equal((byte)30, px.R);
    }

    [Fact]
    public void Clear_部分透明アルファを保持する()
    {
        using var bmp = Create();
        // 50% アルファの赤（Avalonia Color は乗算して SKColor へ変換される）
        bmp.Clear(new AvaloniaColor(128, 255, 0, 0));
        var px = bmp.GetPixelBgra(0, 0);
        Assert.Equal((byte)128, px.A);
    }

    [Fact]
    public void FromAppPixelData_アルファを保持したままコピーする()
    {
        // 部分透明（alpha=128）の BGRA をソースにし、アルファが喪失しないことを検証
        var src = CreateSolidPixelData(4, 4, 255, 0, 0, 128);
        using var bmp = UnifiedBitmap.FromAppPixelData(src);
        var px = bmp.GetPixelBgra(1, 1);
        Assert.Equal((byte)128, px.A);
        Assert.Equal((byte)255, px.B);
    }

    [Fact]
    public void ToAppPixelData_アルファを保持したままエクスポートする()
    {
        using var bmp = Create();
        bmp.SetPixelBgra(0, 0, b: 1, g: 2, r: 3, a: 128);
        var data = bmp.ToAppPixelData();
        Assert.Equal((byte)1, data.PixelData[0]);
        Assert.Equal((byte)2, data.PixelData[1]);
        Assert.Equal((byte)3, data.PixelData[2]);
        Assert.Equal((byte)128, data.PixelData[3]); // アルファ保持
    }

    [Fact]
    public void Clone_アルファを保持したまま複製する()
    {
        using var bmp = Create();
        bmp.SetPixelBgra(0, 0, b: 1, g: 2, r: 3, a: 128);
        using var clone = bmp.Clone();
        var px = clone.GetPixelBgra(0, 0);
        Assert.Equal((byte)1, px.B);
        Assert.Equal((byte)128, px.A);
    }

    // ====================================================================
    // ストライド差の吸収（パディングがあるソースを行ずれなくコピー）
    // ====================================================================

    [Fact]
    public void FromAppPixelData_パディング付きストライドを行ずれなくコピーする()
    {
        // 幅 3 ピクセル（12 バイト）に対し、ストライド 16 バイト（4 バイト余分なパディング）を付与
        var width = 3;
        var height = 2;
        var stride = 16; // 3*4=12 < 16 のパディング付き
        var pixels = new byte[stride * height];

        // 行 0: 各ピクセルに色を割り当て、パディング部分は別の値にする
        // ピクセル (x, y) の BGRA を (100+x, 0, 0, 255) にする
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var off = y * stride + x * 4;
                pixels[off] = (byte)(100 + x);       // B
                pixels[off + 1] = (byte)y;           // G
                pixels[off + 2] = (byte)0;           // R
                pixels[off + 3] = 255;               // A
            }
            // パディングをマーカー値で埋める（コピーに混入しないことを確認）
            for (var p = width * 4; p < stride; p++)
            {
                pixels[y * stride + p] = (byte)0xEE;
            }
        }

        var src = new AppPixelData(width, height, stride, pixels);
        using var bmp = UnifiedBitmap.FromAppPixelData(src);

        // 幅・高さは正しく、パディングを除いたピクセル値のみが反映される
        Assert.Equal(width, bmp.PixelWidth);
        Assert.Equal(height, bmp.PixelHeight);
        Assert.Equal((byte)100, bmp.GetPixelBgra(0, 0).B);
        Assert.Equal((byte)101, bmp.GetPixelBgra(1, 0).B);
        Assert.Equal((byte)102, bmp.GetPixelBgra(2, 0).B);
        // 行 1（y=1）: G=1 であることを確認（行ずれしていない）
        Assert.Equal((byte)1, bmp.GetPixelBgra(0, 1).G);
        Assert.Equal((byte)1, bmp.GetPixelBgra(1, 1).G);
        Assert.Equal((byte)1, bmp.GetPixelBgra(2, 1).G);
    }

    [Fact]
    public void ToAppPixelData_内部ストライドが幅と異なっても行ずれしない()
    {
        // SKBitmap は内部でパディングを持つ場合がある
        using var bmp = Create(3, 2);
        bmp.SetPixelBgra(0, 0, b: 1, g: 2, r: 3, a: 255);
        bmp.SetPixelBgra(0, 1, b: 4, g: 5, r: 6, a: 255);

        var data = bmp.ToAppPixelData();
        Assert.Equal(3, data.Width);
        Assert.Equal(2, data.Height);
        Assert.Equal(3 * 4, data.Stride); // 出力は幅×4 に正規化される
        // 行 0 のピクセル値
        Assert.Equal((byte)1, data.PixelData[0]);
        // 行 1 のピクセル値（= stride 分のオフセット）
        Assert.Equal((byte)4, data.PixelData[data.Stride]);
    }

    // ====================================================================
    // SKBitmap 変換
    // ====================================================================

    [Fact]
    public void ToSKBitmap_内部インスタンスを返す()
    {
        using var bmp = Create();
        var sk = bmp.ToSKBitmap();
        Assert.NotNull(sk);
        Assert.Equal(SKColorType.Bgra8888, sk.ColorType);
        Assert.Equal(SKAlphaType.Premul, sk.AlphaType);
    }

    [Fact]
    public void FromSKBitmap_所有権を譲り受け破棄に連動する()
    {
        var sk = new SKBitmap(new SKImageInfo(4, 4, SKColorType.Bgra8888, SKAlphaType.Premul));
        var bmp = UnifiedBitmap.FromSKBitmap(sk);
        Assert.Equal(4, bmp.PixelWidth);
        bmp.Dispose();
        Assert.True(bmp.IsDisposed);
    }

    [Fact]
    public void FromSKBitmap_部分透明アルファを保持する()
    {
        // SKBitmap に直接ピクセルを書き、UnifiedBitmap 経由で値を検証する。
        // （内部が SKBitmap のため、Avalonia ヘッドレス初期化に依存せず検証できる）
        var sk = new SKBitmap(new SKImageInfo(4, 4, SKColorType.Bgra8888, SKAlphaType.Premul));
        unsafe
        {
            var ptr = (byte*)sk.GetPixels();
            // 50% アルファの青
            ptr[0] = 255; // B
            ptr[1] = 0;   // G
            ptr[2] = 0;   // R
            ptr[3] = 128; // A
        }

        using var bmp = UnifiedBitmap.FromSKBitmap(sk);
        var px = bmp.GetPixelBgra(0, 0);
        Assert.Equal((byte)255, px.B);
        Assert.Equal((byte)0, px.G);
        Assert.Equal((byte)0, px.R);
        Assert.Equal((byte)128, px.A);
    }

    [Fact]
    public void ヘッドレス不要でアルファ付きピクセルを往復させる()
    {
        // SKBitmap を内部に持つ UnifiedBitmap は、Avalonia ヘッドレス初期化に依存せず
        // アルファ付きピクセルデータを SKBitmap → AppPixelData の往復で保持できることを検証する。
        using var bmp = UnifiedBitmap.Create(3, 3);
        bmp.SetPixelBgra(2, 1, b: 10, g: 20, r: 30, a: 64);

        var data = bmp.ToAppPixelData();
        using var fromData = UnifiedBitmap.FromAppPixelData(data);

        var px = fromData.GetPixelBgra(2, 1);
        Assert.Equal((byte)10, px.B);
        Assert.Equal((byte)20, px.G);
        Assert.Equal((byte)30, px.R);
        Assert.Equal((byte)64, px.A);
    }

    [Fact]
    public void FromAppPixelData_ピクセルをコピーする()
    {
        var src = CreateSolidPixelData(2, 2, 0xFF, 0x00, 0x00);
        using var bmp = UnifiedBitmap.FromAppPixelData(src);
        Assert.Equal(src.Width, bmp.PixelWidth);
        Assert.Equal(src.Height, bmp.PixelHeight);
        var px = bmp.GetPixelBgra(0, 0);
        Assert.Equal((byte)255, px.B);
        Assert.Equal((byte)255, px.A);
    }

    [Fact]
    public void ToAppPixelData_ピクセルをエクスポートする()
    {
        using var bmp = Create();
        bmp.SetPixelBgra(0, 0, b: 1, g: 2, r: 3, a: 255);
        var data = bmp.ToAppPixelData();
        Assert.Equal(bmp.PixelWidth, data.Width);
        Assert.Equal(bmp.PixelHeight, data.Height);
        Assert.Equal((byte)1, data.PixelData[0]);
    }

    // ====================================================================
    // WPF（未対応）・その他
    // ====================================================================

    [Fact]
    public void FromWpfBitmapSource_未対応なら例外を投げる()
            => Assert.Throws<PlatformNotSupportedException>(() => UnifiedBitmap.FromWpfBitmapSource(new object()));

    [Fact]
    public void ToWpfBitmapImage_未対応なら例外を投げる()
    {
        using var bmp = Create();
        Assert.Throws<PlatformNotSupportedException>(() => bmp.ToWpfBitmapImage());
    }

    [Fact]
    public void Size_ピクセルサイズを返す()
    {
        using var bmp = Create(6, 8);
        Assert.Equal(new PixelSize(6, 8), bmp.Size());
    }

    [Fact]
    public void Clear_AvaloniaColorで塗りつぶす()
    {
        using var bmp = Create();
        bmp.Clear(new AvaloniaColor(255, 255, 0, 0)); // 赤・不透明
        var px = bmp.GetPixelBgra(0, 0);
        Assert.Equal((byte)0, px.B);
        Assert.Equal((byte)255, px.R);
        Assert.Equal((byte)255, px.A);
    }
}
