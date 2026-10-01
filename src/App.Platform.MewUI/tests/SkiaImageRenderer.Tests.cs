using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Platform.MewUI.Imaging;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Platform.MewUI.Tests;

/// <summary><see cref="SkiaImageRenderer"/> の描画テスト（塗り分け・前提条件）。</summary>
/// <remarks>
/// コントロールを表示せず、キャンバスと画素だけで検証する（ウィンドウを必要としない）。<br/>
/// 配置の計算そのものは <c>ImageDrawMath</c>（Platform.Core）のテストが担うため、<br/>
/// 本テストは「描画された結果」（どの画素がどの色か）を固定する。
/// </remarks>
public class SkiaImageRendererTests
{
    [Fact]
    public void Render_Fillは縦横比を無視して領域全体へ画像で埋める()
    {
        using var source = TestHelper.CreateBitmap(4, 2, DrawingColor.Red);
        using var target = TestHelper.CreateTarget(4, 4);

        SkiaImageRenderer.Render(target.Canvas, target.Info, source, ImageDrawMode.Fill);

        // 領域全体（4x4 = 16 画素）が赤になる（縦方向へ引き伸ばす）
        Assert.Equal(16, target.CountColor(0, 0, 4, 4, SKColors.Red));
    }

    [Fact]
    public void Render_Uniformは縦横比を保ち余白を透明のまま残す()
    {
        using var source = TestHelper.CreateBitmap(4, 2, DrawingColor.Red);
        using var target = TestHelper.CreateTarget(8, 8);

        SkiaImageRenderer.Render(target.Canvas, target.Info, source, ImageDrawMode.Uniform);

        // 8x8 の領域へ 8x4 で収まり、上下に 2 画素ずつ透明の余白が残る
        Assert.Equal(32, target.CountColor(0, 0, 8, 8, SKColors.Red));
        Assert.Equal(0, target.CountColor(0, 0, 8, 2, SKColors.Red));
        Assert.Equal(0, target.CountColor(0, 6, 8, 8, SKColors.Red));
        Assert.Equal(0, target.GetPixel(0, 0).Alpha);
        Assert.Equal(0, target.GetPixel(7, 7).Alpha);
    }

    [Fact]
    public void Render_画像が無い場合は何も描画しない()
    {
        using var target = TestHelper.CreateTarget(4, 4);

        SkiaImageRenderer.Render(target.Canvas, target.Info, null, ImageDrawMode.Fill);

        Assert.Equal(0, target.CountColor(0, 0, 4, 4, SKColors.Red));
        Assert.Equal(0, target.GetPixel(1, 1).Alpha);
    }

    [Fact]
    public void Render_破棄済みの画像は何も描画しない()
    {
        var source = TestHelper.CreateBitmap(4, 4, DrawingColor.Red);
        source.Dispose();
        using var target = TestHelper.CreateTarget(4, 4);

        SkiaImageRenderer.Render(target.Canvas, target.Info, source, ImageDrawMode.Fill);

        Assert.Equal(0, target.CountColor(0, 0, 4, 4, SKColors.Red));
        Assert.Equal(0, target.GetPixel(1, 1).Alpha);
    }

    [Fact]
    public void Render_描画先が空の場合は何も描画しない()
    {
        using var source = TestHelper.CreateBitmap(4, 4, DrawingColor.Red);
        using var target = TestHelper.CreateTarget(4, 4);

        // 幅 0 の描画先（配置が空になる）
        SkiaImageRenderer.Render(target.Canvas, new SKImageInfo(0, 4), source, ImageDrawMode.Fill);

        Assert.Equal(0, target.CountColor(0, 0, 4, 4, SKColors.Red));
        Assert.Equal(0, target.GetPixel(1, 1).Alpha);
    }

    [Fact]
    public void Render_前回の描画内容を透明へクリアする()
    {
        using var source = TestHelper.CreateBitmap(4, 4, DrawingColor.Red);
        using var target = TestHelper.CreateTarget(4, 4);

        // 前回の描画を模して緑で塗ってから、画像を描かずに画像無しで再描画する
        SkiaImageRenderer.Render(target.Canvas, target.Info, source, ImageDrawMode.Fill);
        Assert.Equal(16, target.CountColor(0, 0, 4, 4, SKColors.Red));

        SkiaImageRenderer.Render(target.Canvas, target.Info, null, ImageDrawMode.Fill);

        // クリアされるため、前回の内容が残らない
        Assert.Equal(0, target.CountColor(0, 0, 4, 4, SKColors.Red));
        Assert.Equal(0, target.GetPixel(0, 0).Alpha);
        Assert.Equal(0, target.GetPixel(3, 3).Alpha);
    }
}
