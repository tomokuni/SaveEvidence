using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Common.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Common.Tests;

/// <summary><see cref="EsLabelElement"/> のテスト（コアの配置から UI 要素の配置指示への写像）。</summary>
/// <remarks>
/// 各コントロールの UIBase 層が同じ写像を書くと写し漏れで見た目がずれるため、<br/>
/// 写像が値と余白の導出を正しく行うことを固定する。
/// </remarks>
public class EsLabelElementTests
{
    private static readonly DrawingColor s_text = DrawingColor.FromArgb(255, 0, 0, 0);
    private static readonly DrawingColor s_background = DrawingColor.FromArgb(153, 0, 0, 0);

    [Fact]
    public void Create_配置と外観を写す()
    {
        var frame = new EsLabelFrame(new EsRect(5, 7, 38, 16), new EsPoint(9, 11));

        var element = EsLabelElement.Create(frame, "abc", fontSize: 12, s_text, s_background, cornerRadius: 2);

        Assert.Equal(new EsRect(5, 7, 38, 16), element.Bounds);
        Assert.Equal(new EsPoint(9, 11), element.TextOrigin);
        Assert.Equal("abc", element.Text);
        Assert.Equal(12, element.FontSize);
        Assert.Equal(s_text, element.TextColor);
        Assert.Equal(s_background, element.BackgroundColor);
        Assert.Equal(2, element.CornerRadius);
    }

    [Fact]
    public void Create_角丸の既定は四角形になる()
    {
        var element = EsLabelElement.Create(
            new EsLabelFrame(new EsRect(0, 0, 10, 10), new EsPoint(1, 1)), "a", fontSize: 12, s_text, s_background);

        Assert.Equal(0, element.CornerRadius);
    }

    [Fact]
    public void PaddingX_背景の左端から文字原点までの距離を返す()
    {
        var element = EsLabelElement.Create(
            new EsLabelFrame(new EsRect(5, 7, 38, 16), new EsPoint(9, 11)), "abc", 12, s_text, s_background);

        Assert.Equal(4, element.PaddingX);
        Assert.Equal(4, element.PaddingY);
    }
}
