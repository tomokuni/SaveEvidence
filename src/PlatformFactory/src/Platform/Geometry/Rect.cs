namespace Estable.AvaloniaUI.Platform.Geometry;

/// <summary>2 次元の矩形（位置・サイズ・int）の軽量な不変値型。ピクセル矩形専用で、UI フレームワークに依存しない。</summary>
/// <param name="X">左上 X 座標（ピクセル）。</param>
/// <param name="Y">左上 Y 座標（ピクセル）。</param>
/// <param name="Width">幅（ピクセル）。</param>
/// <param name="Height">高さ（ピクセル）。</param>
/// <remarks>
/// <b>目的</b>: <see cref="PbBitmap"/> 等のピクセル矩形（int）を UI フレームワーク（System.Drawing / Avalonia）から分離する。<br/>
/// <b>double 版</b>: DIP の矩形（小数を扱う）は <see cref="RectD"/> を使用する。
/// </remarks>
public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    /// <summary>空の矩形 (0, 0, 0, 0)。</summary>
    public static Rect Empty => new(0, 0, 0, 0);

    /// <summary>左上 X 座標。</summary>
    public int Left => X;

    /// <summary>左上 Y 座標。</summary>
    public int Top => Y;

    /// <summary>右端 X 座標（左端 + 幅）。</summary>
    public int Right => X + Width;

    /// <summary>下端 Y 座標（上端 + 高さ）。</summary>
    public int Bottom => Y + Height;

    /// <summary>幅・高さが 0 超かどうか。</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>左上の位置。</summary>
    public Point Position => new(X, Y);

    /// <summary>左上点。</summary>
    public Point TopLeft => new(X, Y);

    /// <summary>右上点。</summary>
    public Point TopRight => new(X + Width, Y);

    /// <summary>左下点。</summary>
    public Point BottomLeft => new(X, Y + Height);

    /// <summary>右下点。</summary>
    public Point BottomRight => new(X + Width, Y + Height);

    /// <summary>指定点が矩形内（境界を含む）かどうかを判定する。</summary>
    /// <param name="point">判定する点。</param>
    /// <returns>矩形内の場合は <see langword="true"/>。</returns>
    public bool Contains(Point point)
        => point.X >= X && point.X <= X + Width && point.Y >= Y && point.Y <= Y + Height;

    /// <summary>JSON 等での表示に便利な文字列表現を返す。</summary>
    /// <returns>例: "(10, 20, 100, 50)"</returns>
    public override string ToString() => $"({X}, {Y}, {Width}, {Height})";
}
