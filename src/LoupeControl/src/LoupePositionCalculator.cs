using Avalonia;

namespace Estable.AvaloniaUI.Loupe;

/// <summary>ルーペの表示位置・サイズ計算ロジック（純粋関数）。</summary>
/// <remarks>
/// <see cref="LoupeControl.UpdatePosition"/> の表示サイズクランプと表示位置計算（カーソル右下 + オフセット + クランプ）を<br/>
/// UI 非依存の純粋関数として提供する。<br/>
/// 座標変換（<see cref="ICoordTransform.Inverse"/>）は呼び出し側で行い、本クラスは変換後（DIP）の座標を受け取る。<br/>
/// UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class LoupePositionCalculator
{
    /// <summary>ルーペの表示サイズ・位置を計算する（親パネルサイズへのクランプ・オフセット・範囲クランプ）。</summary>
    /// <param name="requestedSize">基本サイズ（DIP）。</param>
    /// <param name="parentSize">親パネルのサイズ（DIP）。</param>
    /// <param name="hasParentSize">親パネルのサイズが確定しているか（正の幅・高さを持つか）。</param>
    /// <param name="centerDip">ルーペ中心（変換後の親パネル DIP 座標）。</param>
    /// <param name="offset">表示オフセット（カーソル右下方向）。</param>
    /// <returns>表示サイズ（正方形）と表示位置（左上座標）。</returns>
    /// <remarks>
    /// 表示サイズは基本サイズを親パネルの最小辺にクランプする。<br/>
    /// 表示位置は中心（DIP）+ オフセット、親パネルサイズ内にクランプする。
    /// </remarks>
    public static (Size Size, Point Position) Compute(
        double requestedSize, Size parentSize, bool hasParentSize,
        Point centerDip, double offset)
    {
        double size = requestedSize;
        if (hasParentSize)
        {
            size = Math.Min(size, Math.Min(parentSize.Width, parentSize.Height));
        }

        double mx = centerDip.X + offset;
        double my = centerDip.Y + offset;
        if (hasParentSize)
        {
            mx = Math.Clamp(mx, 0, Math.Max(0, parentSize.Width - size));
            my = Math.Clamp(my, 0, Math.Max(0, parentSize.Height - size));
        }

        return (new Size(size, size), new Point(mx, my));
    }
}
