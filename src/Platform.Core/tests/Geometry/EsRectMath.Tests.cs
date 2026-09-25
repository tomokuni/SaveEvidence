using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary><see cref="EsRectMath"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 枠線の内側 4 辺と領域クランプは、選択矩形（Marquee）とルーペ（Loupe）が同じ値を使う。<br/>
/// 境界（太さが半分を超える場合・領域外の矩形）を固定する。
/// </remarks>
public class EsRectMathTests
{
    [Fact]
    public void ComputeInnerEdges_四辺の矩形を上_下_左_右の順に返す()
    {
        var edges = EsRectMath.ComputeInnerEdges(new EsRect(10, 20, 100, 80), thickness: 2);

        Assert.Equal(4, edges.Count);
        Assert.Equal(new EsRect(10, 20, 100, 2), edges[0]);
        Assert.Equal(new EsRect(10, 98, 100, 2), edges[1]);
        Assert.Equal(new EsRect(10, 22, 2, 76), edges[2]);
        Assert.Equal(new EsRect(108, 22, 2, 76), edges[3]);
    }

    [Fact]
    public void ComputeInnerEdges_左右の辺は上下の辺の内側へ収まる()
    {
        var edges = EsRectMath.ComputeInnerEdges(new EsRect(0, 0, 100, 80), thickness: 2);

        // 左右の辺は上端 +t から始まり、高さは h - 2t（角で二重に塗らない）
        Assert.Equal(2, edges[2].Y);
        Assert.Equal(80 - 4, edges[2].Height);
        Assert.Equal(2, edges[3].Y);
        Assert.Equal(80 - 4, edges[3].Height);
    }

    [Fact]
    public void ComputeInnerEdges_太さが半分を超える場合は辺の高さを0にクランプする()
    {
        var edges = EsRectMath.ComputeInnerEdges(new EsRect(0, 0, 10, 6), thickness: 4);

        Assert.Equal(0, edges[2].Height);
        Assert.Equal(0, edges[3].Height);
    }

    [Fact]
    public void ComputeInnerEdges_太さ0は各辺を厚み0で返す()
    {
        var edges = EsRectMath.ComputeInnerEdges(new EsRect(5, 6, 10, 20), thickness: 0);

        Assert.Equal(new EsRect(5, 6, 10, 0), edges[0]);
        Assert.Equal(new EsRect(5, 6, 0, 20), edges[2]);
    }

    [Fact]
    public void ClampToArea_領域内の矩形は変更しない()
    {
        var rect = new EsRect(10, 20, 30, 40);

        Assert.Equal(rect, EsRectMath.ClampToArea(rect, new EsSize(200, 100)));
    }

    [Theory]
    [InlineData(-5, -5, 25, 35)]
    [InlineData(250, 120, 200, 100)]
    public void ClampToArea_領域外の矩形は四隅の包絡矩形へ縮める(double x, double y, double expectedRight, double expectedBottom)
    {
        var clamped = EsRectMath.ClampToArea(new EsRect(x, y, 30, 40), new EsSize(200, 100));

        Assert.Equal(expectedRight, clamped.Right);
        Assert.Equal(expectedBottom, clamped.Bottom);
        Assert.True(clamped.Width >= 0);
        Assert.True(clamped.Height >= 0);
    }

    [Fact]
    public void ClampToArea_領域全体を覆う矩形は領域全体になる()
    {
        var clamped = EsRectMath.ClampToArea(new EsRect(-100, -100, 500, 500), new EsSize(200, 100));

        Assert.Equal(new EsRect(0, 0, 200, 100), clamped);
    }

    [Fact]
    public void ClampToArea_領域の外にある矩形は辺0になる()
    {
        var clamped = EsRectMath.ClampToArea(new EsRect(300, 200, 30, 40), new EsSize(200, 100));

        Assert.Equal(200, clamped.X);
        Assert.Equal(100, clamped.Y);
        Assert.Equal(0, clamped.Width);
        Assert.Equal(0, clamped.Height);
    }
}
