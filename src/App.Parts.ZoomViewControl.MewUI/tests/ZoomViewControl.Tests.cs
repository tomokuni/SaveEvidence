using Aprillz.MewUI;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.ZoomView.MewUI.Tests;

/// <summary><see cref="ZoomViewControl"/>（MewUI）のテスト。</summary>
/// <remarks>
/// 判断・値・計算はコア（<see cref="ZoomViewSession"/>。 Core.Tests が全網羅する）が単一所有するため、<br/>
/// 本テストは <b>UI 層の責務（入力の反映・通知の転送・描画内容）</b>のみを検証する。<br/>
/// MewUI の要素生成とレイアウト（<c>Measure</c> / <c>Arrange</c>）はホスト登録を必要としないため、UI を表示せずに検証できる。<br/>
/// 描画結果の検証は <c>ZoomSkiaRenderer.Tests.cs</c> が担う。
/// </remarks>
public class ZoomViewControlTests
{
    /// <summary>ビューポートを確定させたコントロールを生成する。</summary>
    /// <param name="width">ビューポートの幅（DIP）。</param>
    /// <param name="height">ビューポートの高さ（DIP）。</param>
    /// <returns>レイアウト済みのコントロール。</returns>
    private static ZoomViewControl CreateLaidOut(double width = 200, double height = 200)
    {
        var control = new ZoomViewControl();
        control.Measure(new Size(width, height));
        control.Arrange(new Rect(0, 0, width, height));
        return control;
    }

    /// <summary>画像を設定したコントロールを生成する（Fit 表示で開始する）。</summary>
    /// <param name="imageWidth">画像の幅（ピクセル）。</param>
    /// <param name="imageHeight">画像の高さ（ピクセル）。</param>
    /// <param name="viewportWidth">ビューポートの幅（DIP）。</param>
    /// <param name="viewportHeight">ビューポートの高さ（DIP）。</param>
    /// <returns>コントロールと画像（画像の破棄は呼び出し側）。</returns>
    private static (ZoomViewControl Control, EsBitmap Image) CreateWithImage(
        int imageWidth = 100, int imageHeight = 100, double viewportWidth = 200, double viewportHeight = 200)
    {
        var image = TestHelper.CreateBitmap(imageWidth, imageHeight, SKColors.Red);
        var control = CreateLaidOut(viewportWidth, viewportHeight);
        control.Source = image;
        return (control, image);
    }

    // ====================================================================
    // 既定値・画像設定
    // ====================================================================

    /// <summary>既定値が想定どおりであることを検証する。</summary>
    [Fact]
    public void 既定値が想定どおりである()
    {
        var control = new ZoomViewControl();

        // 画像なし: Fit・実効ズーム率はフォールバック（100%）・可否は何れも不可
        Assert.True(control.IsFit);
        Assert.Equal(100.0, control.ZoomPercent, 6);
        Assert.False(control.CanZoomIn);
        Assert.False(control.CanZoomOut);
        Assert.Equal(0, control.Appearance.CheckerSize); // 市松模様なしが既定
        Assert.True(control.Appearance.ShowScrollBars);
    }

    /// <summary>画像を設定すると Fit 表示になることを検証する。</summary>
    [Fact]
    public void 画像設定でFit表示になる()
    {
        var (control, image) = CreateWithImage();
        using var _ = image;

        Assert.True(control.IsFit);
        // 100x100 の画像を 200x200 のビューポートへ Fit → 200%
        Assert.Equal(200.0, control.ZoomPercent, 3);
    }

    /// <summary>実効ズーム率の設定値が設定範囲へクランプされることを検証する。</summary>
    [Fact]
    public void ZoomPercent_設定範囲へクランプする()
    {
        var (control, image) = CreateWithImage();
        using var _ = image;

        control.ZoomPercent = 100000;

        Assert.Equal(ZoomConfig.Default.MaxZoom, control.ZoomPercent, 3);
    }

    /// <summary>0 以下の設定は受け付けない（Fit は <see cref="ZoomViewControl.ZoomFit"/> を使用する）ことを検証する。</summary>
    [Fact]
    public void ZoomPercent_0以下は無視する()
    {
        var (control, image) = CreateWithImage();
        using var _ = image;

        control.ZoomPercent = 100;
        control.ZoomPercent = 0;

        Assert.False(control.IsFit);
        Assert.Equal(100.0, control.ZoomPercent, 3);
    }

    // ====================================================================
    // ズーム操作と通知
    // ====================================================================

    /// <summary>ズームイン・アウトが段階レベルへ移動することを検証する。</summary>
    [Fact]
    public void ZoomIn_ZoomOut_段階レベルへ移動する()
    {
        // 200x200 の画像を 100x100 のビューポートへ Fit → 50%
        var (control, image) = CreateWithImage(200, 200, 100, 100);
        using var _ = image;

        Assert.Equal(50.0, control.ZoomPercent, 3);

        control.ZoomIn();
        Assert.Equal(62.0, control.ZoomPercent, 3);

        control.ZoomOut();
        Assert.Equal(50.0, control.ZoomPercent, 3);
    }

    /// <summary>ズーム操作が <c>ZoomChanged</c> を発火し、4 値が通知されることを検証する。</summary>
    [Fact]
    public void ZoomIn_ZoomChangedを発火する()
    {
        var (control, image) = CreateWithImage(200, 200, 100, 100);
        using var _ = image;

        ZoomChangedEventArgs? captured = null;
        control.ZoomChanged += (_, e) => captured = e;

        control.ZoomIn();

        Assert.NotNull(captured);
        Assert.Equal(62.0, captured.ZoomPercent, 3);
        Assert.False(captured.IsFit);
        Assert.True(captured.CanZoomIn);
        Assert.True(captured.CanZoomOut);
    }

    /// <summary>ズーム操作のログが実効ズーム率（Fit 中は実効倍率）を記録することを検証する。</summary>
    /// <remarks>
    /// 内部ズーム率の 0 ではなく、実際に表示されている実効倍率を記録する（コアが単一所有する書式）。
    /// </remarks>
    [Fact]
    public void ZoomIn_ログに実効ズーム率を記録する()
    {
        var logger = new RecordingLogger();
        var (control, image) = CreateWithImage(200, 200, 100, 100);
        using var _ = image;
        control.Log = logger;

        control.ZoomIn();

        Assert.Equal("[ZoomIn] 50% -> 62%", Assert.Single(logger.Messages));
    }

    /// <summary>Fit へ戻すとオフセットがリセットされることを検証する。</summary>
    [Fact]
    public void ZoomFit_オフセットをリセットする()
    {
        var (control, image) = CreateWithImage(400, 400, 200, 200);
        using var _ = image;

        control.ZoomPercent = 200;
        control.PanByWheel(new EsVector(0, 1), shift: false);
        Assert.True(control.ImageRectViewport().Y < 0); // パンで画像が上へ移動している

        control.ZoomFit();

        Assert.True(control.IsFit);
        // Fit では画像（400x400）がビューポート（200x200）へ収まり、原点は 0 に戻る
        Assert.Equal(0.0, control.ImageRectViewport().Y, 3);
    }

    // ====================================================================
    // パン
    // ====================================================================

    /// <summary>通常ホイールが 1 ノッチ = 48 DIP を実効倍率で除した量だけパンすることを検証する。</summary>
    [Fact]
    public void PanByWheel_1ノッチは48DIPを実効倍率で除す()
    {
        var (control, image) = CreateWithImage(400, 400, 200, 200);
        using var _ = image;
        control.ZoomPercent = 100;

        var before = control.ImageRectViewport();
        control.PanByWheel(new EsVector(0, 1), shift: false);

        // 実効倍率 1.0 のため 48 コンテンツ座標ぶん移動する（画像は上へ）
        Assert.Equal(before.Y - 48.0, control.ImageRectViewport().Y, 3);
    }

    /// <summary>Shift 併用のホイールが縦回転を横パンとして扱うことを検証する。</summary>
    [Fact]
    public void PanByWheel_Shift併用は横パンになる()
    {
        var (control, image) = CreateWithImage(400, 400, 200, 200);
        using var _ = image;
        control.ZoomPercent = 100;

        var before = control.ImageRectViewport();
        control.PanByWheel(new EsVector(0, 1), shift: true);

        Assert.Equal(before.X - 48.0, control.ImageRectViewport().X, 3);
        Assert.Equal(before.Y, control.ImageRectViewport().Y, 3);
    }

    /// <summary>Fit 表示中はパンしても移動しないことを検証する。</summary>
    [Fact]
    public void PanByWheel_Fit中は移動しない()
    {
        var (control, image) = CreateWithImage(400, 400, 200, 200);
        using var _ = image;

        var before = control.ImageRectViewport();
        control.PanByWheel(new EsVector(0, 1), shift: false);

        Assert.Equal(before, control.ImageRectViewport());
    }

    /// <summary>パンが <c>ViewportChanged</c> を発火することを検証する。</summary>
    [Fact]
    public void PanByWheel_ViewportChangedを発火する()
    {
        var (control, image) = CreateWithImage(400, 400, 200, 200);
        using var _ = image;
        control.ZoomPercent = 100;

        var count = 0;
        control.ViewportChanged += (_, _) => count++;

        control.PanByWheel(new EsVector(0, 1), shift: false);

        Assert.True(count > 0);
    }

    // ====================================================================
    // 表示値・座標変換（コアの値の委譲）
    // ====================================================================

    /// <summary>Fit の表示サイズがビューポートへ収まることを検証する。</summary>
    [Fact]
    public void DisplaySize_Fitでビューポートに収まる()
    {
        var (control, image) = CreateWithImage(400, 200, 200, 200);
        using var _ = image;

        var size = control.DisplaySize();

        Assert.True(size.Width <= 200, $"幅: {size.Width}");
        Assert.True(size.Height <= 200, $"高さ: {size.Height}");
    }

    /// <summary>座標変換（画像表示矩形の原点基準）が往復することを検証する。</summary>
    [Fact]
    public void 座標変換が往復する()
    {
        var (control, image) = CreateWithImage(100, 100, 200, 200);
        using var _ = image;

        var viewport = control.ContentToViewportImage(new EsPoint(25, 30));
        var content = control.ViewportToContentImage(viewport);

        Assert.Equal(25.0, content.X, 3);
        Assert.Equal(30.0, content.Y, 3);
    }

    /// <summary>画像未設定時の表示矩形が空であることを検証する。</summary>
    [Fact]
    public void ImageRectViewport_画像なしは空になる()
    {
        var control = CreateLaidOut();

        var rect = control.ImageRectViewport();

        Assert.Equal(0, rect.Width);
        Assert.Equal(0, rect.Height);
    }

    // ====================================================================
    // 外観（コアの ZoomViewAppearance を渡す窓口）
    // ====================================================================

    /// <summary>外観の設定がセッションへ反映されることを検証する。</summary>
    [Fact]
    public void Appearance_セッションへ反映される()
    {
        var control = CreateLaidOut();
        var appearance = ZoomViewAppearance.Default with { CheckerSize = 8, ShowScrollBars = false };

        control.Appearance = appearance;

        Assert.Equal(8, control.Appearance.CheckerSize);
        Assert.False(control.Appearance.ShowScrollBars);
    }

    /// <summary>外観の市松模様の明色タイル矩形がコアで計算されることを検証する。</summary>
    [Fact]
    public void Appearance_市松模様の明色タイルを列挙する()
    {
        var appearance = ZoomViewAppearance.Default with { CheckerSize = 10 };

        var tiles = appearance.ComputeLightTileRects(new EsRect(0, 0, 25, 25));

        // 3x3 のタイルのうち明色は (0,0)(0,2)(1,1)(2,0)(2,2) の 5 枚（端は領域で切り取られる）
        Assert.Equal(5, tiles.Count);
        Assert.All(tiles, tile =>
        {
            Assert.True(tile.Right <= 25 && tile.Bottom <= 25, $"領域外: {tile}");
            Assert.True(tile.Width is 10 or 5, $"切り取り幅が不正: {tile}");
        });
    }

    /// <summary>市松模様を無効にすると明色タイルが空になることを検証する。</summary>
    [Fact]
    public void Appearance_市松模様なしは明色タイルが空になる()
    {
        var appearance = ZoomViewAppearance.Default with { CheckerSize = 0 };

        Assert.Empty(appearance.ComputeLightTileRects(new EsRect(0, 0, 100, 100)));
    }

    // ====================================================================
    // スクロールバー（コアの同期値の委譲）
    // ====================================================================

    /// <summary>画像がビューポートより大きい場合にスクロール可能として同期されることを検証する。</summary>
    [Fact]
    public void ScrollMetrics_拡大時はスクロール可能になる()
    {
        var (control, image) = CreateWithImage(400, 400, 200, 200);
        using var _ = image;
        control.ZoomPercent = 200;

        var horizontal = control.Session.ScrollMetrics(vertical: false);
        var vertical = control.Session.ScrollMetrics(vertical: true);

        Assert.True(horizontal.IsScrollable);
        Assert.True(vertical.IsScrollable);
        Assert.True(horizontal.MaxOffset > 0);
    }

    /// <summary>Fit 表示ではスクロール不要（スクロールバー非表示）であることを検証する。</summary>
    [Fact]
    public void ScrollMetrics_Fit中はスクロール不可になる()
    {
        var (control, image) = CreateWithImage(400, 400, 200, 200);
        using var _ = image;

        Assert.False(control.Session.ScrollMetrics(vertical: true).IsScrollable);
    }
}
