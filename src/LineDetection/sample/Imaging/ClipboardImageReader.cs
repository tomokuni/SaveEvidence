using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace LineDetectionSample.Imaging;

/// <summary>Win32 クリップボードから画像（CF_DIB）を読み取るユーティリティ。</summary>
/// <remarks>
/// クリップボードの標準画像形式である CF_DIB（BITMAPINFOHEADER + パレット + ピクセルデータ）を直接読み取り、<br/>
/// Avalonia の <see cref="WriteableBitmap"/>（BGRA8888）へ変換する。<br/>
/// スクリーンショットツール（Snipping Tool / PrintScreen 等）は主に CF_DIB を提供するため、<br/>
/// PNG 文字列フォーマットが無い場合のフォールバックとして使用する（Windows 専用）。
/// </remarks>
internal static class ClipboardImageReader
{
    /// <summary>CF_DIB フォーマット識別子。</summary>
    private const uint CF_DIB = 8;

    /// <summary>クリップボードを開いて CF_DIB 画像を読み取る。</summary>
    /// <returns>取得した画像。画像が無い・失敗時は null。</returns>
    /// <remarks>
    /// 1. <c>OpenClipboard</c> でクリップボードを開く<br/>
    /// 2. <c>GetClipboardData(CF_DIB)</c> で DIB を取得<br/>
    /// 3. <see cref="BitmapFromDib"/> で BITMAPINFOHEADER を解析し BGRA8888 へ変換する<br/>
    /// 4. <c>CloseClipboard</c> で必ず解放する
    /// </remarks>
    public static Bitmap? ReadImageFromDib()
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            return null;
        }

        try
        {
            var hGlobal = GetClipboardData(CF_DIB);
            if (hGlobal == IntPtr.Zero)
            {
                return null;
            }

            var ptr = GlobalLock(hGlobal);
            if (ptr == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return BitmapFromDib(ptr);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>DIB メモリ（BITMAPINFOHEADER 先頭）を解析して BGRA8888 の Bitmap を生成する。</summary>
    /// <param name="dibPtr">DIB メモリの先頭ポインター。</param>
    /// <returns>生成した Bitmap。解析できない場合は null。</returns>
    /// <remarks>
    /// BITMAPINFOHEADER（40 バイト）→ カラーマスク/パレット → ピクセルデータのレイアウトを解析し、<br/>
    /// 32/24/16/8bpp を BGRA8888 へ変換する（ボトムアップ行は上下反転して配置する）。
    /// </remarks>
    private static unsafe Bitmap? BitmapFromDib(IntPtr dibPtr)
    {
        var p = (byte*)dibPtr;
        var biSize = ReadInt32(p);
        if (biSize < 40)
        {
            return null;
        }

        var width = ReadInt32(p + 4);
        var signedHeight = ReadInt32(p + 8);
        if (width <= 0 || signedHeight == 0)
        {
            return null;
        }

        var topDown = signedHeight < 0;
        var height = Math.Abs(signedHeight);
        var planes = ReadInt16(p + 12);
        var bitCount = ReadInt16(p + 14);
        var compression = ReadInt32(p + 16);
        if (planes != 1)
        {
            return null;
        }

        // パレット/マスク領域の先頭（BITMAPINFOHEADER 直後）
        var palettePtr = p + biSize;
        var pixelPtr = palettePtr;

        // 16bpp の BI_BITFIELDS はマスク 3 つ（12 バイト）がカラーマスク
        if (compression == BI_BITFIELDS && bitCount == 16)
        {
            pixelPtr += 12;
        }
        else if (bitCount <= 8)
        {
            pixelPtr += (1 << bitCount) * 4;
        }

        var wb = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormats.Bgra8888);
        using var fb = wb.Lock();
        var dst = (byte*)fb.Address;
        var dstStride = fb.RowBytes;

        switch (bitCount)
        {
            case 32:
                Copy32Bpp(pixelPtr, dst, width, height, dstStride, topDown);
                break;
            case 24:
                Copy24Bpp(pixelPtr, dst, width, height, dstStride, topDown);
                break;
            case 16:
                Copy16Bpp(pixelPtr, dst, width, height, dstStride, topDown);
                break;
            case 8:
                Copy8Bpp(pixelPtr, (uint*)palettePtr, dst, width, height, dstStride, topDown);
                break;
            default:
                return null;
        }

        return wb;
    }

    /// <summary>32bpp（BGRA）をコピーする。</summary>
    private static unsafe void Copy32Bpp(byte* src, byte* dst, int width, int height, int dstStride, bool topDown)
    {
        for (var y = 0; y < height; y++)
        {
            var srcRow = src + y * width * 4;
            var dstRow = dst + (topDown ? y : height - 1 - y) * dstStride;
            Buffer.MemoryCopy(srcRow, dstRow, (nuint)(width * 4), (nuint)(width * 4));
        }
    }

    /// <summary>24bpp（BGR）を BGRA8888 へ展開してコピーする。</summary>
    private static unsafe void Copy24Bpp(byte* src, byte* dst, int width, int height, int dstStride, bool topDown)
    {
        for (var y = 0; y < height; y++)
        {
            var srcRow = src + y * width * 3;
            var dstRow = dst + (topDown ? y : height - 1 - y) * dstStride;
            for (var x = 0; x < width; x++)
            {
                dstRow[x * 4] = srcRow[x * 3];
                dstRow[x * 4 + 1] = srcRow[x * 3 + 1];
                dstRow[x * 4 + 2] = srcRow[x * 3 + 2];
                dstRow[x * 4 + 3] = 255;
            }
        }
    }

    /// <summary>16bpp（RGB565）を BGRA8888 へ変換してコピーする。</summary>
    private static unsafe void Copy16Bpp(byte* src, byte* dst, int width, int height, int dstStride, bool topDown)
    {
        for (var y = 0; y < height; y++)
        {
            var srcRow = src + y * width * 2;
            var dstRow = dst + (topDown ? y : height - 1 - y) * dstStride;
            for (var x = 0; x < width; x++)
            {
                var v = *(ushort*)(srcRow + x * 2);
                var r = (v >> 11) & 0x1F;
                var g = (v >> 5) & 0x3F;
                var b = v & 0x1F;
                dstRow[x * 4] = (byte)((b << 3) | (b >> 2));
                dstRow[x * 4 + 1] = (byte)((g << 2) | (g >> 4));
                dstRow[x * 4 + 2] = (byte)((r << 3) | (r >> 2));
                dstRow[x * 4 + 3] = 255;
            }
        }
    }

    /// <summary>8bpp（パレット）を BGRA8888 へ変換してコピーする。</summary>
    private static unsafe void Copy8Bpp(byte* src, uint* palette, byte* dst, int width, int height, int dstStride, bool topDown)
    {
        for (var y = 0; y < height; y++)
        {
            var srcRow = src + y * width;
            var dstRow = dst + (topDown ? y : height - 1 - y) * dstStride;
            for (var x = 0; x < width; x++)
            {
                var color = palette[srcRow[x]];
                dstRow[x * 4] = (byte)color;
                dstRow[x * 4 + 1] = (byte)(color >> 8);
                dstRow[x * 4 + 2] = (byte)(color >> 16);
                dstRow[x * 4 + 3] = 255;
            }
        }
    }

    /// <summary>ポインター位置の 32 ビット符号付き整数を読み取る。</summary>
    private static unsafe int ReadInt32(byte* p) => *(int*)p;

    /// <summary>ポインター位置の 16 ビット符号付き整数を読み取る。</summary>
    private static unsafe short ReadInt16(byte* p) => *(short*)p;

    /// <summary>BI_BITFIELDS 圧縮（16bpp のカラーマスク）。</summary>
    private const int BI_BITFIELDS = 3;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);
}
