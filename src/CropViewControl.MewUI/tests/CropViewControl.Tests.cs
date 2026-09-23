using Aprillz.MewUI;
using Estable.CropView;
using Estable.MewUI.ZoomView;
using Estable.ZoomView;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.MewUI.CropView.Tests;

/// <summary><see cref="CropViewControl"/> の Facade としてのテスト。</summary>
/// <remarks>
/// MewUI の要素生成とレイアウトはホスト登録を必要としないため、UI を表示せずに検証できる。<br/>
/// 矩形オーバーレイの描画結果は <see cref="CropSkiaRendererTests"/> が担う。
/// </remarks>
public class CropViewControlTests
{
    /// <summary>検証に使う矩形情報を生成する。</summary>
    /// <param name="x">左上 X 座標（ドメイン座標）。</param>
    /// <param name="y">左上 Y 座標（ドメイン座標）。</param>
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
    public void 既定値が想定どおりである()
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
    public void Rectsを設定すると保持される()
    {
        var control = CreateLaidOut();

        control.Rects = [CreateRect(10, 10, 20, 20)];

        Assert.Single(control.Rects);
    }

    /// <summary>Rects に null を設定すると例外になることを検証する。</summary>
    [Fact]
    public void Rectsにnullを設定すると例外になる()
    {
        var control = CreateLaidOut();

        Assert.Throws<ArgumentNullException>(() => control.Rects = null!);
    }

    /// <summary>Rects が空でも例外にならないことを検証する。</summary>
    [Fact]
    public void Rectsが空でも例外にしない()
    {
        var control = CreateLaidOut();
        control.Rects = [];

        Assert.Empty(control.Rects);
    }

    /// <summary>Source の設定が内包するズームビューへ委譲されることを検証する。</summary>
    [Fact]
    public void Sourceの設定がズームビューへ委譲される()
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
    public void ズーム操作がズームビューへ委譲される()
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
    public void ズーム可否がズームビューから取得される()
    {
        using var image = TestHelper.CreateBitmap(200, 200, SKColors.Red);
        var control = CreateLaidOut(100, 100);
        control.Source = image;

        Assert.Equal(control.ZoomView.CanZoomIn, control.CanZoomIn);
        Assert.Equal(control.ZoomView.CanZoomOut, control.CanZoomOut);
    }

    /// <summary>ズームイベントが中継されることを検証する。</summary>
    [Fact]
    public void ズームイベントが中継される()
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
    public void ビューポート変化イベントが中継される()
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
    public void 座標変換が往復する()
    {
        using var image = TestHelper.CreateBitmap(100, 100, SKColors.Red);
        var control = CreateLaidOut();
        control.Source = image;

        // Fit = 200%。画像表示矩形の原点 (0,0)。
        var viewport = control.ContentToViewport(new EsPoint(25, 30));
        var content = control.ViewportToContent(viewport);

        Assert.Equal(50, viewport.X, 3);
        Assert.Equal(60, viewport.Y, 3);
        Assert.Equal(25, content.X, 3);
        Assert.Equal(30, content.Y, 3);
    }

    /// <summary>画像未設定では表示矩形が空になることを検証する。</summary>
    [Fact]
    public void 画像未設定なら表示矩形が空になる()
    {
        var control = CreateLaidOut();

        Assert.Equal(default, control.ImageRectViewport());
    }

    /// <summary>外観の設定が内包するズームビューへ委譲されることを検証する。</summary>
    [Fact]
    public void Appearanceの設定がズームビューへ委譲される()
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
    public void CenterAlignの設定がズームビューへ委譲される()
    {
        var control = CreateLaidOut();

        control.CenterAlign = false;

        Assert.False(control.CenterAlign);
        Assert.False(control.ZoomView.CenterAlign);
    }

    /// <summary>子要素（ズームビューとオーバーレイ）が 2 つ配置されることを検証する。</summary>
    [Fact]
    public void 子要素がズームビューとオーバーレイの2つである()
    {
        var control = CreateLaidOut();

        Assert.Equal(2, control.Count);
    }

    /// <summary>ログの設定が内包するズームビューへ伝搬されることを検証する。</summary>
    [Fact]
    public void Logの設定がズームビューへ伝搬される()
    {
        var control = CreateLaidOut();

        Assert.Same(control.ZoomView.Log, control.Log);

        // null は NullLogger へフォールバックし、内包するズームビューと同一のインスタンスを共有する
        control.Log = null!;

        Assert.NotNull(control.Log);
        Assert.Same(control.ZoomView.Log, control.Log);
    }
}
