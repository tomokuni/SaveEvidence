namespace Estable.AvaloniaUI.Platform.Geometry;

/// <summary>2 次元の矩形（位置・サイズ・double）の軽量な不変値型。UI フレームワークに依存しない。</summary>
/// <param name="X">左上 X 座標。</param>
/// <param name="Y">左上 Y 座標。</param>
/// <param name="Width">幅。</param>
/// <param name="Height">高さ。</param>
/// <remarks>
/// 選択矩形・レターボックス・画像表示領域などを、UI フレームワークから分離して扱うための純粋値型。<br/>
/// 幅・高さは常に 0 以上（負の幅を許さない）とし、正規化は呼び出し側の責務とする。<br/>
/// UI 境界では <see cref="RectDExt.ToAvaloniaRect"/> 等のアダプタで変換する。
/// </remarks>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    /// <summary>空の矩形 (0, 0, 0, 0)。</summary>
    public static RectD Empty => new(0, 0, 0, 0);

    /// <summary>左上 X 座標。</summary>
    public double Left => X;

    /// <summary>左上 Y 座標。</summary>
    public double Top => Y;

    /// <summary>右端 X 座標（左端 + 幅）。</summary>
    public double Right => X + Width;

    /// <summary>下端 Y 座標（上端 + 高さ）。</summary>
    public double Bottom => Y + Height;

    /// <summary>幅・高さが 0 超かどうか。</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>中心 X 座標。</summary>
    public double CenterX => X + Width / 2.0;

    /// <summary>中心 Y 座標。</summary>
    public double CenterY => Y + Height / 2.0;

    /// <summary>中心点。</summary>
    public PointD Center => new(CenterX, CenterY);

    /// <summary>左上の位置。</summary>
    public PointD Position => new(X, Y);

    /// <summary>矩形のサイズ。</summary>
    public SizeD Size => new(Width, Height);

    /// <summary>左上点。</summary>
    public PointD TopLeft => new(X, Y);

    /// <summary>右上点。</summary>
    public PointD TopRight => new(X + Width, Y);

    /// <summary>左下点。</summary>
    public PointD BottomLeft => new(X, Y + Height);

    /// <summary>右下点。</summary>
    public PointD BottomRight => new(X + Width, Y + Height);

    /// <summary>指定点が矩形内（境界を含む）かどうかを判定する。</summary>
    /// <param name="point">判定する点。</param>
    /// <returns>矩形内の場合は <see langword="true"/>。</returns>
    public bool Contains(PointD point)
        => point.X >= X && point.X <= X + Width && point.Y >= Y && point.Y <= Y + Height;

    /// <summary>矩形のサイズを変更した新しい矩形を返す。</summary>
    /// <param name="width">新しい幅。</param>
    /// <param name="height">新しい高さ。</param>
    /// <returns>位置を維持した新しい矩形。</returns>
    public RectD WithSize(double width, double height) => new(X, Y, width, height);

    /// <summary>指定位置へ移動した新しい矩形を返す。</summary>
    /// <param name="x">新しい左端 X 座標。</param>
    /// <param name="y">新しい上端 Y 座標。</param>
    /// <returns>サイズを維持した新しい矩形。</returns>
    public RectD WithPosition(double x, double y) => new(x, y, Width, Height);

    /// <summary>JSON 等での表示に便利な文字列表現を返す。</summary>
    /// <returns>例: "(10, 20, 100, 50)"</returns>
    public override string ToString() => $"({X}, {Y}, {Width}, {Height})";
}
