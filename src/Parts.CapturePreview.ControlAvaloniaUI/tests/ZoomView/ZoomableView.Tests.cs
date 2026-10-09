using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.ZoomView;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Tests.ZoomView;

/// <summary><see cref="ZoomableView"/>（内部ビューポート）のテスト。</summary>
/// <remarks>
/// 判断・値・計算はコア（<see cref="EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView.ZoomViewSession"/>。 Core.Tests が全網羅する）が単一所有するため、<br/>
/// 本テストは <b>Avalonia 固有の UI 層の責務</b>のみを検証する。<br/>
/// - 画像のレイアウト（Fit = Uniform / 固定倍率 = RenderTransform）<br/>
/// - 標準スクロールバーへの同期（範囲・表示範囲・表示可否）<br/>
/// - 入力イベントのコア型への変換と効果の適用（パン・ズーム）
/// </remarks>
[Collection("AvaloniaHeadless")]
public class ZoomableViewTests
{
    /// <summary>テスト用の画像（指定サイズ・白の単色）を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した画像。</returns>
    private static EsBitmapAvaloniaAdapter CreateBitmap(int width, int height)
        => EsBitmapAvaloniaExtensions.CreateAvaloniaImage(width, height, Colors.White);

    /// <summary>ビューポートを確定させたビューポートを生成する。</summary>
    /// <param name="image">表示する画像。 null の場合は設定しない。</param>
    /// <param name="viewportWidth">ビューポートの幅（DIP）。</param>
    /// <param name="viewportHeight">ビューポートの高さ（DIP）。</param>
    /// <returns>生成したビューポート。</returns>
    private static ZoomableView CreateView(IImage? image = null, double viewportWidth = 200, double viewportHeight = 200)
    {
        var view = new ZoomableView();
        if (image is not null)
        {
            view.Source = image;
        }

        view.Measure(new Size(viewportWidth, viewportHeight));
        view.Arrange(new Rect(0, 0, viewportWidth, viewportHeight));
        return view;
    }

    /// <summary>コントロールをウィンドウに表示する。</summary>
    /// <param name="control">表示するコントロール。</param>
    /// <param name="size">ウィンドウ（＝ビューポート）の一辺（DIP）。 画像より小さくするとパン可能になる。</param>
    /// <returns>表示したウィンドウ。</returns>
    private static Window Show(Control control, double size = 200)
    {
        var window = new Window { Content = control, Width = size, Height = size };
        window.Show();
        return window;
    }

    /// <summary>テスト用のポインターを生成する。</summary>
    /// <returns>ポインター。</returns>
    private static Pointer CreatePointer() => new(1, PointerType.Mouse, true);

    /// <summary>左ボタン押下状態の PointerPointProperties を生成する。</summary>
    /// <returns>押下状態のプロパティ。</returns>
    private static PointerPointProperties LeftPressed()
        => new(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);

    /// <summary>コントロールローカル座標をスクリーン座標へ変換する。</summary>
    /// <param name="source">変換元のビジュアル。</param>
    /// <param name="local">コントロールローカル座標。</param>
    /// <returns>スクリーン座標。</returns>
    private static Point ToScreenPoint(Visual source, Point local)
    {
        var screen = source.PointToScreen(local);
        return new Point(screen.X, screen.Y);
    }

    // ====================================================================
    // 画像のレイアウト（Fit / 固定倍率）
    // ====================================================================

    /// <summary>Fit 表示では画像が Uniform・RenderTransform なしで配置されることを検証する。</summary>
    [AvaloniaFact]
    internal void Fit表示では画像をUniformで配置する()
    {
        var view = CreateView(CreateBitmap(400, 200));

        Assert.True(view.Session.IsFit);
        Assert.Equal(Stretch.Uniform, view.ImageControl.Stretch);
        Assert.Null(view.ImageControl.RenderTransform);
    }

    /// <summary>固定倍率では元サイズのまま RenderTransform が設定されることを検証する。</summary>
    [AvaloniaFact]
    internal void 固定倍率ではRenderTransformを設定する()
    {
        var view = CreateView(CreateBitmap(400, 200));
        view.Session.SetZoom(100);

        Assert.Equal(Stretch.None, view.ImageControl.Stretch);
        Assert.Equal(400.0, view.ImageControl.Width, 3);
        Assert.NotNull(view.ImageControl.RenderTransform);
    }

    /// <summary>固定倍率の Transform がセッションのオフセットとスケールを反映することを検証する。</summary>
    [AvaloniaFact]
    internal void 固定倍率のTransformがオフセットとスケールを反映する()
    {
        var view = CreateView(CreateBitmap(400, 400));
        view.Session.SetZoom(200);
        view.Session.SetOffset(new EsVector(30, 40));

        var translate = Assert.IsType<TranslateTransform>(
            Assert.IsType<TransformGroup>(view.ImageControl.RenderTransform).Children[0]);
        var scale = Assert.IsType<ScaleTransform>(
            Assert.IsType<TransformGroup>(view.ImageControl.RenderTransform).Children[1]);

        Assert.Equal(-30.0, translate.X, 3);
        Assert.Equal(-40.0, translate.Y, 3);
        Assert.Equal(2.0, scale.ScaleX, 3);
    }

    // ====================================================================
    // スクロールバーの同期
    // ====================================================================

    /// <summary>Fit 表示ではスクロールバーが非表示であることを検証する。</summary>
    [AvaloniaFact]
    internal void Fit表示ではスクロールバーを非表示にする()
    {
        var view = CreateView(CreateBitmap(400, 400));

        Assert.False(view.VerticalScrollBar.IsVisible);
        Assert.False(view.HorizontalScrollBar.IsVisible);
    }

    /// <summary>拡大時にスクロールバーが表示され、コアの同期値が反映されることを検証する。</summary>
    [AvaloniaFact]
    internal void 拡大時はスクロールバーを表示して同期する()
    {
        var view = CreateView(CreateBitmap(400, 400));
        view.Session.SetZoom(200);

        Assert.True(view.VerticalScrollBar.IsVisible);
        Assert.True(view.HorizontalScrollBar.IsVisible);
        // 最大オフセット = 400 − 200 / 2 = 300、表示範囲 = 200 / 2 = 100
        Assert.Equal(300.0, view.VerticalScrollBar.Maximum, 3);
        Assert.Equal(100.0, view.VerticalScrollBar.ViewportSize, 3);
    }

    /// <summary>パンでスクロールバーの値が同期されることを検証する。</summary>
    [AvaloniaFact]
    internal void パンでスクロールバーの値が同期される()
    {
        var view = CreateView(CreateBitmap(400, 400));
        view.Session.SetZoom(100);

        view.Session.PanByWheel(new EsVector(0, 1), shift: false);

        Assert.Equal(48.0, view.VerticalScrollBar.Value, 3);
    }

    // ====================================================================
    // 入力ブリッジ（イベント → コア型 → セッション → 効果の適用）
    // ====================================================================

    /// <summary>ポインター押下と移動でパンすることを検証する。</summary>
    /// <remarks>入力イベントを実際に発火し、UI 層の変換とセッションへの伝達を検証する。</remarks>
    [AvaloniaFact]
    internal void ポインター押下と移動でパンする()
    {
        var view = CreateView(CreateBitmap(400, 400));
        var window = Show(view);
        view.Session.SetZoom(100);
        var pointer = CreatePointer();

        view.RaiseEvent(new PointerPressedEventArgs(
            view, pointer, window, ToScreenPoint(view, new Point(50, 50)), 0UL,
            LeftPressed(), KeyModifiers.None, 1));
        Assert.True(view.Session.IsPanning);
        view.RaiseEvent(new PointerEventArgs(
            InputElement.PointerMovedEvent, view, pointer, window,
            ToScreenPoint(view, new Point(30, 40)), 0UL,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));

        // オフセット = 掴んだコンテンツ座標（50） − ポインター（30）= 20
        Assert.Equal(20.0, view.Session.Offset.X, 3);
        Assert.Equal(10.0, view.Session.Offset.Y, 3);
    }

    /// <summary>Ctrl+ホイールでズームすることを検証する。</summary>
    [AvaloniaFact]
    internal void Ctrlホイールでズームする()
    {
        var view = CreateView(CreateBitmap(1000, 1000));
        var window = Show(view);
        view.Session.SetZoom(100);
        var pointer = CreatePointer();

        view.RaiseEvent(new PointerWheelEventArgs(
            view, pointer, window, ToScreenPoint(view, new Point(100, 100)), 0UL,
            new PointerPointProperties(RawInputModifiers.Control, PointerUpdateKind.Other),
            KeyModifiers.Control, new Vector(0, 1)));

        // 100 × 1.1 = 110（WheelFactor 倍）
        Assert.Equal(110.0, view.Session.ZoomPercent, 3);
    }

    /// <summary>通常ホイールでパンすることを検証する。</summary>
    [AvaloniaFact]
    internal void 通常ホイールでパンする()
    {
        var view = CreateView(CreateBitmap(400, 400));
        var window = Show(view);
        view.Session.SetZoom(100);
        var pointer = CreatePointer();

        view.RaiseEvent(new PointerWheelEventArgs(
            view, pointer, window, ToScreenPoint(view, new Point(100, 100)), 0UL,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
            KeyModifiers.None, new Vector(0, 1)));

        // 1 ノッチ = 48 DIP（実効倍率 1.0 のためそのまま 48 コンテンツ座標）
        Assert.Equal(48.0, view.Session.Offset.Y, 3);
    }
}
