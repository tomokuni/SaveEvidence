using Avalonia;
using PointInt = System.Drawing.Point;
using RectInt = System.Drawing.Rectangle;
using SizeInt = System.Drawing.Size;

namespace Estable.AvaloniaUI.Platform.Geometry;

/// <summary>
/// <see cref="Point"/> / <see cref="Rect"/> / <see cref="Size"/> / <see cref="Vector"/> を
/// Avalonia / System.Drawing の型へ変換するアダプタ（UI 境界専用）。
/// </summary>
/// <remarks>
/// コアロジック（<see cref="Point"/> 等）は UI フレームワークに依存しない。<br/>
/// UI 境界（コントロール描画・イベント引数・表示）でのみ本アダプタで変換する（コアと UI の分離）。<br/>
/// <b>依存</b>: 本クラスは <c>Avalonia</c> のみに依存し、コアロジックには依存しない（一方向）。
/// </remarks>
public static class RectDExt
{
    /// <summary>System.Drawing.Point へ変換する（int に切り捨て）。</summary>
    /// <param name="point">変換元の点。</param>
    /// <returns>System.Drawing.Point。</returns>
    public static PointInt ToDrawingPoint(this Point point)
        => new((int)point.X, (int)point.Y);

    /// <summary>System.Drawing.Size へ変換する（int に切り捨て）。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>System.Drawing.Size。</returns>
    public static SizeInt ToDrawingSize(this Size size)
        => new((int)size.Width, (int)size.Height);

    /// <summary>System.Drawing.Rectangle へ変換する（int に切り捨て）。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>System.Drawing.Rectangle。</returns>
    public static RectInt ToDrawingRect(this Rect rect)
        => new((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);

    /// <summary>System.Drawing.Point から構築する。</summary>
    /// <param name="point">変換元の点。</param>
    /// <returns>Point。</returns>
    public static Point ToPoint(this PointInt point) => new(point.X, point.Y);

    /// <summary>System.Drawing.Rectangle から構築する。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>Rect。</returns>
    public static Rect ToRect(this RectInt rect)
        => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>System.Drawing.Size から構築する。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>Size。</returns>
    public static Size ToSize(this SizeInt size) => new(size.Width, size.Height);
}
