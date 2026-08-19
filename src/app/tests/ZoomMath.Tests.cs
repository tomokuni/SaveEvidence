using app.Controls;
using Avalonia;
using Xunit;

namespace app.Tests;

/// <summary>
/// ZoomMath（ズーム中心維持の純粋関数）の回帰テスト。
/// </summary>
/// <remarks>
/// ズーム中心維持の計算仕様（画像内の点の導出・ズーム後オフセットの算出・Fit 実効倍率）を検証する。<br/>
/// バグ修正（ズーム率の変化が考慮されず常に左上基準になる・中央寄せ時のずれ）の回帰防止を目的とする。
/// </remarks>
public class ZoomMathTests
{
    // ====================================================================
    // FitZoomPercent（Fit 実効倍率）
    // ====================================================================

    [Fact]
    public void FitZoomPercent_幅と高さのスケールの小さい方を返す()
    {
        // 画像 1000x2000、ビューポート 500x400 → X=50%, Y=20% → 20%
        var result = ZoomMath.FitZoomPercent(new Size(1000, 2000), new Size(500, 400));
        Assert.Equal(20.0, result);
    }

    [Fact]
    public void FitZoomPercent_幅が基準になる場合()
    {
        // 画像 2000x1000、ビューポート 500x400 → X=25%, Y=40% → 25%
        var result = ZoomMath.FitZoomPercent(new Size(2000, 1000), new Size(500, 400));
        Assert.Equal(25.0, result);
    }

    [Fact]
    public void FitZoomPercent_画像サイズが0なら100を返す()
    {
        Assert.Equal(100.0, ZoomMath.FitZoomPercent(new Size(0, 100), new Size(500, 400)));
        Assert.Equal(100.0, ZoomMath.FitZoomPercent(new Size(100, 0), new Size(500, 400)));
    }

    // ====================================================================
    // ComputeImagePoint（ズーム中心の画像内の点）
    // ====================================================================

    [Fact]
    public void ComputeImagePoint_非中央寄せではOffsetとCenterの和になる()
    {
        // 非中央寄せ（Left/Top）: コンテンツ座標 = Offset + Center がそのまま画像内の点
        var result = ZoomMath.ComputeImagePoint(
            new Vector(100, 50), new Point(200, 150),
            new Size(800, 600), new Size(800, 600), wasCentered: false);
        Assert.Equal(new Point(300, 200), result);
    }

    [Fact]
    public void ComputeImagePoint_中央寄せでは画像原点分を補正する()
    {
        // 中央寄せ: 画像 400x300 がビューポート 800x600 の中央 → 原点は (200, 150)
        // コンテンツ座標 (400, 350) - 原点 (200, 150) = 画像内 (200, 200)
        var result = ZoomMath.ComputeImagePoint(
            new Vector(0, 0), new Point(400, 350),
            new Size(800, 600), new Size(400, 300), wasCentered: true);
        Assert.Equal(new Point(200, 200), result);
    }

    [Fact]
    public void ComputeImagePoint_中央寄せでスクロール中も正しく補正する()
    {
        // 画像 400x300 がビューポート 800x600 の中央、スクロール (100, 50)
        // コンテンツ座標 = (100+300, 50+200) = (400, 250) - 原点 (200, 150) = (200, 100)
        var result = ZoomMath.ComputeImagePoint(
            new Vector(100, 50), new Point(300, 200),
            new Size(800, 600), new Size(400, 300), wasCentered: true);
        Assert.Equal(new Point(200, 100), result);
    }

    // ====================================================================
    // ComputeOffsetAfterZoom（ズーム後のオフセット）
    // ====================================================================

    [Fact]
    public void ComputeOffsetAfterZoom_ズームインでオフセットが進む()
    {
        // ズーム前: 画像 2000x1000、中心の画像内の点 (1000, 500)
        // ズーム後: 画像 2200x1100（110%）→ 点は (1100, 550) に移動。中心 (660, 300) を引いて (440, 250)
        var result = ZoomMath.ComputeOffsetAfterZoom(
            new Point(1000, 500), new Size(2000, 1000), new Size(2200, 1100),
            new Point(660, 300), new Size(1320, 600), new Size(2200, 1100));
        Assert.Equal(new Vector(440, 250), result);
    }

    [Fact]
    public void ComputeOffsetAfterZoom_ズームアウトでオフセットが戻る()
    {
        // ズーム前: 画像 2200x1100、中心の画像内の点 (1100, 550)
        // ズーム後: 画像 2000x1000 → 点は (1000, 500) に移動。中心 (660, 300) を引いて (340, 200)
        var result = ZoomMath.ComputeOffsetAfterZoom(
            new Point(1100, 550), new Size(2200, 1100), new Size(2000, 1000),
            new Point(660, 300), new Size(1320, 600), new Size(2000, 1000));
        Assert.Equal(new Vector(340, 200), result);
    }

    [Fact]
    public void ComputeOffsetAfterZoom_スクロール範囲を超えないようクランプする()
    {
        // 画像内の点が画像の右端 (1990, 990)。ズーム後オフセットは (1990*1.1-660, ...) = (1529, ...)
        // Extent=2200 Viewport=1320 → 最大オフセット = 880
        var result = ZoomMath.ComputeOffsetAfterZoom(
            new Point(1990, 990), new Size(2000, 1000), new Size(2200, 1100),
            new Point(660, 300), new Size(1320, 600), new Size(2200, 1100));
        Assert.Equal(880.0, result.X);
        Assert.Equal(500.0, result.Y);
    }

    [Fact]
    public void ComputeOffsetAfterZoom_オフセットが負にならないよう0にクランプする()
    {
        // 画像内の点が原点 (0, 0)。ズームアウトでオフセットが負になり得る → 0 にクランプ
        var result = ZoomMath.ComputeOffsetAfterZoom(
            new Point(0, 0), new Size(2000, 1000), new Size(1000, 500),
            new Point(660, 300), new Size(1320, 600), new Size(1000, 500));
        Assert.Equal(0.0, result.X);
        Assert.Equal(0.0, result.Y);
    }

    [Fact]
    public void ComputeOffsetAfterZoom_画像がビューポート以下の場合はオフセット0になる()
    {
        // 画像 500x300 < ビューポート 1320x600 → スクロール範囲 0
        var result = ZoomMath.ComputeOffsetAfterZoom(
            new Point(100, 100), new Size(500, 300), new Size(500, 300),
            new Point(660, 300), new Size(1320, 600), new Size(500, 300));
        Assert.Equal(0.0, result.X);
        Assert.Equal(0.0, result.Y);
    }

    [Fact]
    public void ComputeOffsetAfterZoom_ズーム前画像サイズが0でもゼロ除算しない()
    {
        // ゼロ除算を防ぎ、スケール 1 として計算される
        var result = ZoomMath.ComputeOffsetAfterZoom(
            new Point(100, 100), new Size(0, 0), new Size(1000, 1000),
            new Point(660, 300), new Size(1320, 600), new Size(1000, 1000));
        Assert.Equal(0.0, result.X); // 100 - 660 → 負 → 0 にクランプ
        Assert.Equal(0.0, result.Y);
    }

    // ====================================================================
    // ComputeOffsetAfterScaleChange（ズーム率変更時のオフセット。コンテンツ座標不変）
    // ====================================================================

    [Fact]
    public void ComputeOffsetAfterScaleChange_ズーム中心のコンテンツ座標を維持する()
    {
        // ビューポート 800x600、コンテンツ 2000x1000、ズーム 100%→200%（scale 1→2）
        // ズーム中心 = ビューポート中心 (400, 300)
        // ズーム前: offset=(0,0) のとき中心のコンテンツ座標 = 0 + 400/1 = (400, 300)
        // ズーム後: offset' = (400, 300) - (400/2, 300/2) = (200, 150)
        var result = ZoomMath.ComputeOffsetAfterScaleChange(
            new Vector(0, 0), new Point(400, 300), 1.0, 2.0,
            new Size(2000, 1000), new Size(800, 600));
        Assert.Equal(new Vector(200, 150), result);
    }

    [Fact]
    public void ComputeOffsetAfterScaleChange_スクロール中のズームで中心を維持する()
    {
        // ズーム前: offset=(500, 300)、ズーム 100%→200%
        // 中心のコンテンツ座標 = (500 + 400/1, 300 + 300/1) = (900, 600)
        // ズーム後: offset' = (900 - 400/2, 600 - 300/2) = (700, 450)
        var result = ZoomMath.ComputeOffsetAfterScaleChange(
            new Vector(500, 300), new Point(400, 300), 1.0, 2.0,
            new Size(2000, 1000), new Size(800, 600));
        Assert.Equal(new Vector(700, 450), result);
    }

    [Fact]
    public void ComputeOffsetAfterScaleChange_ズームアウトでオフセットが戻る()
    {
        // ズーム 200%→100%: 上記の逆
        // ズーム前: offset=(700, 450)、中心のコンテンツ座標 = (700 + 400/2, 450 + 300/2) = (900, 600)
        // ズーム後: offset' = (900 - 400/1, 600 - 300/1) = (500, 300)
        var result = ZoomMath.ComputeOffsetAfterScaleChange(
            new Vector(700, 450), new Point(400, 300), 2.0, 1.0,
            new Size(2000, 1000), new Size(800, 600));
        Assert.Equal(new Vector(500, 300), result);
    }

    [Fact]
    public void ComputeOffsetAfterScaleChange_スクロール範囲を超えないようクランプする()
    {
        // ズーム 100%→200%: ズーム前 offset=(1590, 690)
        // 中心のコンテンツ座標 = (1590+400, 690+300) = (1990, 990)
        // offset' = (1990-200, 990-150) = (1790, 840) → クランプ
        // 最大: maxX = 2000-800/2 = 1600, maxY = 1000-600/2 = 700
        var result = ZoomMath.ComputeOffsetAfterScaleChange(
            new Vector(1590, 690), new Point(400, 300), 1.0, 2.0,
            new Size(2000, 1000), new Size(800, 600));
        Assert.Equal(1600.0, result.X);
        Assert.Equal(700.0, result.Y);
    }

    [Fact]
    public void ComputeOffsetAfterScaleChange_スケールが0でもゼロ除算しない()
    {
        // ゼロ除算を防ぎ、スケール 1 として扱われる
        // oldScale=0→1: 中心のコンテンツ座標 = (0+400, 0+300) = (400, 300)
        // offset' = (400-400, 300-300) = (0, 0)
        var result = ZoomMath.ComputeOffsetAfterScaleChange(
            new Vector(0, 0), new Point(400, 300), 0.0, 1.0,
            new Size(2000, 1000), new Size(800, 600));
        Assert.Equal(0.0, result.X);
        Assert.Equal(0.0, result.Y);
    }

    // ====================================================================
    // ClampOffset（スクロール範囲クランプ）
    // ====================================================================

    [Fact]
    public void ClampOffset_スクロール範囲内はそのまま()
    {
        var result = ZoomMath.ClampOffset(new Vector(500, 300), 1.0,
            new Size(2000, 1000), new Size(800, 600));
        Assert.Equal(new Vector(500, 300), result);
    }

    [Fact]
    public void ClampOffset_範囲外はクランプされる()
    {
        // 最大 = 2000-800=1200, 1000-600=400
        var result = ZoomMath.ClampOffset(new Vector(2000, 500), 1.0,
            new Size(2000, 1000), new Size(800, 600));
        Assert.Equal(new Vector(1200, 400), result);
    }

    [Fact]
    public void ClampOffset_負の値は0にクランプされる()
    {
        var result = ZoomMath.ClampOffset(new Vector(-100, -50), 1.0,
            new Size(2000, 1000), new Size(800, 600));
        Assert.Equal(new Vector(0, 0), result);
    }

    [Fact]
    public void ClampOffset_コンテンツがビューポート以下なら0になる()
    {
        var result = ZoomMath.ClampOffset(new Vector(100, 50), 1.0,
            new Size(500, 300), new Size(800, 600));
        Assert.Equal(new Vector(0, 0), result);
    }

    // ====================================================================
    // ViewportToContent / ContentToViewport（ビューポート ↔ コンテンツ座標変換）
    // ====================================================================

    [Fact]
    public void ViewportToContent_固定倍率でオフセットを加算して変換する()
    {
        // オフセット (100,50)・スケール 2.0 → (100 + 10/2, 50 + 20/2) = (105, 60)
        var result = ZoomMath.ViewportToContent(new Point(10, 20), new Vector(100, 50), 2.0);

        Assert.Equal(new Point(105, 60), result);
    }

    [Fact]
    public void ViewportToContent_スケール0以下は1として扱う()
    {
        var result = ZoomMath.ViewportToContent(new Point(10, 20), new Vector(100, 50), 0);

        Assert.Equal(new Point(110, 70), result);
    }

    [Fact]
    public void ContentToViewport_固定倍率でオフセットを減算して変換する()
    {
        // オフセット (100,50)・スケール 2.0 → ((105-100)*2, (60-50)*2) = (10, 20)
        var result = ZoomMath.ContentToViewport(new Point(105, 60), new Vector(100, 50), 2.0);

        Assert.Equal(new Point(10, 20), result);
    }

    [Fact]
    public void ContentToViewport_スケール0以下は1として扱う()
    {
        var result = ZoomMath.ContentToViewport(new Point(110, 70), new Vector(100, 50), 0);

        Assert.Equal(new Point(10, 20), result);
    }

    [Fact]
    public void 往復変換で元のビューポート座標に戻る()
    {
        var viewportPos = new Point(123, 456);
        var offset = new Vector(30, 40);
        var scale = 2.5;

        var content = ZoomMath.ViewportToContent(viewportPos, offset, scale);
        var back = ZoomMath.ContentToViewport(content, offset, scale);

        Assert.Equal(viewportPos.X, back.X, 6);
        Assert.Equal(viewportPos.Y, back.Y, 6);
    }
}
