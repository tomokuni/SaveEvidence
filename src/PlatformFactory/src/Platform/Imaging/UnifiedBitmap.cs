using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingImageLockMode = System.Drawing.Imaging.ImageLockMode;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using AvaloniaColor = Avalonia.Media.Color;

namespace Estable.AvaloniaUI.Platform.Imaging;

/// <summary>複数のビットマップ種別を横断して扱う軽量なユニバーサルビットマップラッパー。</summary>
/// <remarks>
/// 内部に <see cref="SKBitmap"/>（BGRA8888・アルファ乗算済み）を唯一所有する。<br/>
/// Avalonia / System.Drawing / SkiaSharp とは 1 回コピー以下の高速な変換を行い、WPF は API のみ用意して未実装例外を投げる。<br/>
/// <br/>
/// <b>ピクセル形式</b>: BGRA8888（アルファ乗算済み、System.Drawing の Format32bppPArgb 相当）。<br/>
/// <b>所有権</b>: <see cref="FromSKBitmap"/> は引数の SKBitmap の所有権を譲り受け、本型の破棄時に Dispose する。<br/>
/// <see cref="ToSKBitmap"/> は内部インスタンスをそのまま返す（呼び出し側で Dispose しないこと）。<br/>
/// <see cref="ToAvaloniaBitmap"/> / <see cref="ToSystemDrawingBitmap"/> / <see cref="ToAppPixelData"/> は複製を返す（呼び出し側が所有）。<br/>
/// <br/>
/// <b>スレッド安全性</b>: 静的共有状態を持たないため各インスタンスは独立して並列利用できるが、<br/>
/// 単一インスタンスへの同時ピクセル操作は直列化すること（<see cref="SKBitmap"/> はスレッドセーフではない）。<br/>
/// <br/>
/// <b>置き換え方針</b>: <see cref="AppPixelData"/>（byte[] ベース）と Avalonia <see cref="Bitmap"/> を、表示境界では <see cref="ToAvaloniaBitmap"/>、
/// ピクセル演算では <see cref="ToAppPixelData"/> / <see cref="GetPixelBgra"/> / <see cref="SetPixelBgra"/> で置き換えることを想定する。
/// </remarks>
public sealed class UnifiedBitmap : IDisposable
{
    /// <summary>1 ピクセルあたりのバイト数（BGRA8888）。</summary>
    private const int BytesPerPixel = 4;

    private readonly SKBitmap _bitmap;
    private bool _disposed;

    /// <summary>内部 SKBitmap からラッパーを構築する（所有権を譲り受ける）。</summary>
    /// <param name="bitmap">内部表現となる SKBitmap（BGRA8888・Premul）。所有権が本型へ移る。</param>
    private UnifiedBitmap(SKBitmap bitmap) => _bitmap = bitmap;

    // ====================================================================
    // 生成
    // ====================================================================

    /// <summary>指定サイズの空のビットマップ（BGRA8888・Premul）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。正の値。</param>
    /// <param name="height">高さ（ピクセル）。正の値。</param>
    /// <returns>生成されたビットマップ。</returns>
    public static UnifiedBitmap Create(int width, int height)
        => new(new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul)));

    /// <summary>SKBitmap からラッパーを生成する（引数の所有権を譲り受ける）。</summary>
    /// <param name="bitmap">内部表現となる SKBitmap（BGRA8888・Premul）。</param>
    /// <returns>生成されたビットマップ。</returns>
    public static UnifiedBitmap FromSKBitmap(SKBitmap bitmap) => new(bitmap);

    /// <summary>Avalonia Bitmap からラッパーを生成する（複製するため元は変更されない）。</summary>
    /// <param name="bitmap">変換元の Avalonia Bitmap。</param>
    /// <returns>生成されたビットマップ。</returns>
    /// <remarks>
    /// <c>Bitmap.CopyPixels</c>（ネイティブ memcpy 相当）で直接読み取り、<see cref="SKBitmap"/> へコピーする。<br/>
    /// PNG エンコード/デコードや System.Drawing を経由しないため高速・可逆である。
    /// </remarks>
    public static unsafe UnifiedBitmap FromAvaloniaBitmap(Bitmap bitmap)
    {
        var w = bitmap.PixelSize.Width;
        var h = bitmap.PixelSize.Height;
        var result = Create(w, h);
        var dstStride = result.Stride;
        var srcStride = w * BytesPerPixel;
        var rowBytes = Math.Min(srcStride, dstStride);
        var dst = (byte*)result.GetPixelsAddress();
        var pixels = new byte[srcStride * h];
        fixed (byte* p = pixels)
        {
            bitmap.CopyPixels(new PixelRect(0, 0, w, h), (nint)p, srcStride * h, srcStride);
            for (var y = 0; y < h; y++)
            {
                Buffer.MemoryCopy(p + y * srcStride, dst + y * dstStride, dstStride, rowBytes);
            }
        }
        return result;
    }

    /// <summary>Avalonia 非依存ピクセルバッファからラッパーを生成する。</summary>
    /// <param name="pixelData">変換元のピクセルバッファ（BGRA8888）。</param>
    /// <returns>生成されたビットマップ。</returns>
    public static UnifiedBitmap FromAppPixelData(AppPixelData pixelData)
    {
        var result = Create(pixelData.Width, pixelData.Height);
        var src = pixelData.PixelData;
        var srcStride = pixelData.Stride;
        var dstStride = result.Stride;
        var rowBytes = Math.Min(srcStride, dstStride);
        for (var y = 0; y < pixelData.Height; y++)
        {
            Marshal.Copy(src, y * srcStride, result.GetPixelsAddress() + y * dstStride, rowBytes);
        }
        return result;
    }

    /// <summary>System.Drawing.Bitmap からラッパーを生成する（複製するため元は変更されない）。</summary>
    /// <param name="bitmap">変換元の System.Drawing.Bitmap。</param>
    /// <returns>生成されたビットマップ。</returns>
    /// <remarks>System.Drawing は Windows 専用のため、本メソッドは Windows でのみ利用可能。</remarks>
    [SupportedOSPlatform("windows")]
    public static UnifiedBitmap FromSystemDrawingBitmap(DrawingBitmap bitmap)
    {
        var w = bitmap.Width;
        var h = bitmap.Height;
        var buffer = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
        var data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, w, h), DrawingImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppPArgb);
        try
        {
            var dstStride = buffer.RowBytes;
            var srcStride = data.Stride;
            var rowBytes = Math.Min(dstStride, srcStride);
            unsafe
            {
                var dst = (byte*)buffer.GetPixels();
                var src = (byte*)data.Scan0;
                for (var y = 0; y < h; y++)
                {
                    Buffer.MemoryCopy(src + y * srcStride, dst + y * dstStride, dstStride, rowBytes);
                }
            }
            return new UnifiedBitmap(buffer);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    /// <summary>WPF の BitmapSource / BitmapImage からラッパーを生成する。</summary>
    /// <param name="source">変換元の WPF BitmapSource（object 型。UseWPF 無効時は未対応）。</param>
    /// <returns>生成されたビットマップ。</returns>
    /// <remarks>UseWPF を有効化していない構成では未実装例外を投げる（対応は将来）。</remarks>
    public static UnifiedBitmap FromWpfBitmapSource(object source)
        => throw new PlatformNotSupportedException("WPF (System.Windows.Media.Imaging) は未対応です。UseWPF を有効化してから対応してください。");

    // ====================================================================
    // 基本プロパティ
    // ====================================================================

    /// <summary>幅（ピクセル）。</summary>
    public int PixelWidth => _bitmap.Width;

    /// <summary>高さ（ピクセル）。</summary>
    public int PixelHeight => _bitmap.Height;

    /// <summary>幅（ピクセル）。System.Drawing.Bitmap.Width の代替。</summary>
    public int Width => _bitmap.Width;

    /// <summary>高さ（ピクセル）。System.Drawing.Bitmap.Height の代替。</summary>
    public int Height => _bitmap.Height;

    /// <summary>1 ラインあたりのバイト数（パディングを含む）。</summary>
    public int Stride => _bitmap.RowBytes;

    /// <summary>画像サイズ（Avalonia PixelSize）。</summary>
    public PixelSize Size => new(_bitmap.Width, _bitmap.Height);

    /// <summary>破棄済みかどうか。</summary>
    public bool IsDisposed => _disposed;

    // ====================================================================
    // 変換
    // ====================================================================

    /// <summary>内部 SKBitmap を返す（呼び出し側で Dispose しないこと）。</summary>
    /// <returns>内部 SKBitmap（共有参照）。</returns>
    public SKBitmap ToSKBitmap()
    {
        ThrowIfDisposed();
        return _bitmap;
    }

    /// <summary>Avalonia Bitmap を返す（複製を所有するため安全に使用できる）。</summary>
    /// <returns>変換された Avalonia Bitmap。</returns>
    /// <remarks>
    /// 内部 SKBitmap を複製して Avalonia に渡すため、返された Bitmap の破棄は呼び出し側が所有する。<br/>
    /// ゼロコピーで受け渡したい場合は <see cref="ToSKBitmap"/> を使用すること。
    /// </remarks>
    public unsafe Bitmap ToAvaloniaBitmap()
    {
        ThrowIfDisposed();
        var wb = new WriteableBitmap(Size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var fb = wb.Lock();
        var dstStride = fb.RowBytes;
        var srcStride = _bitmap.RowBytes;
        var rowBytes = Math.Min(srcStride, dstStride);
        var src = (byte*)_bitmap.GetPixels();
        var dst = (byte*)fb.Address;
        for (var y = 0; y < _bitmap.Height; y++)
        {
            Buffer.MemoryCopy(src + y * srcStride, dst + y * dstStride, dstStride, rowBytes);
        }
        return wb;
    }

    /// <summary>System.Drawing.Bitmap（Format32bppPArgb）を返す（複製を所有する）。</summary>
    /// <returns>変換された System.Drawing.Bitmap。</returns>
    /// <remarks>System.Drawing は Windows 専用のため、本メソッドは Windows でのみ利用可能。</remarks>
    [SupportedOSPlatform("windows")]
    public DrawingBitmap ToSystemDrawingBitmap()
    {
        ThrowIfDisposed();
        var sd = new DrawingBitmap(_bitmap.Width, _bitmap.Height, DrawingPixelFormat.Format32bppPArgb);
        var data = sd.LockBits(new System.Drawing.Rectangle(0, 0, _bitmap.Width, _bitmap.Height), DrawingImageLockMode.WriteOnly, DrawingPixelFormat.Format32bppPArgb);
        try
        {
            var srcStride = _bitmap.RowBytes;
            var dstStride = data.Stride;
            var rowBytes = Math.Min(srcStride, dstStride);
            unsafe
            {
                var src = (byte*)_bitmap.GetPixels();
                var dst = (byte*)data.Scan0;
                for (var y = 0; y < _bitmap.Height; y++)
                {
                    Buffer.MemoryCopy(src + y * srcStride, dst + y * dstStride, dstStride, rowBytes);
                }
            }
            return sd;
        }
        finally
        {
            sd.UnlockBits(data);
        }
    }

    /// <summary>Avalonia 非依存ピクセルバッファ（BGRA8888）へ変換する。</summary>
    /// <returns>変換されたピクセルバッファ（複製）。</returns>
    public AppPixelData ToAppPixelData()
    {
        ThrowIfDisposed();
        var stride = _bitmap.Width * BytesPerPixel;
        var pixels = new byte[stride * _bitmap.Height];
        var srcStride = _bitmap.RowBytes;
        var rowBytes = Math.Min(srcStride, stride);
        unsafe
        {
            fixed (byte* p = pixels)
            {
                var src = (byte*)_bitmap.GetPixels();
                for (var y = 0; y < _bitmap.Height; y++)
                {
                    Buffer.MemoryCopy(src + y * srcStride, p + y * stride, stride, rowBytes);
                }
            }
        }
        return new AppPixelData(_bitmap.Width, _bitmap.Height, stride, pixels);
    }

    /// <summary>WPF の BitmapImage を生成して返す。</summary>
    /// <returns>変換された WPF BitmapImage。</returns>
    /// <remarks>UseWPF を有効化していない構成では未実装例外を投げる（対応は将来）。</remarks>
    public object ToWpfBitmapImage()
        => throw new PlatformNotSupportedException("WPF (System.Windows.Media.Imaging) は未対応です。UseWPF を有効化してから対応してください。");

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

    /// <summary>全ピクセルを指定色（Avalonia Color）で塗りつぶす。</summary>
    /// <param name="color">塗りつぶし色。アルファ乗算して書き込む。</param>
    public void Clear(AvaloniaColor color) => _bitmap.Erase(Premultiply(color));

    /// <summary>全ピクセルを指定色（SKColor）で塗りつぶす。</summary>
    /// <param name="color">塗りつぶし色（SKColor）。入力はアルファ乗算済みであること。</param>
    public void Clear(SKColor color) => _bitmap.Erase(color);

    /// <summary>本ビットマップの複製を生成する。</summary>
    /// <returns>独立した複製（所有権は呼び出し側）。</returns>
    public UnifiedBitmap Clone()
    {
        ThrowIfDisposed();
        return new UnifiedBitmap(_bitmap.Copy());
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

    /// <summary>内部 SKBitmap のピクセルアドレスを取得する。</summary>
    /// <returns>ピクセルデータ先頭アドレス。</returns>
    private nint GetPixelsAddress() => _bitmap.GetPixels();

    /// <summary>破棄済みなら例外を投げる。</summary>
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(UnifiedBitmap));
    }

    /// <summary>Avalonia Color をアルファ乗算済み SKColor へ変換する。</summary>
    /// <param name="color">変換対象の色。</param>
    /// <returns>アルファ乗算済みの SKColor。</returns>
    private static SKColor Premultiply(AvaloniaColor color)
    {
        var a = color.A;
        if (a == 255) return new SKColor(color.R, color.G, color.B, a);
        return new SKColor(
            (byte)(color.R * a / 255),
            (byte)(color.G * a / 255),
            (byte)(color.B * a / 255),
            a);
    }
}
