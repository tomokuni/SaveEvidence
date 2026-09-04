using Avalonia;
using Xunit;

namespace Estable.AvaloniaUI.ZoomView.Tests;

/// <summary>
/// ZoomMath（ズーム表示の幾何計算・純粋関数）の回帰テスト。
/// </summary>
/// <remarks>
/// ズーム中心維持の計算仕様（ズーム率変更時のオフセット算出・クランプ・Fit 実効倍率・座標変換）を検証する。
/// </remarks>
public class ZoomMathTests
{
    // ====================================================================
    // FitScale / FitZoomPercent（Fit 実効スケール・倍率）
    // ====================================================================

    [Fact]
    public void FitScale_幅と高さのスケールの小さい方を返す()
    {
        // 画像 1000x2000、ビューポート 500x400 → X=0.5, Y=0.2 → 0.2
        var result = ZoomMath.FitScale(new Size(1000, 2000), new Size(500, 400));
        Assert.Equal(0.2, result);
    }

    [Fact]
    public void FitScale_幅が基準になる場合()
    {
        // 画像 2000x1000、ビューポート 500x400 → X=0.25, Y=0.4 → 0.25
        var result = ZoomMath.FitScale(new Size(2000, 1000), new Size(500, 400));
        Assert.Equal(0.25, result);
    }

    [Fact]
    public void FitScale_画像サイズが0なら1を返す()
    {
        Assert.Equal(1.0, ZoomMath.FitScale(new Size(0, 100), new Size(500, 400)));
        Assert.Equal(1.0, ZoomMath.FitScale(new Size(100, 0), new Size(500, 400)));
    }

    [Fact]
    public void FitScale_スケールが0以下なら1を返す()
    {
        // ビューポート 0x0 → スケール 0 → 1 にフォールバック
        Assert.Equal(1.0, ZoomMath.FitScale(new Size(1000, 500), new Size(0, 0)));
    }

    [Fact]
    public void FitZoomPercent_FitScaleの100倍を返す()
    {
        // 画像 1000x2000、ビューポート 500x400 → FitScale 0.2 → 20%
        var percent = ZoomMath.FitZoomPercent(new Size(1000, 2000), new Size(500, 400));
        Assert.Equal(20.0, percent);

        // FitScale との一貫性（単一所有の委譲）
        var scale = ZoomMath.FitScale(new Size(1000, 2000), new Size(500, 400));
        Assert.Equal(scale * 100.0, percent);
    }

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

    // ====================================================================
    // ViewportToContentImage / ContentToViewportImage（画像表示矩形原点基準の変換）
    // ====================================================================

    [Fact]
    public void ViewportToContentImage_画像原点を基準にビューポートからコンテンツへ変換する()
    {
        // 画像原点 (100,50)・スケール 2.0 → ((10-100)/2, (20-50)/2) = (-45, -15)
        var result = ZoomMath.ViewportToContentImage(new Point(10, 20), new Point(100, 50), 2.0);

        Assert.Equal(new Point(-45, -15), result);
    }

    [Fact]
    public void ViewportToContentImage_スケール0以下は1として扱う()
    {
        var result = ZoomMath.ViewportToContentImage(new Point(110, 70), new Point(100, 50), 0);

        Assert.Equal(new Point(10, 20), result);
    }

    [Fact]
    public void ContentToViewportImage_画像原点を基準にコンテンツからビューポートへ変換する()
    {
        // 画像原点 (100,50)・スケール 2.0 → (100 + (-45)*2, 50 + (-15)*2) = (10, 20)
        var result = ZoomMath.ContentToViewportImage(new Point(-45, -15), new Point(100, 50), 2.0);

        Assert.Equal(new Point(10, 20), result);
    }

    [Fact]
    public void ContentToViewportImage_スケール0以下は1として扱う()
    {
        var result = ZoomMath.ContentToViewportImage(new Point(10, 20), new Point(100, 50), 0);

        Assert.Equal(new Point(110, 70), result);
    }

    [Fact]
    public void 画像基準の往復変換で元のビューポート座標に戻る()
    {
        var viewportPos = new Point(150, 260);
        var imageOrigin = new Point(60, 80);
        var scale = 1.75;

        var content = ZoomMath.ViewportToContentImage(viewportPos, imageOrigin, scale);
        var back = ZoomMath.ContentToViewportImage(content, imageOrigin, scale);

        Assert.Equal(viewportPos.X, back.X, 6);
        Assert.Equal(viewportPos.Y, back.Y, 6);
    }
}
