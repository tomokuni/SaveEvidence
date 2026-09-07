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
    /// <remarks>
    /// ホットパス（描画毎）で <c>DrawRectangle</c> の呼び出しを最小化するため、<br/>
    /// チェス盤の交互パターン（<see cref="IsDarkChecker"/>）を利用し、背景色（明）を先に全面へ描画した後、<br/>
    /// 暗色タイルのみを市松位置へ描画する（描画回数を半減）。<br/>
    /// 背景色が透明の場合は全面描画をスキップし、暗色タイルのみを描画する（無駄な透明描画を排除）。
    /// </remarks>
    public static void DrawCheckerboard(DrawingContext context, int pixelSize, int checkerSize, IBrush? colorA, IBrush? colorB)
    {
        var size = Math.Max(1, checkerSize);
        var light = colorA ?? Brushes.Transparent;
        var dark = colorB ?? Brushes.Transparent;

        // 背景色（明色）を全面へ描画する（暗色タイルの下地）。透明の場合はスキップする。
        if (!ReferenceEquals(light, Brushes.Transparent))
        {
            context.DrawRectangle(light, null, new Rect(0, 0, pixelSize, pixelSize));
        }

        // 暗色タイルのみを市松位置（(col + row) が奇数）へ描画する。
        // 隣接する暗色タイルが上下左右で接しないため、1 タイルずつの描画で正しく市松になる。
        // ループはタイル座標（col / row）で直接回し、除算（x / size）を回避する（ホットパス最適化）。
        var tiles = (int)Math.Ceiling((double)pixelSize / size);
        for (var row = 0; row < tiles; row++)
        {
            var y = row * size;
            for (var col = 0; col < tiles; col++)
            {
                if (IsDarkChecker(col, row))
                {
                    context.DrawRectangle(dark, null, new Rect(col * size, y, size, size));
                }
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
