namespace Estable.AvaloniaUI.Platform.Geometry;

/// <summary>2 次元のサイズ（幅・高さ・double）の軽量な不変値型。UI フレームワークに依存しない。</summary>
/// <param name="Width">幅。</param>
/// <param name="Height">高さ。</param>
/// <remarks>
/// 元画像ピクセルサイズ・ビューポートサイズ（DIP）などを、UI フレームワークから分離して扱うための純粋値型。
/// </remarks>
public readonly record struct SizeD(double Width, double Height)
{
    /// <summary>空のサイズ (0, 0)。</summary>
    public static SizeD Empty => new(0, 0);

    /// <summary>2 つのサイズの和を返す。</summary>
    /// <param name="a">1 つ目のサイズ。</param>
    /// <param name="b">2 つ目のサイズ。</param>
    /// <returns>成分ごとの和。</returns>
    public static SizeD operator +(SizeD a, SizeD b) => new(a.Width + b.Width, a.Height + b.Height);

    /// <summary>2 つのサイズの差を返す。</summary>
    /// <param name="a">1 つ目のサイズ。</param>
    /// <param name="b">2 つ目のサイズ。</param>
    /// <returns>成分ごとの差。</returns>
    public static SizeD operator -(SizeD a, SizeD b) => new(a.Width - b.Width, a.Height - b.Height);

    /// <summary>JSON 等での表示に便利な文字列表現を返す。</summary>
    /// <returns>例: "(10 x 20)"</returns>
    public override string ToString() => $"({Width} x {Height})";
}
