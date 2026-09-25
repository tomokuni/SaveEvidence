using EsUtil.Feature.LoupeCore.Imaging;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.LoupeCore;

/// <summary>ルーペの装飾（十字線・外枠）の矩形計算ロジック（純粋関数）。</summary>
/// <remarks>
/// 装飾矩形の計算を UI 非依存の純粋関数として単一所有し、各 UI 実装は<br/>
/// 返された <see cref="EsRect"/> を自身の描画 API へ渡すだけにする（二重所有を防ぐ）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class LoupeDecorationGeometry
{
    /// <summary>十字線の 2 本の矩形を計算する。</summary>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率（1〜64）。</param>
    /// <returns>十字線の縦・横 2 本の矩形。</returns>
    /// <remarks>
    /// 切り出し領域の一辺（ブロック数）の計算は <see cref="LoupeImageExtractor.ComputeCaptureSize"/> へ委譲する（単一所有）。<br/>
    /// 交点は「切り出した領域を等倍で並べたときの中央のブロック」の左上へ合わせる（<c>blocks / 2</c> の切り捨て）。<br/>
    /// 切り出し領域の一辺は切り上げのため、<b>拡大率が偶数でない場合は交点が表面の中央から最大 1 ブロックずれる</b><br/>
    /// （切り捨てをやめると、奇数倍率で交点が画素境界から外れて十字線がぼやける）。
    /// </remarks>
    public static IReadOnlyList<EsRect> ComputeCrosshairRects(int pixelSize, int zoomLevel)
    {
        var blocks = LoupeImageExtractor.ComputeCaptureSize(pixelSize, zoomLevel);
        var start = blocks / 2 * zoomLevel;
        return [new EsRect(start, 0, zoomLevel, pixelSize), new EsRect(0, start, pixelSize, zoomLevel)];
    }

    /// <summary>外枠の 4 辺の矩形を計算する。</summary>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="width">外枠の太さ（ピクセル）。1〜ルーペサイズに正規化される。</param>
    /// <returns>外枠の上・下・左・右の 4 つの矩形。</returns>
    /// <remarks>
    /// 辺の矩形の計算は <see cref="EsRectMath.ComputeInnerEdges"/> が単一所有する（選択矩形の枠線と同じ式）。<br/>
    /// 太さの下限のみ本メソッドが決める（外枠は 1 ピクセル未満では描けない）。
    /// </remarks>
    public static IReadOnlyList<EsRect> ComputeFrameRects(int pixelSize, int width)
        => EsRectMath.ComputeInnerEdges(
            new EsRect(0, 0, pixelSize, pixelSize), Math.Clamp(width, 1, pixelSize));
}
