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
    /// 整数矩形の <see cref="EsPixelRect.IsEmpty"/> と同じ判定を提供する<br/>
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
}
