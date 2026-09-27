using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.LoupeCore.Drawing;

namespace EsUtil.Feature.LoupeCore.Decoration;

/// <summary>ルーペの装飾（十字線・枠線）の矩形計算ロジック（純粋関数）。</summary>
/// <remarks>
/// 装飾矩形の計算を UI 非依存の純粋関数として単一所有する。<br/>
/// 矩形は描画内容（<see cref="Loupe.ResolveContent"/> の結果）へ解決済みの値として含まれるため、<br/>
/// 描画実装は返された <see cref="EsRect"/> を自身の描画 API へ渡すだけにする（二重所有を 防ぐ）。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証 できる。
/// </remarks>
internal static class LoupeDecoGeometry
{
    /// <summary>十字線の 2 本の矩形を計算する。</summary>
    /// <param name="surface">描画面の全体（左上を原点とする正方形）。</param>
    /// <param name="zoomLevel">拡大率（1〜64）。</param>
    /// <returns>十字線の縦・横 2 本の矩形。</returns>
    /// <remarks>
    /// 切り出し領域の一辺（ブロック数）の計算は <see cref="LoupeDivMath.CeilDiv"/> へ委譲する（単一所有）。<br/>
    /// 交点は「切り出した領域を等倍で並べたときの中央のブロック」の左上へ合わせる（<c>blocks / 2</c> の切り捨て）。<br/>
    /// 切り出し領域の一辺は切り上げのため、<b>拡大率が偶数でない場合は交点が表面の中央から最大 1 ブロックずれる</b><br/>
    /// （切り捨てをやめると、奇数倍率で交点が画素境界から外れて十字線がぼやける）。
    /// </remarks>
    internal static IReadOnlyList<EsRect> ComputeCrosshairRects(EsRect surface, int zoomLevel)
    {
        var pixelSize = (int)surface.Width;
        var blocks = LoupeDivMath.CeilDiv(pixelSize, zoomLevel);
        var start = blocks / 2 * zoomLevel;
        return [new EsRect(surface.X + start, surface.Y, zoomLevel, pixelSize), new EsRect(surface.X, surface.Y + start, pixelSize, zoomLevel)];
    }

    /// <summary>枠線の 4 辺の矩形を計算する。</summary>
    /// <param name="surface">描画面の全体（左上を原点とする正方形）。</param>
    /// <param name="thickness">枠線の太さ（物理ピクセル）。補正済みの値を渡す（安全域は <c>Loupe.Limits</c> が単一所有する）。</param>
    /// <returns>枠線の上・下・左・右の 4 つの矩形。</returns>
    /// <remarks>
    /// 辺の矩形の計算は <see cref="EsRect.ComputeInnerEdges(double)"/> が単一所有する（選択矩形の枠線と同じ式）。<br/>
    /// 太さは面の短辺で上限を取る（面より太い枠線は面全体を覆い、矩形が面からはみ出さない）。<br/>
    /// 太さの補正は設定（<c>LoupeOptions.BorderThickness</c> の生成時）に適用される。
    /// </remarks>
    internal static IReadOnlyList<EsRect> ComputeBorderRects(EsRect surface, double thickness)
        => surface.ComputeInnerEdges(Math.Min(thickness, Math.Min(surface.Width, surface.Height)));
}
