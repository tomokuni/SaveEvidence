using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Coord;

/// <summary><see cref="CoordTransform"/> のファクトリ（<see cref="CoordTransform.DipToDomain"/>）と、座標の純粋計算（原点基準の等方スケーリング）の単体テスト。</summary>
/// <remarks>
/// コア幾何型（<see cref="EsPoint"/> / <see cref="EsSize"/>）のみを使用するため、<br/>
/// UI フレームワークの初期化に依存せず <c>[Fact]</c> で並列実行できる。
/// </remarks>
public class CoordTransformTests
{
    // ====================================================================
    // 倍率の向き（ドメイン座標 / ローカル DIP）
    // ====================================================================

    [Fact]
    public void DipToDomain_倍率2はDIPを2倍してドメインにする()
    {
        var port = CoordTransform.DipToDomain(() => 2.0);

        // ドメイン = ローカル DIP × 倍率（125% DPI ならドメインは物理ピクセルで DIP の 1.25 倍）
        Assert.Equal(new EsPoint(200, 300), port.ToDomain(new EsPoint(100, 150)));
    }

    [Fact]
    public void DipToDomain_ドメインを倍率で除算してDIPにする()
    {
        var port = CoordTransform.DipToDomain(() => 2.0);

        Assert.Equal(new EsPoint(100, 150), port.ToDip(new EsPoint(200, 300)));
    }

    [Fact]
    public void DipToDomain_往復で元の座標に戻る()
    {
        var port = CoordTransform.DipToDomain(() => 1.5);
        var p = new EsPoint(123.4, 456.7);

        var roundTrip = port.ToDip(port.ToDomain(p));

        Assert.Equal(p.X, roundTrip.X, 6);
        Assert.Equal(p.Y, roundTrip.Y, 6);
    }

    [Fact]
    public void DipToDomain_プロバイダは呼び出し毎に評価される()
    {
        var factor = 1.0;
        var port = CoordTransform.DipToDomain(() => factor);
        Assert.Equal(new EsPoint(100, 100), port.ToDomain(new EsPoint(100, 100)));

        factor = 2.0;
        Assert.Equal(new EsPoint(200, 200), port.ToDomain(new EsPoint(100, 100)));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void DipToDomain_倍率が0以下なら等倍として扱う(double domainPerDip)
    {
        var port = CoordTransform.DipToDomain(() => domainPerDip);

        Assert.Equal(new EsPoint(10, 20), port.ToDomain(new EsPoint(10, 20)));
        Assert.Equal(new EsPoint(10, 20), port.ToDip(new EsPoint(10, 20)));
    }

    [Fact]
    public void DipToDomain_境界を省略すると無制限になる()
    {
        var port = CoordTransform.DipToDomain(() => 2.0);

        Assert.Null(port.DomainBounds);
    }

    [Fact]
    public void DipToDomain_境界を指定するとドメイン境界を返す()
    {
        var port = CoordTransform.DipToDomain(() => 2.0, () => new EsSize(1920, 1080));

        Assert.Equal(new EsSize(1920, 1080), port.DomainBounds);
    }

    [Fact]
    public void DipToDomain_境界は呼び出し毎に評価される()
    {
        var bounds = new EsSize(100, 100);
        var port = CoordTransform.DipToDomain(() => 1.0, () => bounds);

        Assert.Equal(new EsSize(100, 100), port.DomainBounds);

        bounds = new EsSize(200, 200);
        Assert.Equal(new EsSize(200, 200), port.DomainBounds);
    }

    // ====================================================================
    // 座標の純粋計算（原点基準の等方スケーリング）
    // ====================================================================

    [Fact]
    public void MultiplyPoint_倍率を乗算する()
    {
        var (x, y) = CoordTransform.MultiplyPoint(100, 150, 2.0);

        Assert.Equal(200, x);
        Assert.Equal(300, y);
    }

    [Fact]
    public void DividePoint_倍率で除算する()
    {
        var (x, y) = CoordTransform.DividePoint(200, 300, 2.0);

        Assert.Equal(100, x);
        Assert.Equal(150, y);
    }
}
