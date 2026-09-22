using Avalonia;
using Avalonia.Media;
using Estable.Loupe;

namespace Estable.AvaloniaUI.Loupe.Rendering;

/// <summary>ルーペの背景（市松模様）を描画します。</summary>
/// <remarks>
/// 明暗の判定は UI 非依存の <see cref="LoupeCheckerboard"/> が単一所有し、MewUI 実装と共用する。<br/>
/// 描画の可否（透明な色は描画しない）はコアの <see cref="LoupeAppearance"/> に問い合わせ、<br/>
/// タイルサイズの物理ピクセル換算も <see cref="LoupeAppearanceScaler"/>（コア）へ委譲するため、<br/>
/// 本クラスは描画入力（<see cref="LoupeRenderRequest"/>）から描くだけにする（MewUI 版の描画層と同じ形）。
/// </remarks>
internal static class LoupeBackgroundRenderer
{
    /// <summary>市松模様の背景を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="request">描画入力（物理ピクセルサイズ・外観・スケーリング）。</param>
    /// <remarks>
    /// ホットパス（描画毎）で <c>DrawRectangle</c> の呼び出しを最小化するため、<br/>
    /// チェス盤の交互パターン（<see cref="LoupeCheckerboard.IsDarkTile"/>）を利用し、背景色（明）を先に全面へ描画した後、<br/>
    /// 暗色タイルのみを市松位置へ描画する（描画回数を半減）。<br/>
    /// 描画の可否は <see cref="LoupeAppearance.HasCheckerColorA"/> / <see cref="LoupeAppearance.HasCheckerColorB"/>（コア）が単一所有する。
    /// </remarks>
    public static void DrawCheckerboard(DrawingContext context, LoupeRenderRequest request)
    {
        var appearance = request.Appearance;

        // 背景色（明色）を全面へ描画する（暗色タイルの下地）。描画しない指定の場合はスキップする。
        if (appearance.HasCheckerColorA)
        {
            context.DrawRectangle(
                LoupeBrushes.Get(appearance.CheckerColorA), null, new Rect(0, 0, request.PixelSize, request.PixelSize));
        }

        if (!appearance.HasCheckerColorB)
        {
            return;
        }

        // タイルサイズの換算と暗色タイルの位置（タイル数・座標）はコアが単一所有する（本クラスは返された矩形を塗るだけ）
        var brush = LoupeBrushes.Get(appearance.CheckerColorB);
        var checkerSize = LoupeAppearanceScaler.ScaleCheckerSize(appearance, request.Scaling);
        foreach (var tile in LoupeCheckerboard.ComputeDarkTileRects(request.PixelSize, checkerSize))
        {
            context.DrawRectangle(brush, null, new Rect(tile.X, tile.Y, tile.Width, tile.Height));
        }
    }
}
