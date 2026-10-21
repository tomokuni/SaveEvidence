using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.ZoomView;
using EsUtil.Ui.AvaloniaUI.Interop.Imaging;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Tests.ZoomView;

/// <summary><see cref="ZoomViewControl"/>（自己完結型複合コントロール）の回帰テスト。</summary>
/// <remarks>
/// 実効ズーム率（<see cref="ZoomViewControl.ZoomPercent"/>）・<see cref="ZoomViewControl.ZoomFit"/>・
/// <see cref="ZoomViewControl.IsFit"/>・setter の 0 拒否などの仕様を検証する。<br/>
/// 計算の本体（<see cref="EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView.ZoomMath"/> / <see cref="EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView.ZoomTransition"/>）は純粋関数として別途テスト済みで、
/// 本テストは ZoomViewControl への適用（実効ズーム率の導出・Fit 移行）を検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class ZoomViewControlTests
{
    /// <summary>テスト用の画像（指定サイズ・白の単色）を生成する。</summary>
    private static EsBitmapAdapter CreateBitmap(int w, int h)
        => EsBitmapExtensions.CreateAvaloniaImage(w, h, Colors.White);

    /// <summary>テスト用の ZoomViewControl（画像を設定済み）を生成する。</summary>
    private static ZoomViewControl CreateView()
    {
        var view = new ZoomViewControl
        {
            Source = CreateBitmap(1000, 500)
        };
        // Bounds を確定させる（Fit 実効倍率の計算に必要）
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));
        return view;
    }

    // ====================================================================
    // ZoomPercent（実効ズーム率）・IsFit・ZoomFit の仕様
    // ====================================================================

    [AvaloniaFact]
    internal void 初期状態ではFit表示で実効ズーム率が0にならない()
    {
        var view = CreateView();

        // Fit（内部ズーム率 = 0）だが、実効ズーム率はビューポートと元画像サイズから算出された実効倍率（0 にならない）
        Assert.True(view.IsFit);
        Assert.True(view.ZoomPercent > 0);
    }

    [AvaloniaFact]
    internal void 画像がWriteableBitmapではなくIImageでもFit実効倍率が計算される()
    {
        // 回帰: ComputeRawFitZoomPercent が Source を Bitmap 型へキャストしていたため、
        // EsBitmapAdapter（WriteableBitmap を生成しない IImage）では常に 100 を返していた。
        // IImage.Size（DIP）から Fit 実効倍率を計算するよう修正したことを検証する。
        var view = new ZoomViewControl
        {
            Source = EsBitmapExtensions.CreateAvaloniaImage(1000, 500, Colors.White)
        };
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));

        // FitScale = min(400/1000, 200/500) = 0.4 → FitZoomPercent = 40
        Assert.True(view.IsFit);
        Assert.Equal(40.0, view.ZoomPercent, 3);
    }

    [AvaloniaFact]
    internal void ZoomFitでFit表示に戻ると実効ズーム率が0にならない()
    {
        var view = CreateView();
        view.ZoomPercent = 200;

        view.ZoomFit();

        Assert.True(view.IsFit);
        Assert.True(view.ZoomPercent > 0);
    }

    [AvaloniaFact]
    internal void ZoomPercentSetterで固定倍率を設定するとIsFitがfalseになる()
    {
        var view = CreateView();

        view.ZoomPercent = 200;

        Assert.False(view.IsFit);
        Assert.Equal(200, view.ZoomPercent);
    }

    [AvaloniaFact]
    internal void ZoomPercentSetterで0以下は無視されFitのままである()
    {
        var view = CreateView();

        view.ZoomFit();
        view.ZoomPercent = 0;
        view.ZoomPercent = -100;

        Assert.True(view.IsFit);
    }

    [AvaloniaFact]
    internal void ZoomPercentSetterで0以下は固定倍率に移行しない()
    {
        var view = CreateView();
        view.ZoomPercent = 200;

        view.ZoomPercent = 0;

        Assert.False(view.IsFit);
        Assert.Equal(200, view.ZoomPercent);
    }

    [AvaloniaFact]
    internal void 固定倍率中は実効ズーム率がズーム率と一致する()
    {
        var view = CreateView();

        view.ZoomPercent = 300;

        Assert.Equal(300, view.ZoomPercent);
    }

    [AvaloniaFact]
    internal void ZoomFitでZoomChangedがIsFitと実効ズーム率を通知する()
    {
        var view = CreateView();
        view.ZoomPercent = 200;

        ZoomChangedEventArgs? received = null;
        view.ZoomChanged += (_, e) => received = e;

        view.ZoomFit();

        // Fit 移行時に IsFit=true・実効ズーム率は 0 にならない（スライダ同期の根拠）
        Assert.NotNull(received);
        Assert.True(received!.IsFit);
        Assert.True(received.ZoomPercent > 0);
    }

    [AvaloniaFact]
    internal void ZoomPercentSetterでZoomChangedがIsFitと実効ズーム率を通知する()
    {
        var view = CreateView();
        view.ZoomFit();

        ZoomChangedEventArgs? received = null;
        view.ZoomChanged += (_, e) => received = e;

        view.ZoomPercent = 200;

        Assert.NotNull(received);
        Assert.False(received!.IsFit);
        Assert.Equal(200, received.ZoomPercent);
    }

    // ====================================================================
    // CenterAlign（外部公開 API・固定倍率で画像がビューポートより小さい場合）
    // ====================================================================

    [AvaloniaFact]
    internal void CenterAlign_固定倍率でズームアウトし画像が小さい場合は中央寄せされる()
    {
        // 画像 1000x500・ビューポート 400x200・ズームアウト 20% → 表示 200x100（ビューポートより小さい）
        var view = new ZoomViewControl
        {
            Source = CreateBitmap(1000, 500),
            CenterAlign = true
        };
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));

        view.ZoomPercent = 20;

        // 中央寄せ原点（ビューポート座標） = (400 − 200) / 2 = 100、(200 − 100) / 2 = 50
        var rect = view.ImageRectViewport();
        Assert.Equal(100.0, rect.X);
        Assert.Equal(50.0, rect.Y);
        Assert.Equal(200.0, rect.Width);
        Assert.Equal(100.0, rect.Height);
    }

    [AvaloniaFact]
    internal void CenterAlign_左上寄せに戻すと固定倍率で画像が左上に配置される()
    {
        // 画像 1000x500・ビューポート 400x200・ズームアウト 20% → 表示 200x100
        var view = new ZoomViewControl
        {
            Source = CreateBitmap(1000, 500),
            CenterAlign = false
        };
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));

        view.ZoomPercent = 20;

        // 左上寄せなら原点は (0, 0)
        var rect = view.ImageRectViewport();
        Assert.Equal(0.0, rect.X);
        Assert.Equal(0.0, rect.Y);
    }

    [AvaloniaFact]
    internal void CenterAlign_横方向が大きく縦方向が小さい場合は縦のみ中央寄せされる()
    {
        // 画像 2000x100・ビューポート 400x200・固定倍率 100% → 表示 2000x100
        // 横方向 2000 > 400（スクロール可能）、縦方向 100 < 200（スクロール不能）
        var view = new ZoomViewControl
        {
            Source = CreateBitmap(2000, 100),
            CenterAlign = true
        };
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));

        view.ZoomPercent = 100;

        // 横方向はパン位置（オフセット 0 → x=0）が優先、縦方向のみ中央寄せ: y = (200 − 100) / 2 = 50
        var rect = view.ImageRectViewport();
        Assert.Equal(0.0, rect.X);
        Assert.Equal(50.0, rect.Y);
    }

    // ====================================================================
    // ホイール操作の公開 API（オーバーレイからの転送用）
    // ====================================================================

    /// <summary>パン可能な状態（100% 表示。 画像 1000x500・ビューポート 400x200）のコントロールを生成する。</summary>
    /// <returns>100% 表示にしたコントロール。</returns>
    private static ZoomViewControl CreatePannableView()
    {
        var view = CreateView();
        view.ZoomPercent = 100;
        return view;
    }

    [AvaloniaFact]
    internal void PanByWheel_縦の回転量で表示位置を移動する()
    {
        var view = CreatePannableView();
        var before = view.ImageRectViewport();

        // 1 ノッチ = 48 DIP。 実効倍率 1.0 のため表示位置が 48 上へ移動する
        view.PanByWheel(new Vector(0, 1), shift: false);

        var after = view.ImageRectViewport();
        Assert.Equal(before.Y - 48.0, after.Y);
        Assert.Equal(before.X, after.X);
    }

    [AvaloniaFact]
    internal void PanByWheel_ズーム中は実効倍率で換算する()
    {
        var view = CreatePannableView();
        view.ZoomPercent = 200;
        var before = view.ImageRectViewport();

        // 実効倍率 2.0 のため 48 DIP ÷ 2 = 24 コンテンツ座標＝画面では 48 DIP 上へ移動する（画面上の移動量は一定）
        view.PanByWheel(new Vector(0, 1), shift: false);

        var after = view.ImageRectViewport();
        Assert.Equal(before.Y - 48.0, after.Y);
    }

    [AvaloniaFact]
    internal void PanByWheel_Shift併用は横へ移動する()
    {
        var view = CreatePannableView();
        var before = view.ImageRectViewport();

        view.PanByWheel(new Vector(0, 1), shift: true);

        var after = view.ImageRectViewport();
        Assert.Equal(before.X - 48.0, after.X);
        Assert.Equal(before.Y, after.Y);
    }

    [AvaloniaFact]
    internal void PanByWheel_Shiftなしの横回転も横へ移動する()
    {
        var view = CreatePannableView();
        var before = view.ImageRectViewport();

        // 横 2 ノッチ = 96 DIP
        view.PanByWheel(new Vector(2, 0), shift: false);

        var after = view.ImageRectViewport();
        Assert.Equal(before.X - 96.0, after.X);
        Assert.Equal(before.Y, after.Y);
    }

    [AvaloniaFact]
    internal void ZoomByWheel_ズームインはWheelFactor倍へ移動する()
    {
        var view = CreatePannableView();

        view.ZoomByWheel(zoomIn: true);

        // 100% × WheelFactor（1.1）= 110%
        Assert.Equal(110, view.ZoomPercent);
    }

    [AvaloniaFact]
    internal void ZoomByWheel_ズームアウトはWheelFactor分の1へ移動する()
    {
        var view = CreatePannableView();

        view.ZoomByWheel(zoomIn: false);

        // 100% ÷ WheelFactor（1.1）→ 四捨五入して 91%
        Assert.Equal(91, view.ZoomPercent);
    }

    [AvaloniaFact]
    internal void ZoomByWheel_指定した位置をズーム中心にする()
    {
        var view = CreatePannableView();
        var before = view.ImageRectViewport();

        // ビューポート座標 (0, 0) をズーム中心にする（左下を中心に拡大するため原点が左上へ寄る）
        view.ZoomByWheel(zoomIn: true, new Point(0, 0));

        var after = view.ImageRectViewport();
        Assert.Equal(110, view.ZoomPercent);
        // 中心 (0, 0) の対象座標が不変であることを、位置の移動量で検証する
        Assert.Equal(0.0, after.X);
        Assert.Equal(0.0, after.Y - before.Y);
    }

    [AvaloniaFact]
    internal void ZoomByWheel_位置を省略するとビューポート中心をズーム中心にする()
    {
        var view = CreatePannableView();
        var before = view.ImageRectViewport();

        view.ZoomByWheel(zoomIn: true);

        // ビューポート中心 (200, 100) の対象座標が不変に保たれる
        var after = view.ImageRectViewport();
        Assert.Equal(110, view.ZoomPercent);
        Assert.Equal(before.X - 20.0, after.X, 6);
        Assert.Equal(before.Y - 10.0, after.Y, 6);
    }

    [AvaloniaFact]
    internal void ZoomByWheel_移動できない場合はズーム率を維持する()
    {
        var view = CreatePannableView();
        var maxZoom = Core.ZoomView.ZoomOptions.Default.MaxZoom;
        view.ZoomPercent = maxZoom;

        view.ZoomByWheel(zoomIn: true);

        // 上限では移動しない（Fit へも移行しない）
        Assert.Equal(maxZoom, view.ZoomPercent);
        Assert.False(view.IsFit);
    }
}
