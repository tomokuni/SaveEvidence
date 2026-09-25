using EsUtil.Platform.Core.Geometry;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.Marquee.Rendering.Tests;

/// <summary><see cref="MarqueeHostLabelRenderer"/> の描画テスト（体裁はコア、描画はポート）。</summary>
/// <remarks>
/// 付加情報ラベルはアプリ固有の情報だが、体裁（背景 = 文字の実寸 + 内側余白 × 2）はサイズラベルと同じ規則であり、<br/>
/// コア（<c>EsLabelLayout</c>）が単一所有する。<br/>
/// 両 UI のホストが同じ組み立てを書かないことを、画素で固定する。
/// </remarks>
public class MarqueeHostLabelRendererTests
{
    /// <summary>文字色（白・不透明）。</summary>
    private static readonly DrawingColor s_text = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>背景色（黒・不透明）。</summary>
    private static readonly DrawingColor s_background = DrawingColor.FromArgb(255, 0, 0, 0);

    /// <summary>塗られていない画素（透明）。</summary>
    private static readonly SKColor s_transparent = new(0, 0, 0, 0);

    [Fact]
    public void Draw_基準点を背景の左上に置き内側余白を四方へ空ける()
    {
        using var target = TestHelper.CreateTarget(120, 60);
        var anchor = new EsPoint(10, 10);

        MarqueeHostLabelRenderer.Draw(target.Canvas, anchor, "W", 14.0, s_text, s_background, padding: 4);

        // 背景は基準点（10,10）を左上とする（基準点の内側が背景の色になる）
        Assert.Equal(new SKColor(0, 0, 0), target.GetPixel(10, 10));
        Assert.Equal(new SKColor(0, 0, 0), target.GetPixel(11, 11));
        // 基準点の外側は背景でない
        Assert.Equal(s_transparent, target.GetPixel(9, 9));
    }

    [Fact]
    public void Draw_文字を背景の後に描く()
    {
        using var target = TestHelper.CreateTarget(120, 60);

        MarqueeHostLabelRenderer.Draw(target.Canvas, new EsPoint(10, 10), "W", 14.0, s_text, s_background, padding: 4);

        // 文字（白）が背景（黒）の上に現れる
        var white = 0;
        for (var y = 0; y < 60; y++)
        {
            for (var x = 0; x < 120; x++)
            {
                if (target.GetPixel(x, y).Red > 200)
                {
                    white++;
                }
            }
        }

        Assert.True(white > 0, "文字が描かれていない（背景に隠れている可能性がある）");
    }

    [Fact]
    public void Draw_余白が大きいほど背景が広がる()
    {
        using var narrow = TestHelper.CreateTarget(120, 60);
        using var wide = TestHelper.CreateTarget(120, 60);

        MarqueeHostLabelRenderer.Draw(narrow.Canvas, new EsPoint(10, 10), "W", 14.0, s_text, s_background, padding: 1);
        MarqueeHostLabelRenderer.Draw(wide.Canvas, new EsPoint(10, 10), "W", 14.0, s_text, s_background, padding: 8);

        var narrowPixels = narrow.CountColor(0, 0, 120, 60, new SKColor(0, 0, 0));
        var widePixels = wide.CountColor(0, 0, 120, 60, new SKColor(0, 0, 0));

        Assert.True(widePixels > narrowPixels, "内側余白が背景の大きさへ反映されていない");
    }

    [Fact]
    public void Draw_文言がnullなら例外()
    {
        using var target = TestHelper.CreateTarget(10, 10);

        Assert.Throws<ArgumentNullException>(
            () => MarqueeHostLabelRenderer.Draw(target.Canvas, new EsPoint(0, 0), null!, 14.0, s_text, s_background, 4));
    }

    [Fact]
    public void Draw_描画ポートがnullなら例外()
        => Assert.Throws<ArgumentNullException>(
            () => MarqueeHostLabelRenderer.Draw(null!, new EsPoint(0, 0), "W", 14.0, s_text, s_background, 4));
}
