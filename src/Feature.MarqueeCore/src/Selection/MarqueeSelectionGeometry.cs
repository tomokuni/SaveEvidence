using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.MarqueeCore.Interaction;

namespace EsUtil.Feature.MarqueeCore.Selection;

/// <summary>選択矩形の純粋な幾何計算（正規化・ハンドル位置・ヒットテスト・対角計算・ドラッグ変形・境界クリップ）を提供する静的クラス。</summary>
/// <remarks>
/// 状態を持たない純粋関数のみを提供し、状態機械（<see cref="EsUtil.Feature.MarqueeCore.Interaction.MarqueeState"/>）と描画内容の解決から共通利用する（DRY 原則・疎結合）。<br/>
/// コア型（<see cref="EsRect"/> / <see cref="EsPoint"/> / <see cref="EsSize"/>）のみに依存し、UI フレームワークへ依存しない<br/>
/// （移植性・テスト容易性。UI 型との相互変換は各 UI 実装の Interop 層が担う）。<br/>
/// <br/>
/// <b>可視性</b>: ハンドル一覧（<see cref="DirectionalHandles"/>）とハンドル位置（<see cref="GetHandlePosition"/>）を使うのは<br/>
/// コアの描画内容の解決（<see cref="Marquee.ResolveContent"/>）だけであり、描画層は解決済みの矩形を受け取る。<br/>
/// それ以外（ドラッグ変形・境界クランプ・ヒットテスト）は状態機械の内部手順であるため internal とする<br/>
/// （利用側はファサード <see cref="Marquee"/> を通す。使わないメンバーを公開しない）。
/// </remarks>
internal static class MarqueeSelectionGeometry
{
    /// <summary>描画対象の 8 方向ハンドル（<see cref="MarqueeHitTarget.None"/> / <see cref="MarqueeHitTarget.Move"/> を除く）。</summary>
    /// <remarks>
    /// 列挙順は <see cref="MarqueeHitTarget"/> の宣言順。描画層は本一覧を走査してハンドルを描く（ハンドル一覧の単一所有）。<br/>
    /// 読み取り専用の一覧として公開する（共有の一覧を呼び出し側が書き換えない）。
    /// </remarks>
    internal static IReadOnlyList<MarqueeHitTarget> DirectionalHandles { get; } =
        [.. Enum.GetValues<MarqueeHitTarget>().Where(static h => h is not (MarqueeHitTarget.None or MarqueeHitTarget.Move))];

    /// <summary>ハンドル種別をカーソル形状の種別へ変換する。</summary>
    /// <param name="handle">ハンドル種別。</param>
    /// <returns>カーソル形状の種別。ハンドル外（<see cref="MarqueeHitTarget.None"/>）の場合は <see cref="MarqueeCursorKind.Default"/>。</returns>
    /// <remarks>
    /// 「どのハンドルが同じカーソル形状になるか」を単一所有する（各 UI は自 UI のカーソル型へ写像するだけ）。<br/>
    /// 左上と右下、右上と左下は同じ斜めカーソルになる（対角のハンドルは同じ向きの図形で表す）。
    /// </remarks>
    internal static MarqueeCursorKind ToCursorKind(MarqueeHitTarget handle) => handle switch
    {
        MarqueeHitTarget.TopLeft or MarqueeHitTarget.BottomRight => MarqueeCursorKind.DiagonalNWSE,
        MarqueeHitTarget.TopRight or MarqueeHitTarget.BottomLeft => MarqueeCursorKind.DiagonalNESW,
        MarqueeHitTarget.TopCenter or MarqueeHitTarget.BottomCenter => MarqueeCursorKind.Vertical,
        MarqueeHitTarget.MiddleLeft or MarqueeHitTarget.MiddleRight => MarqueeCursorKind.Horizontal,
        MarqueeHitTarget.Move => MarqueeCursorKind.Move,
        _ => MarqueeCursorKind.Default,
    };

    /// <summary>始点と終点から正規化された矩形を生成する。</summary>
    /// <param name="start">始点。</param>
    /// <param name="end">終点。</param>
    /// <returns>常に Width, Height が正の矩形。</returns>
    internal static EsRect CreateFromPoints(EsPoint start, EsPoint end)
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
    internal static EsPoint GetHandlePosition(EsRect rect, MarqueeHitTarget handle) => handle switch
    {
        MarqueeHitTarget.TopLeft => rect.TopLeft,
        MarqueeHitTarget.TopCenter => new EsPoint(rect.Center.X, rect.Y),
        MarqueeHitTarget.TopRight => rect.TopRight,
        MarqueeHitTarget.MiddleLeft => new EsPoint(rect.X, rect.Center.Y),
        MarqueeHitTarget.MiddleRight => new EsPoint(rect.Right, rect.Center.Y),
        MarqueeHitTarget.BottomLeft => rect.BottomLeft,
        MarqueeHitTarget.BottomCenter => new EsPoint(rect.Center.X, rect.Bottom),
        MarqueeHitTarget.BottomRight => rect.BottomRight,
        MarqueeHitTarget.Move => rect.Center,
        _ => rect.BottomRight,
    };

    /// <summary>2点が指定半径以内か判定する。</summary>
    /// <param name="p1">1点目。</param>
    /// <param name="p2">2点目。</param>
    /// <param name="radius">判定半径。</param>
    /// <returns>半径以内の場合は true。</returns>
    internal static bool IsNear(EsPoint p1, EsPoint p2, double radius)
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
    internal static EsPoint GetOppositeCorner(EsRect rect, EsPoint startPoint)
        => new(
            Math.Abs(startPoint.X - rect.X) < 0.001 ? rect.Right : rect.X,
            Math.Abs(startPoint.Y - rect.Y) < 0.001 ? rect.Bottom : rect.Y);

    /// <summary>点をドメイン境界内にクリップする。</summary>
    /// <param name="p">クリップする点。</param>
    /// <param name="bounds">クリップ範囲。</param>
    /// <returns>境界内に収めた点。</returns>
    internal static EsPoint ClampPointToBounds(EsPoint p, EsRect bounds)
        => new(
            Math.Clamp(p.X, bounds.X, bounds.Right),
            Math.Clamp(p.Y, bounds.Y, bounds.Bottom));

    /// <summary>矩形をドメイン境界内にクリップする（ハンドル種別に応じて固定角を維持）。</summary>
    /// <param name="rect">クリップする矩形。</param>
    /// <param name="bounds">クリップ範囲。</param>
    /// <param name="handle">アクティブなハンドル。</param>
    /// <returns>境界内に収めた矩形。</returns>
    /// <remarks>
    /// <see cref="MarqueeHitTarget.Move"/> の場合は矩形全体が境界内に収まるようにシフトする（サイズは維持）。<br/>
    /// それ以外のハンドル変形は固定角（ハンドルと反対側の角）を動かさず、動く辺のみ境界内に制限する。
    /// </remarks>
    internal static EsRect ClampRectToBounds(EsRect rect, EsRect bounds, MarqueeHitTarget handle)
    {
        double l = rect.X;
        double t = rect.Y;
        double r = rect.Right;
        double b = rect.Bottom;

        if (handle == MarqueeHitTarget.Move)
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
    /// <remarks>四隅の包絡矩形を取る手順は <see cref="EsRect.ClampToArea(EsSize)"/> が単一所有する（ルーペの位置と同じ規則）。</remarks>
    internal static EsRect ClampRectToDomainBounds(EsRect rect, EsSize bounds)
        => rect.ClampToArea(bounds);

    /// <summary>ドラッグハンドル操作による矩形の変形を計算する。</summary>
    /// <param name="original">ドラッグ開始時の矩形。</param>
    /// <param name="dx">X 方向の移動量。</param>
    /// <param name="dy">Y 方向の移動量。</param>
    /// <param name="handle">アクティブなハンドル。</param>
    /// <param name="minSize">選択矩形の最小サイズ（補正済みの値を渡す。安全域は <c>Marquee.Limits</c> が単一所有する）。</param>
    /// <returns>変形後の矩形（反転を正規化・最小サイズ保証）。</returns>
    /// <remarks>
    /// ハンドルを反対側までドラッグした場合（l&gt;r または t&gt;b）、矩形を反転（正規化）する。<br/>
    /// 最小サイズは幅・高さが <paramref name="minSize"/> 未満にならないよう保証する。
    /// </remarks>
    internal static EsRect ComputeDragRect(EsRect original, double dx, double dy, MarqueeHitTarget handle, double minSize)
    {
        double l = original.X;
        double t = original.Y;
        double r = original.Right;
        double b = original.Bottom;

        switch (handle)
        {
            case MarqueeHitTarget.TopLeft: l += dx; t += dy; break;
            case MarqueeHitTarget.TopCenter: t += dy; break;
            case MarqueeHitTarget.TopRight: r += dx; t += dy; break;
            case MarqueeHitTarget.MiddleLeft: l += dx; break;
            case MarqueeHitTarget.MiddleRight: r += dx; break;
            case MarqueeHitTarget.BottomLeft: l += dx; b += dy; break;
            case MarqueeHitTarget.BottomCenter: b += dy; break;
            case MarqueeHitTarget.BottomRight: r += dx; b += dy; break;
            case MarqueeHitTarget.Move: l += dx; t += dy; r += dx; b += dy; break;
        }

        double x1 = Math.Min(l, r);
        double x2 = Math.Max(l, r);
        double y1 = Math.Min(t, b);
        double y2 = Math.Max(t, b);

        var m = minSize;
        if (x2 - x1 < m) x2 = x1 + m;
        if (y2 - y1 < m) y2 = y1 + m;

        return new EsRect(x1, y1, x2 - x1, y2 - y1);
    }
}
