using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Platform.Core.Tests;

/// <summary><see cref="IEsLabel"/> と <see cref="EsLabelExtensions.ToElement"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 「配置の計算（各ラベル型）」と「要素への写像（拡張メソッド）」の分担を固定する。<br/>
/// 角丸は既定（0）と指定値の両方を検証する（実装しない型が四角形になることの担保）。
/// </remarks>
public class EsLabelExtensionsTests
{
    /// <summary>角丸を持たない（既定値を使う）検証用ラベル。</summary>
    private readonly record struct PlainLabel(
        string Text,
        EsPoint Anchor,
        double FontSize,
        DrawingColor TextColor,
        DrawingColor BackgroundColor) : IEsLabel
    {
        public EsLabelFrame ComputeFrame(EsSize textSize)
        {
            var box = EsLabelLayout.ComputeBoxAtAnchor(Anchor, textSize, EsLabelLayout.Padding, EsLabelLayout.Padding);
            return new EsLabelFrame(
                box, EsLabelLayout.ComputeOffsetTextOrigin(box.TopLeft, EsLabelLayout.Padding, EsLabelLayout.Padding));
        }
    }

    /// <summary>角丸を指定する検証用ラベル。</summary>
    private readonly record struct RoundedLabel(
        string Text,
        double FontSize,
        double CornerRadius,
        DrawingColor TextColor,
        DrawingColor BackgroundColor) : IEsLabel
    {
        public EsLabelFrame ComputeFrame(EsSize textSize)
            => new(new EsRect(0, 0, textSize.Width, textSize.Height), new EsPoint(0, 0));
    }

    private static PlainLabel CreatePlain() => new(
        "(1, 2)", new EsPoint(10, 20), 12.0, DrawingColor.White, DrawingColor.FromArgb(153, 0, 0, 0));

    [Fact]
    public void CornerRadius_実装しない型は0になる()
        => Assert.Equal(0, ((IEsLabel)CreatePlain()).CornerRadius);

    [Fact]
    public void ToElement_実装しない型は角丸0で四角形になる()
    {
        var element = CreatePlain().ToElement(new EsSize(30, 8));

        Assert.Equal(0, element.CornerRadius);
    }

    [Fact]
    public void ToElement_配置と外観を要素の指示へ写す()
    {
        var label = CreatePlain();

        var element = label.ToElement(new EsSize(30, 8));

        Assert.Equal("(1, 2)", element.Text);
        Assert.Equal(12.0, element.FontSize);
        Assert.Equal(DrawingColor.White, element.TextColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), element.BackgroundColor);
        Assert.Equal(label.ComputeFrame(new EsSize(30, 8)).Background, element.Bounds);
        Assert.Equal(label.ComputeFrame(new EsSize(30, 8)).TextOrigin, element.TextOrigin);
    }

    [Fact]
    public void ToElement_角丸を指定した型は指定値を使う()
    {
        var element = new RoundedLabel("(1, 2)", 12.0, 2.0, DrawingColor.White, DrawingColor.Transparent)
            .ToElement(new EsSize(30, 8));

        Assert.Equal(2.0, element.CornerRadius);
    }

    [Fact]
    public void ToElement_内側余白が背景と文字で一致する()
    {
        var element = CreatePlain().ToElement(new EsSize(30, 8));

        Assert.Equal(EsLabelLayout.Padding, element.PaddingX, precision: 9);
        Assert.Equal(EsLabelLayout.Padding, element.PaddingY, precision: 9);
    }

    [Fact]
    public void ToElement_nullのラベルは例外になる()
        => Assert.Throws<ArgumentNullException>(() => EsLabelExtensions.ToElement(null!, new EsSize(30, 8)));
}
