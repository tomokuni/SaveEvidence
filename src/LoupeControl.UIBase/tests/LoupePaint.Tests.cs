using Xunit;
using DrawingColor = System.Drawing.Color;


namespace EsUtil.SaveEvidence.Loupe.UIBase.Tests;

/// <summary><see cref="LoupePaint"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 色は描画にだけ必要な設定であり、本層が単一所有する（コアの寸法と描画の可否は <c>LoupeOptions</c> が持つ）。<br/>
/// 既定値と部分変更を検証する。実際の描画結果は <see cref="LoupeRenderer"/> のテストが画素で検証する。
/// </remarks>
public class LoupePaintTests
{
    [Fact]
    public void 既定色が期待どおりである()
    {
        var paint = new LoupePaint();

        // ペーストボード（画像外）は不透明の中性グレー、十字線は赤 40%、枠線は LightGray
        Assert.Equal(DrawingColor.FromArgb(255, 0x53, 0x53, 0x53), paint.PasteboardColor);
        Assert.Equal(DrawingColor.FromArgb(102, 255, 0, 0), paint.CrossColor);
        Assert.Equal(DrawingColor.FromArgb(255, 0xD3, 0xD3, 0xD3), paint.BorderColor);
        // 座標ラベルは白文字・黒 60% 背景
        Assert.Equal(DrawingColor.White, paint.CoordLabelColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), paint.CoordLabelBackgroundColor);
    }

    /// <summary>ペーストボード（画像外）の既定が不透明の中性グレーであることを検証する。</summary>
    /// <remarks>画像外は無地として見える（透明を指定した場合は背後の要素がそのまま見える）。</remarks>
    [Fact]
    public void 既定のペーストボードは不透明の中性グレーである()
    {
        var paint = new LoupePaint();

        Assert.Equal(DrawingColor.FromArgb(255, 0x53, 0x53, 0x53), paint.PasteboardColor);
    }

    [Fact]
    public void with式で一部だけ変更できる()
    {
        var paint = new LoupePaint();

        var changed = paint with { BorderColor = DrawingColor.Red };

        Assert.Equal(DrawingColor.Red, changed.BorderColor);
        // 他の項目は維持される
        Assert.Equal(paint.PasteboardColor, changed.PasteboardColor);
    }

    [Fact]
    public void 等価性は値で判定される()
    {
        var a = new LoupePaint { CrossColor = DrawingColor.Red, PasteboardColor = DrawingColor.Blue };
        var b = new LoupePaint { CrossColor = DrawingColor.Red, PasteboardColor = DrawingColor.Blue };

        Assert.Equal(a, b);
        Assert.NotEqual(a, a with { PasteboardColor = DrawingColor.Green });
    }

    [Fact]
    public void Defaultは既定値と同じ値を持つ()
    {
        Assert.Equal(new LoupePaint(), LoupePaint.Default);
    }
}
