namespace Estable.Primitives;

/// <summary>2 次元のサイズ（幅・高さ・int）の軽量な不変値型。ピクセルサイズ専用で、UI フレームワークに依存しない。</summary>
/// <param name="Width">幅（ピクセル）。</param>
/// <param name="Height">高さ（ピクセル）。</param>
/// <remarks>
/// <b>目的</b>: ピクセルサイズ（int）を UI フレームワーク（System.Drawing / Avalonia）から分離する。<br/>
/// <b>double 版</b>: DIP のサイズ（小数を扱う）は <see cref="SizeD"/> を使用する。
/// </remarks>
public readonly record struct Size(int Width, int Height)
{
    /// <summary>空のサイズ (0, 0)。</summary>
    public static Size Empty => new(0, 0);

    /// <summary>2 つのサイズの和を返す。</summary>
    /// <param name="a">1 つ目のサイズ。</param>
    /// <param name="b">2 つ目のサイズ。</param>
    /// <returns>成分ごとの和。</returns>
    public static Size operator +(Size a, Size b) => new(a.Width + b.Width, a.Height + b.Height);

    /// <summary>2 つのサイズの差を返す。</summary>
    /// <param name="a">1 つ目のサイズ。</param>
    /// <param name="b">2 つ目のサイズ。</param>
    /// <returns>成分ごとの差。</returns>
    public static Size operator -(Size a, Size b) => new(a.Width - b.Width, a.Height - b.Height);

    /// <summary>JSON 等での表示に便利な文字列表現を返す。</summary>
    /// <returns>例: "(10 x 20)"</returns>
    public override string ToString() => $"({Width} x {Height})";
}
