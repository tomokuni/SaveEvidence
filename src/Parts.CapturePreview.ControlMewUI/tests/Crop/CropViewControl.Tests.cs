using Aprillz.MewUI;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;
using EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.ZoomView;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.MewUI.Tests.Crop;

/// <summary><see cref="CropViewControl"/> の Facade としてのテスト。</summary>
/// <remarks>
/// MewUI の要素生成とレイアウトはホスト登録を必要としないため、UI を表示せずに検証できる。<br/>
/// 矩形オーバーレイの描画結果は <see cref="CropSkiaRendererTests"/> が担う。<br/>
/// <b>要素を生成するテストはコレクションで直列化する</b>（MewUI のビジュアル ツリーはスレッドセーフでなく、<br/>
/// 並列に要素を生成すると <c>Panel.Add</c> が共有の走査バッファを壊す。 実測で再現した）。
/// </remarks>
[Collection("MewUI")]
public class CropViewControlTests
{
    /// <summary>検証に使う矩形情報を生成する。</summary>
    /// <param name="x">左上 X 座標（対象座標）。</param>
    /// <param name="y">左上 Y 座標（対象座標）。</param>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <returns>矩形情報。</returns>
    private static OverlayRect CreateRect(double x, double y, double width, double height)
        => new(
            new EsRect(x, y, width, height), OverlayLineStyle.Solid,
            DrawingColor.FromArgb(255, 255, 0, 0), DrawingColor.FromArgb(255, 0, 0, 255), 2);

    /// <summary>ビューポートを確定させたコントロールを生成する。</summary>
    /// <param name="width">ビューポートの幅（DIP）。</param>
    /// <param name="height">ビューポートの高さ（DIP）。</param>
    /// <returns>レイアウト済みのコントロール。</returns>
    private static CropViewControl CreateLaidOut(double width = 200, double height = 200)
    {
        var control = new CropViewControl();
        control.Measure(new Size(width, height));
        control.Arrange(new Rect(0, 0, width, height));
        return control;
    }

    /// <summary>既定値が想定どおりであることを検証する。</summary>
    [Fact]
    internal void 既定値が想定どおりである()
    {
        var control = new CropViewControl();

        Assert.Null(control.Source);
        Assert.True(control.CenterAlign);
        Assert.Empty(control.Rects);
        Assert.NotNull(control.ZoomView);
        Assert.False(control.CanZoomIn);
        Assert.False(control.CanZoomOut);

        // 外観は内包するズームビューの既定値（コアの単一所有値）をそのまま公開する
        Assert.Equal(ZoomViewAppearance.Default, control.Appearance);
    }

    /// <summary>Rects の設定が保持されることを検証する。</summary>
    [Fact]
    internal void Rectsを設定すると保持される()
    {
        var control = CreateLaidOut();

        control.Rects = [CreateRect(10, 10, 20, 20)];

        Assert.Single(control.Rects);
    }

    /// <summary>Rects に null を設定すると例外になることを検証する。</summary>
    [Fact]
    internal void Rectsにnullを設定すると例外になる()
    {
        var control = CreateLaidOut();

        Assert.Throws<ArgumentNullException>(() => control.Rects = null!);
    }

    /// <summary>Rects が空でも例外にならないことを検証する。</summary>
    [Fact]
    internal void Rectsが空でも例外にしない()
    {
        var control = CreateLaidOut();
        control.Rects = [];

        Assert.Empty(control.Rects);
    }

    // ====================================================================
    // 強調（ハイライト）
    // ====================================================================

    /// <summary>強調矩形が対象座標からビューポート座標へ写像されることを検証する。</summary>
    [Fact]
    internal void HighlightSourceRect_対象座標をビューポート座標へ写像する()
    {
        using var image = TestHelper.CreateBitmap(100, 100, SKColors.Red);
        var control = CreateLaidOut();
        control.Source = image;

        // 100x100 を 200x200 へ Fit した倍率は 2 倍（中央寄せの余りなし）
        control.HighlightSourceRect = new EsRect(10, 10, 20, 20);

        Assert.Equal(new EsRect(20, 20, 40, 40), control.HighlightMarquee.HighlightSourceRect);
    }

    /// <summary>画像未設定の間は強調しないことを検証する（写像の基準が無いため）。</summary>
    [Fact]
    internal void HighlightSourceRect_画像未設定なら強調しない()
    {
        var control = CreateLaidOut();

        control.HighlightSourceRect = new EsRect(10, 10, 20, 20);

        Assert.Null(control.HighlightMarquee.HighlightSourceRect);
    }

    /// <summary>強調の既定の外観が内側の塗りと枠線だけを描くことを検証する。</summary>
    [Fact]
    internal void HighlightAppearance_既定は内側の塗りと枠線だけを描く()
    {
        var control = CreateLaidOut();

        Assert.Equal(CropViewControl.HighlightDefaultAppearance, control.HighlightAppearance);
        Assert.Equal(CropViewControl.HighlightDefaultAppearance, control.HighlightMarquee.Appearance);
        Assert.True(control.HighlightAppearance.ShowInnerFill);
        Assert.True(control.HighlightAppearance.ShowInnerBorder);
        Assert.False(control.HighlightAppearance.ShowHandles);
        Assert.False(control.HighlightAppearance.ShowOverlay);
        Assert.False(control.HighlightAppearance.ShowOuterBorder);
    }

    /// <summary>Source の設定が内包するズームビューへ委譲されることを検証する。</summary>
    [Fact]
    internal void Sourceの設定がズームビューへ委譲される()
    {
        using var image = TestHelper.CreateBitmap(100, 100, SKColors.Red);
        var control = CreateLaidOut();

        control.Source = image;

        Assert.Same(image, control.Source);
        Assert.Same(image, control.ZoomView.Source);
        Assert.True(control.ZoomView.IsFit);
    }

    /// <summary>ズーム操作が内包するズームビューへ委譲されることを検証する。</summary>
    [Fact]
    internal void ズーム操作がズームビューへ委譲される()
    {
        using var image = TestHelper.CreateBitmap(200, 200, SKColors.Red);
        var control = CreateLaidOut(100, 100);
        control.Source = image;

        control.ZoomIn();
        Assert.Equal(62, control.ZoomPercent, 3);

        control.ZoomOut();
        Assert.Equal(50, control.ZoomPercent, 3);

        control.ZoomFit();
        Assert.True(control.ZoomView.IsFit);

        control.ZoomPercent = 100;
        Assert.Equal(100, control.ZoomPercent, 3);
    }

    /// <summary>ズーム可否が内包するズームビューから取得されることを検証する。</summary>
    [Fact]
    internal void ズーム可否がズームビューから取得される()
    {
        using var image = TestHelper.CreateBitmap(200, 200, SKColors.Red);
        var control = CreateLaidOut(100, 100);
        control.Source = image;

        Assert.Equal(control.ZoomView.CanZoomIn, control.CanZoomIn);
        Assert.Equal(control.ZoomView.CanZoomOut, control.CanZoomOut);
    }

    /// <summary>ズームイベントが中継されることを検証する。</summary>
    [Fact]
    internal void ズームイベントが中継される()
    {
        using var image = TestHelper.CreateBitmap(100, 100, SKColors.Red);
        var control = CreateLaidOut();
        control.Source = image;

        ZoomChangedEventArgs? captured = null;
        control.ZoomChanged += (_, e) => captured = e;

        control.ZoomPercent = 100;

        Assert.NotNull(captured);
        Assert.Equal(100, captured.ZoomPercent, 3);
    }

    /// <summary>ビューポート変化イベントが中継されることを検証する。</summary>
    [Fact]
    internal void ビューポート変化イベントが中継される()
    {
        // パンできるよう、ビューポートより大きい画像を 100% で表示する
        using var image = TestHelper.CreateBitmap(400, 400, SKColors.Red);
        var control = CreateLaidOut(200, 200);
        control.Source = image;
        control.ZoomPercent = 100;

        var count = 0;
        control.ViewportChanged += (_, _) => count++;

        control.ZoomView.PanByWheel(new EsVector(1, 0), shift: false);

        Assert.True(count > 0);
    }

    /// <summary>座標変換が画像表示矩形の原点基準で往復することを検証する。</summary>
    [Fact]
    internal void 座標変換が往復する()
    {
        using var image = TestHelper.CreateBitmap(100, 100, SKColors.Red);
        var control = CreateLaidOut();
        control.Source = image;

        // Fit = 200%。 画像表示矩形の原点 (0,0)。
        var viewport = control.SourceToClient(new EsPoint(25, 30));
        var content = control.ClientToSource(viewport);

        Assert.Equal(50, viewport.X, 3);
        Assert.Equal(60, viewport.Y, 3);
        Assert.Equal(25, content.X, 3);
        Assert.Equal(30, content.Y, 3);
    }

    /// <summary>画像未設定では表示矩形が空になることを検証する。</summary>
    [Fact]
    internal void 画像未設定なら表示矩形が空になる()
    {
        var control = CreateLaidOut();

        Assert.Equal(default, control.ImageRectViewport());
    }

    /// <summary>外観の設定が内包するズームビューへ委譲されることを検証する。</summary>
    [Fact]
    internal void Appearanceの設定がズームビューへ委譲される()
    {
        var control = CreateLaidOut();
        var appearance = ZoomViewAppearance.Default with
        {
            CheckerSize = 8,
            BackgroundColor = DrawingColor.FromArgb(255, 16, 16, 16),
        };

        control.Appearance = appearance;

        Assert.Equal(appearance, control.Appearance);
        Assert.Equal(appearance, control.ZoomView.Appearance);
    }

    /// <summary>CenterAlign の設定が内包するズームビューへ委譲されることを検証する。</summary>
    [Fact]
    internal void CenterAlignの設定がズームビューへ委譲される()
    {
        var control = CreateLaidOut();

        control.CenterAlign = false;

        Assert.False(control.CenterAlign);
        Assert.False(control.ZoomView.CenterAlign);
    }

    /// <summary>子要素（ズームビュー・領域選択オーバーレイ・矩形オーバーレイ・強調オーバーレイ）が 4 つ配置されることを検証する。</summary>
    /// <remarks>
    /// 領域選択オーバーレイは切り出しセッションが可視化する（普段は非表示）。<br/>
    /// 順序は「ズームビュー（最下層）→ 領域選択 → 矩形 → 強調（最上層）」で、切り出し中も
    /// 自動切り出しの強調が暗転より上に残る（Avalonia 版と同じ重なり順）。
    /// </remarks>
    [Fact]
    internal void 子要素がズームビューとオーバーレイの4つである()
    {
        var control = CreateLaidOut();

        Assert.Equal(4, control.Count);
    }

    /// <summary>領域選択オーバーレイの既定が非表示・入力不可であることを検証する。</summary>
    /// <remarks>
    /// Crop モードは全面暗転を描くため、プレビュー表示時に可視だと「切り出し中」のような暗転が残る。
    /// </remarks>
    [Fact]
    internal void 領域選択オーバーレイの既定は非表示で入力不可である()
    {
        var control = CreateLaidOut();

        Assert.False(control.SelectionOverlay.IsVisible);
        Assert.False(control.SelectionOverlay.IsHitTestVisible);
    }

    /// <summary>切り出しセッションの開始・終了で可視性と入力の可否が切り替わることを検証する。</summary>
    /// <remarks>非表示へ戻すときは選択も破棄する（暗転マスク・選択枠の残留を防ぐ。 Avalonia 版と同じ）。</remarks>
    [Fact]
    internal void SetSelectionOverlayVisible_可視性と入力の可否を切り替え非表示で選択を破棄する()
    {
        var control = CreateLaidOut();

        control.SetSelectionOverlayVisible(true);
        control.SetSelection(new EsRect(10, 10, 50, 40));

        Assert.True(control.SelectionOverlay.IsVisible);
        Assert.True(control.SelectionOverlay.IsHitTestVisible);
        Assert.True(control.HasSelection);

        control.SetSelectionOverlayVisible(false);

        Assert.False(control.SelectionOverlay.IsVisible);
        Assert.False(control.SelectionOverlay.IsHitTestVisible);
        Assert.False(control.HasSelection);
    }

    /// <summary>ログの設定が内包するズームビューへ伝搬されることを検証する。</summary>
    [Fact]
    internal void Logの設定がズームビューへ伝搬される()
    {
        var control = CreateLaidOut();

        Assert.Same(control.ZoomView.Log, control.Log);

        // null は NullLogger へフォールバックし、内包するズームビューと同一のインスタンスを共有する
        control.Log = null!;

        Assert.NotNull(control.Log);
        Assert.Same(control.ZoomView.Log, control.Log);
    }
}
