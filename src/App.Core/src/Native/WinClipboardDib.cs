using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core.Native.Win;

namespace EsUtil.SaveEvidence.App.Core.Native;

/// <summary>クリップボードとの画像（<c>CF_DIB</c>）の受け渡しを単一所有する（Windows 実装）。</summary>
/// <remarks>
/// <b>クリップボードの開閉は EsUtil が単一所有する</b>（<see cref="WinClipboard.TryReadDib"/>。 解放漏れを作らない）。<br/>
/// <b>内容の解釈（BITMAPINFOHEADER の解析）は本クラスが行う</b>（32 / 24 / 16 / 8 bpp・上下反転の有無）。<br/>
/// <b>書き込みは本クラスが行う</b>（読み取りの API しか無いため、<c>SetClipboardData</c> を直接呼ぶ）。<br/>
/// 失敗は例外にせず戻り値で表す（他アプリがクリップボードを使用中の場合がある）。
/// </remarks>
internal static class WinClipboardDib
{
    /// <summary>クリップボードの DIB 形式（<c>CF_DIB</c>）。</summary>
    private const uint CF_DIB = 8;

    /// <summary>BITMAPINFOHEADER のサイズ（バイト）。</summary>
    private const int HeaderSize = 40;

    /// <summary>圧縮なし（<c>BI_RGB</c>）。</summary>
    private const uint BI_RGB = 0;

    /// <summary>カラー マスク付き（<c>BI_BITFIELDS</c>）。</summary>
    private const uint BI_BITFIELDS = 3;

    /// <summary>アルファ マスク付き（<c>BI_ALPHABITFIELDS</c>）。</summary>
    private const uint BI_ALPHABITFIELDS = 6;

    /// <summary>移動可能なメモリとして確保する（<c>SetClipboardData</c> の要件）。</summary>
    private const uint GMEM_MOVEABLE = 0x0002;

    /// <summary>クリップボードを開閉する（実装は EsUtil が単一所有する）。</summary>
    private static readonly WinClipboard s_clipboard = new();

    /// <summary>クリップボードの画像を読み取る。</summary>
    /// <returns>読み取った画像。 画像が無い・解析できない・他アプリが使用中の場合は null。</returns>
    public static EsBitmap? TryRead()
    {
        EsBitmap? result = null;
        var read = s_clipboard.TryReadDib(CF_DIB, dib => result = Decode(dib));
        return read ? result : null;
    }

    /// <summary>クリップボードを開くときの再試行の回数。</summary>
    /// <remarks>
    /// 他のアプリがクリップボードを開いている短い間に当たると <c>OpenClipboard</c> は失敗する（実測 2026-11-12）。<br/>
    /// 直後は空くことが多いため、短い間隔で数回試す（利用者に「コピーされない」と見せない）。
    /// </remarks>
    private const int OpenRetryCount = 6;

    /// <summary>クリップボードを開くときの再試行の間隔（ミリ秒）。</summary>
    private const int OpenRetryDelayMs = 30;

    /// <summary>画像をクリップボードへ書き込む。</summary>
    /// <param name="image">書き込む画像。</param>
    /// <param name="ownerWindow">クリップボードの所有者にするウィンドウ（<c>OpenClipboard</c> の引数）。</param>
    /// <returns>書き込めた場合は true。 他アプリが使用中・確保できない場合は false。</returns>
    /// <remarks>
    /// 32bpp・圧縮なし・**下から上**（<c>biHeight</c> が正値）の DIB を書く。<br/>
    /// 上から下（負値）は規格上は正しいが、<b>受け取る側が対応していないことがある</b><br/>
    /// （実測 2026-11-12: 上から下だと `System.Windows.Forms.Clipboard.GetImage` が null を返し、
    /// Avalonia の `TryGetBitmapAsync` も読めなかった。 下から上にすると両方で読めた）。
    /// </remarks>
    public static unsafe bool TryWrite(EsReadOnlyBitmap image, IntPtr ownerWindow)
    {
        ArgumentNullException.ThrowIfNull(image);

        var width = image.Width;
        var height = image.Height;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        var imageSize = width * height * 4;
        var total = HeaderSize + imageSize;

        if (!TryOpenClipboard(ownerWindow))
        {
            return false;
        }

        try
        {
            if (EmptyClipboard() == false)
            {
                return false;
            }

            var memory = GlobalAlloc(GMEM_MOVEABLE, (nuint)total);
            if (memory == IntPtr.Zero)
            {
                return false;
            }

            var moved = false;
            try
            {
                var target = (byte*)GlobalLock(memory);
                if (target is null)
                {
                    return false;
                }

                try
                {
                    var header = new Span<byte>(target, HeaderSize);
                    WriteHeader(header, width, height, imageSize);

                    // 画素は BGRA8888・行幅 = 幅 × 4。 下から上へ並べる
                    var pixels = image.ReadOnlyPixels[..imageSize];
                    for (var y = 0; y < height; y++)
                    {
                        var source = pixels.Slice(y * width * 4, width * 4);
                        var destination = new Span<byte>(target + HeaderSize + ((height - 1 - y) * width * 4), width * 4);
                        source.CopyTo(destination);
                    }
                }
                finally
                {
                    _ = GlobalUnlock(memory);
                }

                if (SetClipboardData(CF_DIB, memory) == IntPtr.Zero)
                {
                    return false;
                }

                // 所有権はクリップボードへ移る（解放すると内容が壊れる）
                moved = true;
                return true;
            }
            finally
            {
                if (!moved)
                {
                    _ = GlobalFree(memory);
                }
            }
        }
        finally
        {
            _ = CloseClipboard();
        }
    }

    /// <summary>クリップボードを開く（他のアプリが使用中の場合は短い間隔で再試行する）。</summary>
    /// <param name="ownerWindow">クリップボードの所有者にするウィンドウ。</param>
    /// <returns>開けた場合は true。</returns>
    /// <remarks>
    /// 他のアプリがクリップボードを開いている短い間に当たると失敗するため、数回まで試す<br/>
    /// （失敗したままだと「コピーしても何も起きない」と見える）。
    /// </remarks>
    private static bool TryOpenClipboard(IntPtr ownerWindow)
    {
        for (var attempt = 0; attempt < OpenRetryCount; attempt++)
        {
            if (OpenClipboard(ownerWindow))
            {
                return true;
            }

            Thread.Sleep(OpenRetryDelayMs);
        }

        return false;
    }

    /// <summary>BITMAPINFOHEADER を書き込む。</summary>
    /// <param name="header">書き込み先（40 バイト）。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="imageSize">画素データのバイト数。</param>
    /// <remarks>高さは正値（下から上）にする（<see cref="TryWrite"/> の注記を参照）。</remarks>
    private static void WriteHeader(Span<byte> header, int width, int height, int imageSize)
    {
        var values = header;
        WriteInt32(values, 0, HeaderSize);
        WriteInt32(values, 4, width);
        WriteInt32(values, 8, height);
        WriteInt16(values, 12, 1);
        WriteInt16(values, 14, 32);
        WriteUInt32(values, 16, BI_RGB);
        WriteUInt32(values, 20, (uint)imageSize);
        WriteInt32(values, 24, 0);
        WriteInt32(values, 28, 0);
        WriteUInt32(values, 32, 0);
        WriteUInt32(values, 36, 0);
    }

    /// <summary>DIB メモリ（BITMAPINFOHEADER 先頭）を解析して画像を生成する。</summary>
    /// <param name="dibPtr">DIB メモリの先頭ポインター。</param>
    /// <returns>生成した画像。 解析できない場合は null。</returns>
    /// <remarks>クリップボードを使わずに解析だけを検証できるよう internal にする。</remarks>
    internal static unsafe EsBitmap? Decode(IntPtr dibPtr)
    {
        var p = (byte*)dibPtr;
        var headerSize = *(int*)p;
        if (headerSize < HeaderSize)
        {
            return null;
        }

        var width = *(int*)(p + 4);
        var signedHeight = *(int*)(p + 8);
        var planes = *(ushort*)(p + 12);
        var bitCount = *(ushort*)(p + 14);
        var compression = *(uint*)(p + 16);
        var colorsUsed = *(uint*)(p + 32);

        if (width <= 0 || signedHeight == 0 || planes != 1)
        {
            return null;
        }

        // JPEG / PNG の圧縮 DIB は画素の並びが異なるため対象外
        if (compression is not (BI_RGB or BI_BITFIELDS or BI_ALPHABITFIELDS))
        {
            return null;
        }

        var topDown = signedHeight < 0;
        var height = Math.Abs(signedHeight);
        var palette = p + headerSize;
        var pixel = palette;

        if (bitCount <= 8)
        {
            var colors = colorsUsed > 0 ? colorsUsed : 1u << bitCount;
            pixel += (int)colors * 4;
        }
        else if (bitCount == 16 && compression == BI_BITFIELDS)
        {
            pixel += 12;
        }
        else if (bitCount == 32 && compression == BI_ALPHABITFIELDS)
        {
            pixel += 16;
        }

        var stride = ((width * bitCount) + 31) / 32 * 4;
        var pixels = new byte[width * height * 4];

        fixed (byte* destination = pixels)
        {
            for (var y = 0; y < height; y++)
            {
                var source = pixel + (y * stride);
                var target = destination + ((topDown ? y : height - 1 - y) * width * 4);

                switch (bitCount)
                {
                    case 32:
                        Copy32Bpp(source, target, width);
                        break;
                    case 24:
                        Copy24Bpp(source, target, width);
                        break;
                    case 16:
                        Copy16Bpp(source, target, width, compression == BI_RGB);
                        break;
                    case 8:
                        Copy8Bpp(source, target, width, (uint*)palette);
                        break;
                    default:
                        return null;
                }
            }
        }

        return EsBitmap.FromPixelCopy(width, height, pixels);
    }

    /// <summary>32bpp（BGRA）の 1 行をコピーする。</summary>
    private static unsafe void Copy32Bpp(byte* source, byte* target, int width)
    {
        for (var x = 0; x < width; x++)
        {
            target[x * 4] = source[x * 4];
            target[x * 4 + 1] = source[x * 4 + 1];
            target[x * 4 + 2] = source[x * 4 + 2];
            // アルファが 0 の環境（BI_RGB）は不透明として扱う
            var alpha = source[x * 4 + 3];
            target[x * 4 + 3] = alpha == 0 ? (byte)255 : alpha;
        }
    }

    /// <summary>24bpp（BGR）の 1 行を BGRA へ展開する。</summary>
    private static unsafe void Copy24Bpp(byte* source, byte* target, int width)
    {
        for (var x = 0; x < width; x++)
        {
            target[x * 4] = source[x * 3];
            target[x * 4 + 1] = source[x * 3 + 1];
            target[x * 4 + 2] = source[x * 3 + 2];
            target[x * 4 + 3] = 255;
        }
    }

    /// <summary>16bpp の 1 行を BGRA へ展開する。</summary>
    /// <param name="source">元の行。</param>
    /// <param name="target">書き込み先の行。</param>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="isRgb555">true の場合は RGB555（圧縮なし）、false の場合は RGB565（マスク付き）。</param>
    private static unsafe void Copy16Bpp(byte* source, byte* target, int width, bool isRgb555)
    {
        for (var x = 0; x < width; x++)
        {
            var value = *(ushort*)(source + (x * 2));
            int r;
            int g;
            int b;
            if (isRgb555)
            {
                r = (value >> 10) & 0x1F;
                g = (value >> 5) & 0x1F;
                b = value & 0x1F;
                target[x * 4] = (byte)((b << 3) | (b >> 2));
                target[x * 4 + 1] = (byte)((g << 3) | (g >> 2));
                target[x * 4 + 2] = (byte)((r << 3) | (r >> 2));
            }
            else
            {
                r = (value >> 11) & 0x1F;
                g = (value >> 5) & 0x3F;
                b = value & 0x1F;
                target[x * 4] = (byte)((b << 3) | (b >> 2));
                target[x * 4 + 1] = (byte)((g << 2) | (g >> 4));
                target[x * 4 + 2] = (byte)((r << 3) | (r >> 2));
            }

            target[x * 4 + 3] = 255;
        }
    }

    /// <summary>8bpp（パレット）の 1 行を BGRA へ展開する。</summary>
    private static unsafe void Copy8Bpp(byte* source, byte* target, int width, uint* palette)
    {
        for (var x = 0; x < width; x++)
        {
            var color = palette[source[x]];
            target[x * 4] = (byte)color;
            target[x * 4 + 1] = (byte)(color >> 8);
            target[x * 4 + 2] = (byte)(color >> 16);
            target[x * 4 + 3] = 255;
        }
    }

    /// <summary>32 ビット整数を書き込む。</summary>
    private static void WriteInt32(Span<byte> target, int offset, int value)
        => System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(target[offset..], value);

    /// <summary>16 ビット整数を書き込む。</summary>
    private static void WriteInt16(Span<byte> target, int offset, short value)
        => System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(target[offset..], value);

    /// <summary>32 ビット符号なし整数を書き込む。</summary>
    private static void WriteUInt32(Span<byte> target, int offset, uint value)
        => System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(target[offset..], value);

    /// <summary>クリップボードを開く。</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool OpenClipboard(IntPtr owner);

    /// <summary>クリップボードを閉じる。</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool CloseClipboard();

    /// <summary>クリップボードの内容を空にする。</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool EmptyClipboard();

    /// <summary>クリップボードへ書式を設定する。</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    /// <summary>移動可能なメモリを確保する。</summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GlobalAlloc(uint flags, nuint bytes);

    /// <summary>確保したメモリを解放する。</summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GlobalFree(IntPtr memory);

    /// <summary>確保したメモリをロックする。</summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern unsafe void* GlobalLock(IntPtr memory);

    /// <summary>確保したメモリのロックを解除する。</summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool GlobalUnlock(IntPtr memory);
}
