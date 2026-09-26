using EsUtil.Platform.Core.Geometry;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Feature.MarqueeCore.Drawing;


namespace EsUtil.Feature.MarqueeCore.Tests;

/// <summary><see cref="MarqueeCoordLabel"/> の単体テスト（配置と要素への写像）。</summary>
/// <remarks>
/// カーソル追従ラベルの配置（オフセット・領域内へのクランプ・内側余白）を検証する。<br/>
/// 配置の計算は <see cref="EsUtil.Platform.Core.Imaging.EsLabelLayout"/>（Platform.Core）が単一所有するため、<br/>
/// 本テストは「どの余白・オフセットを使うか」というコア固有の指定を固定する。
/// </remarks>
public class MarqueeCoordLabelTests
{
    private static MarqueeCoordLabel CreateLabel(EsPoint cursor, EsSize clientSize, double fontSize = 12.0)
        => new("(100, 50)", cursor, clientSize, fontSize);

    [Fact]
    public void ComputeFrame_カーソルの右側へオフセットして配置する()
    {
        var label = CreateLabel(new EsPoint(100, 50), new EsSize(800, 600));

        var frame = label.ComputeFrame(new EsSize(120, 24));

        // カーソル (100, 50) + オフセット (8, 8)、背景は文字の実寸へ余白 4 を四方へ加えた大きさ
        Assert.Equal(new EsRect(108, 58, 128, 32), frame.Background);
        Assert.Equal(new EsPoint(112, 62), frame.TextOrigin);
        Assert.Equal(4.0, frame.TextOrigin.X - frame.Background.X);
        Assert.Equal(4.0, frame.TextOrigin.Y - frame.Background.Y);
    }

    [Fact]
    public void ComputeFrame_右端をはみ出す場合は領域内へクランプする()
    {
        var label = CreateLabel(new EsPoint(790, 50), new EsSize(800, 600));

        // x = 798 → クランプ: 800 - (120 + 4*2) = 672
        Assert.Equal(672, label.ComputeFrame(new EsSize(120, 24)).Background.X);
    }

    [Fact]
    public void ComputeFrame_下端をはみ出す場合は領域内へクランプする()
    {
        var label = CreateLabel(new EsPoint(100, 590), new EsSize(800, 600));

        // y = 598 → クランプ: 600 - (24 + 4*2) = 568
        Assert.Equal(568, label.ComputeFrame(new EsSize(120, 24)).Background.Y);
    }

    [Fact]
    public void ComputeFrame_クライアントサイズ0ではクランプしない()
    {
        var label = CreateLabel(new EsPoint(100, 50), new EsSize(0, 0));

        Assert.Equal(new EsPoint(108, 58), label.ComputeFrame(new EsSize(120, 24)).Background.TopLeft);
    }

    [Fact]
    public void ComputeFrame_ラベルが領域より大きい場合は0にクランプする()
    {
        var label = CreateLabel(new EsPoint(100, 50), new EsSize(50, 50));

        Assert.Equal(0, label.ComputeFrame(new EsSize(120, 24)).Background.X);
    }

    [Fact]
    public void ToElement_配置と外観を要素の指示へ写す()
    {
        var label = new MarqueeCoordLabel(
            "(100, 50)", new EsPoint(100, 50), new EsSize(800, 600), 12.0);

        var element = label.ToElement(new EsSize(120, 24), DrawingColor.White, DrawingColor.Black);

        Assert.Equal(new EsRect(108, 58, 128, 32), element.Bounds);
        Assert.Equal(new EsPoint(112, 62), element.TextOrigin);
        Assert.Equal("(100, 50)", element.Text);
        Assert.Equal(12.0, element.FontSize);
        Assert.Equal(DrawingColor.White, element.TextColor);
        Assert.Equal(DrawingColor.Black, element.BackgroundColor);
        Assert.Equal(0, element.CornerRadius);
    }
}
