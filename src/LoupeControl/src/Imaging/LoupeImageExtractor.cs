using Avalonia;
using Avalonia.Media.Imaging;
using Estable.AvaloniaUI.Platform.Imaging;
using System.Runtime.InteropServices;

namespace Estable.AvaloniaUI.Loupe.Imaging;

/// <summary>ルーペの画像処理ヘルパー：Bitmap の領域切り出し（CopyPixels ベース）を提供する。</summary>
/// <remarks>
/// ピクセルデータの抽出・変換など、画像処理（Imaging）に関わる操作を提供する。<br/>
/// 描画（市松模様・拡大・十字線・外枠）は <see cref="LoupeRenderer"/> が担う（責務分離）。<br/>
/// ソース範囲外は透明パディングとなり、市松模様が透ける。<br/>
/// バックグラウンド処理ではピクセルデータだけを返し、AvaloniaのWriteableBitmap生成はUIスレッド側で行う。<br/>
/// </remarks>
internal static class LoupeImageExtractor
{
    /// <summary>ルーペの一辺（ピクセル）から切り出し領域の一辺（ドメインピクセル）を計算する。</summary>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率（1〜64）。</param>
    /// <returns>切り出し領域の一辺（ドメインピクセル。切り上げ）。</returns>
    /// <remarks>切り出し・十字線位置・選択矩形変換の各所で共用する（単一所有）。</remarks>
    public static int ComputeCaptureSize(int pixelSize, int zoomLevel)
        => (int)Math.Ceiling((double)pixelSize / zoomLevel);

    /// <summary>Bitmap の指定位置を中心とした領域を UnifiedBitmap として抽出する。</summary>
    /// <param name="src">切り出し元の Bitmap。</param>
    /// <param name="centerX">中心のX座標（Bitmap ローカルピクセル）。</param>
    /// <param name="centerY">中心のY座標（Bitmap ローカルピクセル）。</param>
    /// <param name="size">切り出す領域の一辺のサイズ（ピクセル）。</param>
    /// <returns>切り出した領域の UnifiedBitmap（BGRA）。範囲外部分は透明。破棄は呼び出し側。</returns>
    /// <remarks>
    /// ルーペのソース画像切り出し（AreaSelect / Crop 共通）で使用する。<br/>
    /// WriteableBitmap は生成せず、バックグラウンドへ渡せる <see cref="UnifiedBitmap"/>（ピクセルコンテナ）を返す。<br/>
    /// 画像端では透明パディングとなり、市松模様が透ける。<br/>
    /// </remarks>
    public static UnifiedBitmap ExtractRegionAt(Bitmap src, int centerX, int centerY, int size)
    {
        var dstStride = checked(size * 4);
        var pixels = new byte[checked(dstStride * size)];

        // ソース範囲内にクリップしてコピー（範囲外はバッファ初期値（透明）のまま = 透明パディング）
        var (clipRect, dstOffset) = ComputeClipRect(centerX, centerY, size, src.PixelSize.Width, src.PixelSize.Height);
        if (clipRect is { } inter)
        {
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                src.CopyPixels(
                    inter,
                    handle.AddrOfPinnedObject() + dstOffset,
                    pixels.Length,
                    dstStride);
            }
            finally
            {
                handle.Free();
            }
        }

        // byte[] → UnifiedBitmap（BGRA8888・Premul）へ変換
        var result = UnifiedBitmap.Create(size, size);
        for (var y = 0; y < size; y++)
        {
            var row = y * dstStride;
            for (var x = 0; x < size; x++)
            {
                var off = row + x * 4;
                result.SetPixel(x, y, b: pixels[off], g: pixels[off + 1], r: pixels[off + 2], a: pixels[off + 3]);
            }
        }

        return result;
    }

    /// <summary>中心座標とサイズから、ソース範囲内にクリップしたコピー領域とバッファ内オフセットを計算する（純粋関数）。</summary>
    /// <param name="centerX">中心のX座標（Bitmap ローカルピクセル）。</param>
    /// <param name="centerY">中心のY座標（Bitmap ローカルピクセル）。</param>
    /// <param name="size">切り出す領域の一辺のサイズ（ピクセル）。</param>
    /// <param name="srcW">ソース Bitmap の幅。</param>
    /// <param name="srcH">ソース Bitmap の高さ。</param>
    /// <returns>
    /// クリップ後のコピー領域（ソース Bitmap 上の矩形。交差なしは null）と、<br/>
    /// デスティネーションバッファ内の書込み開始オフセット（バイト単位）のタプル。
    /// </returns>
    /// <remarks>
    /// UI 非依存の純粋計算のため、ヘッドレス不要の <c>[Fact]</c> で並列テスト可能。<br/>
    /// ソース範囲外の部分はバッファ初期値（透明）のままとなる（クリップ + オフセットコピー）。<br/>
    /// </remarks>
    public static (PixelRect? ClipRect, int DstOffset) ComputeClipRect(
        int centerX, int centerY, int size, int srcW, int srcH)
    {
        var dstStride = checked(size * 4);
        var srcRect = new PixelRect(centerX - size / 2, centerY - size / 2, size, size);
        // 同一スコープで複数回参照する矩形の座標はローカル変数へ一度だけ格納する
        var srcX = srcRect.X;
        var srcY = srcRect.Y;
        var interX = Math.Max(srcX, 0);
        var interY = Math.Max(srcY, 0);
        var interW = Math.Min(srcRect.Right, srcW) - interX;
        var interH = Math.Min(srcRect.Bottom, srcH) - interY;
        if (interW <= 0 || interH <= 0) return (null, 0);

        var dstOffset = (interY - srcY) * dstStride + (interX - srcX) * 4;
        return (new PixelRect(interX, interY, interW, interH), dstOffset);
    }

    /// <summary>抽出済み UnifiedBitmap から表示用 WriteableBitmap を生成する。</summary>
    /// <param name="region">抽出済みピクセル領域（UnifiedBitmap）。</param>
    /// <returns>生成した WriteableBitmap。</returns>
    /// <remarks>呼び出し側はAvalonia UIスレッドで実行する。</remarks>
    public static WriteableBitmap ToWriteableBitmap(this UnifiedBitmap region)
    {
        // UnifiedBitmap → Avalonia WriteableBitmap（表示用）。ToAvaloniaBitmap は複製を作るため、region の破棄は呼び出し側が行う。
        return (WriteableBitmap)region.ToAvaloniaBitmap();
    }
}
