using System.Runtime.InteropServices;
using EsUtil.SaveEvidence.App.Core.Native;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="WinClipboardDib"/> の DIB 解析のテスト。</summary>
/// <remarks>
/// クリップボードを開かずに解析だけを検証する（<see cref="WinClipboardDib.Decode"/>）。<br/>
/// クリップボードの内容はアプリの外（他プロセス）が作るため、並び（上下反転・bpp）の取り違えは<br/>
/// 実際に貼り付けるまで気付けない。 DIB をメモリ上に組み立てて固定する。
/// </remarks>
public class WinClipboardDibTests
{
    /// <summary>BITMAPINFOHEADER のサイズ（バイト）。</summary>
    private const int HeaderSize = 40;

    [Fact]
    internal void Decode_32bppボトムアップのDIBを上下反転して読み取る()
    {
        // 2 x 2。 下の行を青、上の行を赤にして、上下が反転されないことを確かめる
        var dib = CreateDib(width: 2, height: 2, bitsPerPixel: 32, topDown: false, bottomRowBlue: true);

        try
        {
            using var image = WinClipboardDib.Decode(dib);

            Assert.NotNull(image);
            Assert.Equal(2, image.Width);
            Assert.Equal(2, image.Height);

            // 上端の画素は「上の行」（赤）でなければならない
            var top = image.GetPixel(0, 0);
            Assert.Equal(255, top.R);
            Assert.Equal(0, top.B);

            var bottom = image.GetPixel(0, 1);
            Assert.Equal(0, bottom.R);
            Assert.Equal(255, bottom.B);
        }
        finally
        {
            Marshal.FreeHGlobal(dib);
        }
    }

    [Fact]
    internal void Decode_24bppのDIBをBGRAへ展開する()
    {
        var dib = CreateDib(width: 2, height: 2, bitsPerPixel: 24, topDown: true, bottomRowBlue: false);

        try
        {
            using var image = WinClipboardDib.Decode(dib);

            Assert.NotNull(image);
            var pixel = image.GetPixel(1, 1);
            // 上から下（topDown）の 24bpp は行の並びがそのまま。 1 行目は赤
            Assert.Equal(255, pixel.R);
            Assert.Equal(0, pixel.B);
            Assert.Equal(255, pixel.A);
        }
        finally
        {
            Marshal.FreeHGlobal(dib);
        }
    }

    [Fact]
    internal void Decode_ヘッダーが短い場合はnullを返す()
    {
        var bytes = new byte[16];
        var dib = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, dib, bytes.Length);
            Assert.Null(WinClipboardDib.Decode(dib));
        }
        finally
        {
            Marshal.FreeHGlobal(dib);
        }
    }

    /// <summary>テスト用の DIB（BITMAPINFOHEADER + 画素）を組み立てる。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <param name="bitsPerPixel">1 ピクセルあたりのビット数（24 または 32）。</param>
    /// <param name="topDown">true の場合は上から下（<c>biHeight</c> が負値）。</param>
    /// <param name="bottomRowBlue">true の場合は下の行を青、上の行を赤にする。</param>
    /// <returns>確保した DIB メモリ（<see cref="Marshal.FreeHGlobal(IntPtr)"/> で解放する）。</returns>
    private static IntPtr CreateDib(int width, int height, int bitsPerPixel, bool topDown, bool bottomRowBlue)
    {
        var stride = ((width * bitsPerPixel) + 31) / 32 * 4;
        var size = HeaderSize + (stride * height);
        var dib = Marshal.AllocHGlobal(size);

        Marshal.WriteInt32(dib, 0, HeaderSize);
        Marshal.WriteInt32(dib, 4, width);
        Marshal.WriteInt32(dib, 8, topDown ? -height : height);
        Marshal.WriteInt16(dib, 12, 1);
        Marshal.WriteInt16(dib, 14, (short)bitsPerPixel);
        Marshal.WriteInt32(dib, 16, 0); // BI_RGB
        Marshal.WriteInt32(dib, 20, stride * height);

        var bytesPerPixel = bitsPerPixel / 8;
        for (var y = 0; y < height; y++)
        {
            // 上の行を赤・下の行を青にする（bottomRowBlue のとき）
            var isTopRow = topDown ? y == 0 : y == height - 1;
            var isRed = bottomRowBlue ? isTopRow : !isTopRow;

            for (var x = 0; x < width; x++)
            {
                var offset = HeaderSize + (y * stride) + (x * bytesPerPixel);
                Marshal.WriteByte(dib, offset, (byte)(isRed ? 0 : 255));     // B
                Marshal.WriteByte(dib, offset + 1, 0);                        // G
                Marshal.WriteByte(dib, offset + 2, (byte)(isRed ? 255 : 0));  // R
                if (bytesPerPixel == 4)
                {
                    Marshal.WriteByte(dib, offset + 3, 255);                  // A
                }
            }
        }

        return dib;
    }
}
