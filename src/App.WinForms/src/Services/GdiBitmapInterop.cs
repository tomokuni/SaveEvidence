using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using EsUtil.Platform.Core.Imaging;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRectangle = System.Drawing.Rectangle;

namespace EsUtil.SaveEvidence.App.WinForms.Services;

/// <summary>GDI+ のビットマップとコアの画像（<see cref="EsBitmap"/> / <see cref="EsReadOnlyBitmap"/>）を相互変換する。</summary>
/// <remarks>
/// <b>ピクセル形式</b>: コアの画像は BGRA8888・アルファ乗算済みであるため、GDI+ 側も<br/>
/// <c>Format32bppPArgb</c>（同じ並び・同じ乗算）を使う（形式が一致しないと成分の順序が入れ替わる）。<br/>
/// <b>所有権</b>: 返す値はすべて複製であり、呼び出し側が所有する（引数は破棄しない）。<br/>
/// <b>行幅</b>: GDI+ の行幅（<c>Stride</c>）は画像ごとに異なりうるため行単位でコピーする。<br/>
/// <b>UI スレッドを必要としない</b>ため、画素の一致をヘッドレスで検証できる（<c>Bitmap</c> と<br/>
/// <c>Graphics.FromImage</c> だけで完結する）。
/// </remarks>
internal static class GdiBitmapInterop
{
    /// <summary>コアの画像を GDI+ のビットマップへ複製する。</summary>
    /// <param name="source">コピー元の画像（読み取りのみ。 破棄しない）。</param>
    /// <returns>複製したビットマップ（<c>Format32bppPArgb</c>）。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が null の場合。</exception>
    /// <remarks>
    /// <b>複製は高価</b>: 1 回の呼び出しが全面の画素を複製するため、描画のたびに呼ばず、<br/>
    /// 画像が差し替わった時点で 1 度だけ作って保持する。
    /// </remarks>
    public static DrawingBitmap ToDrawingBitmap(EsReadOnlyBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var result = new DrawingBitmap(source.Width, source.Height, DrawingPixelFormat.Format32bppPArgb);
        var data = result.LockBits(
            new DrawingRectangle(0, 0, source.Width, source.Height),
            ImageLockMode.WriteOnly,
            DrawingPixelFormat.Format32bppPArgb);
        try
        {
            CopyRowsToLocked(source, data);
            return result;
        }
        finally
        {
            result.UnlockBits(data);
        }
    }

    /// <summary>GDI+ のビットマップから読み取り専用の画像を生成する。</summary>
    /// <param name="bitmap">変換元のビットマップ（読み取りのみ。 破棄しない）。</param>
    /// <returns>生成した画像（読み取り専用）。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bitmap"/> が null の場合。</exception>
    /// <remarks>書き換えが要る場合は <see cref="ToEsBitmap"/> を使う。</remarks>
    public static EsReadOnlyBitmap ToEsReadOnlyBitmap(DrawingBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var width = bitmap.Width;
        var height = bitmap.Height;
        return EsReadOnlyBitmap.CreateFilled(width, height, destination => CopyRowsToSpan(bitmap, destination, height));
    }

    /// <summary>GDI+ のビットマップから書き換え可能な画像を生成する。</summary>
    /// <param name="bitmap">変換元のビットマップ（読み取りのみ。 破棄しない）。</param>
    /// <returns>生成した画像（書き換え可能）。 所有権は呼び出し側へ移る。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bitmap"/> が null の場合。</exception>
    /// <remarks>貼り付けのように<b>書き換える可能性がある</b>画像に使う。</remarks>
    public static EsBitmap ToEsBitmap(DrawingBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var width = bitmap.Width;
        var height = bitmap.Height;
        return EsBitmap.CreateFilled(width, height, destination => CopyRowsToSpan(bitmap, destination, height));
    }

    /// <summary>画像の全行を、ロック済みのビットマップへコピーする。</summary>
    /// <param name="source">コピー元の画像。</param>
    /// <param name="data">ロック済みのコピー先（コピー先の行幅は <c>Stride</c>）。</param>
    /// <remarks>
    /// <b>1 行ずつ配列を経由する理由</b>: コピー元はアンマネージド メモリの span、コピー先は GDI+ が確保した<br/>
    /// ポインタであり、両者を直接つなぐ API が無い（<c>unsafe</c> を使わない方針のため、行ごとに<br/>
    /// <see cref="Marshal.Copy(byte[], int, nint, int)"/> で書き込む）。
    /// </remarks>
    private static void CopyRowsToLocked(EsReadOnlyBitmap source, BitmapData data)
    {
        var rowBytes = source.Width * EsReadOnlyBitmap.BytesPerPixel;
        var pixels = source.ReadOnlyPixels;
        var row = new byte[rowBytes];

        for (var offset = 0; offset < source.Height; offset++)
        {
            pixels.Slice(offset * source.Stride, rowBytes).CopyTo(row);
            Marshal.Copy(row, 0, nint.Add(data.Scan0, offset * data.Stride), rowBytes);
        }
    }

    /// <summary>ビットマップの全行を、コアの画像のバッファへコピーする。</summary>
    /// <param name="bitmap">変換元のビットマップ（未ロック）。</param>
    /// <param name="destination">コピー先（<c>行幅 × 高さ</c> バイト以上）。</param>
    /// <param name="height">コピーする行数。</param>
    /// <remarks>
    /// <b>上書きされない右端を透明にする</b>: 元の行幅がコピー先より小さい場合は、行ごとに不足する分を 0 にする<br/>
    /// （元のビットマップの仕様上ありうる。 行単位のコピーでは覆われない領域が残るため）。
    /// </remarks>
    private static void CopyRowsToSpan(DrawingBitmap bitmap, Span<byte> destination, int height)
    {
        if (height <= 0)
        {
            return;
        }

        var destinationStride = destination.Length / height;
        var data = bitmap.LockBits(
            new DrawingRectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppPArgb);
        try
        {
            var rowBytes = Math.Min(destinationStride, data.Stride);
            if (rowBytes <= 0)
            {
                return;
            }

            if (data.Stride < destinationStride)
            {
                destination.Clear();
            }

            var row = new byte[rowBytes];
            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(nint.Add(data.Scan0, y * data.Stride), row, 0, rowBytes);
                row.CopyTo(destination.Slice(y * destinationStride, rowBytes));
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
