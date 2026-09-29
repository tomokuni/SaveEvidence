using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Coord;

/// <summary><see cref="CoordTransform"/> の合成・スクロール・ファクトリと、座標の純粋計算（画像レイアウト・平行移動・スケーリング）の単体テスト。</summary>
/// <remarks>
/// コア幾何型（<see cref="EsPoint"/> / <see cref="EsSize"/> / <see cref="EsVector"/>）のみを使用するため、<br/>
/// UI フレームワークの初期化に依存せず <c>[Fact]</c> で並列実行できる。
/// </remarks>
public class CoordTransformTests
{
    // ====================================================================
    // Compose（ポート合成）
    // ====================================================================

    [Fact]
    public void Compose_順変換はinnerを先に適用する()
    {
        // inner: +10（座標系 A → 中間）、outer: ×2（中間 → 座標系 B）
        var inner = new CoordTransform(p => new EsPoint(p.X + 10, p.Y + 10), p => new EsPoint(p.X - 10, p.Y - 10), () => null);
        var outer = new CoordTransform(p => new EsPoint(p.X * 2, p.Y * 2), p => new EsPoint(p.X / 2, p.Y / 2), () => null);
        var composed = CoordTransform.Compose(outer, inner);

        // Forward(5) = outer.Forward(inner.Forward(5)) = outer.Forward(15) = 30
        Assert.Equal(new EsPoint(30, 30), composed.Forward(new EsPoint(5, 5)));
        // Inverse(30) = inner.Inverse(outer.Inverse(30)) = inner.Inverse(15) = 5
        Assert.Equal(new EsPoint(5, 5), composed.Inverse(new EsPoint(30, 30)));
    }

    [Fact]
    public void Compose_Boundsはouterを優先しnull時はinnerを使う()
    {
        var inner = new CoordTransform(p => p, p => p, () => new EsSize(100, 100));
        var outer = new CoordTransform(p => p, p => p, () => null);
        Assert.Equal(new EsSize(100, 100), CoordTransform.Compose(outer, inner).Bounds);

        var inner2 = new CoordTransform(p => p, p => p, () => new EsSize(50, 50));
        var outer2 = new CoordTransform(p => p, p => p, () => new EsSize(200, 200));
        Assert.Equal(new EsSize(200, 200), CoordTransform.Compose(outer2, inner2).Bounds);
    }

    // ====================================================================
    // ScrollOffset（コンテンツ ↔ ビューポート）
    // ====================================================================

    [Fact]
    public void ScrollOffset_順変換でスクロールを減算する()
    {
        var port = CoordTransform.ScrollOffset(() => new EsVector(30, 50));
        Assert.Equal(new EsPoint(70, 150), port.Forward(new EsPoint(100, 200)));
    }

    [Fact]
    public void ScrollOffset_逆変換でスクロールを加算する()
    {
        var port = CoordTransform.ScrollOffset(() => new EsVector(30, 50));
        Assert.Equal(new EsPoint(100, 200), port.Inverse(new EsPoint(70, 150)));
    }

    [Fact]
    public void ScrollOffset_プロバイダは呼び出し毎に評価される()
    {
        var offset = new EsVector(10, 10);
        var port = CoordTransform.ScrollOffset(() => offset);
        Assert.Equal(new EsPoint(90, 90), port.Forward(new EsPoint(100, 100)));
        offset = new EsVector(20, 20);
        Assert.Equal(new EsPoint(80, 80), port.Forward(new EsPoint(100, 100)));
    }

    // ====================================================================
    // Scale（原点基準の等方スケーリング）
    // ====================================================================

    [Fact]
    public void Scale_順変換でfactorで除算する()
    {
        var port = CoordTransform.Scale(2.0);
        Assert.Equal(new EsPoint(50, 75), port.Forward(new EsPoint(100, 150)));
    }

    [Fact]
    public void Scale_逆変換でfactorで乗算する()
    {
        var port = CoordTransform.Scale(2.0);
        Assert.Equal(new EsPoint(100, 150), port.Inverse(new EsPoint(50, 75)));
    }

    [Fact]
    public void Scale_往復で元の座標に戻る()
    {
        var port = CoordTransform.Scale(1.5);
        var p = new EsPoint(123.4, 456.7);
        var roundTrip = port.Forward(port.Inverse(p));
        Assert.Equal(p.X, roundTrip.X, 6);
        Assert.Equal(p.Y, roundTrip.Y, 6);
    }

    [Fact]
    public void Scale_倍率0以下は等倍として扱う()
    {
        var port = CoordTransform.Scale(0);
        Assert.Equal(new EsPoint(10, 20), port.Forward(new EsPoint(10, 20)));
        Assert.Equal(new EsPoint(10, 20), port.Inverse(new EsPoint(10, 20)));
    }

    // ====================================================================
    // DomainScale（DIP → ドメインの乗算。Marquee のドメイン方向）
    // ====================================================================

    [Fact]
    public void DomainScale_順変換で倍率を乗算する()
    {
        // Forward = DIP → ドメイン（乗算）
        var port = CoordTransform.DomainScale(() => 2.0);
        Assert.Equal(new EsPoint(200, 300), port.Forward(new EsPoint(100, 150)));
    }

    [Fact]
    public void DomainScale_逆変換で倍率で除算する()
    {
        // Inverse = ドメイン → DIP（除算）
        var port = CoordTransform.DomainScale(() => 2.0);
        Assert.Equal(new EsPoint(100, 150), port.Inverse(new EsPoint(200, 300)));
    }

    [Fact]
    public void DomainScale_往復で元の座標に戻る()
    {
        var port = CoordTransform.DomainScale(() => 1.5);
        var p = new EsPoint(123.4, 456.7);
        var roundTrip = port.Inverse(port.Forward(p));
        Assert.Equal(p.X, roundTrip.X, 6);
        Assert.Equal(p.Y, roundTrip.Y, 6);
    }

    [Fact]
    public void DomainScale_プロバイダは呼び出し毎に評価される()
    {
        var factor = 1.0;
        var port = CoordTransform.DomainScale(() => factor);
        Assert.Equal(new EsPoint(100, 100), port.Forward(new EsPoint(100, 100)));

        factor = 2.0;
        Assert.Equal(new EsPoint(200, 200), port.Forward(new EsPoint(100, 100)));
    }

    [Fact]
    public void DomainScale_倍率0以下は等倍として扱う()
    {
        var port = CoordTransform.DomainScale(() => 0);
        Assert.Equal(new EsPoint(10, 20), port.Forward(new EsPoint(10, 20)));
        Assert.Equal(new EsPoint(10, 20), port.Inverse(new EsPoint(10, 20)));
    }

    [Fact]
    public void DomainScale_Boundsは無制限を返す()
    {
        var port = CoordTransform.DomainScale(() => 2.0);
        Assert.Null(port.Bounds);
    }

    // ====================================================================
    // ImageLayout（動的画像レイアウトポート）
    // ====================================================================

    [Fact]
    public void ImageLayout_順変換でDIPを論理に変換する()
    {
        // 表示領域 200x100（横長）、元画像 100x100（正方形）→ 表示 100x100、オフセット (50, 0)、スケール 1.0
        var port = CoordTransform.ImageLayout(() => new EsSize(200, 100), new EsSize(100, 100));

        // 表示中央（DIP 100, 50）→ 画像中央（50, 50）
        var p = port.Forward(new EsPoint(100, 50));
        Assert.Equal(50, p.X, 6);
        Assert.Equal(50, p.Y, 6);
    }

    [Fact]
    public void ImageLayout_逆変換で論理をDIPに変換する()
    {
        // 表示領域 200x100（横長）、元画像 100x100（正方形）→ 表示 100x100、オフセット (50, 0)、スケール 1.0
        var port = CoordTransform.ImageLayout(() => new EsSize(200, 100), new EsSize(100, 100));

        // 画像中央（50, 50）→ 表示中央（DIP 100, 50）
        var p = port.Inverse(new EsPoint(50, 50));
        Assert.Equal(100, p.X, 6);
        Assert.Equal(50, p.Y, 6);
    }

    [Fact]
    public void ImageLayout_往復で元の座標に戻る()
    {
        var port = CoordTransform.ImageLayout(() => new EsSize(800, 600), new EsSize(400, 300));
        var dip = new EsPoint(123.4, 456.7);
        var roundTrip = port.Inverse(port.Forward(dip));
        Assert.Equal(dip.X, roundTrip.X, 6);
        Assert.Equal(dip.Y, roundTrip.Y, 6);
    }

    [Fact]
    public void ImageLayout_表示サイズ変更に追従する()
    {
        var viewport = new EsSize(200, 100);
        var port = CoordTransform.ImageLayout(() => viewport, new EsSize(100, 100));
        // 表示 200x100 → 表示領域 100x100、オフセット (50, 0)、スケール 1.0
        Assert.Equal(50, port.Forward(new EsPoint(100, 50)).X, 6);

        // ズームで表示領域が 400x200 に変化 → 表示領域 200x200、オフセット (100, 0)、スケール 0.5（動的評価）
        viewport = new EsSize(400, 200);
        Assert.Equal(75, port.Forward(new EsPoint(250, 100)).X, 6);
    }

    [Fact]
    public void ImageLayout_非Uniformをプロバイダで切り替えられる()
    {
        var uniform = true;
        var port = CoordTransform.ImageLayout(
            () => new EsSize(200, 100),
            new EsSize(100, 100),
            () => uniform);

        // Uniform: 表示 100x100、オフセット (50, 0)、スケール 1.0 → (100, 50) → (50, 50)
        Assert.Equal(50, port.Forward(new EsPoint(100, 50)).X, 6);

        // Fill: 表示 200x100、スケール (0.5, 1.0) → (100, 50) → (50, 50)
        uniform = false;
        Assert.Equal(50, port.Forward(new EsPoint(100, 50)).X, 6);
    }

    [Fact]
    public void ImageLayout_表示領域が0以下なら等価変換する()
    {
        var port = CoordTransform.ImageLayout(() => new EsSize(0, 0), new EsSize(100, 50));
        Assert.Equal(new EsPoint(10, 20), port.Forward(new EsPoint(10, 20)));
        Assert.Equal(new EsPoint(10, 20), port.Inverse(new EsPoint(10, 20)));
    }

    [Fact]
    public void ImageLayout_Boundsは元画像サイズを返す()
    {
        var port = CoordTransform.ImageLayout(() => new EsSize(200, 100), new EsSize(100, 50));
        Assert.Equal(new EsSize(100, 50), port.Bounds);
    }

    // ====================================================================
    // ImageLayoutScaleX（ズーム率導出用の X スケール）
    // ====================================================================

    [Fact]
    public void ImageLayoutScaleX_レターボックス時にスケールを返す()
    {
        // 表示 200x100、元画像 100x100 → スケール 1.0
        var sx = CoordTransform.ImageLayoutScaleX(() => new EsSize(200, 100), new EsSize(100, 100));
        Assert.Equal(1.0, sx, 6);
    }

    [Fact]
    public void ImageLayoutScaleX_縮小時にスケール係数を返す()
    {
        // 表示 100x100、元画像 200x100（横長）→ 表示 100x50、スケール 2.0
        var sx = CoordTransform.ImageLayoutScaleX(() => new EsSize(100, 100), new EsSize(200, 100));
        Assert.Equal(2.0, sx, 6);
    }

    [Fact]
    public void ImageLayoutScaleX_呼び出し毎に現在の表示状態を反映する()
    {
        var viewport = new EsSize(200, 100);
        // 表示 200x100、元画像 100x100 → スケール 1.0
        Assert.Equal(1.0, CoordTransform.ImageLayoutScaleX(() => viewport, new EsSize(100, 100)), 6);

        // ズームで表示領域が 2 倍 → スケール 0.5（呼び出し毎に評価）
        viewport = new EsSize(400, 200);
        Assert.Equal(0.5, CoordTransform.ImageLayoutScaleX(() => viewport, new EsSize(100, 100)), 6);
    }

    [Fact]
    public void ImageLayoutScaleX_表示領域0以下は等倍を返す()
    {
        var sx = CoordTransform.ImageLayoutScaleX(() => new EsSize(0, 0), new EsSize(100, 50));
        Assert.Equal(1.0, sx, 6);
    }

    [Fact]
    public void ImageLayoutScaleX_元画像サイズ0以下は等倍を返す()
    {
        var sx = CoordTransform.ImageLayoutScaleX(() => new EsSize(100, 100), new EsSize(0, 50));
        Assert.Equal(1.0, sx, 6);
    }

    [Fact]
    public void ImageLayoutScaleX_uniformProviderを指定してスケールを切り替えられる()
    {
        // uniformProvider: false（非 Uniform = 各軸独立スケール）→ X スケール = 元幅 / 表示幅
        var sx = CoordTransform.ImageLayoutScaleX(
            () => new EsSize(200, 100), new EsSize(100, 100), uniformProvider: () => false);
        Assert.Equal(0.5, sx, 6);

        // uniformProvider: true（Uniform = 最小スケールに揃える）→ X スケールは 1.0
        var sxUniform = CoordTransform.ImageLayoutScaleX(
            () => new EsSize(200, 100), new EsSize(100, 100), uniformProvider: () => true);
        Assert.Equal(1.0, sxUniform, 6);
    }

    // ====================================================================
    // CalculateUniformLayout（レターボックス計算の純粋ロジック）
    // ====================================================================

    [Fact]
    public void CalculateUniformLayout_同一アスペクト比で等倍レイアウトを返す()
    {
        var (rw, rh, ox, oy) = CoordTransform.CalculateUniformLayout(100, 100, 100, 100);
        Assert.Equal(100, rw);
        Assert.Equal(100, rh);
        Assert.Equal(0, ox);
        Assert.Equal(0, oy);
    }

    [Fact]
    public void CalculateUniformLayout_横長表示領域で左右に余白を算出する()
    {
        // 表示領域 200x100（横長）、元画像 100x100（正方形）
        var (rw, rh, ox, oy) = CoordTransform.CalculateUniformLayout(200, 100, 100, 100);
        Assert.Equal(100, rh);   // 高さでフィット
        Assert.Equal(100, rw);   // 幅 = 高さ * 1.0
        Assert.Equal(50, ox);    // 左右に余白
        Assert.Equal(0, oy);
    }

    [Fact]
    public void CalculateUniformLayout_縦長表示領域で上下に余白を算出する()
    {
        // 表示領域 100x200（縦長）、元画像 100x100（正方形）
        var (rw, rh, ox, oy) = CoordTransform.CalculateUniformLayout(100, 200, 100, 100);
        Assert.Equal(100, rw);   // 幅でフィット
        Assert.Equal(100, rh);   // 高さ = 幅 * 1.0
        Assert.Equal(0, ox);
        Assert.Equal(50, oy);    // 上下に余白
    }

    [Fact]
    public void CalculateUniformLayout_横長画像を横長表示領域にフィットする()
    {
        // 表示領域 200x200、元画像 200x100（横長）
        var (rw, rh, ox, oy) = CoordTransform.CalculateUniformLayout(200, 200, 200, 100);
        Assert.Equal(200, rw);   // 幅でフィット（アスペクト比 2.0 で高さ 100）
        Assert.Equal(100, rh);
        Assert.Equal(0, ox);
        Assert.Equal(50, oy);    // 上下に余白
    }

    // ====================================================================
    // ComputeImageRenderInfo（レイアウト + スケール係数の一元計算）
    // ====================================================================

    [Fact]
    public void ComputeImageRenderInfo_同一アスペクトで等倍スケールを返す()
    {
        var (rw, rh, ox, oy, sx, sy) = CoordTransform.ComputeImageRenderInfo(100, 100, 100, 100);
        Assert.Equal(100, rw);
        Assert.Equal(100, rh);
        Assert.Equal(0, ox);
        Assert.Equal(0, oy);
        Assert.Equal(1.0, sx);
        Assert.Equal(1.0, sy);
    }

    [Fact]
    public void ComputeImageRenderInfo_レターボックス時にスケール係数を算出する()
    {
        // 表示領域 200x100（横長）、元画像 100x100（正方形）→ 表示 100x100、スケール 1.0
        var (rw, rh, ox, oy, sx, sy) = CoordTransform.ComputeImageRenderInfo(200, 100, 100, 100);
        Assert.Equal(100, rw);
        Assert.Equal(100, rh);
        Assert.Equal(50, ox);
        Assert.Equal(0, oy);
        Assert.Equal(1.0, sx);
        Assert.Equal(1.0, sy);
    }

    [Fact]
    public void ComputeImageRenderInfo_縮小時にスケール係数を算出する()
    {
        // 表示領域 100x100、元画像 200x100（横長）→ 表示 100x50、スケール 2.0
        var (rw, rh, ox, oy, sx, sy) = CoordTransform.ComputeImageRenderInfo(100, 100, 200, 100);
        Assert.Equal(100, rw);
        Assert.Equal(50, rh);
        Assert.Equal(0, ox);
        Assert.Equal(25, oy);
        Assert.Equal(2.0, sx);
        Assert.Equal(2.0, sy);
    }

    [Fact]
    public void ComputeImageRenderInfo_非UniformでFillスケールを算出する()
    {
        // 表示領域 100x50、元画像 200x100、Fill → 表示領域いっぱい、スケール 2.0
        var (rw, rh, ox, oy, sx, sy) = CoordTransform.ComputeImageRenderInfo(100, 50, 200, 100, uniform: false);
        Assert.Equal(100, rw);
        Assert.Equal(50, rh);
        Assert.Equal(0, ox);
        Assert.Equal(0, oy);
        Assert.Equal(2.0, sx);
        Assert.Equal(2.0, sy);
    }

    [Fact]
    public void ComputeImageRenderInfo_表示領域サイズ0時は等倍を返す()
    {
        var (rw, rh, ox, oy, sx, sy) = CoordTransform.ComputeImageRenderInfo(0, 100, 100, 100);
        Assert.Equal(0, rw);
        Assert.Equal(100, rh);
        Assert.Equal(0, ox);
        Assert.Equal(0, oy);
        Assert.Equal(1.0, sx);
        Assert.Equal(1.0, sy);
    }

    [Fact]
    public void ComputeImageRenderInfo_元画像サイズ0時は等倍を返す()
    {
        var (rw, rh, ox, oy, sx, sy) = CoordTransform.ComputeImageRenderInfo(100, 100, 0, 100);
        Assert.Equal(100, rw);
        Assert.Equal(100, rh);
        Assert.Equal(0, ox);
        Assert.Equal(0, oy);
        Assert.Equal(1.0, sx);
        Assert.Equal(1.0, sy);
    }
}
