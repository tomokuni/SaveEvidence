namespace Estable.Primitives;

/// <summary>2 次元の移動・差分量（double）の軽量な不変値型。UI フレームワークに依存しない。</summary>
/// <param name="X">X 成分。</param>
/// <param name="Y">Y 成分。</param>
/// <remarks>
/// <see cref="PointD"/> と同様に、コアロジックを UI フレームワークから分離するための純粋値型。<br/>
/// スクロールオフセット・ズーム差分量などに使用する。
/// </remarks>
public readonly record struct VectorD(double X, double Y)
{
    /// <summary>零ベクター (0, 0)。</summary>
    public static VectorD Zero => new(0, 0);

    /// <summary>2 つのベクターの和を返す。</summary>
    /// <param name="a">1 つ目のベクター。</param>
    /// <param name="b">2 つ目のベクター。</param>
    /// <returns>成分ごとの和。</returns>
    public static VectorD operator +(VectorD a, VectorD b) => new(a.X + b.X, a.Y + b.Y);

    /// <summary>2 つのベクターの差を返す。</summary>
    /// <param name="a">1 つ目のベクター。</param>
    /// <param name="b">2 つ目のベクター。</param>
    /// <returns>成分ごとの差。</returns>
    public static VectorD operator -(VectorD a, VectorD b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>逆ベクターを返す。</summary>
    /// <param name="value">対象のベクター。</param>
    /// <returns>符号を反転したベクター。</returns>
    public static VectorD operator -(VectorD value) => new(-value.X, -value.Y);

    /// <summary>JSON 等での表示に便利な文字列表現を返す。</summary>
    /// <returns>例: "(10, 20)"</returns>
    public override string ToString() => $"({X}, {Y})";
}
