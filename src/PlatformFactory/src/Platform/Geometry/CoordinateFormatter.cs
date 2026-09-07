using Avalonia;

namespace Estable.AvaloniaUI.Platform.Geometry;

/// <summary>座標を表示用の文字列へ整形するユーティリティ（純粋関数）。</summary>
/// <remarks>
/// 座標ラベル（カーソル座標・ルーペ中心座標等）の表示整形を UI 非依存の純粋関数として提供する。<br/>
/// <see cref="LoupeControl"/> と <see cref="DragRectControl"/> で共用する（重複排除・DRY）。<br/>
/// UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class CoordinateFormatter
{
    /// <summary>座標を <c>(x, y)</c> 形式の文字列へ整形する（整数に丸める）。</summary>
    /// <param name="point">整形する座標。</param>
    /// <returns><c>(x, y)</c> 形式の表示テキスト。</returns>
    /// <remarks>座標は整数（<c>F0</c>）に丸めて表示する（ピクセル座標として可読性を優先）。</remarks>
    public static string Format(Point point)
        => $"({point.X:F0}, {point.Y:F0})";
}
