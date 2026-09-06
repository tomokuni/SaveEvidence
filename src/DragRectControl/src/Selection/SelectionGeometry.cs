using Avalonia;

namespace Estable.AvaloniaUI.DragRect.Selection;

/// <summary>選択矩形の純粋な幾何計算（正規化・ハンドル位置・ヒットテスト・対角計算）を提供する静的クラス。</summary>
/// <remarks>
/// 状態を持たない純粋関数のみを提供し、状態管理（<see cref="SelectionController"/>）と描画層
/// （<see cref="DragRectDrawer"/>）から共通利用する（DRY 原則・疎結合）。<br/>
/// 本クラスは <see cref="Point"/> と <see cref="Rect"/> 以外の UI 要素に依存しないため、単体テストが容易である<br/>
/// （UI 境界では <see cref="AvaloniaGeometryAdapters"/> / <see cref="RectDExt"/> で Avalonia / System.Drawing と変換する）。<br/>
/// 描画層は本クラスのみに依存し、状態保持クラス（Controller）へ直接依存しない。<br/>
/// <b>依存分離</b>: コアの幾何計算（本クラス）は UI フレームワークに依存しない（移植性・テスト容易性）。
/// </remarks>
internal static class SelectionGeometry
{
    /// <summary>始点と終点から正規化された矩形を生成する。</summary>
    /// <param name="start">始点。</param>
    /// <param name="end">終点。</param>
    /// <returns>常に Width, Height が正の矩形。</returns>
    public static Rect CreateFromPoints(Point start, Point end)
        => new(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Abs(end.X - start.X),
            Math.Abs(end.Y - start.Y));

    /// <summary>指定矩形上のドラッグハンドルの位置を算出する。</summary>
    /// <param name="rect">矩形。</param>
    /// <param name="handle">ハンドル種別。</param>
    /// <returns>ハンドルの位置。未知のハンドルの場合は右下隅。</returns>
    /// <remarks>
    /// ハンドル位置はヒットテスト・nudge のカーソル追従先・描画（ハンドル矩形の中心）で共通利用する（DRY 原則）。<br/>
    /// 描画側（<see cref="DragRectDrawer"/>）は本メソッドからハンドル中心位置を取得し、ハンドルサイズ分を展開して描画する。<br/>
    /// </remarks>
    public static Point GetHandlePosition(Rect rect, DragHandle handle) => handle switch
    {
        DragHandle.TopLeft => rect.TopLeft,
        DragHandle.TopCenter => new Point(rect.Center.X, rect.Top),
        DragHandle.TopRight => rect.TopRight,
        DragHandle.MiddleLeft => new Point(rect.Left, rect.Center.Y),
        DragHandle.MiddleRight => new Point(rect.Right, rect.Center.Y),
        DragHandle.BottomLeft => rect.BottomLeft,
        DragHandle.BottomCenter => new Point(rect.Center.X, rect.Bottom),
        DragHandle.BottomRight => rect.BottomRight,
        DragHandle.Move => rect.Center,
        _ => rect.BottomRight,
    };

    /// <summary>2点が指定半径以内か判定する。</summary>
    /// <param name="p1">1点目。</param>
    /// <param name="p2">2点目。</param>
    /// <param name="radius">判定半径。</param>
    /// <returns>半径以内の場合は <see langword="true"/>。</returns>
    public static bool IsNear(Point p1, Point p2, double radius)
        => Math.Abs(p1.X - p2.X) <= radius && Math.Abs(p1.Y - p2.Y) <= radius;

    /// <summary>矩形の「始点でない方の隅」（終点）を取得する。</summary>
    /// <param name="rect">選択矩形。</param>
    /// <param name="startPoint">選択の始点。</param>
    /// <returns>始点と反対側の隅の座標。</returns>
    /// <remarks>
    /// 矩形の X が始点 X と一致する場合は右辺（Right）、それ以外は左辺（X）を終点とする。<br/>
    /// Y も同様に下辺（Bottom）または上辺（Y）を終点とする。<br/>
    /// nudge の終点移動・基点更新で共通利用する（DRY 原則）。<br/>
    /// </remarks>
    public static Point GetOppositeCorner(Rect rect, Point startPoint)
        => new(
            Math.Abs(startPoint.X - rect.X) < 0.001 ? rect.Right : rect.X,
            Math.Abs(startPoint.Y - rect.Y) < 0.001 ? rect.Bottom : rect.Y);
}

