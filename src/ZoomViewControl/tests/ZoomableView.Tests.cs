using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Xunit;

namespace Estable.AvaloniaUI.ZoomView.Tests;

/// <summary>
/// ZoomableView（ScrollViewer 不使用のパン・ズームコントロール）の回帰テスト。
/// </summary>
/// <remarks>
/// ズーム中心維持・パン・Fit 表示・座標変換を検証する。<br/>
/// 計算の本体（<see cref="ZoomMath"/>）は純粋関数として別途テスト済みで、<br/>
/// 本テストは ZoomableView への適用（オフセット設定・表示サイズ）を検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class ZoomableViewTests
{
    /// <summary>テスト用の画像（1000x500）を生成する。</summary>
    private static WriteableBitmap CreateBitmap(int w, int h)
        => new(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

    /// <summary>テスト用の ZoomableView を生成する（画像 1000x500 を設定・400x200 に配置）。</summary>
    private static ZoomableView CreateView()
    {
        var view = new ZoomableView();
        view.SetSource(CreateBitmap(1000, 500));
        // Bounds を確定させる（クランプ・Fit 計算に必要）
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));
        return view;
    }

    [AvaloniaFact]
    public void SetSource_画像を設定するとFit表示になる()
    {
        var view = CreateView();
        Assert.Equal(0.0, view.ZoomPercent); // 0 = Fit
    }

    [AvaloniaFact]
    public void SetZoom_固定倍率に設定するとスケールが更新される()
    {
        var view = CreateView();
        view.SetZoom(200);
        Assert.Equal(2.0, view.Scale);
    }

    [AvaloniaFact]
    public void ViewportToContent_固定倍率で変換が正しい()
    {
        var view = CreateView();
        view.SetZoom(100);
        view.SetOffset(new Vector(100, 50));

        // ビューポート (200, 150) → コンテンツ (200+100, 150+50) = (300, 200)
        var result = view.ViewportToContent(new Point(200, 150));
        Assert.Equal(new Point(300, 200), result);
    }

    [AvaloniaFact]
    public void ContentToViewport_逆変換が正しい()
    {
        var view = CreateView();
        view.SetZoom(200); // scale = 2
        view.SetOffset(new Vector(100, 50));

        // コンテンツ (300, 200) → ビューポート ((300-100)*2, (200-50)*2) = (400, 300)
        var result = view.ContentToViewport(new Point(300, 200));
        Assert.Equal(new Point(400, 300), result);
    }

    [AvaloniaFact]
    public void ViewportToContentImage_画像原点基準で固定倍率の変換が正しい()
    {
        var view = CreateView();
        view.SetZoom(100); // scale = 1
        view.SetOffset(new Vector(100, 50));

        // 画像原点 = (-offset*scale) = (-100, -50)
        // ビューポート (200, 150) → コンテンツ ((200+100)/1, (150+50)/1) = (300, 200)
        var result = view.ViewportToContentImage(new Point(200, 150));
        Assert.Equal(new Point(300, 200), result);
    }

    [AvaloniaFact]
    public void ContentToViewportImage_画像原点基準で固定倍率の逆変換が正しい()
    {
        var view = CreateView();
        view.SetZoom(100); // scale = 1
        view.SetOffset(new Vector(100, 50));

        // 画像原点 = (-100, -50)、コンテンツ (300, 200) → ビューポート (-100+300, -50+200) = (200, 150)
        var result = view.ContentToViewportImage(new Point(300, 200));
        Assert.Equal(new Point(200, 150), result);
    }

    [AvaloniaFact]
    public void Fit表示中にViewportToContentImageが正しい()
    {
        var view = CreateView();
        // Fit（zoomPercent = 0）・CenterAlign（デフォルト）・表示 400x200 → 画像原点 (0, 0)

        // 画像 1000x500、ビューポート 400x200 → FitScale = 0.4
        // ビューポート (200, 100) → コンテンツ (200/0.4, 100/0.4) = (500, 250)
        var result = view.ViewportToContentImage(new Point(200, 100));
        Assert.Equal(500.0, result.X);
        Assert.Equal(250.0, result.Y);
    }

    [AvaloniaFact]
    public void Fit表示中にContentToViewportImageが正しい()
    {
        var view = CreateView();

        // コンテンツ (500, 250) → ビューポート (500*0.4, 250*0.4) = (200, 100)
        var result = view.ContentToViewportImage(new Point(500, 250));
        Assert.Equal(200.0, result.X);
        Assert.Equal(100.0, result.Y);
    }

    [AvaloniaFact]
    public void 画像基準の往復変換で元のビューポート座標に戻る()
    {
        var view = CreateView();
        view.SetZoom(200);
        view.SetOffset(new Vector(30, 40));

        var viewportPos = new Point(123, 456);
        var content = view.ViewportToContentImage(viewportPos);
        var back = view.ContentToViewportImage(content);

        Assert.Equal(viewportPos.X, back.X, 6);
        Assert.Equal(viewportPos.Y, back.Y, 6);
    }

    [AvaloniaFact]
    public void SetOffset_スクロール範囲を超えないようクランプされる()
    {
        var view = CreateView();
        view.SetZoom(100); // scale = 1

        // 最大オフセット = コンテンツ 1000x500 - ビューポート 400x200 → (600, 300)
        view.SetOffset(new Vector(9999, 9999));
        Assert.Equal(600.0, view.Offset.X);
        Assert.Equal(300.0, view.Offset.Y);
    }

    [AvaloniaFact]
    public void PanBy_負方向は0にクランプされる()
    {
        var view = CreateView();
        view.SetZoom(100);

        view.PanBy(new Vector(-100, -50));
        Assert.Equal(0.0, view.Offset.X);
        Assert.Equal(0.0, view.Offset.Y);
    }

    [AvaloniaFact]
    public void SetZoom_Fitから固定倍率へ移行するとオフセットが0になる()
    {
        var view = CreateView();

        // Fit（scale=0）→ 100%: オフセットはリセットされる
        view.SetZoom(100);
        Assert.Equal(0.0, view.Offset.X);
        Assert.Equal(0.0, view.Offset.Y);
    }

    // ====================================================================
    // Fit 中の座標変換（回帰: 座標表示が更新されない）
    // ====================================================================

    [AvaloniaFact]
    public void Fit表示中にViewportToContentでゼロ除算しない()
    {
        var view = CreateView();
        // Fit（zoomPercent = 0）のまま

        // 画像 1000x500、ビューポート 400x200 → FitScale = min(400/1000, 200/500) = 0.4
        var content = view.ViewportToContent(new Point(200, 100));

        // ゼロ除算（NaN）にならず、正しいコンテンツ座標が返る
        Assert.True(double.IsFinite(content.X));
        Assert.True(double.IsFinite(content.Y));
        Assert.Equal(500.0, content.X); // 200 / 0.4 = 500
        Assert.Equal(250.0, content.Y); // 100 / 0.4 = 250
    }

    [AvaloniaFact]
    public void Fit表示中のEffectiveScaleが正しい()
    {
        var view = CreateView();

        // 画像 1000x500、ビューポート 400x200 → FitScale = min(0.4, 0.4) = 0.4
        Assert.Equal(0.4, view.EffectiveScale);
    }

    [AvaloniaFact]
    public void Fit表示中にContentToViewportが正しい()
    {
        var view = CreateView();

        // コンテンツ (500, 250) → ビューポート (500*0.4, 250*0.4) = (200, 100)
        var result = view.ContentToViewport(new Point(500, 250));
        Assert.Equal(200.0, result.X);
        Assert.Equal(100.0, result.Y);
    }

    [AvaloniaFact]
    public void Fit表示中にPanByしてもオフセットが変化しない()
    {
        var view = CreateView();
        // Fit 中はスクロール不要（オフセット 0 のまま）

        view.PanBy(new Vector(100, 50));
        Assert.Equal(0.0, view.Offset.X);
        Assert.Equal(0.0, view.Offset.Y);
    }

    [AvaloniaFact]
    public void DisplaySize_固定倍率では元サイズにスケールを掛けたサイズになる()
    {
        var view = CreateView();
        view.SetZoom(200);
        var size = view.DisplaySize();
        Assert.Equal(2000.0, size.Width);
        Assert.Equal(1000.0, size.Height);
    }

    [AvaloniaFact]
    public void DisplaySize_Fitではビューポートに収まるサイズになる()
    {
        var view = CreateView();

        // 画像 1000x500、ビューポート 400x200 → FitScale = min(0.4, 0.4) = 0.4
        var size = view.DisplaySize();
        Assert.Equal(400.0, size.Width);
        Assert.Equal(200.0, size.Height);
    }

    // ====================================================================
    // ImageRectViewport（画像の表示矩形。原点補正含む）
    // ====================================================================

    [AvaloniaFact]
    public void ImageRectViewport_固定倍率ではオフセット分シフトした矩形を返す()
    {
        var view = CreateView();
        view.SetZoom(100); // scale = 1
        view.SetOffset(new Vector(100, 50));

        // 画像 1000x500 が 1 倍表示、オフセット (-100, -50) 分シフト
        var rect = view.ImageRectViewport();
        Assert.Equal(-100.0, rect.X);
        Assert.Equal(-50.0, rect.Y);
        Assert.Equal(1000.0, rect.Width);
        Assert.Equal(500.0, rect.Height);
    }

    [AvaloniaFact]
    public void ImageRectViewport_Fit中央寄せで原点が補正される()
    {
        var view = CreateView();
        view.CenterAlign = true;

        // 画像 1000x500 が FitScale 0.4 で 400x200、ビューポート 400x200 と一致 → 原点 (0,0)
        var rect = view.ImageRectViewport();
        Assert.Equal(0.0, rect.X);
        Assert.Equal(0.0, rect.Y);
        Assert.Equal(400.0, rect.Width);
        Assert.Equal(200.0, rect.Height);
    }

    [AvaloniaFact]
    public void ImageRectViewport_Fit左上寄せで原点が0になる()
    {
        var view = CreateView();
        view.CenterAlign = false;

        // 画像 1000x500 が FitScale 0.4 で 400x200 → 左上寄せなら原点 (0,0)
        var rect = view.ImageRectViewport();
        Assert.Equal(0.0, rect.X);
        Assert.Equal(0.0, rect.Y);
    }

    [AvaloniaFact]
    public void ImageRectViewport_画像未設定時はdefaultを返す()
    {
        var view = new ZoomableView();
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));

        Assert.Equal(default, view.ImageRectViewport());
    }

    // ====================================================================
    // CenterAlign（Fit 時の画像自体の配置）
    // ====================================================================

    [AvaloniaFact]
    public void CenterAlign_Fit中は画像が中央配置になる()
    {
        var view = CreateView();
        view.CenterAlign = true;

        // 画像自体の配置も中央に切り替わる（回帰: オーバーレイ座標のみ中央で描画とずれていた）
        Assert.Equal(HorizontalAlignment.Center, view.ImageControl.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, view.ImageControl.VerticalAlignment);
    }

    [AvaloniaFact]
    public void CenterAlign_Fit中は左上配置に戻る()
    {
        var view = CreateView();
        view.CenterAlign = true;

        view.CenterAlign = false;

        Assert.Equal(HorizontalAlignment.Left, view.ImageControl.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Top, view.ImageControl.VerticalAlignment);
    }

    [AvaloniaFact]
    public void CenterAlign_固定倍率中は左上配置のまま()
    {
        var view = CreateView();
        view.SetZoom(200);

        view.CenterAlign = true;

        // 固定倍率はパン位置で制御するため、配置は常に左上
        Assert.Equal(HorizontalAlignment.Left, view.ImageControl.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Top, view.ImageControl.VerticalAlignment);
    }

    [AvaloniaFact]
    public void CenterAlign_変更でViewportChangedが発火する()
    {
        var view = CreateView();
        var count = 0;
        view.ViewportChanged += (_, _) => count++;

        view.CenterAlign = false;

        // オーバーレイ座標・画像配置が変わるため通知する
        Assert.Equal(1, count);
    }

    [AvaloniaFact]
    public void SetZoom_固定倍率への移行で配置が左上に戻る()
    {
        var view = CreateView();
        view.CenterAlign = true; // Fit: 中央配置
        Assert.Equal(HorizontalAlignment.Center, view.ImageControl.HorizontalAlignment);

        // 固定倍率へ移行 → パン制御に切り替わり、配置は左上に戻る（回帰: Center が残る）
        view.SetZoom(200);
        Assert.Equal(HorizontalAlignment.Left, view.ImageControl.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Top, view.ImageControl.VerticalAlignment);
    }

    [AvaloniaFact]
    public void SetZoom_画像が小さい場合の中央寄せの原点補正が正しい()
    {
        // 画像 100x50 がビューポート 400x200 に Fit → 表示 400x200 と一致 → 原点補正なし
        var view = new ZoomableView();
        view.SetSource(CreateBitmap(100, 50));
        view.CenterAlign = true;
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));

        Assert.Equal(HorizontalAlignment.Center, view.ImageControl.HorizontalAlignment);
        var rect = view.ImageRectViewport();
        Assert.Equal(0.0, rect.X);
        Assert.Equal(0.0, rect.Y);
        Assert.Equal(400.0, rect.Width);
        Assert.Equal(200.0, rect.Height);
    }

    [AvaloniaFact]
    public void SetZoom_指定した中心のコンテンツ座標を維持する()
    {
        var view = CreateView();
        view.SetZoom(100); // scale = 1, offset = (0, 0)

        // ズーム中心 = ビューポート中心 (200, 100)、ズーム 100%→200%
        // 中心のコンテンツ座標 = (0 + 200/1, 0 + 100/1) = (200, 100)
        // offset' = (200 - 200/2, 100 - 100/2) = (100, 50)
        view.SetZoom(200, new Point(200, 100));

        Assert.Equal(new Vector(100, 50), view.Offset);
    }

    [AvaloniaFact]
    public void SetZoom_ズーム後も指定中心のコンテンツ座標が不変である()
    {
        var view = CreateView();
        view.SetZoom(100);
        view.SetOffset(new Vector(100, 50)); // スクロール状態

        // ズーム中心 (200, 100)、100%→200%
        // 中心のコンテンツ座標 = (100 + 200, 50 + 100) = (300, 150)
        // offset' = (300 - 100, 150 - 50) = (200, 100)
        view.SetZoom(200, new Point(200, 100));

        // ズーム後の中心のコンテンツ座標 = offset' + center/scale' = (200 + 100, 100 + 50) = (300, 150) → 不変
        var contentCenter = view.ViewportToContent(new Point(200, 100));
        Assert.Equal(new Point(300, 150), contentCenter);
        Assert.Equal(new Vector(200, 100), view.Offset);
    }

    [AvaloniaFact]
    public void SetZoom_ズームアウトでオフセットが戻る()
    {
        var view = CreateView();
        view.SetZoom(200, new Point(200, 100)); // offset = (100, 50)

        // ズームアウト 200%→100%、中心 (200, 100)
        // 中心のコンテンツ座標 = (100 + 200/2, 50 + 100/2) = (200, 100)
        // offset' = (200 - 200, 100 - 100) = (0, 0)
        view.SetZoom(100, new Point(200, 100));
        Assert.Equal(new Vector(0, 0), view.Offset);
    }

    // ====================================================================
    // スクロールバー（ZoomableView のスクロールバー状態）
    // ====================================================================

    [AvaloniaFact]
    public void Fit表示ではスクロールバーが非表示になる()
    {
        var view = CreateView();
        view.SetZoom(0); // Fit

        // Fit では画像がビューポートに収まるためスクロールバーは非表示
        Assert.False(view.VerticalScrollBar.IsVisible);
        Assert.False(view.HorizontalScrollBar.IsVisible);
    }

    [AvaloniaFact]
    public void 拡大時はスクロールバーが表示され範囲が設定される()
    {
        var view = CreateView();
        view.SetZoom(200); // 画像 1000x500 が 2000x1000 に拡大。ビューポート 400x200

        // 縦: 最大 = 500 - 200/2 = 400
        Assert.True(view.VerticalScrollBar.IsVisible);
        Assert.Equal(400.0, view.VerticalScrollBar.Maximum);

        // 横: 最大 = 1000 - 400/2 = 800
        Assert.True(view.HorizontalScrollBar.IsVisible);
        Assert.Equal(800.0, view.HorizontalScrollBar.Maximum);
    }

    [AvaloniaFact]
    public void パンするとスクロールバーの値が同期される()
    {
        var view = CreateView();
        view.SetZoom(200);
        view.SetOffset(new Vector(100, 50));

        Assert.Equal(100.0, view.HorizontalScrollBar.Value);
        Assert.Equal(50.0, view.VerticalScrollBar.Value);
    }

    [AvaloniaFact]
    public void スクロールバーの操作でパンされる()
    {
        var view = CreateView();
        view.SetZoom(200);

        // 縦スクロールバーの値を変更 → オフセット Y が更新される
        view.VerticalScrollBar.Value = 200;
        Assert.Equal(200.0, view.Offset.Y);

        // 横スクロールバーの値を変更 → オフセット X が更新される
        view.HorizontalScrollBar.Value = 300;
        Assert.Equal(300.0, view.Offset.X);
    }

    [AvaloniaFact]
    public void スクロールバー操作はクランプされる()
    {
        var view = CreateView();
        view.SetZoom(200);

        // 最大値を超える値を設定 → クランプされる
        view.VerticalScrollBar.Value = 9999;
        Assert.Equal(400.0, view.Offset.Y); // 最大 400
    }

    // ====================================================================
    // マウスドラッグパン（grab 方式: 掴んだ位置がポインタに追従）
    // ====================================================================

    /// <summary>ドラッグパンテスト用に、ビューポート 400x200 のヘッドレスウィンドウに配置した view を生成する。</summary>
    /// <returns>ウィンドウと ZoomableView の組。</returns>
    private static (Window Window, ZoomableView View) CreateDragWindow()
    {
        var view = new ZoomableView();
        view.SetSource(CreateBitmap(1000, 500));
        var window = new Window { Content = view, Width = 400, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    [AvaloniaFact]
    public void ドラッグパン_掴んだ位置がポインタに追従する()
    {
        var (window, view) = CreateDragWindow();
        try
        {
            view.SetZoom(100); // scale = 1
            view.SetOffset(new Vector(300, 150));

            // 押下 (100, 50): 掴んだコンテンツ = (300+100, 150+50) = (400, 200)
            window.MouseDown(new Point(100, 50), MouseButton.Left);

            // 移動 (200, 100): オフセット = (400-200, 200-100) = (200, 100)
            window.MouseMove(new Point(200, 100));

            Assert.Equal(200.0, view.Offset.X);
            Assert.Equal(100.0, view.Offset.Y);

            window.MouseUp(new Point(200, 100), MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ドラッグパン_ドラッグ方向と同じ方向へコンテンツが移動する()
    {
        var (window, view) = CreateDragWindow();
        try
        {
            view.SetZoom(100);
            view.SetOffset(new Vector(300, 150));

            window.MouseDown(new Point(100, 50), MouseButton.Left);

            // 右下へドラッグ（+100, +50）→ 掴んだコンテンツがポインタに追従し、コンテンツ自体も右下へ移動
            // オフセットは減少する = 描画位置（Scale×(content−offset)）は増加 = コンテンツが右下へ移動
            window.MouseMove(new Point(200, 100));
            Assert.Equal(200.0, view.Offset.X); // 300 → 200
            Assert.Equal(100.0, view.Offset.Y); // 150 → 100

            // 左上へドラッグ（-100, -50）→ コンテンツは左上へ移動（オフセット増加）
            window.MouseMove(new Point(0, 0));
            Assert.Equal(400.0, view.Offset.X); // 200 → 400
            Assert.Equal(200.0, view.Offset.Y); // 100 → 200

            window.MouseUp(new Point(0, 0), MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ドラッグパン_スクロール範囲端でクランプされる()
    {
        var (window, view) = CreateDragWindow();
        try
        {
            view.SetZoom(100); // 最大オフセット (600, 300)
            view.SetOffset(new Vector(300, 150));

            window.MouseDown(new Point(100, 50), MouseButton.Left);

            // 左上へ大きくドラッグ → オフセットは増加し上限（600, 300）でクランプされる
            window.MouseMove(new Point(-500, -500));
            Assert.Equal(600.0, view.Offset.X);
            Assert.Equal(300.0, view.Offset.Y);

            // 右下へ大きくドラッグ → オフセットは減少し下限（0, 0）でクランプされる
            window.MouseMove(new Point(1000, 500));
            Assert.Equal(0.0, view.Offset.X);
            Assert.Equal(0.0, view.Offset.Y);

            window.MouseUp(new Point(1000, 500), MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ドラッグパン_解放でパンが終了する()
    {
        var (window, view) = CreateDragWindow();
        try
        {
            view.SetZoom(100);
            view.SetOffset(new Vector(300, 150));

            window.MouseDown(new Point(100, 50), MouseButton.Left);
            window.MouseMove(new Point(200, 100));
            window.MouseUp(new Point(200, 100), MouseButton.Left);

            // 解放後の移動はパンに反映されない
            var before = view.Offset;
            window.MouseMove(new Point(300, 150));
            Assert.Equal(before, view.Offset);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ドラッグパン_Fit表示中はパンされない()
    {
        var (window, view) = CreateDragWindow();
        try
        {
            // Fit（zoomPercent = 0）のまま
            window.MouseDown(new Point(100, 50), MouseButton.Left);
            window.MouseMove(new Point(200, 100));

            // Fit 中はオフセット 0 のまま（クランプで 0）
            Assert.Equal(0.0, view.Offset.X);
            Assert.Equal(0.0, view.Offset.Y);

            window.MouseUp(new Point(200, 100), MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    // ====================================================================
    // イベント（ZoomRequested）
    // ====================================================================

    [AvaloniaFact]
    public void ZoomRequested_イベントが発火する()
    {
        var view = CreateView();
        ZoomRequestedEventArgs? received = null;
        view.ZoomRequested += (_, e) => received = e;

        // Ctrl+ホイールを発火させる（コンストラクタで KeyModifiers / Delta を指定）
        var pointer = new Pointer(1, PointerType.Mouse, true);
        var props = new PointerPointProperties();
        var e = new PointerWheelEventArgs(
            view, pointer, view, new Point(0, 0), 0, props, KeyModifiers.Control, new Vector(0, 1))
        {
            RoutedEvent = InputElement.PointerWheelChangedEvent,
        };
        view.RaiseEvent(e);

        Assert.NotNull(received);
        Assert.True(received!.ZoomIn);
    }

    // ====================================================================
    // イベント（ViewportChanged: Crop オーバーレイの追従通知）
    // ====================================================================

    [AvaloniaFact]
    public void ViewportChanged_SetZoomで発火する()
    {
        var view = CreateView();
        var count = 0;
        view.ViewportChanged += (_, _) => count++;

        view.SetZoom(200);

        Assert.Equal(1, count);
    }

    [AvaloniaFact]
    public void ViewportChanged_PanByで発火する()
    {
        var view = CreateView();
        view.SetZoom(200);
        var count = 0;
        view.ViewportChanged += (_, _) => count++;

        view.PanBy(new Vector(100, 50));

        Assert.Equal(1, count);
    }

    [AvaloniaFact]
    public void ViewportChanged_SetOffsetで発火する()
    {
        var view = CreateView();
        view.SetZoom(200);
        var count = 0;
        view.ViewportChanged += (_, _) => count++;

        view.SetOffset(new Vector(100, 50));

        Assert.Equal(1, count);
    }

    [AvaloniaFact]
    public void ViewportChanged_スクロールバー操作で発火する()
    {
        var view = CreateView();
        view.SetZoom(200);
        var count = 0;
        view.ViewportChanged += (_, _) => count++;

        view.VerticalScrollBar.Value = 100;

        Assert.Equal(1, count);
    }

    [AvaloniaFact]
    public void ViewportChanged_同じ値のSetOffsetでは発火しない()
    {
        var view = CreateView();
        view.SetZoom(200);
        view.SetOffset(new Vector(100, 50));
        var count = 0;
        view.ViewportChanged += (_, _) => count++;

        // クランプ結果が現在値と等しい場合は更新・通知をスキップする（ホットパス最適化）
        view.SetOffset(new Vector(100, 50));
        Assert.Equal(0, count);

        // 値が変われば発火する
        view.SetOffset(new Vector(200, 50));
        Assert.Equal(1, count);
    }

    [AvaloniaFact]
    public void ViewportChanged_クランプ端でのPanByでは発火しない()
    {
        var view = CreateView();
        view.SetZoom(100); // 最大オフセット (600, 300)
        view.SetOffset(new Vector(600, 300)); // スクロール範囲端
        var count = 0;
        view.ViewportChanged += (_, _) => count++;

        // 端でさらに右へパン → クランプ結果が変化しないためスキップ
        view.PanBy(new Vector(100, 0));
        Assert.Equal(0, count);
    }

    // ====================================================================
    // SetSource（画像の変更・解除）
    // ====================================================================

    [AvaloniaFact]
    public void SetSource_nullでスクロールバーが非表示になる()
    {
        var view = CreateView();
        view.SetZoom(200); // スクロールバー表示状態にする

        Assert.True(view.VerticalScrollBar.IsVisible);

        // 画像を解除 → スクロールバーが非表示に戻る（回帰: SetSource(null) で残っていた）
        view.SetSource(null);

        Assert.False(view.VerticalScrollBar.IsVisible);
        Assert.False(view.HorizontalScrollBar.IsVisible);
        Assert.Equal(0.0, view.ZoomPercent); // Fit にリセット
    }

    [AvaloniaFact]
    public void SetSource_nullでViewportChangedが発火する()
    {
        var view = CreateView();
        view.SetZoom(200);
        var count = 0;
        view.ViewportChanged += (_, _) => count++;

        view.SetSource(null);

        Assert.Equal(1, count);
    }

    [AvaloniaFact]
    public void SetSource_nullでEffectiveScaleが1になる()
    {
        var view = CreateView();

        view.SetSource(null);

        // 画像なし時は実効スケールが 1（ゼロ除算防止のフォールバック）
        Assert.Equal(1.0, view.EffectiveScale);
    }

    // ====================================================================
    // リサイズ（Fit 表示の再適用）
    // ====================================================================

    [AvaloniaFact]
    public void リサイズでFit表示の最大サイズが更新される()
    {
        var view = new ZoomableView();
        view.SetSource(CreateBitmap(1000, 500));
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));
        Assert.Equal(400.0, view.ImageControl.MaxWidth); // Fit: ビューポート幅に制約

        // ビューポートを拡大 → Fit の最大幅も新しいサイズに更新される（回帰: リサイズで古い値のまま）
        view.Measure(new Size(800, 400));
        view.Arrange(new Rect(0, 0, 800, 400));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(800.0, view.ImageControl.MaxWidth);
        Assert.Equal(400.0, view.ImageControl.MaxHeight);
    }

    [AvaloniaFact]
    public void リサイズでFit表示の実効スケールが再計算される()
    {
        var view = new ZoomableView();
        view.SetSource(CreateBitmap(1000, 500));
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));
        Assert.Equal(0.4, view.EffectiveScale);

        // ビューポート拡大 → FitScale = min(800/1000, 400/500) = 0.8
        view.Measure(new Size(800, 400));
        view.Arrange(new Rect(0, 0, 800, 400));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0.8, view.EffectiveScale);
    }
}
