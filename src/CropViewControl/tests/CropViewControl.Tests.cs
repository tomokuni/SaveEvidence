using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Estable.AvaloniaUI.FadeMaskDragRect;
using Xunit;

namespace Estable.AvaloniaUI.CropView.Tests;

/// <summary><see cref="CropViewControl"/> の単体テスト（公開 API・座標変換・矩形オーバーレイ）。</summary>
/// <remarks>
/// 対象: <c>CropViewControl.cs</c>（画像表示 + 領域選択 + 外部提供矩形の統合コントロール）。<br/>
/// 公開 API: Source / Rects / SelectionMode / ZoomPercent / ZoomIn / ZoomOut / Fit /
/// SetSelection / ClearSelection / SetCoordinateTransform / 座標変換（ContentToViewport / ViewportToContent）。<br/>
/// パブリック API 経由で内包コントロール（ZoomViewControl / FadeMaskDragRect）との連携を検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class CropViewTests
{
    /// <summary>テスト用の単色 Bitmap（10x10・青）を生成する。</summary>
    private static Bitmap CreateBitmap()
    {
        var bitmap = new WriteableBitmap(new PixelSize(10, 10), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var locked = bitmap.Lock();
        var stride = locked.RowBytes;
        unsafe
        {
            var ptr = (byte*)locked.Address;
            for (var i = 0; i < stride * 10; i += 4)
            {
                ptr[i] = 255;     // B
                ptr[i + 1] = 0;   // G
                ptr[i + 2] = 0;   // R
                ptr[i + 3] = 255; // A
            }
        }
        return bitmap;
    }

    /// <summary>テスト用の CropViewControl（画像を設定済み）を生成する。</summary>
    private static CropViewControl CreateView()
    {
        var view = new CropViewControl { Source = CreateBitmap() };
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));
        return view;
    }

    [AvaloniaFact]
    public void Source_設定した画像が取得できる()
    {
        using var bitmap = CreateBitmap();
        var view = new CropViewControl { Source = bitmap };

        Assert.Same(bitmap, view.Source);
    }

    [AvaloniaFact]
    public void SelectionMode_既定値はCropである()
    {
        var view = new CropViewControl();

        Assert.Equal(FadeMaskDragRectMode.Crop, view.SelectionMode);

        // AreaSelect へ変更できる
        view.SelectionMode = FadeMaskDragRectMode.AreaSelect;
        Assert.Equal(FadeMaskDragRectMode.AreaSelect, view.SelectionMode);
    }

    [AvaloniaFact]
    public void Rects_設定した矩形の一覧が取得できる()
    {
        var view = new CropViewControl();
        var rects = new[]
        {
            new OverlayRect(new Rect(10, 10, 50, 40), OverlayLineStyle.Dashed, Colors.Red, Colors.White, 2.0),
            new OverlayRect(new Rect(100, 50, 200, 150), OverlayLineStyle.Solid, Colors.Blue, Colors.Transparent, 1.0),
        };

        view.Rects = rects;

        Assert.Equal(rects, view.Rects);
    }

    [AvaloniaFact]
    public void ZoomPercent_設定した値が取得できる()
    {
        var view = CreateView();

        view.ZoomPercent = 200;

        Assert.Equal(200, view.ZoomPercent);
    }

    [AvaloniaFact]
    public void ContentToViewport_ドメイン座標をビューポート座標へ変換する()
    {
        var view = CreateView();

        // 画像 10x10・ビューポート 400x200 → FitScale = min(40, 20) = 20、中央寄せ
        // 画像の表示矩形は 200x200、原点は (100, 0)（横中央）。
        // ドメイン (5, 5) → ビューポート (100 + 5*20, 0 + 5*20) = (200, 100)
        var result = view.ContentToViewport(new Point(5, 5));

        Assert.Equal(200.0, result.X);
        Assert.Equal(100.0, result.Y);
    }

    [AvaloniaFact]
    public void ViewportToContent_ビューポート座標をドメイン座標へ変換する()
    {
        var view = CreateView();

        // ビューポート (200, 100) → ドメイン (5, 5)
        var result = view.ViewportToContent(new Point(200, 100));

        Assert.Equal(5.0, result.X, 3);
        Assert.Equal(5.0, result.Y, 3);
    }
}
