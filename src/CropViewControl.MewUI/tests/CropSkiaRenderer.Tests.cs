using EsUtil.SaveEvidence.CropView;
using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.CropView.MewUI.Tests;

/// <summary><see cref="CropSkiaRenderer"/> の描画テスト（Skia のビットマップ上で画素を検証する）。</summary>
/// <remarks>
/// Skia のビットマップはウィンドウを必要としないため、UI 初期化なしで並列実行できる。<br/>
/// 描画入力（配置・太さ・破線の判定と長さ・色）はコアの <c>OverlayRectLayout</c> が確定するため、<br/>
/// 本テストは「コアが確定した値がそのとおり画素になるか」を検証する（式・判断はコアのテストが担う）。
/// </remarks>
public class CropSkiaRendererTests
{
    /// <summary>検証に使う前景色（赤）。</summary>
    private static readonly DrawingColor s_foreground = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>検証に使う背景色（青）。</summary>
    private static readonly DrawingColor s_background = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>Skia の赤。</summary>
    private static readonly SKColor s_skForeground = new(255, 0, 0, 255);

    /// <summary>Skia の青。</summary>
    private static readonly SKColor s_skBackground = new(0, 0, 255, 255);

    /// <summary>指定スタイルの矩形情報を生成する。</summary>
    /// <param name="rect">矩形（ビューポート座標）。</param>
    /// <param name="style">枠線スタイル。</param>
    /// <param name="thickness">枠線の太さ。</param>
    /// <returns>矩形情報。</returns>
    private static OverlayRect Create(EsRect rect, OverlayLineStyle style, double thickness = 2)
        => new(rect, style, s_foreground, s_background, thickness);

    /// <summary>矩形情報（ビューポート座標とみなす）をコアの描画入力へ解決する。</summary>
    /// <param name="rect">矩形情報。</param>
    /// <returns>コアが確定した描画入力。</returns>
    private static OverlayRectFrame CreateFrame(OverlayRect rect)
        => OverlayRectLayout.Resolve(
            rect, new EsPoint(rect.Bounds.X, rect.Bounds.Y), new EsPoint(rect.Bounds.Right, rect.Bounds.Bottom));

    /// <summary>コアが確定した描画入力をキャンバスへ描画する。</summary>
    /// <param name="target">描画先。</param>
    /// <param name="rect">矩形情報。</param>
    private static void Draw(RenderTarget target, OverlayRect rect)
        => CropSkiaRenderer.Render(target.Canvas, CreateFrame(rect));
    /// <summary>実線は枠のみを描画し、内側は塗らないことを検証する。</summary>
    [Fact]
    public void Render_実線は枠のみを描画する()
    {
        using var target = TestHelper.CreateTarget(100, 100);
        var info = Create(new EsRect(10, 10, 40, 30), OverlayLineStyle.Solid);

        Draw(target, info);

        // 4 辺に色が付く
        Assert.Equal(s_skForeground, target.GetPixel(30, 10));  // 上
        Assert.Equal(s_skForeground, target.GetPixel(30, 39));  // 下
        Assert.Equal(s_skForeground, target.GetPixel(10, 25));  // 左
        Assert.Equal(s_skForeground, target.GetPixel(49, 25));  // 右
        // 内側と外側は描画されない
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(30, 25));
        Assert.Equal(new SKColor(0, 0, 0, 0), target.GetPixel(5, 5));
    }

    /// <summary>実線では背景色が描画されないことを検証する。</summary>
    [Fact]
    public void Render_実線では背景色を使わない()
    {
        using var target = TestHelper.CreateTarget(100, 100);
        var info = Create(new EsRect(10, 10, 40, 30), OverlayLineStyle.Solid);

        Draw(target, info);

        Assert.Equal(0, target.CountColor(0, 0, 100, 100, s_skBackground));
    }

    /// <summary>破線は前景色と背景色を交互に描画することを検証する（市松風）。</summary>
    [Fact]
    public void Render_破線は前景色と背景色を交互に描画する()
    {
        const int size = 100;
        using var target = TestHelper.CreateTarget(size, size);
        var info = Create(new EsRect(10, 10, 40, 30), OverlayLineStyle.Dashed);

        Draw(target, info);

        var red = target.CountColor(0, 0, size, size, s_skForeground);
        var blue = target.CountColor(0, 0, size, size, s_skBackground);

        Assert.True(red > 0, "前景色（破線）が描画されていない。");
        Assert.True(blue > 0, "背景色（隙間）が描画されていない。");
    }

    /// <summary>破線のダッシュ長がコアの単一所有値（破線 8 / 間隔 4）と一致することを検証する。</summary>
    [Fact]
    public void Render_破線のダッシュ長がコアの値と一致する()
    {
        const int size = 100;
        using var target = TestHelper.CreateTarget(size, size);
        var info = Create(new EsRect(10, 10, 40, 30), OverlayLineStyle.Dashed);

        Draw(target, info);

        // 上辺（枠の中心行）の色の連続長を測る
        var runs = new List<(SKColor Color, int Length)>();
        var previous = target.GetPixel(0, 11);
        var start = 0;
        for (var x = 1; x < size; x++)
        {
            var color = target.GetPixel(x, 11);
            if (color != previous)
            {
                runs.Add((previous, x - start));
                previous = color;
                start = x;
            }
        }

        // 描画された区間のみを対象にする（両端の透明は除外する）
        var drawn = runs.Where(r => r.Color != new SKColor(0, 0, 0, 0)).ToList();

        var (dashLength, gapLength) = OverlayLineDash.Get(OverlayLineStyle.Dashed);

        Assert.NotEmpty(drawn);
        Assert.Contains(drawn, r => r.Color == s_skForeground && r.Length == (int)dashLength);
        Assert.Contains(drawn, r => r.Color == s_skBackground && r.Length == (int)gapLength);
    }

    /// <summary>点線のダッシュ長がコアの単一所有値（点線 2 / 間隔 2）と一致することを検証する。</summary>
    [Fact]
    public void Render_点線のダッシュ長がコアの値と一致する()
    {
        const int size = 100;
        using var target = TestHelper.CreateTarget(size, size);
        var info = Create(new EsRect(10, 10, 40, 30), OverlayLineStyle.Dotted, thickness: 1);

        Draw(target, info);

        var runs = new List<(SKColor Color, int Length)>();
        var previous = target.GetPixel(0, 10);
        var start = 0;
        for (var x = 1; x < size; x++)
        {
            var color = target.GetPixel(x, 10);
            if (color != previous)
            {
                runs.Add((previous, x - start));
                previous = color;
                start = x;
            }
        }

        var drawn = runs.Where(r => r.Color != new SKColor(0, 0, 0, 0)).ToList();
        var (dashLength, gapLength) = OverlayLineDash.Get(OverlayLineStyle.Dotted);

        Assert.NotEmpty(drawn);
        Assert.Contains(drawn, r => r.Color == s_skForeground && r.Length == (int)dashLength);
        Assert.Contains(drawn, r => r.Color == s_skBackground && r.Length == (int)gapLength);
    }

    /// <summary>幅・高さが 0 以下の矩形は描画しないことを検証する。</summary>
    /// <param name="width">矩形の幅。</param>
    /// <param name="height">矩形の高さ。</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-5, 10)]
    public void Render_サイズ0以下は描画しない(double width, double height)
    {
        using var target = TestHelper.CreateTarget(50, 50);
        var info = Create(new EsRect(5, 5, width, height), OverlayLineStyle.Solid);

        Draw(target, info);

        Assert.Equal(0, target.CountColor(0, 0, 50, 50, s_skForeground));
    }

    /// <summary>太さが 0 以下でも 1 ピクセルで描画されることを検証する。</summary>
    [Fact]
    public void Render_太さが0以下でも1ピクセルで描画する()
    {
        using var target = TestHelper.CreateTarget(50, 50);
        var info = Create(new EsRect(10, 10, 20, 20), OverlayLineStyle.Solid, thickness: 0);

        Draw(target, info);

        Assert.True(target.CountColor(0, 0, 50, 50, s_skForeground) > 0);
    }

    /// <summary>半透明の色がアルファ込みで描画されることを検証する。</summary>
    [Fact]
    public void Render_半透明の色をアルファ込みで描画する()
    {
        using var target = TestHelper.CreateTarget(50, 50);
        var info = new OverlayRect(
            new EsRect(10, 10, 20, 20), OverlayLineStyle.Solid,
            DrawingColor.FromArgb(128, 255, 0, 0), s_background, 2);

        Draw(target, info);

        // 画素は乗算済みで保持されるため、非乗算に戻した色で比較する
        var pixel = target.GetPixel(20, 10);

        Assert.Equal(128, pixel.Alpha);
        Assert.Equal(255, pixel.Red);
    }

    /// <summary>キャンバスが null の場合は例外になることを検証する。</summary>
    [Fact]
    public void Render_キャンバスがnullなら例外()
    {
        var info = Create(new EsRect(10, 10, 20, 20), OverlayLineStyle.Solid);

        Assert.Throws<ArgumentNullException>(() => CropSkiaRenderer.Render(null!, CreateFrame(info)));
    }
}
