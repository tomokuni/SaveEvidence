using Avalonia;
using Xunit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary><see cref="CoordinatePort"/> の合成・スクロール・拡張メソッドの単体テスト。</summary>
public class CoordinatePortTests
{
    // ====================================================================
    // Compose（ポート合成）
    // ====================================================================

    [Fact]
    public void Compose_順変換はinnerを先に適用する()
    {
        // inner: +10（座標系 A → 中間）、outer: ×2（中間 → 座標系 B）
        var inner = new CoordinatePort(p => p + new Vector(10, 10), p => p - new Vector(10, 10), () => null);
        var outer = new CoordinatePort(p => new Point(p.X * 2, p.Y * 2), p => new Point(p.X / 2, p.Y / 2), () => null);
        var composed = CoordinatePort.Compose(outer, inner);

        // Forward(5) = outer.Forward(inner.Forward(5)) = outer.Forward(15) = 30
        Assert.Equal(new Point(30, 30), composed.Forward(new Point(5, 5)));
        // Inverse(30) = inner.Inverse(outer.Inverse(30)) = inner.Inverse(15) = 5
        Assert.Equal(new Point(5, 5), composed.Inverse(new Point(30, 30)));
    }

    [Fact]
    public void Compose_Boundsはouterを優先しnull時はinnerを使う()
    {
        var inner = new CoordinatePort(p => p, p => p, () => new Size(100, 100));
        var outer = new CoordinatePort(p => p, p => p, () => null);
        Assert.Equal(new Size(100, 100), CoordinatePort.Compose(outer, inner).Bounds);

        var inner2 = new CoordinatePort(p => p, p => p, () => new Size(50, 50));
        var outer2 = new CoordinatePort(p => p, p => p, () => new Size(200, 200));
        Assert.Equal(new Size(200, 200), CoordinatePort.Compose(outer2, inner2).Bounds);
    }

    // ====================================================================
    // ScrollOffset（コンテンツ ↔ ビューポート）
    // ====================================================================

    [Fact]
    public void ScrollOffset_順変換でスクロールを減算する()
    {
        var port = CoordinatePort.ScrollOffset(() => new Vector(30, 50));
        Assert.Equal(new Point(70, 150), port.Forward(new Point(100, 200)));
    }

    [Fact]
    public void ScrollOffset_逆変換でスクロールを加算する()
    {
        var port = CoordinatePort.ScrollOffset(() => new Vector(30, 50));
        Assert.Equal(new Point(100, 200), port.Inverse(new Point(70, 150)));
    }

    [Fact]
    public void ScrollOffset_プロバイダは呼び出し毎に評価される()
    {
        var offset = new Vector(10, 10);
        var port = CoordinatePort.ScrollOffset(() => offset);
        Assert.Equal(new Point(90, 90), port.Forward(new Point(100, 100)));
        offset = new Vector(20, 20);
        Assert.Equal(new Point(80, 80), port.Forward(new Point(100, 100)));
    }

    // ====================================================================
    // Scale（原点基準の等方スケーリング）
    // ====================================================================

    [Fact]
    public void Scale_順変換でfactorで除算する()
    {
        var port = CoordinatePort.Scale(2.0);
        Assert.Equal(new Point(50, 75), port.Forward(new Point(100, 150)));
    }

    [Fact]
    public void Scale_逆変換でfactorで乗算する()
    {
        var port = CoordinatePort.Scale(2.0);
        Assert.Equal(new Point(100, 150), port.Inverse(new Point(50, 75)));
    }

    [Fact]
    public void Scale_往復で元の座標に戻る()
    {
        var port = CoordinatePort.Scale(1.5);
        var p = new Point(123.4, 456.7);
        var roundTrip = port.Forward(port.Inverse(p));
        Assert.Equal(p.X, roundTrip.X, 6);
        Assert.Equal(p.Y, roundTrip.Y, 6);
    }

    [Fact]
    public void Scale_倍率0以下は等倍として扱う()
    {
        var port = CoordinatePort.Scale(0);
        Assert.Equal(new Point(10, 20), port.Forward(new Point(10, 20)));
        Assert.Equal(new Point(10, 20), port.Inverse(new Point(10, 20)));
    }

    [Fact]
    public void Scale_矩形変換でスケールを適用する()
    {
        var port = CoordinatePort.Scale(2.0);
        // ドメイン矩形（100, 100, 200, 100）→ DIP（50, 50, 100, 50）
        var rect = port.ForwardRect(new Rect(100, 100, 200, 100));
        Assert.Equal(new Rect(50, 50, 100, 50), rect);
    }

    // ====================================================================
    // DomainScale（DIP → ドメインの乗算。DragRect のドメイン方向）
    // ====================================================================

    [Fact]
    public void DomainScale_順変換で倍率を乗算する()
    {
        // Forward = DIP → ドメイン（乗算）
        var port = CoordinatePort.DomainScale(() => 2.0);
        Assert.Equal(new Point(200, 300), port.Forward(new Point(100, 150)));
    }

    [Fact]
    public void DomainScale_逆変換で倍率で除算する()
    {
        // Inverse = ドメイン → DIP（除算）
        var port = CoordinatePort.DomainScale(() => 2.0);
        Assert.Equal(new Point(100, 150), port.Inverse(new Point(200, 300)));
    }

    [Fact]
    public void DomainScale_往復で元の座標に戻る()
    {
        var port = CoordinatePort.DomainScale(() => 1.5);
        var p = new Point(123.4, 456.7);
        var roundTrip = port.Inverse(port.Forward(p));
        Assert.Equal(p.X, roundTrip.X, 6);
        Assert.Equal(p.Y, roundTrip.Y, 6);
    }

    [Fact]
    public void DomainScale_プロバイダは呼び出し毎に評価される()
    {
        var factor = 1.0;
        var port = CoordinatePort.DomainScale(() => factor);
        Assert.Equal(new Point(100, 100), port.Forward(new Point(100, 100)));

        factor = 2.0;
        Assert.Equal(new Point(200, 200), port.Forward(new Point(100, 100)));
    }

    [Fact]
    public void DomainScale_倍率0以下は等倍として扱う()
    {
        var port = CoordinatePort.DomainScale(() => 0);
        Assert.Equal(new Point(10, 20), port.Forward(new Point(10, 20)));
        Assert.Equal(new Point(10, 20), port.Inverse(new Point(10, 20)));
    }

    [Fact]
    public void DomainScale_Boundsは無制限を返す()
    {
        var port = CoordinatePort.DomainScale(() => 2.0);
        Assert.Null(port.Bounds);
    }

    // ====================================================================
    // ImageLayout（動的画像レイアウトポート）
    // ====================================================================

    [Fact]
    public void ImageLayout_順変換でDIPを論理に変換する()
    {
        // 表示領域 200x100（横長）、元画像 100x100（正方形）→ 表示 100x100、オフセット (50, 0)、スケール 1.0
        var port = CoordinatePort.ImageLayout(() => new Size(200, 100), new Size(100, 100));

        // 表示中央（DIP 100, 50）→ 画像中央（50, 50）
        var p = port.Forward(new Point(100, 50));
        Assert.Equal(50, p.X, 6);
        Assert.Equal(50, p.Y, 6);
    }

    [Fact]
    public void ImageLayout_逆変換で論理をDIPに変換する()
    {
        // 表示領域 200x100（横長）、元画像 100x100（正方形）→ 表示 100x100、オフセット (50, 0)、スケール 1.0
        var port = CoordinatePort.ImageLayout(() => new Size(200, 100), new Size(100, 100));

        // 画像中央（50, 50）→ 表示中央（DIP 100, 50）
        var p = port.Inverse(new Point(50, 50));
        Assert.Equal(100, p.X, 6);
        Assert.Equal(50, p.Y, 6);
    }

    [Fact]
    public void ImageLayout_往復で元の座標に戻る()
    {
        var port = CoordinatePort.ImageLayout(() => new Size(800, 600), new Size(400, 300));
        var dip = new Point(123.4, 456.7);
        var roundTrip = port.Inverse(port.Forward(dip));
        Assert.Equal(dip.X, roundTrip.X, 6);
        Assert.Equal(dip.Y, roundTrip.Y, 6);
    }

    [Fact]
    public void ImageLayout_表示サイズ変更に追従する()
    {
        var viewport = new Size(200, 100);
        var port = CoordinatePort.ImageLayout(() => viewport, new Size(100, 100));
        // 表示 200x100 → 表示領域 100x100、オフセット (50, 0)、スケール 1.0
        Assert.Equal(50, port.Forward(new Point(100, 50)).X, 6);

        // ズームで表示領域が 400x200 に変化 → 表示領域 200x200、オフセット (100, 0)、スケール 0.5（動的評価）
        viewport = new Size(400, 200);
        Assert.Equal(75, port.Forward(new Point(250, 100)).X, 6);
    }

    [Fact]
    public void ImageLayout_非Uniformをプロバイダで切り替えられる()
    {
        var uniform = true;
        var port = CoordinatePort.ImageLayout(
            () => new Size(200, 100),
            new Size(100, 100),
            () => uniform);

        // Uniform: 表示 100x100、オフセット (50, 0)、スケール 1.0 → (100, 50) → (50, 50)
        Assert.Equal(50, port.Forward(new Point(100, 50)).X, 6);

        // Fill: 表示 200x100、スケール (0.5, 1.0) → (100, 50) → (50, 50)
        uniform = false;
        Assert.Equal(50, port.Forward(new Point(100, 50)).X, 6);
    }

    [Fact]
    public void ImageLayout_表示領域が0以下なら等価変換する()
    {
        var port = CoordinatePort.ImageLayout(() => new Size(0, 0), new Size(100, 50));
        Assert.Equal(new Point(10, 20), port.Forward(new Point(10, 20)));
        Assert.Equal(new Point(10, 20), port.Inverse(new Point(10, 20)));
    }

    [Fact]
    public void ImageLayout_Boundsは元画像サイズを返す()
    {
        var port = CoordinatePort.ImageLayout(() => new Size(200, 100), new Size(100, 50));
        Assert.Equal(new Size(100, 50), port.Bounds);
    }

    // ====================================================================
    // ImageLayoutScaleX（ズーム率導出用の X スケール）
    // ====================================================================

    [Fact]
    public void ImageLayoutScaleX_レターボックス時にスケールを返す()
    {
        // 表示 200x100、元画像 100x100 → スケール 1.0
        var sx = CoordinatePort.ImageLayoutScaleX(() => new Size(200, 100), new Size(100, 100));
        Assert.Equal(1.0, sx, 6);
    }

    [Fact]
    public void ImageLayoutScaleX_縮小時にスケール係数を返す()
    {
        // 表示 100x100、元画像 200x100（横長）→ 表示 100x50、スケール 2.0
        var sx = CoordinatePort.ImageLayoutScaleX(() => new Size(100, 100), new Size(200, 100));
        Assert.Equal(2.0, sx, 6);
    }

    [Fact]
    public void ImageLayoutScaleX_呼び出し毎に現在の表示状態を反映する()
    {
        var viewport = new Size(200, 100);
        // 表示 200x100、元画像 100x100 → スケール 1.0
        Assert.Equal(1.0, CoordinatePort.ImageLayoutScaleX(() => viewport, new Size(100, 100)), 6);

        // ズームで表示領域が 2 倍 → スケール 0.5（呼び出し毎に評価）
        viewport = new Size(400, 200);
        Assert.Equal(0.5, CoordinatePort.ImageLayoutScaleX(() => viewport, new Size(100, 100)), 6);
    }

    [Fact]
    public void ImageLayoutScaleX_表示領域0以下は等倍を返す()
    {
        var sx = CoordinatePort.ImageLayoutScaleX(() => new Size(0, 0), new Size(100, 50));
        Assert.Equal(1.0, sx, 6);
    }

    [Fact]
    public void ImageLayoutScaleX_元画像サイズ0以下は等倍を返す()
    {
        var sx = CoordinatePort.ImageLayoutScaleX(() => new Size(100, 100), new Size(0, 50));
        Assert.Equal(1.0, sx, 6);
    }

    // ====================================================================
    // ForwardRect / InverseRect（矩形変換）
    // ====================================================================

    [Fact]
    public void ForwardRect_順変換で矩形を変換する()
    {
        var port = CoordinatePort.ScrollOffset(() => new Vector(30, 50));
        var rect = port.ForwardRect(new Rect(100, 200, 50, 60));
        Assert.Equal(new Rect(70, 150, 50, 60), rect);
    }

    [Fact]
    public void InverseRect_逆変換で矩形を変換する()
    {
        var port = CoordinatePort.ScrollOffset(() => new Vector(30, 50));
        var rect = port.InverseRect(new Rect(70, 150, 50, 60));
        Assert.Equal(new Rect(100, 200, 50, 60), rect);
    }

    [Fact]
    public void ForwardRect_画像レイアウトで矩形を変換する()
    {
        var port = CoordinatePort.ImageLayout(() => new Size(200, 100), new Size(100, 100));
        // 論理矩形 (0, 0, 100, 100) → DIP 矩形 (50, 0, 100, 100)
        var rect = port.InverseRect(new Rect(0, 0, 100, 100));
        Assert.Equal(new Rect(50, 0, 100, 100), rect);
    }
}
