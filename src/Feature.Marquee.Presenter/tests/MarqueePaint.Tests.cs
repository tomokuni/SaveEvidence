using Xunit;
using DrawingColor = System.Drawing.Color;


namespace EsUtil.Feature.Marquee.Presenter.Tests;

/// <summary><see cref="MarqueePaint"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 色は描画にだけ必要な設定であり、本層が単一所有する（コアの寸法・表示可否は <c>MarqueeOptions</c> と<br/>
/// <c>MarqueeDecoOptions</c> が持つ）。既定値と未指定の塗り色の解決を検証する。<br/>
/// 実際の描画結果は <see cref="MarqueeRenderer"/> のテストが画素で検証する。
/// </remarks>
public class MarqueePaintTests
{
    [Fact]
    public void 既定色が期待どおりである()
    {
        var paint = new MarqueePaint();

        // ハンドルは白の塗り・ダークグレーの枠線、マーカーは白
        Assert.Equal(DrawingColor.White, paint.HandleColor);
        Assert.Equal(DrawingColor.FromArgb(255, 169, 169, 169), paint.HandleBorderColor);
        Assert.Equal(DrawingColor.White, paint.MarkerColor);
        // ラベルは白文字・黒 60% 背景
        Assert.Equal(DrawingColor.White, paint.SizeLabelColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), paint.SizeLabelBackgroundColor);
        Assert.Equal(DrawingColor.White, paint.CoordLabelColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), paint.CoordLabelBackgroundColor);
    }

    [Fact]
    public void 既定の暗転マスクは黒50パーセントである()
    {
        Assert.Equal(DrawingColor.FromArgb(128, 0, 0, 0), new MarqueePaint().OverlayColor);
    }

    [Fact]
    public void 既定の穴あけ枠はハンドル枠と同じ色である()
    {
        var paint = new MarqueePaint();

        // 穴あけ枠とハンドル枠は同じ矩形に重なるため、既定値を共有する
        Assert.Equal(MarqueePaint.DefaultHandleBorderColor, paint.OuterBorderColor);
        Assert.Equal(paint.HandleBorderColor, paint.OuterBorderColor);
    }

    [Fact]
    public void 既定の内側の塗りと枠線は透明である()
    {
        var paint = new MarqueePaint();

        // 描くかどうかは表示可否のフラグが単一所有する（色のアルファを代用にしない）
        Assert.Equal(DrawingColor.Transparent, paint.InnerFillColor);
        Assert.Equal(DrawingColor.Transparent, paint.InnerBorderColor);
    }

    [Fact]
    public void 未指定のハンドル塗り色は白へ解決される()
    {
        // 全成分 0（default）はハンドルが見えなくなるため白へ解決する
        Assert.Equal(DrawingColor.White, (new MarqueePaint { HandleColor = default }).HandleColor);
        Assert.Equal(DrawingColor.White, (new MarqueePaint { HandleColor = DrawingColor.FromArgb(0, 0, 0, 0) }).HandleColor);
    }

    [Fact]
    public void 指定したハンドル塗り色はそのまま使われる()
    {
        // アルファ 1 以上は指定値を尊重する（半透明の指定も許す）
        Assert.Equal(DrawingColor.Red, (new MarqueePaint { HandleColor = DrawingColor.Red }).HandleColor);
        Assert.Equal(
            DrawingColor.FromArgb(1, 2, 3, 4),
            (new MarqueePaint { HandleColor = DrawingColor.FromArgb(1, 2, 3, 4) }).HandleColor);
    }

    [Fact]
    public void with式で一部だけ変更できる()
    {
        var paint = new MarqueePaint();

        var changed = paint with { OverlayColor = DrawingColor.Red };

        Assert.Equal(DrawingColor.Red, changed.OverlayColor);
        // 他の項目は維持される
        Assert.Equal(paint.HandleColor, changed.HandleColor);
        Assert.Equal(paint.OuterBorderColor, changed.OuterBorderColor);
    }

    [Fact]
    public void 等価性は値で判定される()
    {
        var a = new MarqueePaint { OverlayColor = DrawingColor.Red, InnerFillColor = DrawingColor.Blue };
        var b = new MarqueePaint { OverlayColor = DrawingColor.Red, InnerFillColor = DrawingColor.Blue };

        Assert.Equal(a, b);
        Assert.NotEqual(a, a with { InnerFillColor = DrawingColor.Green });
    }

    [Fact]
    public void Defaultは既定値と同じ値を持つ()
    {
        Assert.Equal(new MarqueePaint(), MarqueePaint.Default);
    }
}
