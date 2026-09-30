using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Coord;

/// <summary><see cref="ICoordTransform"/> の具象実装（Func 委譲）。</summary>
/// <remarks>
/// ホスト（上位 AP）が変換関数と境界を提供して生成する。<br/>
/// 等価変換（ローカル DIP = ドメイン座標・境界なし）の既定実装は <see cref="Identity"/> で提供する。<br/>
/// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できる。<br/>
/// <b>軸はローカル DIP とドメイン座標の 1 つだけである</b>: DIP とデバイスピクセルの換算は <see cref="DpiScaler"/> が単一所有する。<br/>
/// 原点基準の等方スケーリングの純粋計算（乗算・除算）も本クラスが単一所有する。
/// </remarks>
public sealed class CoordTransform : ICoordTransform
{
    /// <summary>等価変換（ローカル DIP = ドメイン座標・境界なし）の既定ポート。</summary>
    public static ICoordTransform Identity { get; } = new CoordTransform(
        p => p, p => p, () => null);

    /// <summary>ドメイン座標 = ローカル DIP × 倍率のポートを生成する（倍率はドメイン座標 / ローカル DIP）。</summary>
    /// <param name="domainPerDip">ドメイン座標 / ローカル DIP の倍率を提供する関数（呼び出し毎に評価。 0 以下は 1.0 として扱う）。</param>
    /// <param name="domainBounds">ドメイン座標の境界（画像サイズ等）を提供する関数（呼び出し毎に評価）。 null の場合は境界なし（無制限）。</param>
    /// <returns>ローカル DIP とドメイン座標の双方向変換ポート。</returns>
    /// <remarks>
    /// 同一原点の座標系間の比例変換（例: ドメイン = 画面物理ピクセル、ローカル DIP × DPI スケーリング）を提供する。<br/>
    /// <b>倍率の向きは 1 通りだけである</b>: <paramref name="domainPerDip"/> は常に「ドメイン座標 / ローカル DIP」であり、<br/>
    /// 逆数（ローカル DIP / ドメイン）を渡す入口は設けない（同じ変換が 2 通りの数値で書ける状態を残さない）。<br/>
    /// 倍率は関数で提供されるため、DPI スケール等の動的変化を反映できる。<br/>
    /// 境界は選択をドメイン外へ出さないためのクランプに使われる（境界が不要なホストは省略する）。
    /// </remarks>
    public static ICoordTransform DipToDomain(
        Func<double> domainPerDip,
        Func<EsSize?>? domainBounds = null)
    {
        static double Normalize(double factor) => factor > 0 ? factor : 1.0;

        return new CoordTransform(
            dip =>
            {
                var (x, y) = MultiplyPoint(dip.X, dip.Y, Normalize(domainPerDip()));
                return new EsPoint(x, y);
            },
            domain =>
            {
                var (x, y) = DividePoint(domain.X, domain.Y, Normalize(domainPerDip()));
                return new EsPoint(x, y);
            },
            domainBounds ?? (static () => null));
    }

    // ====================================================================
    // 座標の純粋計算（原点基準の等方スケーリング）
    // ====================================================================

    /// <summary>指定点を原点基準で等方乗算する（純粋関数）。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <param name="factor">乗算する倍率。</param>
    /// <returns>乗算後の座標。</returns>
    internal static (double X, double Y) MultiplyPoint(double x, double y, double factor)
        => (x * factor, y * factor);

    /// <summary>指定点を原点基準で等方除算する（純粋関数）。</summary>
    /// <param name="x">X 座標。</param>
    /// <param name="y">Y 座標。</param>
    /// <param name="factor">除算する倍率（0 を渡さないこと）。</param>
    /// <returns>除算後の座標。</returns>
    internal static (double X, double Y) DividePoint(double x, double y, double factor)
        => (x / factor, y / factor);

    private readonly Func<EsPoint, EsPoint> _toDomain;
    private readonly Func<EsPoint, EsPoint> _toDip;
    private readonly Func<EsSize?> _domainBounds;

    /// <summary><see cref="ICoordTransform"/> の新しいインスタンスを初期化する。</summary>
    /// <param name="toDomain">ローカル DIP 座標をドメイン座標へ変換する関数。</param>
    /// <param name="toDip">ドメイン座標をローカル DIP 座標へ変換する関数。</param>
    /// <param name="domainBounds">ドメイン座標の境界を返す関数（null = 無制限）。</param>
    /// <remarks>変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できる。</remarks>
    public CoordTransform(
        Func<EsPoint, EsPoint> toDomain,
        Func<EsPoint, EsPoint> toDip,
        Func<EsSize?> domainBounds)
    {
        _toDomain = toDomain;
        _toDip = toDip;
        _domainBounds = domainBounds;
    }

    /// <inheritdoc/>
    public EsPoint ToDomain(EsPoint dip) => _toDomain(dip);

    /// <inheritdoc/>
    public EsPoint ToDip(EsPoint domain) => _toDip(domain);

    /// <inheritdoc/>
    public EsSize? DomainBounds => _domainBounds();
}
