namespace EsUtil.Platform.Core.Geometry;

/// <summary>UI フレームワーク非依存の矩形（左上座標＋サイズ）を表す値オブジェクト。</summary>
/// <remarks>
/// コアロジックは本型を使用し、UI 境界でのみ各 UI フレームワークの矩形型へ変換する（一方向依存）。<br/>
/// Avalonia の矩形型との変換は <c>EsGeometryAvaloniaInterop</c> が提供する。<br/>
/// </remarks>
/// <param name="X">左上 X 座標（呼び出し側の座標系に従う）。</param>
/// <param name="Y">左上 Y 座標（呼び出し側の座標系に従う）。</param>
/// <param name="Width">幅（負値は空の矩形を表す）。</param>
/// <param name="Height">高さ（負値は空の矩形を表す）。</param>
public readonly record struct EsRect(double X, double Y, double Width, double Height)
{
    /// <summary>右端の X 座標（左上 X + 幅）。</summary>
    /// <value>呼び出し側の座標系に従う。</value>
    public double Right => X + Width;

    /// <summary>下端の Y 座標（左上 Y + 高さ）。</summary>
    /// <value>呼び出し側の座標系に従う。</value>
    public double Bottom => Y + Height;

    /// <summary>幅または高さが 0 以下（切り出せる範囲・描画できる面積が無い）かどうかを取得する。</summary>
    /// <value>幅または高さが 0 以下の場合は true。</value>
    /// <remarks>
    /// 整数矩形の <see cref="EsRectInt.IsEmpty"/> と同じ判定を提供する<br/>
    /// （UI が <c>Width &gt; 0 &amp;&amp; Height &gt; 0</c> を書くと、同じ意味の判定が 0 以下の比較として散る）。
    /// </remarks>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>左上座標（<see cref="X"/> / <see cref="Y"/> と同値）を取得する。</summary>
    /// <value>左上の座標。</value>
    public EsPoint TopLeft => new(X, Y);

    /// <summary>右上座標を取得する。</summary>
    /// <value>右上の座標。</value>
    public EsPoint TopRight => new(Right, Y);

    /// <summary>左下座標を取得する。</summary>
    /// <value>左下の座標。</value>
    public EsPoint BottomLeft => new(X, Bottom);

    /// <summary>右下座標を取得する。</summary>
    /// <value>右下の座標。</value>
    public EsPoint BottomRight => new(Right, Bottom);

    /// <summary>矩形の中心座標を取得する。</summary>
    /// <value>中心の座標。</value>
    public EsPoint Center => new(X + Width / 2, Y + Height / 2);

    /// <summary>指定した点が本矩形の内側に含まれるかどうかを判定する。</summary>
    /// <param name="point">判定する点。</param>
    /// <returns>含まれる場合は true。</returns>
    /// <remarks>
    /// 左右端・上下端のいずれも含む（閉区間）。Avalonia の矩形型と同じ判定であり、
    /// 置き換え前の挙動（ハンドルのヒットテスト等）を維持する。
    /// </remarks>
    public bool Contains(EsPoint point)
        => point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;

    /// <summary>本矩形の内側に太さ分の余白を取った 4 辺の矩形を計算する。</summary>
    /// <param name="thickness">辺の太さ（矩形の内側へ取る量）。負値はそのまま扱う（下限の正規化は呼び出し側が行う）。</param>
    /// <returns>上・下・左・右の順の辺の矩形（4 個）。左右の辺の高さは負にならず 0 にクランプされる。</returns>
    /// <remarks>
    /// 上辺は上端、下辺は下端から太さ分、左右の辺は上下の辺の内側へ収まる（角で二重に塗らない）。<br/>
    /// 太さの下限（0.5 未満を無効化する・1 未満を 1 にする等）は用途で異なるため本メソッドは正規化しない。<br/>
    /// 選択矩形の枠線とルーペの外枠が同じ式を共用する（各コントロールが書くと片方だけ端の扱いが変わる）。
    /// </remarks>
    public IReadOnlyList<EsRect> ComputeInnerEdges(double thickness)
    {
        var inner = Math.Max(0, Height - thickness * 2);
        return
        [
            new EsRect(X, Y, Width, thickness),                             // 上
            new EsRect(X, Bottom - thickness, Width, thickness),            // 下
            new EsRect(X, Y + thickness, thickness, inner),                 // 左
            new EsRect(Right - thickness, Y + thickness, thickness, inner), // 右
        ];
    }

    /// <summary>本矩形を領域（左上を原点・指定サイズ）内へクランプする（四隅の包絡矩形）。</summary>
    /// <param name="area">クランプ先の領域のサイズ（左上を原点とする。幅・高さは 0 以上）。</param>
    /// <returns>領域内へ収めた矩形。負の辺（幅・高さ）は 0 になる。</returns>
    /// <remarks>
    /// 四隅を個別にクランプし、その包絡矩形（bounding box）を返す（矩形全体を領域内へ収める）。<br/>
    /// 「四隅をクランプして包絡矩形を取る」規則を単一所有するため、一部だけがはみ出す場合も同じ手順で収まる。<br/>
    /// 領域の幅・高さが負の場合は <c>Math.Clamp</c> の前提に反する（呼び出し側が 0 以上を保証する）。
    /// </remarks>
    public EsRect ClampToArea(EsSize area)
    {
        var left = Math.Clamp(X, 0, area.Width);
        var top = Math.Clamp(Y, 0, area.Height);
        var right = Math.Clamp(Right, 0, area.Width);
        var bottom = Math.Clamp(Bottom, 0, area.Height);
        return new EsRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }
}
