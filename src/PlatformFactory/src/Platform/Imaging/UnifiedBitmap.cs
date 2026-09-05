using SkiaSharp;

namespace Estable.AvaloniaUI.Platform.Imaging;

/// <summary>複数のビットマップ種別を横断して扱う軽量なユニバーサルビットマップラッパー（コア）。</summary>
/// <remarks>
/// <b>設計</b>: 本型は内部表現（<see cref="SKBitmap"/> BGRA8888・Premul）と、ビットマップ種別に依存しない
/// ピクセル操作のみを提供する。<br/>
/// Avalonia / System.Drawing / WPF 依存の生成・変換（<c>Create</c> / <c>From～</c> / <c>To～</c>）は
/// <see cref="UnifiedBitmapExtensions"/>（<c>extension(UnifiedBitmap)</c>）が担う。<br/>
/// <br/>
/// <b>ピクセル形式</b>: BGRA8888（アルファ乗算済み、System.Drawing の Format32bppPArgb 相当）。<br/>
/// <b>所有権</b>: 内部 SKBitmap の所有権は本型が保持する。<br/>
/// <b>スレッド安全性</b>: 静的共有状態を持たないため各インスタンスは独立して並列利用できるが、
/// 単一インスタンスへの同時ピクセル操作は直列化すること（<see cref="SKBitmap"/> はスレッドセーフではない）。
/// </remarks>
public sealed class UnifiedBitmap : IDisposable
{
    /// <summary>1 ピクセルあたりのバイト数（BGRA8888）。</summary>
    private const int BytesPerPixel = 4;

    private readonly SKBitmap _bitmap;

    private bool _disposed;

    /// <summary>内部 SKBitmap からラッパーを構築する（所有権を譲り受ける）。</summary>
    /// <param name="bitmap">内部表現となる SKBitmap（BGRA8888・Premul）。所有権が本型へ移る。</param>
    internal UnifiedBitmap(SKBitmap bitmap) => _bitmap = bitmap;

    // ====================================================================
    // 生成・変換
    // ====================================================================

    /// <summary>指定サイズの空のビットマップ（BGRA8888・Premul）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。正の値。</param>
    /// <param name="height">高さ（ピクセル）。正の値。</param>
    /// <returns>生成されたビットマップ。</returns>
    /// <remarks>
    /// <c>SkiaSharp</c> のみに依存し、Avalonia / System.Drawing への依存を持たない。<br/>
    /// 本型はコア（ピクセル操作）に該当するため、拡張クラスではなく本型に内包する。
    /// </remarks>
    public static UnifiedBitmap Create(int width, int height)
        => new(new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul)));

    /// <summary>SKBitmap からラッパーを生成する（引数の所有権を譲り受ける）。</summary>
    /// <param name="bitmap">内部表現となる SKBitmap（BGRA8888・Premul）。</param>
    /// <returns>生成されたビットマップ。</returns>
    /// <remarks>
    /// 本型はコア（ピクセル操作）に該当するため、拡張クラスではなく本型に内包する。<br/>
    /// 引数はアルファ乗算済み BGRA8888 であること。<br/>
    /// 戻り値の破棄時に引数の SKBitmap も Dispose される（所有権が移る）。
    /// </remarks>
    public static UnifiedBitmap FromSKBitmap(SKBitmap bitmap) => new(bitmap);

    /// <summary>内部 SKBitmap を返す（呼び出し側で Dispose しないこと）。</summary>
    /// <returns>内部 SKBitmap（共有参照）。</returns>
    /// <remarks>
    /// 本型はコア（ピクセル操作）に該当するため、拡張クラスではなく本型に内包する。<br/>
    /// 内部インスタンスをそのまま返すため、呼び出し側で Dispose しないこと（所有権は本型が保持）。
    /// </remarks>
    public SKBitmap ToSKBitmap()
    {
        ThrowIfDisposed();
        return _bitmap;
    }

    // ====================================================================
    // 解放
    // ====================================================================

    /// <summary>内部 SKBitmap を解放する。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _bitmap.Dispose();
        _disposed = true;
    }

    /// <summary>破棄済みなら例外を投げる（拡張クラスからも利用）。</summary>
    internal void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    // ====================================================================
    // 基本プロパティ
    // ====================================================================

    /// <summary>幅（ピクセル）。</summary>
    public int PixelWidth => _bitmap.Width;

    /// <summary>高さ（ピクセル）。</summary>
    public int PixelHeight => _bitmap.Height;

    /// <summary>1 ラインあたりのバイト数（パディングを含む）。</summary>
    public int Stride => _bitmap.RowBytes;

    /// <summary>破棄済みかどうか。</summary>
    public bool IsDisposed => _disposed;

    /// <summary>内部 SKBitmap を公開する（拡張クラス専用）。</summary>
    /// <remarks>所有権は本型が保持する。呼び出し側で Dispose しないこと。</remarks>
    internal SKBitmap Bitmap => _bitmap;

    /// <summary>内部 SKBitmap のピクセルアドレスを取得する（拡張クラス専用）。</summary>
    /// <returns>ピクセルデータ先頭アドレス。</returns>
    internal nint GetPixelsAddress() => _bitmap.GetPixels();

    // ====================================================================
    // ピクセル操作
    // ====================================================================

    /// <summary>指定座標のピクセルを BGRA（アルファ乗算済み）で取得する。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <returns>(B, G, R, A) のタプル。範囲外は IndexOutOfRangeException。</returns>
    public unsafe (byte B, byte G, byte R, byte A) GetPixelBgra(int x, int y)
    {
        ThrowIfDisposed();
        var p = (byte*)_bitmap.GetPixels() + y * _bitmap.RowBytes + x * BytesPerPixel;
        return (p[0], p[1], p[2], p[3]);
    }

    /// <summary>指定座標のピクセルを BGRA（アルファ乗算済み）で設定する。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <param name="b">青成分（0〜255）。</param>
    /// <param name="g">緑成分（0〜255）。</param>
    /// <param name="r">赤成分（0〜255）。</param>
    /// <param name="a">アルファ成分（0〜255）。</param>
    public unsafe void SetPixelBgra(int x, int y, byte b, byte g, byte r, byte a)
    {
        ThrowIfDisposed();
        var p = (byte*)_bitmap.GetPixels() + y * _bitmap.RowBytes + x * BytesPerPixel;
        p[0] = b;
        p[1] = g;
        p[2] = r;
        p[3] = a;
    }

    /// <summary>指定座標のピクセルを BGRA（アルファ乗算済み）で設定する。</summary>
    /// <param name="x">X 座標（ピクセル）。</param>
    /// <param name="y">Y 座標（ピクセル）。</param>
    /// <param name="color">設定する色（BGRA、アルファ乗算済み）。</param>
    public void SetPixelBgra(int x, int y, (byte b, byte g, byte r, byte a) color)
    {
        SetPixelBgra(x, y, color.b, color.g, color.r, color.a);
    }

    /// <summary>全ピクセルを指定色（BGRA）で塗りつぶす。</summary>
    /// <param name="b">青成分（0〜255）。</param>
    /// <param name="g">緑成分（0〜255）。</param>
    /// <param name="r">赤成分（0〜255）。</param>
    /// <param name="a">アルファ成分（0〜255）。</param>
    public void Clear(byte b, byte g, byte r, byte a)
    {
        ThrowIfDisposed();
        _bitmap.Erase(new SKColor(r, g, b, a));
    }

    /// <summary>全ピクセルを指定色（BGRA）で塗りつぶす。</summary>
    /// <param name="color">塗りつぶし色（BGRA、アルファ乗算済み）。</param>
    public void Clear((byte b, byte g, byte r, byte a) color)
    {
        Clear(color.b, color.g, color.r, color.a);
    }

    /// <summary>本ビットマップの複製を生成する。</summary>
    /// <returns>独立した複製（所有権は呼び出し側）。</returns>
    public UnifiedBitmap Clone()
    {
        ThrowIfDisposed();
        return new UnifiedBitmap(_bitmap.Copy());
    }
}
