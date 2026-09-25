using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary><see cref="EsRect"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 値型の算術のみを検証するため、UI フレームワークの初期化に依存せず <c>[Fact]</c> で並列実行する。
/// </remarks>
public class EsRectTests
{
    // ====================================================================
    // 境界（Right / Bottom）
    // ====================================================================

    [Fact]
    public void Right_左上Xと幅の和を返す()
    {
        var rect = new EsRect(10.5, 20.25, 100.5, 50.25);

        Assert.Equal(111.0, rect.Right, 6);
    }

    [Fact]
    public void Bottom_左上Yと高さの和を返す()
    {
        var rect = new EsRect(10.5, 20.25, 100.5, 50.25);

        Assert.Equal(70.5, rect.Bottom, 6);
    }

    [Fact]
    public void Right_幅が0なら左上Xを返す()
    {
        var rect = new EsRect(7.5, 3.5, 0, 0);

        Assert.Equal(7.5, rect.Right, 6);
        Assert.Equal(3.5, rect.Bottom, 6);
    }

    [Fact]
    public void Right_負の幅は左上Xより小さい値を返す()
    {
        // 負の幅は空の矩形を表す（境界も左上より小さくなる）
        var rect = new EsRect(100, 100, -20, -30);

        Assert.Equal(80.0, rect.Right, 6);
        Assert.Equal(70.0, rect.Bottom, 6);
    }

    // ====================================================================
    // ComputeInnerEdges（枠線の内側 4 辺）
    // ====================================================================

    [Fact]
    public void ComputeInnerEdges_四辺の矩形を上_下_左_右の順に返す()
    {
        var edges = new EsRect(10, 20, 100, 80).ComputeInnerEdges(thickness: 2);

        Assert.Equal(4, edges.Count);
        Assert.Equal(new EsRect(10, 20, 100, 2), edges[0]);
        Assert.Equal(new EsRect(10, 98, 100, 2), edges[1]);
        Assert.Equal(new EsRect(10, 22, 2, 76), edges[2]);
        Assert.Equal(new EsRect(108, 22, 2, 76), edges[3]);
    }

    [Fact]
    public void ComputeInnerEdges_左右の辺は上下の辺の内側へ収まる()
    {
        var edges = new EsRect(0, 0, 100, 80).ComputeInnerEdges(thickness: 2);

        // 左右の辺は上端 +t から始まり、高さは h - 2t（角で二重に塗らない）
        Assert.Equal(2, edges[2].Y);
        Assert.Equal(80 - 4, edges[2].Height);
        Assert.Equal(2, edges[3].Y);
        Assert.Equal(80 - 4, edges[3].Height);
    }

    [Fact]
    public void ComputeInnerEdges_太さが半分を超える場合は辺の高さを0にクランプする()
    {
        var edges = new EsRect(0, 0, 10, 6).ComputeInnerEdges(thickness: 4);

        Assert.Equal(0, edges[2].Height);
        Assert.Equal(0, edges[3].Height);
    }

    [Fact]
    public void ComputeInnerEdges_太さ0は各辺を厚み0で返す()
    {
        var edges = new EsRect(5, 6, 10, 20).ComputeInnerEdges(thickness: 0);

        Assert.Equal(new EsRect(5, 6, 10, 0), edges[0]);
        Assert.Equal(new EsRect(5, 6, 0, 20), edges[2]);
    }

    // ====================================================================
    // ClampToArea（領域内へのクランプ）
    // ====================================================================

    [Fact]
    public void ClampToArea_領域内の矩形は変更しない()
    {
        var rect = new EsRect(10, 20, 30, 40);

        Assert.Equal(rect, rect.ClampToArea(new EsSize(200, 100)));
    }

    [Theory]
    [InlineData(-5, -5, 25, 35)]
    [InlineData(250, 120, 200, 100)]
    public void ClampToArea_領域外の矩形は四隅の包絡矩形へ縮める(double x, double y, double expectedRight, double expectedBottom)
    {
        var clamped = new EsRect(x, y, 30, 40).ClampToArea(new EsSize(200, 100));

        Assert.Equal(expectedRight, clamped.Right);
        Assert.Equal(expectedBottom, clamped.Bottom);
        Assert.True(clamped.Width >= 0);
        Assert.True(clamped.Height >= 0);
    }

    [Fact]
    public void ClampToArea_領域全体を覆う矩形は領域全体になる()
    {
        var clamped = new EsRect(-100, -100, 500, 500).ClampToArea(new EsSize(200, 100));

        Assert.Equal(new EsRect(0, 0, 200, 100), clamped);
    }

    [Fact]
    public void ClampToArea_領域の外にある矩形は辺0になる()
    {
        var clamped = new EsRect(300, 200, 30, 40).ClampToArea(new EsSize(200, 100));

        Assert.Equal(200, clamped.X);
        Assert.Equal(100, clamped.Y);
        Assert.Equal(0, clamped.Width);
        Assert.Equal(0, clamped.Height);
    }
}
