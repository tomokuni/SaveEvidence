namespace EsUtil.Platform.Core.Geometry;

/// <summary>矩形の辺とクランプの計算ロジック（純粋関数）。</summary>
/// <remarks>
/// 「矩形の内側に太さ分の 4 辺を取る」「矩形を領域内へクランプする」という、<br/>
/// コントロールに依存しない矩形の規則を単一所有する。<br/>
/// 各コアが個別に書くと、同じ意味の式（内側の 4 辺・四隅クランプ）が複数箇所へ散り、<br/>
/// 片方だけが負値や端の扱いを変えたときに同じ矩形から違う結果になる。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public static class EsRectMath
{
    /// <summary>矩形の内側に太さ分の余白を取った 4 辺の矩形を計算する。</summary>
    /// <param name="rect">基準の矩形。</param>
    /// <param name="thickness">辺の太さ（矩形の内側へ取る量）。負値はそのまま扱う（下限の正規化は呼び出し側が行う）。</param>
    /// <returns>上・下・左・右の順の辺の矩形（4 個）。左右の辺の高さは負にならず 0 にクランプされる。</returns>
    /// <remarks>
    /// 上辺は上端、下辺は下端から太さ分、左右の辺は上下の辺の内側へ収まる（角で二重に塗らない）。<br/>
    /// 太さの下限（0.5 未満を無効化する・1 未満を 1 にする等）は用途で異なるため本メソッドは正規化しない。
    /// </remarks>
    public static IReadOnlyList<EsRect> ComputeInnerEdges(EsRect rect, double thickness)
    {
        var inner = Math.Max(0, rect.Height - thickness * 2);
        return
        [
            new EsRect(rect.X, rect.Y, rect.Width, thickness),                            // 上
            new EsRect(rect.X, rect.Bottom - thickness, rect.Width, thickness),           // 下
            new EsRect(rect.X, rect.Y + thickness, thickness, inner),                     // 左
            new EsRect(rect.Right - thickness, rect.Y + thickness, thickness, inner),     // 右
        ];
    }

    /// <summary>矩形を領域（左上を原点・指定サイズ）内へクランプする（四隅の包絡矩形）。</summary>
    /// <param name="rect">クランプする矩形。</param>
    /// <param name="area">クランプ先の領域のサイズ（左上を原点とする。幅・高さは 0 以上）。</param>
    /// <returns>領域内へ収めた矩形。負の辺（幅・高さ）は 0 になる。</returns>
    /// <remarks>
    /// 四隅を個別にクランプし、その包絡矩形（bounding box）を返す（矩形全体を領域内へ収める）。<br/>
    /// 「四隅をクランプして包絡矩形を取る」規則を単一所有するため、一部だけがはみ出す場合も同じ手順で収まる。<br/>
    /// 領域の幅・高さが負の場合は <c>Math.Clamp</c> の前提に反する（呼び出し側が 0 以上を保証する）。
    /// </remarks>
    public static EsRect ClampToArea(EsRect rect, EsSize area)
    {
        var left = Math.Clamp(rect.X, 0, area.Width);
        var top = Math.Clamp(rect.Y, 0, area.Height);
        var right = Math.Clamp(rect.Right, 0, area.Width);
        var bottom = Math.Clamp(rect.Bottom, 0, area.Height);
        return new EsRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }
}
