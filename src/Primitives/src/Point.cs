namespace Estable.Primitives;

/// <summary>2 次元座標（int）の軽量な不変値型。ピクセル座標専用で、UI フレームワークに依存しない。</summary>
/// <param name="X">X 座標（ピクセル）。</param>
/// <param name="Y">Y 座標（ピクセル）。</param>
/// <remarks>
/// <b>目的</b>: ピクセル座標（int）を UI フレームワーク（System.Drawing / Avalonia）から分離する。<br/>
/// <b>double 版</b>: DIP・ドメイン座標（小数を扱う）は <see cref="PointD"/> を使用する（座標系の混在防止）。<br/>
/// <b>スレッド安全性</b>: 不変値型のため、インスタンスはスレッド間で安全に共有できる。
/// </remarks>
public readonly record struct Point(int X, int Y)
{
    /// <summary>原点 (0, 0)。</summary>
    public static Point Origin => new(0, 0);

    /// <summary>2 点の和を返す。</summary>
    /// <param name="a">1 つ目の点。</param>
    /// <param name="b">2 つ目の点。</param>
    /// <returns>成分ごとの和。</returns>
    public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);

    /// <summary>2 点の差を返す。</summary>
    /// <param name="a">1 つ目の点。</param>
    /// <param name="b">2 つ目の点。</param>
    /// <returns>成分ごとの差。</returns>
    public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>JSON 等での表示に便利な文字列表現を返す。</summary>
    /// <returns>例: "(10, 20)"</returns>
    public override string ToString() => $"({X}, {Y})";
}
