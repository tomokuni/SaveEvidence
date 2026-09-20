using Avalonia;
using Estable.Platform.Geometry;

namespace Estable.AvaloniaUI.Platform;

/// <summary>コア幾何型と Avalonia の座標型を相互変換する。</summary>
/// <remarks>
/// コアロジック（<see cref="EsPoint"/> 等）は UI フレームワークに依存せず、UI 境界でのみ本クラスで変換する。<br/>
/// 変換は拡張メソッドとして提供し、呼び出し側は <c>using Estable.AvaloniaUI.Platform;</c> のみで利用できる。<br/>
/// </remarks>
public static class EsGeometryAvaloniaInterop
{
    /// <summary><see cref="EsPoint"/> を Avalonia の座標型へ変換する。</summary>
    /// <param name="point">変換元の座標。</param>
    /// <returns>Avalonia の座標。</returns>
    public static Point ToAvaloniaPoint(this EsPoint point) => new(point.X, point.Y);

    /// <summary>Avalonia の座標型を <see cref="EsPoint"/> へ変換する。</summary>
    /// <param name="point">変換元の座標。</param>
    /// <returns>コアの座標。</returns>
    public static EsPoint ToEsPoint(this Point point) => new(point.X, point.Y);

    /// <summary><see cref="EsSize"/> を Avalonia のサイズ型へ変換する。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>Avalonia のサイズ。</returns>
    public static Size ToAvaloniaSize(this EsSize size) => new(size.Width, size.Height);

    /// <summary>Avalonia のサイズ型を <see cref="EsSize"/> へ変換する。</summary>
    /// <param name="size">変換元のサイズ。</param>
    /// <returns>コアのサイズ。</returns>
    public static EsSize ToEsSize(this Size size) => new(size.Width, size.Height);

    /// <summary><see cref="EsVector"/> を Avalonia のベクター型へ変換する。</summary>
    /// <param name="vector">変換元のベクター。</param>
    /// <returns>Avalonia のベクター。</returns>
    public static Vector ToAvaloniaVector(this EsVector vector) => new(vector.X, vector.Y);

    /// <summary>Avalonia のベクター型を <see cref="EsVector"/> へ変換する。</summary>
    /// <param name="vector">変換元のベクター。</param>
    /// <returns>コアのベクター。</returns>
    public static EsVector ToEsVector(this Vector vector) => new(vector.X, vector.Y);

    /// <summary><see cref="EsRect"/> を Avalonia の矩形型へ変換する。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>Avalonia の矩形。</returns>
    public static Rect ToAvaloniaRect(this EsRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>Avalonia の矩形型を <see cref="EsRect"/> へ変換する。</summary>
    /// <param name="rect">変換元の矩形。</param>
    /// <returns>コアの矩形。</returns>
    public static EsRect ToEsRect(this Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary><see cref="EsPixelPoint"/> を Avalonia の画面物理座標型へ変換する。</summary>
    /// <param name="point">変換元の画面物理座標。</param>
    /// <returns>Avalonia の画面物理座標。</returns>
    public static PixelPoint ToAvaloniaPixelPoint(this EsPixelPoint point) => new(point.X, point.Y);

    /// <summary>Avalonia の画面物理座標型を <see cref="EsPixelPoint"/> へ変換する。</summary>
    /// <param name="point">変換元の画面物理座標。</param>
    /// <returns>コアの画面物理座標。</returns>
    public static EsPixelPoint ToEsPixelPoint(this PixelPoint point) => new(point.X, point.Y);

    /// <summary><see cref="EsPixelRect"/> を Avalonia の画面物理矩形型へ変換する。</summary>
    /// <param name="rect">変換元の画面物理矩形。</param>
    /// <returns>Avalonia の画面物理矩形。</returns>
    public static PixelRect ToAvaloniaPixelRect(this EsPixelRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>Avalonia の画面物理矩形型を <see cref="EsPixelRect"/> へ変換する。</summary>
    /// <param name="rect">変換元の画面物理矩形。</param>
    /// <returns>コアの画面物理矩形。</returns>
    public static EsPixelRect ToEsPixelRect(this PixelRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
