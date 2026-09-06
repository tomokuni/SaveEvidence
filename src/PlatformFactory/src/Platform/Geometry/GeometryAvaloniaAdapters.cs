using Avalonia;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaRect = Avalonia.Rect;
using AvaloniaSize = Avalonia.Size;
using AvaloniaVector = Avalonia.Vector;
using Estable;

namespace Estable.AvaloniaUI.Platform.Geometry;

/// <summary>
/// <see cref="PointD"/> / <see cref="RectD"/> / <see cref="SizeD"/> / <see cref="VectorD"/> と
/// Avalonia の <see cref="Point"/> / <see cref="Rect"/> / <see cref="Size"/> / <see cref="Vector"/> を相互変換するアダプタ。
/// </summary>
/// <remarks>
/// <b>依存分離</b>: コアロジック（<see cref="PointD"/> 等）は UI フレームワークに依存せず、<br/>
/// UI 境界（コントロール描画・イベント引数）でのみ本アダプタで変換する（コアと UI の一方向依存）。<br/>
/// <b>Avalonia 依存</b>: 本クラスは Avalonia のみに依存する（コアロジックには依存しない）。
/// </remarks>
public static class AvaloniaGeometryAdapters
{
    /// <summary>Avalonia Point へ変換する。</summary>
    /// <param name="point">変換元の点。</param>
    /// <returns>Avalonia Point。</returns>
    public static AvaloniaPoint ToAvaloniaPoint(this PointD point) => new(point.X, point.Y);

    /// <summary>Avalonia Size へ変換する。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>Avalonia Size。</returns>
    public static AvaloniaSize ToAvaloniaSize(this SizeD size) => new(size.Width, size.Height);

    /// <summary>Avalonia Vector へ変換する。</summary>
    /// <param name="vector">変換元のベクター。</param>
    /// <returns>Avalonia Vector。</returns>
    public static AvaloniaVector ToAvaloniaVector(this VectorD vector) => new(vector.X, vector.Y);

    /// <summary>Avalonia Rect へ変換する。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>Avalonia Rect。</returns>
    public static AvaloniaRect ToAvaloniaRect(this RectD rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>Avalonia Point から構築する。</summary>
    /// <param name="point">変換元の点。</param>
    /// <returns>PointD。</returns>
    public static PointD ToPointD(this AvaloniaPoint point) => new(point.X, point.Y);

    /// <summary>Avalonia Size から構築する。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>SizeD。</returns>
    public static SizeD ToSizeD(this AvaloniaSize size) => new(size.Width, size.Height);

    /// <summary>Avalonia Vector から構築する。</summary>
    /// <param name="vector">変換元のベクター。</param>
    /// <returns>VectorD。</returns>
    public static VectorD ToVectorD(this AvaloniaVector vector) => new(vector.X, vector.Y);

    /// <summary>Avalonia Rect から構築する。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>RectD。</returns>
    public static RectD ToRectD(this AvaloniaRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
