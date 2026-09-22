using Avalonia.Media;
using EsUtil.Platform.AvaloniaUI;
using EsUtil.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core.Imaging;
using Estable.Loupe;
using Estable.Loupe.Imaging;

namespace Estable.AvaloniaUI.Loupe.Rendering;

/// <summary>切り出し済み画像をルーペ領域へ描画します。</summary>
/// <remarks>
/// 切り出しの可否（ソースと拡大中心が揃っているか）と切り出し手順は <see cref="LoupeImageExtractor.TryExtractRegionAt"/>（コア）へ委譲し、<br/>
/// 切り出し元と描画先の矩形（切り出し領域を <c>zoomLevel</c> 倍で描く）は <see cref="LoupeImageExtractor.ComputeMagnifiedRects"/>（コア）が単一所有する。<br/>
/// 本クラスは返された値を Avalonia の描画 API へ写すだけにする（MewUI 実装と同一の拡大倍率になる）。
/// </remarks>
internal static class LoupeImageRenderer
{
    /// <summary>ソース画像を切り出してズーム倍率で拡大描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="source">拡大元の画像。null または切り出せない場合は何も描画しない。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・拡大率・拡大中心）。</param>
    /// <remarks>
    /// 破棄済みソースの扱い（例外にせず市松模様のみを残す）もコアが単一所有する。<br/>
    /// <c>ToAvaloniaBitmap</c> の複製は描画後に破棄する（<see cref="EsBitmap"/> の所有権は呼び出し側に残る）。
    /// </remarks>
    public static void Draw(DrawingContext context, EsBitmap? source, LoupeRenderRequest request)
    {
        using var region = LoupeImageExtractor.TryExtractRegionAt(source, request);
        if (region is null)
        {
            return;
        }

        // 切り出し元・描画先の矩形はコアが、実寸（EsBitmap.Size）から計算する
        var (sourceRect, destination) = LoupeImageExtractor.ComputeMagnifiedRects(region.Size, request.ZoomLevel);

        // DrawImage は IImage を要求するため、表示境界で Avalonia Bitmap へ変換する。
        // 標準 Bitmap（内部実装 ImmutableBitmap）は Lock() がなく、WriteableBitmap 変換より高速に描画できる。
        using var av = region.ToAvaloniaBitmap();
        context.DrawImage(av, sourceRect.ToAvaloniaRect(), destination.ToAvaloniaRect());
    }
}
