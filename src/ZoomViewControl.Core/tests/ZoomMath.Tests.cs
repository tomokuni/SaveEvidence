using Estable.Platform.Geometry;
using Xunit;

namespace Estable.ZoomView.Tests;

/// <summary>
/// ZoomMath（ズーム表示の幾何計算・純粋関数）の回帰テスト。
/// </summary>
/// <remarks>
/// ズーム中心維持の計算仕様（ズーム率変更時のオフセット算出・クランプ・Fit 実効倍率・座標変換）を検証する。<br/>
/// UI 初期化を必要としないため、Avalonia ヘッドレスに依存せず並列実行できる。
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
        var result = ZoomMath.FitScale(new EsSize(1000, 2000), new EsSize(500, 400));
        Assert.Equal(0.2, result);
    }

    [Fact]
    public void FitScale_幅が基準になる場合()
    {
        // 画像 2000x1000、ビューポート 500x400 → X=0.25, Y=0.4 → 0.25
        var result = ZoomMath.FitScale(new EsSize(2000, 1000), new EsSize(500, 400));
        Assert.Equal(0.25, result);
    }

    [Fact]
    public void FitScale_画像サイズが0なら1を返す()
    {
        Assert.Equal(1.0, ZoomMath.FitScale(new EsSize(0, 100), new EsSize(500, 400)));
        Assert.Equal(1.0, ZoomMath.FitScale(new EsSize(100, 0), new EsSize(500, 400)));
    }

    [Fact]
    public void FitScale_スケールが0以下なら1を返す()
    {
        // ビューポート 0x0 → スケール 0 → 1 にフォールバック
        Assert.Equal(1.0, ZoomMath.FitScale(new EsSize(1000, 500), new EsSize(0, 0)));
    }

    [Fact]
    public void FitZoomPercent_FitScaleの100倍を返す()
    {
        // 画像 1000x2000、ビューポート 500x400 → FitScale 0.2 → 20%
        var percent = ZoomMath.FitZoomPercent(new EsSize(1000, 2000), new EsSize(500, 400));
        Assert.Equal(20.0, percent);

        // FitScale との一貫性（単一所有の委譲）
        var scale = ZoomMath.FitScale(new EsSize(1000, 2000), new EsSize(500, 400));
        Assert.Equal(scale * 100.0, percent);
    }

    [Fact]
    public void FitZoomPercent_幅と高さのスケールの小さい方を返す()
    {
        // 画像 1000x2000、ビューポート 500x400 → X=50%, Y=20% → 20%
        var result = ZoomMath.FitZoomPercent(new EsSize(1000, 2000), new EsSize(500, 400));
        Assert.Equal(20.0, result);
    }

    [Fact]
    public void FitZoomPercent_幅が基準になる場合()
    {
        // 画像 2000x1000、ビューポート 500x400 → X=25%, Y=40% → 25%
        var result = ZoomMath.FitZoomPercent(new EsSize(2000, 1000), new EsSize(500, 400));
        Assert.Equal(25.0, result);
    }

    [Fact]
    public void FitZoomPercent_画像サイズが0なら100を返す()
    {
        Assert.Equal(100.0, ZoomMath.FitZoomPercent(new EsSize(0, 100), new EsSize(500, 400)));
        Assert.Equal(100.0, ZoomMath.FitZoomPercent(new EsSize(100, 0), new EsSize(500, 400)));
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
            new EsVector(0, 0), new EsPoint(400, 300), 1.0, 2.0,
            new EsSize(2000, 1000), new EsSize(800, 600));
        Assert.Equal(new EsVector(200, 150), result);
    }

    [Fact]
    public void ComputeOffsetAfterScaleChange_スクロール中のズームで中心を維持する()
    {
        // ズーム前: offset=(500, 300)、ズーム 100%→200%
        // 中心のコンテンツ座標 = (500 + 400/1, 300 + 300/1) = (900, 600)
        // ズーム後: offset' = (900 - 400/2, 600 - 300/2) = (700, 450)
        var result = ZoomMath.ComputeOffsetAfterScaleChange(
            new EsVector(500, 300), new EsPoint(400, 300), 1.0, 2.0,
            new EsSize(2000, 1000), new EsSize(800, 600));
        Assert.Equal(new EsVector(700, 450), result);
    }

    [Fact]
    public void ComputeOffsetAfterScaleChange_ズームアウトでオフセットが戻る()
    {
        // ズーム 200%→100%: 上記の逆
        // ズーム前: offset=(700, 450)、中心のコンテンツ座標 = (700 + 400/2, 450 + 300/2) = (900, 600)
        // ズーム後: offset' = (900 - 400/1, 600 - 300/1) = (500, 300)
        var result = ZoomMath.ComputeOffsetAfterScaleChange(
            new EsVector(700, 450), new EsPoint(400, 300), 2.0, 1.0,
            new EsSize(2000, 1000), new EsSize(800, 600));
        Assert.Equal(new EsVector(500, 300), result);
    }

    [Fact]
    public void ComputeOffsetAfterScaleChange_スクロール範囲を超えないようクランプする()
    {
        // ズーム 100%→200%: ズーム前 offset=(1590, 690)
        // 中心のコンテンツ座標 = (1590+400, 690+300) = (1990, 990)
        // offset' = (1990-200, 990-150) = (1790, 840) → クランプ
        // 最大: maxX = 2000-800/2 = 1600, maxY = 1000-600/2 = 700
        var result = ZoomMath.ComputeOffsetAfterScaleChange(
            new EsVector(1590, 690), new EsPoint(400, 300), 1.0, 2.0,
            new EsSize(2000, 1000), new EsSize(800, 600));
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
            new EsVector(0, 0), new EsPoint(400, 300), 0.0, 1.0,
            new EsSize(2000, 1000), new EsSize(800, 600));
        Assert.Equal(0.0, result.X);
        Assert.Equal(0.0, result.Y);
    }

    // ====================================================================
    // ClampOffset（スクロール範囲クランプ）
    // ====================================================================

    [Fact]
    public void ClampOffset_スクロール範囲内はそのまま()
    {
        var result = ZoomMath.ClampOffset(new EsVector(500, 300), 1.0,
            new EsSize(2000, 1000), new EsSize(800, 600));
        Assert.Equal(new EsVector(500, 300), result);
    }

    [Fact]
    public void ClampOffset_範囲外はクランプされる()
    {
        // 最大 = 2000-800=1200, 1000-600=400
        var result = ZoomMath.ClampOffset(new EsVector(2000, 500), 1.0,
            new EsSize(2000, 1000), new EsSize(800, 600));
        Assert.Equal(new EsVector(1200, 400), result);
    }

    [Fact]
    public void ClampOffset_負の値は0にクランプされる()
    {
        var result = ZoomMath.ClampOffset(new EsVector(-100, -50), 1.0,
            new EsSize(2000, 1000), new EsSize(800, 600));
        Assert.Equal(new EsVector(0, 0), result);
    }

    [Fact]
    public void ClampOffset_コンテンツがビューポート以下なら0になる()
    {
        var result = ZoomMath.ClampOffset(new EsVector(100, 50), 1.0,
            new EsSize(500, 300), new EsSize(800, 600));
        Assert.Equal(new EsVector(0, 0), result);
    }

    // ====================================================================
    // ViewportToContent / ContentToViewport（ビューポート ↔ コンテンツ座標変換）
    // ====================================================================

    [Fact]
    public void ViewportToContent_固定倍率でオフセットを加算して変換する()
    {
        // オフセット (100,50)・スケール 2.0 → (100 + 10/2, 50 + 20/2) = (105, 60)
        var result = ZoomMath.ViewportToContent(new EsPoint(10, 20), new EsVector(100, 50), 2.0);

        Assert.Equal(new EsPoint(105, 60), result);
    }

    [Fact]
    public void ViewportToContent_スケール0以下は1として扱う()
    {
        var result = ZoomMath.ViewportToContent(new EsPoint(10, 20), new EsVector(100, 50), 0);

        Assert.Equal(new EsPoint(110, 70), result);
    }

    [Fact]
    public void ContentToViewport_固定倍率でオフセットを減算して変換する()
    {
        // オフセット (100,50)・スケール 2.0 → ((105-100)*2, (60-50)*2) = (10, 20)
        var result = ZoomMath.ContentToViewport(new EsPoint(105, 60), new EsVector(100, 50), 2.0);

        Assert.Equal(new EsPoint(10, 20), result);
    }

    [Fact]
    public void ContentToViewport_スケール0以下は1として扱う()
    {
        var result = ZoomMath.ContentToViewport(new EsPoint(110, 70), new EsVector(100, 50), 0);

        Assert.Equal(new EsPoint(10, 20), result);
    }

    [Fact]
    public void 往復変換で元のビューポート座標に戻る()
    {
        var viewportPos = new EsPoint(123, 456);
        var offset = new EsVector(30, 40);
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
        var result = ZoomMath.ViewportToContentImage(new EsPoint(10, 20), new EsPoint(100, 50), 2.0);

        Assert.Equal(new EsPoint(-45, -15), result);
    }

    [Fact]
    public void ViewportToContentImage_スケール0以下は1として扱う()
    {
        var result = ZoomMath.ViewportToContentImage(new EsPoint(110, 70), new EsPoint(100, 50), 0);

        Assert.Equal(new EsPoint(10, 20), result);
    }

    [Fact]
    public void ContentToViewportImage_画像原点を基準にコンテンツからビューポートへ変換する()
    {
        // 画像原点 (100,50)・スケール 2.0 → (100 + (-45)*2, 50 + (-15)*2) = (10, 20)
        var result = ZoomMath.ContentToViewportImage(new EsPoint(-45, -15), new EsPoint(100, 50), 2.0);

        Assert.Equal(new EsPoint(10, 20), result);
    }

    [Fact]
    public void ContentToViewportImage_スケール0以下は1として扱う()
    {
        var result = ZoomMath.ContentToViewportImage(new EsPoint(10, 20), new EsPoint(100, 50), 0);

        Assert.Equal(new EsPoint(110, 70), result);
    }

    [Fact]
    public void 画像基準の往復変換で元のビューポート座標に戻る()
    {
        var viewportPos = new EsPoint(150, 260);
        var imageOrigin = new EsPoint(60, 80);
        var scale = 1.75;

        var content = ZoomMath.ViewportToContentImage(viewportPos, imageOrigin, scale);
        var back = ZoomMath.ContentToViewportImage(content, imageOrigin, scale);

        Assert.Equal(viewportPos.X, back.X, 6);
        Assert.Equal(viewportPos.Y, back.Y, 6);
    }

    // ====================================================================
    // ComputeEffectiveScale（実効スケール）
    // ====================================================================

    [Fact]
    public void ComputeEffectiveScale_固定倍率中はズーム率を100で割った値を返す()
    {
        // zoomPercent = 200 → scale = 2
        var scale = ZoomMath.ComputeEffectiveScale(200, new EsSize(1000, 500), new EsSize(400, 200));

        Assert.Equal(2.0, scale, 6);
    }

    [Fact]
    public void ComputeEffectiveScale_Fit中はFitScaleを返す()
    {
        // zoomPercent = 0（Fit）。画像 1000x500、ビューポート 400x200
        // scaleX = 400/1000 = 0.4、scaleY = 200/500 = 0.4（min = 0.4）
        var scale = ZoomMath.ComputeEffectiveScale(0, new EsSize(1000, 500), new EsSize(400, 200));

        Assert.Equal(0.4, scale, 6);
    }

    [Fact]
    public void ComputeEffectiveScale_Fit中で画像サイズが0なら1を返す()
    {
        var scale = ZoomMath.ComputeEffectiveScale(0, new EsSize(0, 0), new EsSize(400, 200));

        Assert.Equal(1.0, scale, 6);
    }

    // ====================================================================
    // MaxCentered（中央寄せ原点補正）
    // ====================================================================

    [Fact]
    public void MaxCentered_中央寄せならビューポート差分の半分を返す()
    {
        // 表示 100、ビューポート 400 → (400 - 100) / 2 = 150
        Assert.Equal(150.0, ZoomMath.MaxCentered(100, 400, centerAlign: true), 6);
    }

    [Fact]
    public void MaxCentered_CenterAlignがfalseなら0を返す()
    {
        Assert.Equal(0.0, ZoomMath.MaxCentered(100, 400, centerAlign: false), 6);
    }

    [Fact]
    public void MaxCentered_表示サイズがビューポートより大きい場合は0を返す()
    {
        // 表示 500、ビューポート 400 → スクロール対象のため補正しない
        Assert.Equal(0.0, ZoomMath.MaxCentered(500, 400, centerAlign: true), 6);
    }

    // ====================================================================
    // ComputeImageOrigin（画像原点のビューポート座標）
    // ====================================================================

    [Fact]
    public void ComputeImageOrigin_Fitで中央寄せならビューポート中央を原点にする()
    {
        // 表示 100x100、ビューポート 400x300 → 中央寄せ (150, 100)
        var origin = ZoomMath.ComputeImageOrigin(
            new EsSize(100, 100), new EsSize(400, 300), zoomPercent: 0,
            offset: default, scale: 0, centerAlign: true);

        Assert.Equal(new EsPoint(150, 100), origin);
    }

    [Fact]
    public void ComputeImageOrigin_Fitで左上寄せなら原点は0になる()
    {
        var origin = ZoomMath.ComputeImageOrigin(
            new EsSize(100, 100), new EsSize(400, 300), zoomPercent: 0,
            offset: default, scale: 0, centerAlign: false);

        Assert.Equal(new EsPoint(0, 0), origin);
    }

    [Fact]
    public void ComputeImageOrigin_固定倍率でスクロール可能な軸はパン位置になる()
    {
        // 表示 600x500、ビューポート 400x300（両軸ともスクロール可能）
        // offset=(100,50), scale=2 → x=-200, y=-100
        var origin = ZoomMath.ComputeImageOrigin(
            new EsSize(600, 500), new EsSize(400, 300), zoomPercent: 200,
            offset: new EsVector(100, 50), scale: 2, centerAlign: true);

        Assert.Equal(new EsPoint(-200, -100), origin);
    }

    [Fact]
    public void ComputeImageOrigin_固定倍率で中央寄せの軸はビューポート中央を原点にする()
    {
        // 表示 100x80、ビューポート 400x300（両軸ともスクロール不能）
        // offset は 0 にクランプされ中央寄せ → x=(400-100)/2=150, y=(300-80)/2=110
        var origin = ZoomMath.ComputeImageOrigin(
            new EsSize(100, 80), new EsSize(400, 300), zoomPercent: 200,
            offset: default, scale: 2, centerAlign: true);

        Assert.Equal(new EsPoint(150, 110), origin);
    }
}
