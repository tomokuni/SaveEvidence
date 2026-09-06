using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Estable.AvaloniaUI.Platform.Imaging;
using Xunit;

namespace Estable.AvaloniaUI.ZoomView.Tests;

/// <summary>
/// <see cref="ZoomViewControl"/>（自己完結型複合コントロール）の回帰テスト。
/// </summary>
/// <remarks>
/// 実効ズーム率（<see cref="ZoomViewControl.ZoomPercent"/>）・<see cref="ZoomViewControl.ZoomFit"/>・
/// <see cref="ZoomViewControl.IsFit"/>・setter の 0 拒否などの仕様を検証する。<br/>
/// 計算の本体（<see cref="ZoomMath"/> / <see cref="ZoomTransition"/>）は純粋関数として別途テスト済みで、
/// 本テストは ZoomViewControl への適用（実効ズーム率の導出・Fit 移行）を検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class ZoomViewControlTests
{
    /// <summary>テスト用の画像（指定サイズ・白の単色）を生成する。</summary>
    private static PbBitmapAvaloniaImage CreateBitmap(int w, int h)
        => PbBitmapAvaloniaExtensions.CreateAvaloniaImage(w, h, Colors.White);

    /// <summary>テスト用の ZoomViewControl（画像を設定済み）を生成する。</summary>
    private static ZoomViewControl CreateView()
    {
        var view = new ZoomViewControl();
        view.Source = CreateBitmap(1000, 500);
        // Bounds を確定させる（Fit 実効倍率の計算に必要）
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));
        return view;
    }

    // ====================================================================
    // ZoomPercent（実効ズーム率）・IsFit・ZoomFit の仕様
    // ====================================================================

    [AvaloniaFact]
    public void 初期状態ではFit表示で実効ズーム率が0にならない()
    {
        var view = CreateView();

        // Fit（内部ズーム率 = 0）だが、実効ズーム率はビューポートと元画像サイズから算出された実効倍率（0 にならない）
        Assert.True(view.IsFit);
        Assert.True(view.ZoomPercent > 0);
    }

    [AvaloniaFact]
    public void ZoomFitでFit表示に戻ると実効ズーム率が0にならない()
    {
        var view = CreateView();
        view.ZoomPercent = 200;

        view.ZoomFit();

        Assert.True(view.IsFit);
        Assert.True(view.ZoomPercent > 0);
    }

    [AvaloniaFact]
    public void ZoomPercentSetterで固定倍率を設定するとIsFitがfalseになる()
    {
        var view = CreateView();

        view.ZoomPercent = 200;

        Assert.False(view.IsFit);
        Assert.Equal(200, view.ZoomPercent);
    }

    [AvaloniaFact]
    public void ZoomPercentSetterで0以下は無視されFitのままである()
    {
        var view = CreateView();

        view.ZoomFit();
        view.ZoomPercent = 0;
        view.ZoomPercent = -100;

        Assert.True(view.IsFit);
    }

    [AvaloniaFact]
    public void ZoomPercentSetterで0以下は固定倍率に移行しない()
    {
        var view = CreateView();
        view.ZoomPercent = 200;

        view.ZoomPercent = 0;

        Assert.False(view.IsFit);
        Assert.Equal(200, view.ZoomPercent);
    }

    [AvaloniaFact]
    public void 固定倍率中は実効ズーム率がズーム率と一致する()
    {
        var view = CreateView();

        view.ZoomPercent = 300;

        Assert.Equal(300, view.ZoomPercent);
    }

    [AvaloniaFact]
    public void ZoomFitでZoomChangedがIsFitと実効ズーム率を通知する()
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
    public void ZoomPercentSetterでZoomChangedがIsFitと実効ズーム率を通知する()
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
    public void CenterAlign_固定倍率でズームアウトし画像が小さい場合は中央寄せされる()
    {
        // 画像 1000x500・ビューポート 400x200・ズームアウト 20% → 表示 200x100（ビューポートより小さい）
        var view = new ZoomViewControl();
        view.Source = CreateBitmap(1000, 500);
        view.CenterAlign = true;
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
    public void CenterAlign_左上寄せに戻すと固定倍率で画像が左上に配置される()
    {
        // 画像 1000x500・ビューポート 400x200・ズームアウト 20% → 表示 200x100
        var view = new ZoomViewControl();
        view.Source = CreateBitmap(1000, 500);
        view.CenterAlign = false;
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));

        view.ZoomPercent = 20;

        // 左上寄せなら原点は (0, 0)
        var rect = view.ImageRectViewport();
        Assert.Equal(0.0, rect.X);
        Assert.Equal(0.0, rect.Y);
    }

    [AvaloniaFact]
    public void CenterAlign_横方向が大きく縦方向が小さい場合は縦のみ中央寄せされる()
    {
        // 画像 2000x100・ビューポート 400x200・固定倍率 100% → 表示 2000x100
        // 横方向 2000 > 400（スクロール可能）、縦方向 100 < 200（スクロール不能）
        var view = new ZoomViewControl();
        view.Source = CreateBitmap(2000, 100);
        view.CenterAlign = true;
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));

        view.ZoomPercent = 100;

        // 横方向はパン位置（オフセット 0 → x=0）が優先、縦方向のみ中央寄せ: y = (200 − 100) / 2 = 50
        var rect = view.ImageRectViewport();
        Assert.Equal(0.0, rect.X);
        Assert.Equal(50.0, rect.Y);
    }
}
