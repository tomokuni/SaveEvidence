namespace Estable.Primitives;

/// <summary>2 次元座標（double）の軽量な不変値型。UI フレームワーク（Avalonia / System.Drawing）に依存しない。</summary>
/// <param name="X">X 座標。</param>
/// <param name="Y">Y 座標。</param>
/// <remarks>
/// <b>目的</b>: コアロジック（座標変換・幾何計算・検出）を UI フレームワークから分離し、移植性とテスト容易性を高める。<br/>
/// <b>特徴</b>: <c>double</c> ベースの <c>readonly record struct</c>。加減算・等価比較を提供する。<br/>
/// UI 境界では <see cref="PointDExt.ToAvaloniaPoint"/> 等のアダプタで変換する。<br/>
/// <b>スレッド安全性</b>: 不変値型のため、インスタンスはスレッド間で安全に共有できる。
/// </remarks>
public readonly record struct PointD(double X, double Y)
{
    /// <summary>原点 (0, 0)。</summary>
    public static PointD Origin => new(0, 0);

    /// <summary>2 点の和を返す。</summary>
    /// <param name="a">1 つ目の点。</param>
    /// <param name="b">2 つ目の点。</param>
    /// <returns>成分ごとの和。</returns>
    public static PointD operator +(PointD a, PointD b) => new(a.X + b.X, a.Y + b.Y);

    /// <summary>2 点の差を返す。</summary>
    /// <param name="a">1 つ目の点。</param>
    /// <param name="b">2 つ目の点。</param>
    /// <returns>成分ごとの差。</returns>
    public static PointD operator -(PointD a, PointD b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>ベクターを加算する。</summary>
    /// <param name="point">点。</param>
    /// <param name="vector">加算するベクター。</param>
    /// <returns>加算後の点。</returns>
    public static PointD operator +(PointD point, VectorD vector) => new(point.X + vector.X, point.Y + vector.Y);

    /// <summary>ベクターを減算する。</summary>
    /// <param name="point">点。</param>
    /// <param name="vector">減算するベクター。</param>
    /// <returns>減算後の点。</returns>
    public static PointD operator -(PointD point, VectorD vector) => new(point.X - vector.X, point.Y - vector.Y);

    /// <summary>JSON 等での表示に便利な文字列表現を返す。</summary>
    /// <returns>例: "(10, 20)"</returns>
    public override string ToString() => $"({X}, {Y})";
}
