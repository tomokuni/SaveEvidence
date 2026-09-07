using Avalonia;
using Avalonia.Media;

namespace Estable.AvaloniaUI.Loupe.Rendering;

/// <summary>ルーペの背景（市松模様）を描画します。</summary>
internal static class LoupeBackgroundRenderer
{
    /// <summary>市松模様の背景を描画する。</summary>
    /// <param name="context">描画コンテキスト。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="checkerSize">市松模様の 1 タイルサイズ（ピクセル）。1 未満は 1 に正規化される。</param>
    /// <param name="colorA">色A（明色）。null の場合は透明。</param>
    /// <param name="colorB">色B（暗色）。null の場合は透明。</param>
    public static void DrawCheckerboard(DrawingContext context, int pixelSize, int checkerSize, IBrush? colorA, IBrush? colorB)
    {
        var size = Math.Max(1, checkerSize);
        var light = colorA ?? Brushes.Transparent;
        var dark = colorB ?? Brushes.Transparent;

        for (var y = 0; y < pixelSize; y += size)
        {
            for (var x = 0; x < pixelSize; x += size)
            {
                var brush = IsDarkChecker(x / size, y / size) ? dark : light;
                context.DrawRectangle(brush, null, new Rect(x, y, size, size));
            }
        }
    }

    /// <summary>市松模様のタイル（行・列インデックス）が暗色かどうかを判定する。</summary>
    /// <param name="col">タイルの列インデックス（左から 0 始まり）。</param>
    /// <param name="row">タイルの行インデックス（上から 0 始まり）。</param>
    /// <returns>暗色（色B）なら true、明色（色A）なら false。</returns>
    /// <remarks>
    /// 交互パターン（(col + row) が奇数なら暗色）を純粋関数で判定する。<br/>
    /// UI 非依存のため、<c>[Fact]</c> で並列検証できる。
    /// </remarks>
    public static bool IsDarkChecker(int col, int row)
        => (col + row) % 2 == 1;
}
