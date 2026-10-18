using System.Drawing.Drawing2D;
using EsUtil.SaveEvidence.App.WinForms.Icons;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinForms.Tests;

/// <summary><see cref="SvgPathData"/> の解析を検証するテスト。</summary>
/// <remarks>
/// 解析は GDI+ のパスだけで完結するため、ウィンドウ（ハンドル）を生成せずに図形の座標を検証できる。<br/>
/// 共有する SVG（Fluent System Icons）は <c>M</c> / <c>L</c> / <c>H</c> / <c>V</c> / <c>C</c> / <c>Z</c> で構成される。
/// </remarks>
public class SvgPathDataTests
{
    /// <summary>絶対コマンドの直線と閉じた図形を解釈することを検証する。</summary>
    [Fact]
    public void 絶対コマンドを解釈する()
    {
        using var path = SvgPathData.Parse("M1 2 L11 2 L11 12 Z");

        Assert.Equal(3, path.PointCount);
        var bounds = path.GetBounds();
        Assert.Equal(1f, bounds.Left, 3);
        Assert.Equal(2f, bounds.Top, 3);
        Assert.Equal(11f, bounds.Right, 3);
        Assert.Equal(12f, bounds.Bottom, 3);
    }

    /// <summary>相対コマンドを現在点からの移動として解釈することを検証する。</summary>
    [Fact]
    public void 相対コマンドを解釈する()
    {
        using var path = SvgPathData.Parse("m1 1 l2 0 v2 z");

        var bounds = path.GetBounds();
        Assert.Equal(1f, bounds.Left, 3);
        Assert.Equal(1f, bounds.Top, 3);
        Assert.Equal(3f, bounds.Right, 3);
        Assert.Equal(3f, bounds.Bottom, 3);
    }

    /// <summary>水平・垂直コマンドを解釈することを検証する。</summary>
    [Fact]
    public void 水平垂直コマンドを解釈する()
    {
        using var path = SvgPathData.Parse("M0 0 H10 V10 Z");

        var bounds = path.GetBounds();
        Assert.Equal(10f, bounds.Right, 3);
        Assert.Equal(10f, bounds.Bottom, 3);
    }

    /// <summary>3 次ベジェを解釈することを検証する。</summary>
    [Fact]
    public void 三次ベジェを解釈する()
    {
        using var path = SvgPathData.Parse("M0 0 C0 5 5 10 10 10 Z");

        Assert.True(path.PointCount > 3);
        Assert.Equal(10f, path.GetBounds().Right, 3);
    }

    /// <summary>指数表記の数値を解釈することを検証する。</summary>
    [Fact]
    public void 指数表記を解釈する()
    {
        using var path = SvgPathData.Parse("M1e1 1E1 L20 10 Z");

        var bounds = path.GetBounds();
        Assert.Equal(10f, bounds.Left, 3);
        Assert.Equal(10f, bounds.Top, 3);
    }

    /// <summary>未対応のコマンド（円弧）は空のパスになることを検証する。</summary>
    [Fact]
    public void 未対応のコマンドは空のパスになる()
    {
        using var path = SvgPathData.Parse("M0 0 A1 1 0 0 1 2 2 Z");

        Assert.Equal(0, path.PointCount);
    }

    /// <summary>壊れたデータ（座標の欠落）は空のパスになることを検証する。</summary>
    [Fact]
    public void 壊れたデータは空のパスになる()
    {
        using var path = SvgPathData.Parse("M0");

        Assert.Equal(0, path.PointCount);
    }

    /// <summary>空のデータは空のパスになることを検証する。</summary>
    [Fact]
    public void 空のデータは空のパスになる()
    {
        using var path = SvgPathData.Parse("");

        Assert.Equal(0, path.PointCount);
    }

    /// <summary>塗りが交差の向きで穴を表す規則（nonzero）であることを検証する。</summary>
    /// <remarks>
    /// Fluent System Icons は穴をサブパスの向きで表すため、<see cref="FillMode.Alternate"/> で塗ると穴が埋まる。
    /// </remarks>
    [Fact]
    public void 塗りの規則が交差の向きである()
    {
        using var path = SvgPathData.Parse("M0 0 L1 0 L1 1 Z");

        Assert.Equal(FillMode.Winding, path.FillMode);
    }
}
