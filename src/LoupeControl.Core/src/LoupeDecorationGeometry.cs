using Estable.Loupe.Imaging;
using Estable.Platform.Geometry;

namespace Estable.Loupe;

/// <summary>ルーペの装飾（十字線・外枠）の矩形計算ロジック（純粋関数）。</summary>
/// <remarks>
/// 装飾矩形の計算を UI 非依存の純粋関数として単一所有し、各 UI（Avalonia / MewUI）は<br/>
/// 返された <see cref="EsRect"/> を自身の描画 API へ渡すだけにする（二重所有を防ぐ）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class LoupeDecorationGeometry
{
    /// <summary>十字線の 2 本の矩形を計算する。</summary>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率（1〜64）。</param>
    /// <returns>十字線の縦・横 2 本の矩形。</returns>
    /// <remarks>
    /// 切り出し領域の一辺（ブロック数）の計算は <see cref="LoupeImageExtractor.ComputeCaptureSize"/> へ委譲する（単一所有）。
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
    public static IReadOnlyList<EsRect> ComputeFrameRects(int pixelSize, int width)
    {
        var frame = Math.Clamp(width, 1, pixelSize);
        var inner = Math.Max(0, pixelSize - frame * 2);
        return
        [
            new EsRect(0, 0, pixelSize, frame),
            new EsRect(0, pixelSize - frame, pixelSize, frame),
            new EsRect(0, frame, frame, inner),
            new EsRect(pixelSize - frame, frame, frame, inner),
        ];
    }
}
