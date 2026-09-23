namespace EsUtil.Platform.Core.Geometry;

/// <summary>UI フレームワーク非依存の画面物理矩形（整数ピクセル）を表す値オブジェクト。</summary>
/// <remarks>
/// 画面の物理ピクセル矩形（DPI スケーリング適用後）を表す。<br/>
/// DIP（論理座標）の矩形は <see cref="EsRect"/> を使用する。<br/>
/// 各 UI 実装の物理矩形型との変換は Interop 層が提供する。<br/>
/// </remarks>
/// <param name="X">左上 X 座標（画面物理ピクセル）。</param>
/// <param name="Y">左上 Y 座標（画面物理ピクセル）。</param>
/// <param name="Width">幅（ピクセル）。</param>
/// <param name="Height">高さ（ピクセル）。</param>
public readonly record struct EsPixelRect(int X, int Y, int Width, int Height)
{
    /// <summary>右端の X 座標（左端 + 幅）を取得する（排他的境界）。</summary>
    /// <value>右端の X 座標（画面物理ピクセル）。</value>
    public int Right => X + Width;

    /// <summary>幅または高さが 0 以下かどうか（切り出せる範囲が無い）を取得する。</summary>
    /// <value>幅または高さが 0 以下の場合は true。</value>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>下端の Y 座標（上端 + 高さ）を取得する（排他的境界）。</summary>
    /// <value>下端の Y 座標（画面物理ピクセル）。</value>
    public int Bottom => Y + Height;

    /// <summary>右下端の座標（右端・下端）を取得する。</summary>
    /// <value>右下端の座標（画面物理ピクセル）。</value>
    public EsPixelPoint BottomRight => new(Right, Bottom);

    /// <summary>左上端の座標（左端・上端）を取得する。</summary>
    /// <value>左上端の座標（画面物理ピクセル）。</value>
    /// <remarks>矩形の原点（画像の原点等）を取り出すときに、UI が <c>new EsPixelPoint(rect.X, rect.Y)</c> を書かないようにする。</remarks>
    public EsPixelPoint TopLeft => new(X, Y);

    /// <summary>矩形の中心座標を取得する（整数除算）。</summary>
    /// <value>中心の座標（画面物理ピクセル）。</value>
    public EsPixelPoint Center => new(X + Width / 2, Y + Height / 2);

    /// <summary>指定した点が本矩形の内側に含まれるかどうかを判定する。</summary>
    /// <param name="point">判定する点（画面物理ピクセル）。</param>
    /// <returns>含まれる場合は <see langword="true"/>。</returns>
    /// <remarks>左端・上端は含み、右端・下端は含まない（半開区間）。<see cref="Width"/> / <see cref="Height"/> が 0 以下の場合は常に false。</remarks>
    public bool Contains(EsPixelPoint point)
        => point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;

    /// <summary>2 つの矩形を包含する最小の矩形を返す（和集合）。</summary>
    /// <param name="other">もう一方の矩形。</param>
    /// <returns>両者を包含する矩形。いずれかが空（幅・高さが 0 以下）の場合はもう一方を返す。</returns>
    /// <remarks>仮想スクリーン境界（全モニタのバウンディングボックス）の算出に使用する。</remarks>
    public EsPixelRect Union(EsPixelRect other)
    {
        if (Width <= 0 || Height <= 0) return other;
        if (other.Width <= 0 || other.Height <= 0) return this;

        var left = Math.Min(X, other.X);
        var top = Math.Min(Y, other.Y);
        var right = Math.Max(Right, other.Right);
        var bottom = Math.Max(Bottom, other.Bottom);
        return new EsPixelRect(left, top, right - left, bottom - top);
    }
}
