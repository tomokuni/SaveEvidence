using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using RectInt = System.Drawing.Rectangle;
using SizeInt = System.Drawing.Size;

namespace Estable.Loupe.Imaging;

/// <summary>ルーペの切り出し領域の幾何計算と領域切り出しを提供する。</summary>
/// <remarks>
/// ピクセルデータの抽出・切り出しは <see cref="EsBitmap"/> が担い、本クラスはルーペ固有の領域計算<br/>
/// （<see cref="ComputeCaptureSize"/> / <see cref="ComputeCaptureTopLeft"/>）と切り出しの委譲を提供する。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class LoupeImageExtractor
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

    /// <summary>切り出した領域をルーペ領域へ拡大描画するときの矩形（切り出し元・描画先）を計算する。</summary>
    /// <param name="captureSize">切り出した領域のサイズ（ドメインピクセル・正の値）。ビットマップの実寸（<see cref="EsBitmap.Size"/>）をそのまま渡す。</param>
    /// <param name="zoomLevel">拡大率（1〜64）。</param>
    /// <returns>切り出し元の矩形（左上原点・切り出しサイズ）と描画先の矩形（左上原点・<paramref name="zoomLevel"/> 倍）。</returns>
    /// <remarks>
    /// 「切り出した領域を左上原点から <paramref name="zoomLevel"/> 倍の大きさで描く」規則を単一所有する。<br/>
    /// 拡大の倍率と描画先の大きさの関係は、両 UI で同じでなければ拡大画像の見た目がずれる<br/>
    /// （切り出し領域の算出は <see cref="ComputeCaptureSize"/> が単一所有する）。
    /// </remarks>
    public static (EsRect Source, EsRect Destination) ComputeMagnifiedRects(SizeInt captureSize, int zoomLevel)
        => (new EsRect(0, 0, captureSize.Width, captureSize.Height),
            new EsRect(0, 0, captureSize.Width * zoomLevel, captureSize.Height * zoomLevel));

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

    /// <summary>拡大表示用の領域を切り出す（描画できない状態では null を返す）。</summary>
    /// <param name="source">切り出し元の EsBitmap。未設定は null。</param>
    /// <param name="request">描画入力（切り出し領域の算出に使う物理ピクセルサイズ・拡大率・中心）。</param>
    /// <returns>切り出した領域。切り出せない場合は null（呼び出し側は市松模様のみを描画する）。破棄は呼び出し側。</returns>
    /// <remarks>
    /// 「拡大画像を描画できるか」の判断（ソースと中心が揃っているか）と、切り出し領域の算出・切り出しを単一所有する。<br/>
    /// 描画中にソースが破棄された場合（<see cref="ObjectDisposedException"/>）も null を返し、例外を描画層へ伝播させない<br/>
    /// （ホストの状態と競合しても描画を継続できる）。<br/>
    /// 本メソッドが扱うのは描画側の都合であり、切り出しサイズの算出は <see cref="ComputeCaptureSize"/> が単一所有する。
    /// </remarks>
    public static EsBitmap? TryExtractRegionAt(EsBitmap? source, LoupeRenderRequest request)
    {
        if (source is null || request.Center is not { } origin)
        {
            return null;
        }

        try
        {
            return ExtractRegionAt(source, (int)origin.X, (int)origin.Y, ComputeCaptureSize(request.PixelSize, request.ZoomLevel));
        }
        catch (ObjectDisposedException)
        {
            // 描画中にソースが破棄された: 市松模様のみを描画する
            return null;
        }
    }
}
