using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.ZoomView;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.ZoomView;
using EsUtil.Ui.WinUI3.Interop.Drawing;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Tests.ZoomView;

/// <summary><see cref="ZoomScrollBarShapes"/> のテスト（描画リストの内容と順序）。</summary>
/// <remarks>
/// 画素ではなく描画リストを検証する（XAML の要素はウィンドウ無しで生成できないため、要素の生成は対象外とする）。<br/>
/// 検証するのは「どの矩形がどの色で、どの順に並ぶか」であり、移行前の画素テストと同じ回帰検出力を持つ。
/// </remarks>
public class ZoomScrollBarShapesTests
{
    /// <summary>スクロール不要な場合は何も追加しないことを検証する。</summary>
    [Fact]
    internal void AddScrollBars_バーが無い場合は何も追加しない()
    {
        var list = new DrawList();

        ZoomScrollBarShapes.AddScrollBars(list, (null, null), ZoomViewAppearance.Default);

        Assert.Equal(0, list.RectCount);
    }

    /// <summary>トラック → 枠 4 辺 → サムの順に追加することを検証する。</summary>
    [Fact]
    internal void AddScrollBars_トラックと枠とサムを順に追加する()
    {
        var list = new DrawList();
        var appearance = ZoomViewAppearance.Default with { ScrollBarBorderThickness = 2 };
        var bar = new ZoomScrollBar(new EsRect(10, 20, 100, 6), new EsRect(30, 20, 40, 6));

        ZoomScrollBarShapes.AddScrollBars(list, (bar, null), appearance);

        // トラック 1 + 枠 4 + サム 1
        Assert.Equal(6, list.RectCount);
        Assert.Equal(appearance.ScrollBarTrackColor, list.Rects[0].Color);
        Assert.Equal(appearance.ScrollBarBorderColor, list.Rects[1].Color);
        Assert.Equal(appearance.ScrollBarColor, list.Rects[5].Color);
        Assert.Equal(bar.Thumb, list.Rects[5].Bounds);
    }

    /// <summary>枠を描かない外観では枠を追加しないことを検証する。</summary>
    [Fact]
    internal void AddScrollBars_枠を描かない外観では枠を追加しない()
    {
        var list = new DrawList();
        var appearance = ZoomViewAppearance.Default with { ScrollBarBorderColor = DrawingColor.Transparent };
        var bar = new ZoomScrollBar(new EsRect(0, 0, 100, 4), new EsRect(0, 0, 50, 4));

        ZoomScrollBarShapes.AddScrollBars(list, (bar, null), appearance);

        // トラック + サムのみ
        Assert.Equal(2, list.RectCount);
    }

    /// <summary>枠をトラックの内側へ置くことを検証する。</summary>
    [Fact]
    internal void AddScrollBars_枠はトラックの内側へ置く()
    {
        var list = new DrawList();
        var appearance = ZoomViewAppearance.Default with
        {
            ScrollBarBorderThickness = 2,
            ScrollBarBorderColor = DrawingColor.Red,
        };
        var bar = new ZoomScrollBar(new EsRect(100, 100, 50, 10), new EsRect(100, 100, 20, 10));

        ZoomScrollBarShapes.AddScrollBars(list, (null, bar), appearance);

        // 最初の枠は上辺（トラックの内側の左上から幅いっぱい、太さ 2）
        var top = list.Rects[1].Bounds;
        Assert.Equal(102, top.X);
        Assert.Equal(102, top.Y);
        Assert.Equal(46, top.Width);
        Assert.Equal(2, top.Height);
    }
}
