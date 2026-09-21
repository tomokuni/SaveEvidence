using EsUtil.Platform.Core.Geometry;

namespace Estable.DragRect.Selection;

/// <summary>選択矩形の純粋な幾何計算（正規化・ハンドル位置・ヒットテスト・対角計算・ドラッグ変形・境界クリップ）を提供する静的クラス。</summary>
/// <remarks>
/// 状態を持たない純粋関数のみを提供し、状態管理（<see cref="DragRectState"/>）と描画層（UI 実装側）から共通利用する（DRY 原則・疎結合）。<br/>
/// コア型（<see cref="EsRect"/> / <see cref="EsPoint"/> / <see cref="EsSize"/>）のみに依存し、UI フレームワークへ依存しない<br/>
/// （移植性・テスト容易性。UI 型との相互変換は各 UI 実装の Interop 層が担う）。<br/>
/// 描画層は本クラスのみに依存し、状態保持クラスへ直接依存しない。
/// </remarks>
internal static class SelectionGeometry
{
    /// <summary>選択矩形の最小サイズの下限（ドメイン座標。0 以下の指定を無効化）。</summary>
    private const double MinSelectionSizeLowerBound = 1.0;

    /// <summary>始点と終点から正規化された矩形を生成する。</summary>
    /// <param name="start">始点。</param>
    /// <param name="end">終点。</param>
    /// <returns>常に Width, Height が正の矩形。</returns>
    public static EsRect CreateFromPoints(EsPoint start, EsPoint end)
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
    /// 描画側は本メソッドからハンドル中心位置を取得し、ハンドルサイズ分を展開して描画する。<br/>
    /// </remarks>
    public static EsPoint GetHandlePosition(EsRect rect, DragHandle handle) => handle switch
    {
        DragHandle.TopLeft => rect.TopLeft,
        DragHandle.TopCenter => new EsPoint(rect.Center.X, rect.Y),
        DragHandle.TopRight => rect.TopRight,
        DragHandle.MiddleLeft => new EsPoint(rect.X, rect.Center.Y),
        DragHandle.MiddleRight => new EsPoint(rect.Right, rect.Center.Y),
        DragHandle.BottomLeft => rect.BottomLeft,
        DragHandle.BottomCenter => new EsPoint(rect.Center.X, rect.Bottom),
        DragHandle.BottomRight => rect.BottomRight,
        DragHandle.Move => rect.Center,
        _ => rect.BottomRight,
    };

    /// <summary>2点が指定半径以内か判定する。</summary>
    /// <param name="p1">1点目。</param>
    /// <param name="p2">2点目。</param>
    /// <param name="radius">判定半径。</param>
    /// <returns>半径以内の場合は true。</returns>
    public static bool IsNear(EsPoint p1, EsPoint p2, double radius)
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
    public static EsPoint GetOppositeCorner(EsRect rect, EsPoint startPoint)
        => new(
            Math.Abs(startPoint.X - rect.X) < 0.001 ? rect.Right : rect.X,
            Math.Abs(startPoint.Y - rect.Y) < 0.001 ? rect.Bottom : rect.Y);

    /// <summary>点をドメイン境界内にクリップする。</summary>
    /// <param name="p">クリップする点。</param>
    /// <param name="bounds">クリップ範囲。</param>
    /// <returns>境界内に収めた点。</returns>
    public static EsPoint ClampPointToBounds(EsPoint p, EsRect bounds)
        => new(
            Math.Clamp(p.X, bounds.X, bounds.Right),
            Math.Clamp(p.Y, bounds.Y, bounds.Bottom));

    /// <summary>矩形をドメイン境界内にクリップする（ハンドル種別に応じて固定角を維持）。</summary>
    /// <param name="rect">クリップする矩形。</param>
    /// <param name="bounds">クリップ範囲。</param>
    /// <param name="handle">アクティブなハンドル。</param>
    /// <returns>境界内に収めた矩形。</returns>
    /// <remarks>
    /// <see cref="DragHandle.Move"/> の場合は矩形全体が境界内に収まるようにシフトする（サイズは維持）。<br/>
    /// それ以外のハンドル変形は固定角（ハンドルと反対側の角）を動かさず、動く辺のみ境界内に制限する。
    /// </remarks>
    public static EsRect ClampRectToBounds(EsRect rect, EsRect bounds, DragHandle handle)
    {
        double l = rect.X;
        double t = rect.Y;
        double r = rect.Right;
        double b = rect.Bottom;

        if (handle == DragHandle.Move)
        {
            double shiftX = l < bounds.X ? bounds.X - l : (r > bounds.Right ? bounds.Right - r : 0);
            double shiftY = t < bounds.Y ? bounds.Y - t : (b > bounds.Bottom ? bounds.Bottom - b : 0);
            l += shiftX;
            r += shiftX;
            t += shiftY;
            b += shiftY;
        }
        else
        {
            if (l < bounds.X) l = bounds.X;
            if (t < bounds.Y) t = bounds.Y;
            if (r > bounds.Right) r = bounds.Right;
            if (b > bounds.Bottom) b = bounds.Bottom;
        }

        return new EsRect(l, t, r - l, b - t);
    }

    /// <summary>矩形をドメイン境界（サイズ）内にクリップする（四隅の包絡矩形・bounding box）。</summary>
    /// <param name="rect">クリップする矩形（ドメイン座標）。</param>
    /// <param name="bounds">ドメイン境界のサイズ。</param>
    /// <returns>境界内に収めた矩形。</returns>
    /// <remarks>矩形の四隅を境界内にクリップし、その包絡矩形（bounding box）を返す（外部復元・設定変更時の即時適用用）。</remarks>
    public static EsRect ClampRectToDomainBounds(EsRect rect, EsSize bounds)
    {
        var l = Math.Clamp(rect.X, 0, bounds.Width);
        var t = Math.Clamp(rect.Y, 0, bounds.Height);
        var r = Math.Clamp(rect.Right, 0, bounds.Width);
        var b = Math.Clamp(rect.Bottom, 0, bounds.Height);
        return new EsRect(l, t, Math.Max(0, r - l), Math.Max(0, b - t));
    }

    /// <summary>ドラッグハンドル操作による矩形の変形を計算する。</summary>
    /// <param name="original">ドラッグ開始時の矩形。</param>
    /// <param name="dx">X 方向の移動量。</param>
    /// <param name="dy">Y 方向の移動量。</param>
    /// <param name="handle">アクティブなハンドル。</param>
    /// <param name="minSize">選択矩形の最小サイズ。</param>
    /// <returns>変形後の矩形（反転を正規化・最小サイズ保証）。</returns>
    /// <remarks>
    /// ハンドルを反対側までドラッグした場合（l&gt;r または t&gt;b）、矩形を反転（正規化）する。<br/>
    /// 最小サイズは幅・高さが <paramref name="minSize"/> 未満にならないよう保証する。
    /// </remarks>
    public static EsRect ComputeDragRect(EsRect original, double dx, double dy, DragHandle handle, double minSize)
    {
        double l = original.X;
        double t = original.Y;
        double r = original.Right;
        double b = original.Bottom;

        switch (handle)
        {
            case DragHandle.TopLeft: l += dx; t += dy; break;
            case DragHandle.TopCenter: t += dy; break;
            case DragHandle.TopRight: r += dx; t += dy; break;
            case DragHandle.MiddleLeft: l += dx; break;
            case DragHandle.MiddleRight: r += dx; break;
            case DragHandle.BottomLeft: l += dx; b += dy; break;
            case DragHandle.BottomCenter: b += dy; break;
            case DragHandle.BottomRight: r += dx; b += dy; break;
            case DragHandle.Move: l += dx; t += dy; r += dx; b += dy; break;
        }

        double x1 = Math.Min(l, r);
        double x2 = Math.Max(l, r);
        double y1 = Math.Min(t, b);
        double y2 = Math.Max(t, b);

        var m = Math.Max(minSize, MinSelectionSizeLowerBound);
        if (x2 - x1 < m) x2 = x1 + m;
        if (y2 - y1 < m) y2 = y1 + m;

        return new EsRect(x1, y1, x2 - x1, y2 - y1);
    }
}
