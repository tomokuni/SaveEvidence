using Avalonia;

namespace Estable.AvaloniaUI.Platform;

/// <summary><see cref="ICoordTransform"/> の具象実装（Func 委譲）。</summary>
/// <remarks>
/// ホスト（上位 AP）が変換関数と境界を提供して生成する。<br/>
/// 等価変換（座標系 A = 座標系 B・無制限）の既定実装は <see cref="Identity"/> で提供する。<br/>
/// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できる。
/// </remarks>
public sealed class CoordTransform : ICoordTransform
{
    /// <summary>等価変換（座標系 A = 座標系 B）の既定ポート。</summary>
    public static ICoordTransform Identity { get; } = new CoordTransform(
        p => p, p => p, () => null);

    /// <summary>2 つのポートを合成したポートを生成する（inner を先に適用し、outer を後に適用する）。</summary>
    /// <param name="outer">後段のポート（中間座標系 → 座標系 B）。</param>
    /// <param name="inner">前段のポート（座標系 A → 中間座標系）。</param>
    /// <returns>座標系 A ↔ 座標系 B の合成ポート。</returns>
    /// <remarks>
    /// 合成ポートの Forward は「inner.Forward → outer.Forward」の順に適用され、<br/>
    /// Inverse は「outer.Inverse → inner.Inverse」の順に適用される（逆変換の逆順）。<br/>
    /// Bounds は outer を優先し、null の場合は inner の Bounds を使用する。<br/>
    /// 多段の座標変換（例: 論理 → コンテンツDIP → ビューポートDIP → 画面物理）を 1 つのポートに集約できる。
    /// </remarks>
    public static ICoordTransform Compose(ICoordTransform outer, ICoordTransform inner)
        => new CoordTransform(
            p => outer.Forward(inner.Forward(p)),
            p => inner.Inverse(outer.Inverse(p)),
            () => outer.Bounds ?? inner.Bounds);

    /// <summary>スクロールオフセットによる平行移動ポートを生成する（コンテンツ ↔ ビューポート）。</summary>
    /// <param name="offsetProvider">スクロールオフセット（DIP）を提供する関数（呼び出し毎に評価）。</param>
    /// <returns>Forward = コンテンツ→ビューポート（オフセット減算）、Inverse = ビューポート→コンテンツ（オフセット加算）のポート。</returns>
    /// <remarks>
    /// ScrollViewer 内の画像プレビューで、コンテンツ座標系とビューポート座標系の変換を提供する。<br/>
    /// オフセットは関数で提供されるため、スクロールの動的変化を反映できる。
    /// </remarks>
    public static ICoordTransform ScrollOffset(Func<Vector> offsetProvider)
        => new CoordTransform(
            p => { var (x, y) = CoordMath.OffsetPoint(p.X, p.Y, offsetProvider().X, offsetProvider().Y); return new Point(x, y); },
            p => { var (x, y) = CoordMath.InverseOffsetPoint(p.X, p.Y, offsetProvider().X, offsetProvider().Y); return new Point(x, y); },
            () => null);

    /// <summary>原点基準の等方スケーリングポートを生成する（座標系 A ↔ 座標系 B = 座標系 A × 倍率）。</summary>
    /// <param name="factor">座標系 A → 座標系 B のスケール倍率（Forward = 除算 / Inverse = 乗算）。</param>
    /// <returns>Forward = 座標系 B → 座標系 A（factor で除算）、Inverse = 座標系 A → 座標系 B（factor で乗算）のポート。</returns>
    /// <remarks>
    /// 同一原点の座標系間の比例変換（例: DIP ↔ ドメインピクセル = DIP × DisplayScaling）を提供する。<br/>
    /// 倍率は生成時に固定される（呼び出し毎に評価する必要がある場合は <see cref="CoordTransform(Func{Point, Point}, Func{Point, Point}, Func{Size?})"/> を使用する）。
    /// </remarks>
    public static ICoordTransform Scale(double factor)
    {
        double s = factor > 0 ? factor : 1.0;
        return new CoordTransform(
            p => { var (x, y) = CoordMath.ScalePoint(p.X, p.Y, s); return new Point(x, y); },
            p => { var (x, y) = CoordMath.InverseScalePoint(p.X, p.Y, s); return new Point(x, y); },
            () => null);
    }

    /// <summary>ドメイン座標 = ローカル DIP × 倍率 のスケーリングポートを生成する（Forward = DIP→ドメインの乗算）。</summary>
    /// <param name="factorProvider">DIP → ドメインのスケール倍率を提供する関数（呼び出し毎に評価。0 以下の場合は 1.0 として扱う）。</param>
    /// <returns>Forward = ローカル DIP → ドメイン（倍率で乗算）、Inverse = ドメイン → ローカル DIP（倍率で除算）のポート。</returns>
    /// <remarks>
    /// ドメイン座標（例: 画面物理ピクセル = ローカル DIP × DisplayScaling）に合わせた方向のスケーリングポートを提供する。<br/>
    /// 倍率は関数で提供されるため、DPI スケール等の動的変化を反映できる（<see cref="Scale(double)"/> は固定値・方向が逆）。<br/>
    /// Bounds は未指定（null = 無制限）。
    /// </remarks>
    public static ICoordTransform DomainScale(Func<double> factorProvider)
    {
        static double Scale(double factor) => factor > 0 ? factor : 1.0;

        return new CoordTransform(
            p => { double s = Scale(factorProvider()); var (x, y) = CoordMath.ScalePoint(p.X, p.Y, 1.0 / s); return new Point(x, y); },
            p => { double s = Scale(factorProvider()); var (x, y) = CoordMath.ScalePoint(p.X, p.Y, s); return new Point(x, y); },
            () => null);
    }

    /// <summary>画像レイアウト（レターボックス・スケール）を呼び出し毎に評価する動的ポートを生成する（Forward = DIP→論理）。</summary>
    /// <param name="viewportSizeProvider">表示領域サイズ（DIP）を提供する関数（呼び出し毎に評価）。</param>
    /// <param name="sourceSize">元画像サイズ（ピクセル）。</param>
    /// <param name="uniformProvider">Stretch.Uniform 相当かどうかを提供する関数（呼び出し毎に評価。null の場合は true）。</param>
    /// <returns>Forward = DIP→論理、Inverse = 論理→DIP、Bounds = 元画像サイズのポート。</returns>
    /// <remarks>
    /// レイアウト計算（レターボックス・スケール）は <see cref="CoordMath.ComputeImageRenderInfo"/> に委譲し、
    /// 呼び出し毎に再評価する（ズーム・サイズ変更に追従する動的ポート）。<br/>
    /// 表示領域が 0 以下の場合は等価変換（<see cref="Identity"/>）を使用する。<br/>
    /// 多段変換（例: 親パネルDIP → プレビュー内部DIP → 論理）は <see cref="Compose"/> で合成する。<br/>
    /// ズーム率（X スケール）の導出には <see cref="ImageLayoutScaleX"/> を使用する。
    /// </remarks>
    public static ICoordTransform ImageLayout(
        Func<Size> viewportSizeProvider,
        Size sourceSize,
        Func<bool>? uniformProvider = null)
    {
        ICoordTransform CreateLayout()
        {
            var disp = viewportSizeProvider();
            if (disp.Width <= 0 || disp.Height <= 0) return Identity;
            var uniform = uniformProvider?.Invoke() ?? true;
            var (ox, oy, sx, sy) = ComputeImageLayout(disp, sourceSize, uniform);
            return new CoordTransform(
                dip => new Point((dip.X - ox) * sx, (dip.Y - oy) * sy),
                logical => new Point(logical.X / sx + ox, logical.Y / sy + oy),
                () => sourceSize);
        }

        return new CoordTransform(
            dip => CreateLayout().Forward(dip),
            logical => CreateLayout().Inverse(logical),
            () => sourceSize);
    }

    /// <summary>画像レイアウトの X スケール（元画像ピクセル / DIP）を取得する（ズーム率導出用）。</summary>
    /// <param name="viewportSizeProvider">表示領域サイズ（DIP）を提供する関数（呼び出し毎に評価）。</param>
    /// <param name="sourceSize">元画像サイズ（ピクセル）。</param>
    /// <param name="uniformProvider">Stretch.Uniform 相当かどうかを提供する関数（呼び出し毎に評価。null の場合は true）。</param>
    /// <returns>X スケール（元画像ピクセル / DIP）。表示領域・元画像サイズが 0 以下の場合は 1.0。</returns>
    /// <remarks>
    /// <see cref="ImageLayout"/> と同じレイアウト計算（<see cref="CoordMath.ComputeImageRenderInfo"/>）から
    /// X スケールのみを導出する（サイズラベルのズーム率表示等に使用・単一実装・DRY 原則）。
    /// </remarks>
    public static double ImageLayoutScaleX(
        Func<Size> viewportSizeProvider,
        Size sourceSize,
        Func<bool>? uniformProvider = null)
    {
        var disp = viewportSizeProvider();
        if (disp.Width <= 0 || disp.Height <= 0 || sourceSize.Width <= 0 || sourceSize.Height <= 0) return 1.0;
        var uniform = uniformProvider?.Invoke() ?? true;
        return ComputeImageLayout(disp, sourceSize, uniform).ScaleX;
    }

    /// <summary>画像レイアウトの描画情報（オフセット・スケール）を算出する（スケールは 0.01 未満を 0.01 に補正）。</summary>
    /// <param name="viewport">表示領域サイズ（DIP）。</param>
    /// <param name="source">元画像サイズ（ピクセル）。</param>
    /// <param name="uniform">Stretch.Uniform 相当かどうか。</param>
    /// <returns>オフセット X（DIP）、オフセット Y（DIP）、スケール X、スケール Y。</returns>
    /// <remarks>
    /// <see cref="CoordMath.ComputeImageRenderInfo"/> に委譲し、<c>Forward/Inverse</c> の除算安全域
    /// （0.01 未満を 0.01 に補正）を適用する。<br/>
    /// <see cref="ImageLayout"/> / <see cref="ImageLayoutScaleX"/> で共通利用する（単一実装・DRY 原則）。
    /// </remarks>
    private static (double OffsetX, double OffsetY, double ScaleX, double ScaleY) ComputeImageLayout(
        Size viewport, Size source, bool uniform)
    {
        var (_, _, ox, oy, sx, sy) = CoordMath.ComputeImageRenderInfo(
            viewport.Width, viewport.Height, source.Width, source.Height, uniform);
        return (ox, oy, Math.Max(sx, 0.01), Math.Max(sy, 0.01));
    }

    private readonly Func<Point, Point> _forward;
    private readonly Func<Point, Point> _inverse;
    private readonly Func<Size?> _bounds;

    /// <summary><see cref="ICoordTransform"/> の新しいインスタンスを初期化します。</summary>
    /// <param name="forward">順変換（座標系 A → 座標系 B）。</param>
    /// <param name="inverse">逆変換（座標系 B → 座標系 A）。</param>
    /// <param name="bounds">座標系 B の境界（null = 無制限）。</param>
    /// <remarks>変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できる。</remarks>
    public CoordTransform(
        Func<Point, Point> forward,
        Func<Point, Point> inverse,
        Func<Size?> bounds)
    {
        _forward = forward;
        _inverse = inverse;
        _bounds = bounds;
    }

    /// <inheritdoc/>
    public Point Forward(Point p) => _forward(p);

    /// <inheritdoc/>
    public Point Inverse(Point p) => _inverse(p);

    /// <inheritdoc/>
    public Size? Bounds => _bounds();
}
