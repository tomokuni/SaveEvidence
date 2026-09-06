using Avalonia;
using Rect = Estable.Primitives.RectInt;
using Estable.AvaloniaUI.Platform.Imaging;
using SkiaSharp;
using Xunit;
using AvaloniaColor = Avalonia.Media.Color;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary>
/// <see cref="PbBitmap"/> の単体テスト。
/// </summary>
/// <remarks>
/// <see cref="PbBitmap"/> は静的共有状態を持たないため、本テストは Avalonia ヘッドレス初期化に依存しない
/// （コレクションに属さない <c>[Fact]</c> として並列実行される）。<br/>
/// Avalonia Bitmap の変換（<c>ToAvaloniaWriteableBitmap</c> / 入力側変換）は
/// ヘッドレス環境が必要なため、別テスト（<see cref="PbBitmapAvaloniaTests"/>）で検証する。
/// </remarks>
public class PbBitmapTests
{
    /// <summary>指定サイズの空のビットマップを生成する。</summary>
    private static PbBitmap Create(int w = 4, int h = 4) => PbBitmap.Create(w, h);

    // ====================================================================
    // 生成・基本プロパティ
    // ====================================================================

    [Fact]
    public void Create_指定サイズで生成する()
    {
        using var bmp = Create(6, 8);
        Assert.Equal(6, bmp.Size.Width);
        Assert.Equal(8, bmp.Size.Height);
        Assert.True(bmp.Stride >= 6 * 4);
        Assert.False(bmp.IsDisposed);
    }

    [Fact]
    public void Create_色付きで指定サイズを生成し全ピクセルが塗りつぶされる()
    {
        // 赤・不透明（BGRA=0,0,255,255）で生成し、全ピクセルが指定色になることを検証する。
        var color = System.Drawing.Color.FromArgb(255, 255, 0, 0); // A=255, R=255, G=0, B=0
        using var bmp = PbBitmap.Create(6, 8, color);

        // サイズが指定どおり
        Assert.Equal(6, bmp.Size.Width);
        Assert.Equal(8, bmp.Size.Height);

        // 全ピクセルが指定色（BGRA）になる
        var px = bmp.GetPixel(3, 4);
        Assert.Equal((byte)0, px.B);
        Assert.Equal((byte)0, px.G);
        Assert.Equal((byte)255, px.R);
        Assert.Equal((byte)255, px.A);
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
    public void SetPixel_設定したピクセルを取得できる()
    {
        using var bmp = Create();
        bmp.SetPixel(1, 2, b: 10, g: 20, r: 30, a: 255);
        var px = bmp.GetPixel(1, 2);
        Assert.Equal((byte)10, px.B);
        Assert.Equal((byte)20, px.G);
        Assert.Equal((byte)30, px.R);
        Assert.Equal((byte)255, px.A);
    }

    [Fact]
    public void GetPixel_未初期化領域のピクセルを取得できる()
    {
        using var bmp = Create();
        var px = bmp.GetPixel(0, 0);
        Assert.Equal((byte)0, px.B);
        Assert.Equal((byte)0, px.G);
        Assert.Equal((byte)0, px.R);
        Assert.Equal((byte)0, px.A);
    }

    [Fact]
    public void Clear_全ピクセルが指定色になる()
    {
        using var bmp = Create();
        bmp.Clear(b: 0, g: 0, r: 255, a: 255); // 赤・不透明
        var px = bmp.GetPixel(3, 3);
        Assert.Equal((byte)0, px.B);
        Assert.Equal((byte)0, px.G);
        Assert.Equal((byte)255, px.R);
        Assert.Equal((byte)255, px.A);
    }

    [Fact]
    public void Clear_アルファ乗算済みの色で塗りつぶす()
    {
        using var bmp = Create();
        // 50% アルファの赤（R=255, A=128）
        bmp.Clear(b: 0, g: 0, r: 255, a: 128);
        var px = bmp.GetPixel(0, 0);
        Assert.Equal((byte)128, px.A);
    }

    [Fact]
    public void Clone_独立した複製を生成する()
    {
        using var bmp = Create();
        bmp.SetPixel(0, 0, b: 1, g: 2, r: 3, a: 255);
        using var clone = bmp.Clone();
        clone.SetPixel(0, 0, b: 9, g: 8, r: 7, a: 255);
        Assert.Equal((byte)1, bmp.GetPixel(0, 0).B);
        Assert.Equal((byte)9, clone.GetPixel(0, 0).B);
    }

    // ====================================================================
    // アルファ喪失の防止（BGRA8888・Premul に統一し、alpha=0 を強制しない）
    // ====================================================================

    [Fact]
    public void SetPixel_部分透明アルファを保持する()
    {
        using var bmp = Create();
        // 50% アルファの青（BGRA: B=255, G=0, R=0, A=128）
        bmp.SetPixel(2, 3, b: 255, g: 0, r: 0, a: 128);
        var px = bmp.GetPixel(2, 3);
        Assert.Equal((byte)255, px.B);
        Assert.Equal((byte)0, px.G);
        Assert.Equal((byte)0, px.R);
        Assert.Equal((byte)128, px.A); // アルファが喪失（0 に強制）されない
    }

    [Fact]
    public void SetPixel_完全透明アルファも保持する()
    {
        using var bmp = Create();
        bmp.SetPixel(0, 0, b: 10, g: 20, r: 30, a: 0);
        var px = bmp.GetPixel(0, 0);
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
        var px = bmp.GetPixel(0, 0);
        Assert.Equal((byte)128, px.A);
    }

    [Fact]
    public void Clone_アルファを保持したまま複製する()
    {
        using var bmp = Create();
        bmp.SetPixel(0, 0, b: 1, g: 2, r: 3, a: 128);
        using var clone = bmp.Clone();
        var px = clone.GetPixel(0, 0);
        Assert.Equal((byte)1, px.B);
        Assert.Equal((byte)128, px.A);
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
        var bmp = PbBitmap.FromSKBitmap(sk);
        Assert.Equal(4, bmp.Size.Width);
        bmp.Dispose();
        Assert.True(bmp.IsDisposed);
    }

    [Fact]
    public void FromSKBitmap_部分透明アルファを保持する()
    {
        // SKBitmap に直接ピクセルを書き、PbBitmap 経由で値を検証する。
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

        using var bmp = PbBitmap.FromSKBitmap(sk);
        var px = bmp.GetPixel(0, 0);
        Assert.Equal((byte)255, px.B);
        Assert.Equal((byte)0, px.G);
        Assert.Equal((byte)0, px.R);
        Assert.Equal((byte)128, px.A);
    }

    [Fact]
    public void ヘッドレス不要でアルファ付きピクセルを往復させる()
    {
        // SKBitmap を内部に持つ PbBitmap は、Avalonia ヘッドレス初期化に依存せず
        // アルファ付きピクセルデータを SKBitmap 経由の往復で保持できることを検証する。
        using var bmp = PbBitmap.Create(3, 3);
        bmp.SetPixel(2, 1, b: 10, g: 20, r: 30, a: 64);

        using var sk = bmp.ToSKBitmap();
        using var roundTripped = PbBitmap.FromSKBitmap(sk.Copy());

        var px = roundTripped.GetPixel(2, 1);
        Assert.Equal((byte)10, px.B);
        Assert.Equal((byte)20, px.G);
        Assert.Equal((byte)30, px.R);
        Assert.Equal((byte)64, px.A);
    }

    // ====================================================================
    // その他
    // ====================================================================

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
        var px = bmp.GetPixel(0, 0);
        Assert.Equal((byte)0, px.B);
        Assert.Equal((byte)255, px.R);
        Assert.Equal((byte)255, px.A);
    }

    // ====================================================================
    // Crop（矩形切り出し）
    // ====================================================================

    [Fact]
    public void Crop_範囲内の矩形を切り出す()
    {
        using var src = Create(10, 10);
        src.Clear(b: 0, g: 0, r: 255, a: 255); // 赤

        using var result = src.Crop(new Rect(2, 2, 4, 4));

        Assert.Equal(4, result.Size.Width);
        Assert.Equal(4, result.Size.Height);
        // 切り出しピクセルが保持されている
        Assert.Equal((byte)255, result.GetPixel(0, 0).R);
    }

    [Fact]
    public void Crop_幅0の矩形は例外を投げる()
    {
        using var src = Create(10, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => src.Crop(new Rect(0, 0, 0, 4)));
    }

    [Fact]
    public void Crop_高さ0の矩形は例外を投げる()
    {
        using var src = Create(10, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => src.Crop(new Rect(0, 0, 4, 0)));
    }

    [Fact]
    public void Crop_範囲外は透明パディングで要求サイズを返す()
    {
        using var src = Create(10, 10);
        src.Clear(b: 0, g: 0, r: 255, a: 255); // 赤

        // 矩形が右端を超える場合、コピーできる部分のみコピーし、範囲外は透明（0）のまま
        using var result = src.Crop(new Rect(8, 0, 10, 10));

        Assert.Equal(10, result.Size.Width);
        Assert.Equal(10, result.Size.Height);
        // コピー可能な左側 2px 分は赤（R=255）が保持される
        Assert.Equal((byte)255, result.GetPixel(0, 0).R);
        // 範囲外（右側 8px 分）は透明（R=0）
        Assert.Equal((byte)0, result.GetPixel(9, 0).R);
    }

    [Fact]
    public void Crop_完全に範囲外でも要求サイズを透明パディングで返す()
    {
        using var src = Create(10, 10);
        src.Clear(b: 0, g: 0, r: 255, a: 255); // 赤

        // 矩形がソースと交差しない場合でも、要求サイズの全透明ビットマップを返す
        using var result = src.Crop(new Rect(20, 20, 5, 5));

        Assert.Equal(5, result.Size.Width);
        Assert.Equal(5, result.Size.Height);
        // 全ピクセル透明
        Assert.Equal((byte)0, result.GetPixel(0, 0).R);
        Assert.Equal((byte)0, result.GetPixel(4, 4).A);
    }

    [Fact]
    public void Crop_破棄後は例外を投げる()
    {
        var src = Create(10, 10);
        src.Dispose();

        Assert.Throws<ObjectDisposedException>(() => src.Crop(new Rect(0, 0, 4, 4)));
    }
}
