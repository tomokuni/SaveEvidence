using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using EsUtil.Feature.Marquee.AvaloniaUI;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;
using Point = Avalonia.Point;
using Rect = Avalonia.Rect;
using Size = Avalonia.Size;
using EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.FadeMaskDragRect;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.AvaloniaUI.Tests.Crop;

/// <summary><see cref="CropViewControl"/> の単体テスト（公開 API・座標変換・矩形オーバーレイ）。</summary>
/// <remarks>
/// 対象: <c>CropViewControl.cs</c>（画像表示 + 領域選択 + 外部提供矩形の統合コントロール）。<br/>
/// 公開 API: Source / Rects / SelectionMode / ZoomPercent / ZoomIn / ZoomOut / Fit /
/// SetSelection / ClearSelection / SetCoordTransform / 座標変換（SourceToClient / ClientToSource）。<br/>
/// パブリック API 経由で内包コントロール（ZoomViewControl / FadeMaskDragRect）との連携を検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class CropViewControlTests
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
    internal void Source_設定した画像が取得できる()
    {
        using var bitmap = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(10, 10, Colors.Blue);
        var view = new CropViewControl { Source = bitmap };

        Assert.Same(bitmap, view.Source);
    }

    [AvaloniaFact]
    internal void SelectionMode_既定値はCropである()
    {
        var view = new CropViewControl();

        Assert.Equal(FadeMaskDragRectMode.Crop, view.SelectionMode);

        // AreaSelect へ変更できる
        view.SelectionMode = FadeMaskDragRectMode.AreaSelect;
        Assert.Equal(FadeMaskDragRectMode.AreaSelect, view.SelectionMode);
    }

    [AvaloniaFact]
    internal void Rects_設定した矩形の一覧が取得できる()
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
    internal void Rects_破線は背景色の下地とダッシュ配列を持ち太さの下限を適用する()
    {
        var view = CreateView();

        // 太さ 0（コアの下限 1 へ補正される）の破線。 画像 10x10 を 400x200 へ Fit した倍率は 20 倍
        view.Rects = [new OverlayRect(new EsRect(10, 10, 50, 40), OverlayLineStyle.Dashed, DrawingColor.Red, DrawingColor.White, 0.0)];

        var group = Assert.IsType<Panel>(Assert.Single(view.RectOverlayChildren));
        var background = Assert.IsType<Rectangle>(group.Children[0]);
        var foreground = Assert.IsType<Rectangle>(group.Children[1]);

        Assert.NotNull(background.Fill);
        Assert.NotNull(foreground.StrokeDashArray);
        Assert.Equal(1.0, foreground.StrokeThickness);
        Assert.Equal(1000.0, foreground.Width);
        Assert.Equal(800.0, foreground.Height);
        // 左上は画像表示矩形の原点（200x200 を 400x200 へ中央寄せして X のみ 100）+ 対象 10 × 20 倍
        Assert.Equal(300.0, Canvas.GetLeft(group));
        Assert.Equal(200.0, Canvas.GetTop(group));
    }

    [AvaloniaFact]
    internal void Rects_実線は背景を塗らずダッシュ配列を持たない()
    {
        var view = CreateView();
        view.Rects = [new OverlayRect(new EsRect(0, 0, 10, 10), OverlayLineStyle.Solid, DrawingColor.Red, DrawingColor.Transparent, 2.0)];

        var group = Assert.IsType<Panel>(Assert.Single(view.RectOverlayChildren));
        var background = Assert.IsType<Rectangle>(group.Children[0]);
        var foreground = Assert.IsType<Rectangle>(group.Children[1]);

        Assert.Null(background.Fill);
        Assert.Null(foreground.StrokeDashArray);
        Assert.Equal(2.0, foreground.StrokeThickness);
        Assert.Equal(200.0, foreground.Width);
    }

    [AvaloniaFact]
    internal void Rects_画像未設定なら矩形オーバーレイを描画しない()
    {
        var view = new CropViewControl
        {
            Rects = [new OverlayRect(new EsRect(0, 0, 10, 10), OverlayLineStyle.Solid, DrawingColor.Red, DrawingColor.Transparent, 2.0)]
        };

        Assert.Empty(view.RectOverlayChildren);
    }

    // ====================================================================
    // 強調（ハイライト）
    // ====================================================================

    [AvaloniaFact]
    internal void HighlightSourceRect_対象座標をビューポート座標へ写像して強調する()
    {
        var view = CreateView();

        // 画像 10x10 を 400x200 へ Fit した倍率は 20 倍（中央寄せで X のみ 100 ずれる）
        view.HighlightSourceRect = new EsRect(2, 3, 4, 5);

        var mapped = view.HighlightMarquee.HighlightSourceRect;
        Assert.NotNull(mapped);
        Assert.Equal(140.0, mapped!.Value.X);
        Assert.Equal(60.0, mapped.Value.Y);
        Assert.Equal(80.0, mapped.Value.Width);
        Assert.Equal(100.0, mapped.Value.Height);
    }

    [AvaloniaFact]
    internal void HighlightSourceRect_画像未設定なら強調しない()
    {
        var view = new CropViewControl { HighlightSourceRect = new EsRect(1, 1, 2, 2) };

        Assert.Null(view.HighlightMarquee.HighlightSourceRect);
    }

    [AvaloniaFact]
    internal void HighlightSourceRect_ビューポート変化で写像を更新する()
    {
        var view = CreateView();
        view.HighlightSourceRect = new EsRect(0, 0, 10, 10);

        view.ZoomPercent = 100;

        var mapped = view.HighlightMarquee.HighlightSourceRect;
        Assert.NotNull(mapped);
        Assert.Equal(10.0, mapped!.Value.Width);
        Assert.Equal(10.0, mapped.Value.Height);
    }

    [AvaloniaFact]
    internal void HighlightAppearance_既定は内側の塗りと枠線だけを描く()
    {
        var view = new CropViewControl();

        Assert.Equal(CropViewControl.HighlightDefaultAppearance, view.HighlightAppearance);
        Assert.Equal(CropViewControl.HighlightDefaultAppearance, view.HighlightMarquee.Appearance);
        Assert.True(view.HighlightAppearance.ShowInnerFill);
        Assert.True(view.HighlightAppearance.ShowInnerBorder);
        Assert.False(view.HighlightAppearance.ShowHandles);
        Assert.False(view.HighlightAppearance.ShowOverlay);
        Assert.False(view.HighlightAppearance.ShowOuterBorder);
    }

    [AvaloniaFact]
    internal void ZoomPercent_設定した値が取得できる()
    {
        var view = CreateView();

        view.ZoomPercent = 200;

        Assert.Equal(200, view.ZoomPercent);
    }

    [AvaloniaFact]
    internal void SourceToClient_対象座標をビューポート座標へ変換する()
    {
        var view = CreateView();

        // 画像 10x10・ビューポート 400x200 → FitScale = min(40, 20) = 20、中央寄せ
        // 画像の表示矩形は 200x200、原点は (100, 0)（横中央）。
        // 対象 (5, 5) → ビューポート (100 + 5*20, 0 + 5*20) = (200, 100)
        var result = view.SourceToClient(new Point(5, 5));

        Assert.Equal(200.0, result.X);
        Assert.Equal(100.0, result.Y);
    }

    [AvaloniaFact]
    internal void ClientToSource_ビューポート座標を対象座標へ変換する()
    {
        var view = CreateView();

        // ビューポート (200, 100) → 対象 (5, 5)
        var result = view.ClientToSource(new Point(200, 100));

        Assert.Equal(5.0, result.X, 3);
        Assert.Equal(5.0, result.Y, 3);
    }

    // ====================================================================
    // Source（画像なし時の領域選択の無効化）
    // ====================================================================

    [AvaloniaFact]
    internal void Source_未設定の場合は領域選択オーバーレイが不可視でヒットテスト不可になる()
    {
        var view = new CropViewControl();

        // 画像なし（初期状態 Source = null）のとき、領域選択（Marquee）はヒットテスト不可かつ不可視
        // （Crop モードの全面暗転が市松模様を暗くしないよう、オーバーレイごと非表示にする）。
        Assert.Null(view.Source);
        Assert.False(view.SelectionOverlay.IsHitTestVisible);
        Assert.False(view.SelectionOverlay.IsVisible);
    }

    [AvaloniaFact]
    internal void Source_画像を設定しても領域選択オーバーレイは自動では可視にならない()
    {
        var view = CreateView();

        // 画像あり（Source 非 null）でも、オーバーレイは Crop セッションが制御するため自動では可視化しない
        // （キャプチャ完了直後に Crop モードの暗転が残るのを防ぐ）。
        Assert.NotNull(view.Source);
        Assert.False(view.SelectionOverlay.IsHitTestVisible);
        Assert.False(view.SelectionOverlay.IsVisible);
    }

    [AvaloniaFact]
    internal void SetSelectionOverlayVisible_trueでオーバーレイが可視でヒットテスト可能になる()
    {
        var view = CreateView();

        // Crop セッション開始時: オーバーレイを可視化し、ヒットテストを有効化する
        view.SetSelectionOverlayVisible(true);

        Assert.True(view.SelectionOverlay.IsVisible);
        Assert.True(view.SelectionOverlay.IsHitTestVisible);
    }

    [AvaloniaFact]
    internal void SetSelectionOverlayVisible_falseで不可視になり選択がクリアされる()
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
    internal void Source_画像をnullへ戻すと選択がクリアされるがオーバーレイの表示状態は維持される()
    {
        var view = CreateView();
        view.SetSelectionOverlayVisible(true);
        view.SetSelection(new Rect(10, 10, 50, 40));

        // 画像あり → 選択状態
        Assert.True(view.HasSelection);

        // 画像を null へ戻す（画像なしに戻る）
        view.Source = null;

        // 選択はクリアされる。 オーバーレイの表示・ヒットテスト状態は SetSelectionOverlayVisible が専任のため維持される。
        Assert.False(view.HasSelection);
        Assert.True(view.SelectionOverlay.IsVisible);
        Assert.True(view.SelectionOverlay.IsHitTestVisible);
    }

    [AvaloniaFact]
    internal void Source_画像をnullへ戻した後にSetSelectionOverlayVisible_falseで不可視になる()
    {
        var view = CreateView();

        // 画像を null へ戻す
        view.Source = null;

        // Crop セッション終了相当: オーバーレイを非表示にし、選択をクリアする
        view.SetSelectionOverlayVisible(false);

        Assert.False(view.SelectionOverlay.IsVisible);
        Assert.False(view.SelectionOverlay.IsHitTestVisible);
    }

    // ====================================================================
    // オーバーレイ上の実ホイールイベント（Crop 中のパン・ズームの統合検証）
    // ====================================================================

    /// <summary>選択オーバーレイ内のドラッグ矩形コントロール（実際にホイールを受け取る要素）を取得する。</summary>
    /// <param name="view">対象のコントロール。</param>
    /// <returns>ドラッグ矩形コントロール。</returns>
    private static MarqueeControl FindMarquee(CropViewControl view)
        => view.SelectionOverlay.Children.OfType<MarqueeControl>().Single();

    /// <summary>ホイールイベントを生成する。</summary>
    /// <param name="source">イベントの発生元。</param>
    /// <param name="root">イベントのルート（ウィンドウ）。</param>
    /// <param name="modifiers">修飾キー。</param>
    /// <param name="deltaY">縦の回転量。</param>
    /// <returns>生成したホイールイベント。</returns>
    private static PointerWheelEventArgs CreateWheel(Visual source, Window root, KeyModifiers modifiers, double deltaY)
    {
        var pointer = new Pointer(1, PointerType.Mouse, true);
        var screen = source.PointToScreen(new Point(0, 0));
        var rootPoint = new Point(screen.X, screen.Y);
        var raw = modifiers.HasFlag(KeyModifiers.Control) ? RawInputModifiers.Control : RawInputModifiers.None;
        return new PointerWheelEventArgs(
            source, pointer, root, rootPoint, 0UL,
            new PointerPointProperties(raw, PointerUpdateKind.Other), modifiers, new Vector(0, deltaY));
    }

    [AvaloniaFact]
    internal void オーバーレイ上の実ホイールでCrop中もズームする()
    {
        var view = new CropViewControl
        {
            Source = EsBitmapAvaloniaExtensions.CreateAvaloniaImage(400, 200, Colors.Blue),
        };
        var window = new Window { Content = view };
        window.Show();
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));
        view.SetSelectionOverlayVisible(true);

        // オーバーレイ内のドラッグ矩形（ヒットテスト対象）へ実際の Ctrl+ホイールを配送する
        var marquee = FindMarquee(view);
        marquee.RaiseEvent(CreateWheel(marquee, window, KeyModifiers.Control, 1));

        // Fit（等倍）から WheelFactor 倍（1.1）へ移動する
        Assert.Equal(110, view.ZoomPercent);
        window.Close();
    }

    [AvaloniaFact]
    internal void オーバーレイ上の通常ホイールでCrop中もパンする()
    {
        var view = new CropViewControl
        {
            Source = EsBitmapAvaloniaExtensions.CreateAvaloniaImage(1000, 500, Colors.Blue),
        };
        var window = new Window { Content = view };
        window.Show();
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));
        view.ZoomPercent = 100;
        view.SetSelectionOverlayVisible(true);
        var before = view.ImageRectViewport();

        var marquee = FindMarquee(view);
        marquee.RaiseEvent(CreateWheel(marquee, window, KeyModifiers.None, 1));

        // 1 ノッチ（48 DIP）ぶん表示位置が上へ移動する
        var after = view.ImageRectViewport();
        Assert.Equal(before.Y - 48.0, after.Y);
        window.Close();
    }

    /// <summary>ウィンドウ経由（実ヒットテスト）のドラッグで切り出しの選択矩形が作成されることを検証する。</summary>
    /// <remarks>
    /// 他のテストは <c>RaiseEvent</c> でハンドラを直接呼ぶため、<b>ヒットテストの欠落を検出できない</b><br/>
    /// （背景を持たない <see cref="Panel"/> は描画内容が無く、押下を受け取れない）。<br/>
    /// 切り出しの選択は可視化したオーバーレイが入力を受けて成立するため、実入力の経路で検証する。
    /// </remarks>
    [AvaloniaFact]
    internal void ウィンドウ経由のドラッグで切り出しの選択矩形が作成される()
    {
        var view = new CropViewControl
        {
            Source = EsBitmapAvaloniaExtensions.CreateAvaloniaImage(400, 200, Colors.Blue),
        };
        var window = new Window { Content = view };
        window.Show();
        view.Measure(new Size(400, 200));
        view.Arrange(new Rect(0, 0, 400, 200));
        view.SetSelectionOverlayVisible(true);
        Dispatcher.UIThread.RunJobs();

        window.MouseMove(new Point(40, 40));
        window.MouseDown(new Point(40, 40), MouseButton.Left);
        window.MouseMove(new Point(240, 140));
        window.MouseUp(new Point(240, 140), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.HasSelection, "切り出しのドラッグが選択オーバーレイへ届いていない（ヒットテストの欠落）");
        window.Close();
    }
}
