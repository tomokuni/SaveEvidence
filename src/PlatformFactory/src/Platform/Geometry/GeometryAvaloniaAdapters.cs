using Avalonia;

namespace Estable.AvaloniaUI.Platform.Geometry;

/// <summary>
/// <see cref="Point"/> / <see cref="Rect"/> / <see cref="Size"/> / <see cref="Vector"/> と
/// Avalonia の <see cref="Point"/> / <see cref="Rect"/> / <see cref="Size"/> / <see cref="Vector"/> を相互変換するアダプタ。
/// </summary>
/// <remarks>
/// <b>依存分離</b>: コアロジック（<see cref="Point"/> 等）は UI フレームワークに依存せず、<br/>
/// UI 境界（コントロール描画・イベント引数）でのみ本アダプタで変換する（コアと UI の一方向依存）。<br/>
/// <b>Avalonia 依存</b>: 本クラスは Avalonia のみに依存する（コアロジックには依存しない）。
/// </remarks>
public static class AvaloniaGeometryAdapters
{
    /// <summary>Avalonia Point へ変換する。</summary>
    /// <param name="point">変換元の点。</param>
    /// <returns>Avalonia Point。</returns>
    public static Point ToAvaloniaPoint(this Point point) => new(point.X, point.Y);

    /// <summary>Avalonia Size へ変換する。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>Avalonia Size。</returns>
    public static Size ToAvaloniaSize(this Size size) => new(size.Width, size.Height);

    /// <summary>Avalonia Vector へ変換する。</summary>
    /// <param name="vector">変換元のベクター。</param>
    /// <returns>Avalonia Vector。</returns>
    public static Vector ToAvaloniaVector(this Vector vector) => new(vector.X, vector.Y);

    /// <summary>Avalonia Rect へ変換する。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>Avalonia Rect。</returns>
    public static Rect ToAvaloniaRect(this Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>Avalonia Point から構築する。</summary>
    /// <param name="point">変換元の点。</param>
    /// <returns>Point。</returns>
    public static Point ToPoint(this Point point) => new(point.X, point.Y);

    /// <summary>Avalonia Size から構築する。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>Size。</returns>
    public static Size ToSize(this Size size) => new(size.Width, size.Height);

    /// <summary>Avalonia Vector から構築する。</summary>
    /// <param name="vector">変換元のベクター。</param>
    /// <returns>Vector。</returns>
    public static Vector ToVector(this Vector vector) => new(vector.X, vector.Y);

    /// <summary>Avalonia Rect から構築する。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>Rect。</returns>
    public static Rect ToRect(this Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
