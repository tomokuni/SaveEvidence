using Estable.Primitives;

namespace Estable.AvaloniaUI.Platform.Geometry;

/// <summary>
/// <see cref="PointD"/> / <see cref="RectD"/> / <see cref="SizeD"/> / <see cref="VectorD"/> を
/// Avalonia / System.Drawing の型へ変換するアダプタ（UI 境界専用）。
/// </summary>
/// <remarks>
/// コアロジック（<see cref="PointD"/> 等）は UI フレームワークに依存しない。<br/>
/// UI 境界（コントロール描画・イベント引数・表示）でのみ本アダプタで変換する（コアと UI の分離）。<br/>
/// <b>依存</b>: 本クラスは <c>Avalonia</c> のみに依存し、コアロジックには依存しない（一方向）。
/// </remarks>
public static class RectDExt
{
    /// <summary>System.Drawing.Point へ変換する（int に切り捨て）。</summary>
    /// <param name="point">変換元の点。</param>
    /// <returns>System.Drawing.Point。</returns>
    public static System.Drawing.Point ToDrawingPoint(this PointD point)
        => new((int)point.X, (int)point.Y);

    /// <summary>System.Drawing.Size へ変換する（int に切り捨て）。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>System.Drawing.Size。</returns>
    public static System.Drawing.Size ToDrawingSize(this SizeD size)
        => new((int)size.Width, (int)size.Height);

    /// <summary>System.Drawing.Rectangle へ変換する（int に切り捨て）。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>System.Drawing.Rectangle。</returns>
    public static System.Drawing.Rectangle ToDrawingRect(this RectD rect)
        => new((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);

    /// <summary>System.Drawing.Point から構築する。</summary>
    /// <param name="point">変換元の点。</param>
    /// <returns>PointD。</returns>
    public static PointD ToPointD(this System.Drawing.Point point) => new(point.X, point.Y);

    /// <summary>System.Drawing.Rectangle から構築する。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>RectD。</returns>
    public static RectD ToRectD(this System.Drawing.Rectangle rect)
        => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>System.Drawing.Size から構築する。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>SizeD。</returns>
    public static SizeD ToSizeD(this System.Drawing.Size size) => new(size.Width, size.Height);
}
