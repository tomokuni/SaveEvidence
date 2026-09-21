using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace Estable.FadeMask.Tests;

/// <summary><see cref="FadeMaskLabelLayout"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// ラベルの余白・背景矩形・文字位置・次の行の位置を検証する。<br/>
/// テキストの実寸は描画 API に依存するため引数で与え、本テストは与えた実寸からの計算のみを検証する。
/// </remarks>
public class FadeMaskLabelLayoutTests
{
    /// <summary>背景矩形がテキストを内側余白で囲むサイズになることを検証する。</summary>
    [Fact]
    public void ComputeBackground_テキストを内側余白で囲む()
    {
        var rect = FadeMaskLabelLayout.ComputeBackground(
            new EsPoint(100, 50), new EsSize(120, 20));

        Assert.Equal(100, rect.X);
        Assert.Equal(50, rect.Y);
        Assert.Equal(120 + FadeMaskLabelLayout.Padding * 2, rect.Width);
        Assert.Equal(20 + FadeMaskLabelLayout.Padding * 2, rect.Height);
    }

    /// <summary>文字の描画原点が左へ内側余白 × 2、上へ内側余白だけずれることを検証する。</summary>
    /// <remarks>横を 2 倍にするのは Avalonia 版の描画と同一の見た目にするため（既存の見た目を維持する）。</remarks>
    [Fact]
    public void ComputeTextOrigin_左へ余白2倍上へ余白だけずらす()
    {
        var point = FadeMaskLabelLayout.ComputeTextOrigin(new EsPoint(100, 50));

        Assert.Equal(100 + FadeMaskLabelLayout.Padding * 2, point.X);
        Assert.Equal(50 + FadeMaskLabelLayout.Padding, point.Y);
    }

    /// <summary>次の行の位置が背景の下端と内側余白の和になることを検証する。</summary>
    [Fact]
    public void NextLineY_背景の下端と余白の和を返す()
    {
        var background = new EsRect(0, 10, 100, 30);

        Assert.Equal(10 + 30 + FadeMaskLabelLayout.Padding, FadeMaskLabelLayout.NextLineY(background));
    }

    /// <summary>内側余白が 4 DIP であることを検証する。</summary>
    /// <remarks>余白は Avalonia 版の描画と共通の値であり、変更すると両 UI の見た目がずれる。</remarks>
    [Fact]
    public void Padding_4DIPである()
        => Assert.Equal(4.0, FadeMaskLabelLayout.Padding);

    /// <summary>行を積むと各行の原点が単調に下がることを検証する。</summary>
    [Fact]
    public void 行を積むと原点が単調に下がる()
    {
        var origin = new EsPoint(0, 0);
        var textSize = new EsSize(50, 10);

        var first = FadeMaskLabelLayout.ComputeBackground(origin, textSize);
        var secondOrigin = new EsPoint(origin.X, FadeMaskLabelLayout.NextLineY(first));
        var second = FadeMaskLabelLayout.ComputeBackground(secondOrigin, textSize);

        Assert.True(second.Y > first.Y);
        Assert.Equal(first.X, second.X);
    }
}
