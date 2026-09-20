using Estable.Platform.Geometry;
using Estable.Platform.Imaging;
using RectInt = System.Drawing.Rectangle;

namespace Estable.Loupe.Imaging;

/// <summary>ルーペの切り出し領域の幾何計算と領域切り出しを提供する。</summary>
/// <remarks>
/// ピクセルデータの抽出・切り出しは <see cref="EsBitmap"/> が担い、本クラスはルーペ固有の領域計算<br/>
/// （<see cref="ComputeCaptureSize"/> / <see cref="ComputeCaptureTopLeft"/>）と切り出しの委譲を提供する。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
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

    /// <summary>拡大中心から切り出し領域の左上（ドメイン座標）を計算する。</summary>
    /// <param name="centerX">拡大中心のX座標（元画像ピクセル）。</param>
    /// <param name="centerY">拡大中心のY座標（元画像ピクセル）。</param>
    /// <param name="size">切り出し領域の一辺（ドメインピクセル）。</param>
    /// <returns>切り出し領域の左上（ドメイン座標）。</returns>
    /// <remarks>
    /// <see cref="ExtractRegionAt"/> の中心 → 左上変換と、描画完了イベントの切り出し領域左上計算を単一所有する（DRY）。<br/>
    /// 中心座標の整数化（<c>(int)</c>）は <see cref="ExtractRegionAt"/> と同一の丸めで一致させる。
    /// </remarks>
    public static EsPoint ComputeCaptureTopLeft(int centerX, int centerY, int size)
        => new(centerX - size / 2, centerY - size / 2);

    /// <summary>指定位置を中心とした正方形領域を切り出す（ソース範囲外は透明パディング）。</summary>
    /// <param name="source">切り出し元の EsBitmap。</param>
    /// <param name="centerX">中心のX座標（画像ローカルピクセル）。</param>
    /// <param name="centerY">中心のY座標（画像ローカルピクセル）。</param>
    /// <param name="size">切り出す領域の一辺のサイズ（ピクセル）。正の値。</param>
    /// <returns>切り出された新しいビットマップ（<paramref name="size"/> × <paramref name="size"/>）。範囲外部分は透明。破棄は呼び出し側。</returns>
    /// <remarks>
    /// <see cref="EsBitmap.Crop(RectInt)"/> へ委譲する（中心座標 → 左上座標へ変換）。<br/>
    /// 画像端では透明パディングとなり、市松模様が透ける。<br/>
    /// 左上変換は <see cref="ComputeCaptureTopLeft"/> に委譲する（切り出し領域左上計算の単一所有）。
    /// </remarks>
    public static EsBitmap ExtractRegionAt(EsBitmap source, int centerX, int centerY, int size)
    {
        var topLeft = ComputeCaptureTopLeft(centerX, centerY, size);
        return source.Crop(new RectInt((int)topLeft.X, (int)topLeft.Y, size, size));
    }
}
