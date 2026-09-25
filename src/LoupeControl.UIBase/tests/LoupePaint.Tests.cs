using EsUtil.Platform.Core.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.SaveEvidence.Common.Imaging;


namespace EsUtil.SaveEvidence.Loupe.UIBase.Tests;

/// <summary><see cref="LoupePaint"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 色は描画にだけ必要な設定であり、本層が単一所有する（コアの寸法は <c>LoupeMetrics</c> が持つ）。<br/>
/// 既定値・描画可否の判定（アルファ 0 は描かない）を検証する。<br/>
/// 実際の描画結果は <see cref="LoupeRenderer"/> のテストが画素で検証する。
/// </remarks>
public class LoupePaintTests
{
    [Fact]
    public void 既定色が期待どおりである()
    {
        var paint = new LoupePaint();

        // 十字線は赤 40%、市松模様は Gray / DarkGray 30%、外枠は LightGray
        Assert.Equal(DrawingColor.FromArgb(102, 255, 0, 0), paint.CrossColor);
        Assert.Equal(DrawingColor.FromArgb(0x4C, 0x80, 0x80, 0x80), paint.CheckerColorA);
        Assert.Equal(DrawingColor.FromArgb(0x4C, 0xA9, 0xA9, 0xA9), paint.CheckerColorB);
        Assert.Equal(DrawingColor.FromArgb(255, 0xD3, 0xD3, 0xD3), paint.FrameColor);
        // 座標ラベルは白文字・黒 60% 背景
        Assert.Equal(EsLabelDefaults.TextColor, paint.CoordLabelColor);
        Assert.Equal(EsLabelDefaults.BackgroundColor, paint.CoordLabelBackgroundColor);
    }

    [Fact]
    public void 既定の色は描画をすべて有効にする()
    {
        var paint = new LoupePaint();

        Assert.True(paint.HasCrosshair);
        Assert.True(paint.HasFrame);
        Assert.True(paint.HasCheckerColorA);
        Assert.True(paint.HasCheckerColorB);
    }

    /// <summary>下地の既定が透明（背後の要素を透かす）であることを検証する。</summary>
    /// <remarks>下地はホストが指定する（不透明色を指定すると市松模様が背後の要素を透かさない）。</remarks>
    [Fact]
    public void 既定の下地は透明で描画しない()
    {
        var paint = new LoupePaint();

        Assert.Equal(DrawingColor.Transparent, paint.BackgroundColor);
        Assert.False(paint.HasBackground);
    }

    /// <summary>下地のアルファが 1 以上なら描画する判定になることを検証する。</summary>
    [Fact]
    public void 不透明な下地は描画する判定になる()
    {
        Assert.False((new LoupePaint { BackgroundColor = DrawingColor.FromArgb(0, 255, 255, 255) }).HasBackground);
        Assert.True((new LoupePaint { BackgroundColor = DrawingColor.FromArgb(1, 0, 0, 0) }).HasBackground);
        Assert.True((new LoupePaint { BackgroundColor = DrawingColor.White }).HasBackground);
    }

    [Fact]
    public void 透明色は描画しない判定になる()
    {
        var paint = new LoupePaint
        {
            CrossColor = DrawingColor.Transparent,
            FrameColor = DrawingColor.FromArgb(0, 1, 2, 3),
            CheckerColorA = DrawingColor.Transparent,
            CheckerColorB = DrawingColor.Transparent,
        };

        Assert.False(paint.HasCrosshair);
        Assert.False(paint.HasFrame);
        Assert.False(paint.HasCheckerColorA);
        Assert.False(paint.HasCheckerColorB);
        // アルファが 1 以上なら描画する
        Assert.True((paint with { CrossColor = DrawingColor.FromArgb(1, 0, 0, 0) }).HasCrosshair);
    }

    [Fact]
    public void with式で一部だけ変更できる()
    {
        var paint = new LoupePaint();

        var changed = paint with { FrameColor = DrawingColor.Red };

        Assert.Equal(DrawingColor.Red, changed.FrameColor);
        // 他の項目は維持される
        Assert.Equal(paint.CheckerColorA, changed.CheckerColorA);
    }

    [Fact]
    public void 等価性は値で判定される()
    {
        var a = new LoupePaint { CrossColor = DrawingColor.Red, CheckerColorA = DrawingColor.Blue };
        var b = new LoupePaint { CrossColor = DrawingColor.Red, CheckerColorA = DrawingColor.Blue };

        Assert.Equal(a, b);
        Assert.NotEqual(a, a with { CheckerColorA = DrawingColor.Green });
    }

    [Fact]
    public void Defaultは既定値と同じ値を持つ()
    {
        Assert.Equal(new LoupePaint(), LoupePaint.Default);
    }
}
