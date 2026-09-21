using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Rect = Avalonia.Rect;
using Size = Avalonia.Size;
using Estable.AvaloniaUI.Platform.Imaging;
using Estable.CropView;
using Estable.FadeMaskDragRect;
using Estable.Platform.Geometry;
using Xunit;
using DrawingColor = System.Drawing.Color;

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
    /// <summary>テスト用の CropViewControl（画像を設定済み）を生成する。</summary>
    private static CropViewControl CreateView()
    {
        // Source は IImage のため、WriteableBitmap を生成しない EsBitmapAvaloniaImage（ゼロコピー）で高速に生成する。
        var view = new CropViewControl { Source = EsBitmapAvaloniaExtensions.CreateAvaloniaImage(10, 10, Colors.Blue) };
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));
        return view;
    }

    [AvaloniaFact]
    public void Source_設定した画像が取得できる()
    {
        using var bitmap = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(10, 10, Colors.Blue);
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
            new OverlayRect(new EsRect(10, 10, 50, 40), OverlayLineStyle.Dashed, DrawingColor.Red, DrawingColor.White, 2.0),
            new OverlayRect(new EsRect(100, 50, 200, 150), OverlayLineStyle.Solid, DrawingColor.Blue, DrawingColor.Transparent, 1.0),
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

    // ====================================================================
    // Source（画像なし時の領域選択の無効化）
    // ====================================================================

    [AvaloniaFact]
    public void Source_未設定の場合は領域選択オーバーレイが不可視でヒットテスト不可になる()
    {
        var view = new CropViewControl();

        // 画像なし（初期状態 Source = null）のとき、領域選択（DragRect）はヒットテスト不可かつ不可視
        // （Crop モードの全面暗転が市松模様を暗くしないよう、オーバーレイごと非表示にする）。
        Assert.Null(view.Source);
        Assert.False(view.SelectionOverlay.IsHitTestVisible);
        Assert.False(view.SelectionOverlay.IsVisible);
    }

    [AvaloniaFact]
    public void Source_画像を設定しても領域選択オーバーレイは自動では可視にならない()
    {
        var view = CreateView();

        // 画像あり（Source 非 null）でも、オーバーレイは Crop セッションが制御するため自動では可視化しない
        // （キャプチャ完了直後に Crop モードの暗転が残るのを防ぐ）。
        Assert.NotNull(view.Source);
        Assert.False(view.SelectionOverlay.IsHitTestVisible);
        Assert.False(view.SelectionOverlay.IsVisible);
    }

    [AvaloniaFact]
    public void SetSelectionOverlayVisible_trueでオーバーレイが可視でヒットテスト可能になる()
    {
        var view = CreateView();

        // Crop セッション開始時: オーバーレイを可視化し、ヒットテストを有効化する
        view.SetSelectionOverlayVisible(true);

        Assert.True(view.SelectionOverlay.IsVisible);
        Assert.True(view.SelectionOverlay.IsHitTestVisible);
    }

    [AvaloniaFact]
    public void SetSelectionOverlayVisible_falseで不可視になり選択がクリアされる()
    {
        var view = CreateView();
        view.SetSelectionOverlayVisible(true);
        view.SetSelection(new Rect(10, 10, 50, 40));
        Assert.True(view.HasSelection);

        // Crop セッション終了時: オーバーレイを非表示にし、選択をクリアする
        view.SetSelectionOverlayVisible(false);

        Assert.False(view.SelectionOverlay.IsVisible);
        Assert.False(view.SelectionOverlay.IsHitTestVisible);
        Assert.False(view.HasSelection);
    }

    [AvaloniaFact]
    public void Source_画像をnullへ戻すと選択がクリアされるがオーバーレイの表示状態は維持される()
    {
        var view = CreateView();
        view.SetSelectionOverlayVisible(true);
        view.SetSelection(new Rect(10, 10, 50, 40));

        // 画像あり → 選択状態
        Assert.True(view.HasSelection);

        // 画像を null へ戻す（画像なしに戻る）
        view.Source = null;

        // 選択はクリアされる。オーバーレイの表示・ヒットテスト状態は SetSelectionOverlayVisible が専任のため維持される。
        Assert.False(view.HasSelection);
        Assert.True(view.SelectionOverlay.IsVisible);
        Assert.True(view.SelectionOverlay.IsHitTestVisible);
    }

    [AvaloniaFact]
    public void Source_画像をnullへ戻した後にSetSelectionOverlayVisible_falseで不可視になる()
    {
        var view = CreateView();

        // 画像を null へ戻す
        view.Source = null;

        // Crop セッション終了相当: オーバーレイを非表示にし、選択をクリアする
        view.SetSelectionOverlayVisible(false);

        Assert.False(view.SelectionOverlay.IsVisible);
        Assert.False(view.SelectionOverlay.IsHitTestVisible);
    }
}
