using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Estable.AvaloniaUI.Platform.Imaging;

/// <summary>Avalonia <see cref="Bitmap"/> のピクセル領域操作を提供する拡張メソッド。</summary>
/// <remarks>
/// <c>Bitmap.CopyPixels</c>（ネイティブ memcpy 相当）を利用し、PNG 等のエンコード/デコードや<br/>
/// マネージド byte ループを経由せずに領域コピー・切り出しを行う（高速・可逆）。<br/>
/// <br/>
/// ソース範囲外はデスティネーションの初期値（透明）のままとなり、画像端での切り出し時に<br/>
/// 透明パディングが自然に生成される。
/// </remarks>
public static class BitmapExtensions
{
    /// <summary>Bitmap の指定矩形（論理座標・範囲外許容）を、事前ゼロ初期化されたデスティネーションバッファへコピーする。</summary>
    /// <param name="src">コピー元の Bitmap。</param>
    /// <param name="srcRect">ソース上のコピー元矩形（クロップ境界を含む論理座標。負値・超過を許容）。</param>
    /// <param name="dst">デスティネーションバッファ（BGRA8888）。</param>
    /// <param name="dstStride">デスティネーションの1ラインあたりのバイト数。</param>
    /// <param name="dstBufferSize">デスティネーションバッファの合計バイト数。</param>
    /// <param name="dstX">デスティネーション内の書込み開始X座標（ピクセル）。</param>
    /// <param name="dstY">デスティネーション内の書込み開始Y座標（ピクセル）。</param>
    /// <remarks>
    /// <paramref name="srcRect"/> が Bitmap の範囲を超える場合、範囲外の部分はコピーせず<br/>
    /// バッファ初期値（透明）のままとする（クリップ + オフセットコピー）。<br/>
    /// 呼び出し側でバッファをゼロ初期化しておく必要がある。<br/>
    /// </remarks>
    public static void CopyRegionWithPadding(
        this Bitmap src,
        PixelRect srcRect,
        IntPtr dst, int dstStride, int dstBufferSize,
        int dstX, int dstY)
    {
        // ソース範囲内にクリップ（バッファ側のオフセットは clip 位置に応じて調整）
        var srcW = src.PixelSize.Width;
        var srcH = src.PixelSize.Height;
        var interX = Math.Max(srcRect.X, 0);
        var interY = Math.Max(srcRect.Y, 0);
        var interR = Math.Min(srcRect.Right, srcW);
        var interB = Math.Min(srcRect.Bottom, srcH);
        var interW = interR - interX;
        var interH = interB - interY;
        if (interW <= 0 || interH <= 0) return;

        var dstOffset = (dstY + (interY - srcRect.Y)) * dstStride + (dstX + (interX - srcRect.X)) * 4;
        src.CopyPixels(
            new PixelRect(interX, interY, interW, interH),
            dst + dstOffset,
            dstBufferSize - dstOffset,
            dstStride);
    }

    /// <summary>Bitmap の指定矩形領域を切り出す（範囲外は透明パディング）。</summary>
    /// <param name="src">切り出し元の Bitmap。</param>
    /// <param name="rect">切り出し矩形（Bitmap ローカルピクセル。範囲外を許容）。</param>
    /// <returns>切り出した Bitmap（rect サイズ）。範囲外部分は透明。</returns>
    /// <remarks>
    /// 仮想スクリーンからの確定領域切り出し（AreaSelect 確定時）で使用する。<br/>
    /// 結果は WriteableBitmap（Avalonia 標準）で返し、中間バッファ（byte[]）を介さない。<br/>
    /// </remarks>
    public static Bitmap ExtractRegion(this Bitmap src, PixelRect rect)
    {
        var w = rect.Width;
        var h = rect.Height;
        if (w <= 0 || h <= 0) throw new ArgumentOutOfRangeException(nameof(rect), "切り出し矩形のサイズは正の値である必要があります。");

        var wb = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var locked = wb.Lock();
        var dstStride = locked.RowBytes;
        var dstBufferSize = dstStride * h;

        src.CopyRegionWithPadding(
            rect,
            locked.Address, dstStride, dstBufferSize, 0, 0);

        return wb;
    }
}
