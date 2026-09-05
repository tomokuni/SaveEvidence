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

/// <summary>
/// <see cref="UnifiedBitmap"/> の生成・変換拡張。
/// </summary>
/// <remarks>
/// <b>依存分離</b>: 本クラスは Avalonia / System.Drawing / WPF、および <see cref="AppPixelData"/> との変換を担い、
/// <see cref="UnifiedBitmap"/> 本体（コア）の依存を閉じる。<br/>
/// <see cref="UnifiedBitmap"/> はピクセル操作と <c>Create</c> / <c>FromSKBitmap</c> / <c>ToSKBitmap</c>（SkiaSharp のみに依存）を提供し、
/// 外部ビットマップ種別（Avalonia / System.Drawing）の生成・変換は本クラスへ分離する。<br/>
/// <br/>
/// <b>C# 14 拡張メンバー</b>: 静的ファクトリ（<c>UnifiedBitmap.From…</c>）は
/// <c>extension(UnifiedBitmap)</c> ブロックで静的拡張する。インスタンス変換（<c>bmp.ToAvaloniaBitmap()</c> 等）は
/// 従来の <c>this UnifiedBitmap</c> 拡張メソッドとして実装する。<br/>
/// <br/>
/// <b>所有権</b>: <c>ToAvaloniaBitmap()</c> / <c>ToSystemDrawingBitmap()</c> / <c>ToAppPixelData()</c> は複製を返す（呼び出し側が所有）。<br/>
/// <br/>
/// <b>WPF</b>: プロパティ・メソッドのみ用意し、呼ばれたら <see cref="PlatformNotSupportedException"/>（UseWPF 無効時でも使用可）。
/// </remarks>
public static class UnifiedBitmapExtensions
{
    /// <summary>1 ピクセルあたりのバイト数（BGRA8888）。</summary>
    private const int BytesPerPixel = 4;

    // ====================================================================
    // 生成（静的拡張）
    // ====================================================================

    extension(UnifiedBitmap)
    {
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
            var result = UnifiedBitmap.Create(w, h);
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
            var result = UnifiedBitmap.Create(pixelData.Width, pixelData.Height);
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
    }

    // ====================================================================
    // 変換（インスタンス拡張メソッド：this UnifiedBitmap）
    // ====================================================================

    /// <summary>画像サイズ（Avalonia PixelSize）。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <returns>Avalonia PixelSize。</returns>
    public static PixelSize Size(this UnifiedBitmap bitmap)
        => new(bitmap.PixelWidth, bitmap.PixelHeight);

    /// <summary>Avalonia Bitmap を返す（複製を所有するため安全に使用できる）。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <returns>変換された Avalonia Bitmap。</returns>
    /// <remarks>
    /// 内部 SKBitmap を複製して Avalonia に渡すため、返された Bitmap の破棄は呼び出し側が所有する。<br/>
    /// ゼロコピーで受け渡したい場合は <see cref="ToSKBitmap(UnifiedBitmap)"/> を使用すること。
    /// </remarks>
    public static unsafe Bitmap ToAvaloniaBitmap(this UnifiedBitmap bitmap)
    {
        bitmap.ThrowIfDisposed();
        var wb = new WriteableBitmap(bitmap.Size(), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var fb = wb.Lock();
        var dstStride = fb.RowBytes;
        var srcStride = bitmap.Bitmap.RowBytes;
        var rowBytes = Math.Min(srcStride, dstStride);
        var src = (byte*)bitmap.Bitmap.GetPixels();
        var dst = (byte*)fb.Address;
        for (var y = 0; y < bitmap.PixelHeight; y++)
        {
            Buffer.MemoryCopy(src + y * srcStride, dst + y * dstStride, dstStride, rowBytes);
        }
        return wb;
    }

    /// <summary>System.Drawing.Bitmap（Format32bppPArgb）を返す（複製を所有する）。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <returns>変換された System.Drawing.Bitmap。</returns>
    /// <remarks>System.Drawing は Windows 専用のため、本メソッドは Windows でのみ利用可能。</remarks>
    [SupportedOSPlatform("windows")]
    public static DrawingBitmap ToSystemDrawingBitmap(this UnifiedBitmap bitmap)
    {
        bitmap.ThrowIfDisposed();
        var sd = new DrawingBitmap(bitmap.PixelWidth, bitmap.PixelHeight, DrawingPixelFormat.Format32bppPArgb);
        var data = sd.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.PixelWidth, bitmap.PixelHeight), DrawingImageLockMode.WriteOnly, DrawingPixelFormat.Format32bppPArgb);
        try
        {
            var srcStride = bitmap.Bitmap.RowBytes;
            var dstStride = data.Stride;
            var rowBytes = Math.Min(srcStride, dstStride);
            unsafe
            {
                var src = (byte*)bitmap.Bitmap.GetPixels();
                var dst = (byte*)data.Scan0;
                for (var y = 0; y < bitmap.PixelHeight; y++)
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
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <returns>変換されたピクセルバッファ（複製）。</returns>
    public static AppPixelData ToAppPixelData(this UnifiedBitmap bitmap)
    {
        bitmap.ThrowIfDisposed();
        var stride = bitmap.PixelWidth * BytesPerPixel;
        var pixels = new byte[stride * bitmap.PixelHeight];
        var srcStride = bitmap.Bitmap.RowBytes;
        var rowBytes = Math.Min(srcStride, stride);
        unsafe
        {
            fixed (byte* p = pixels)
            {
                var src = (byte*)bitmap.Bitmap.GetPixels();
                for (var y = 0; y < bitmap.PixelHeight; y++)
                {
                    Buffer.MemoryCopy(src + y * srcStride, p + y * stride, stride, rowBytes);
                }
            }
        }
        return new AppPixelData(bitmap.PixelWidth, bitmap.PixelHeight, stride, pixels);
    }

    /// <summary>WPF の BitmapImage を生成して返す。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <returns>変換された WPF BitmapImage。</returns>
    /// <remarks>UseWPF を有効化していない構成では未実装例外を投げる（対応は将来）。</remarks>
    public static object ToWpfBitmapImage(this UnifiedBitmap bitmap)
        => throw new PlatformNotSupportedException("WPF (System.Windows.Media.Imaging) は未対応です。UseWPF を有効化してから対応してください。");

    /// <summary>全ピクセルを指定色（Avalonia Color）で塗りつぶす。</summary>
    /// <param name="bitmap">対象のビットマップ。</param>
    /// <param name="color">塗りつぶし色。アルファ乗算して書き込む。</param>
    public static void Clear(this UnifiedBitmap bitmap, AvaloniaColor color)
        => bitmap.Bitmap.Erase(Premultiply(color));

    // ====================================================================
    // 内部ヘルパー
    // ====================================================================

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
