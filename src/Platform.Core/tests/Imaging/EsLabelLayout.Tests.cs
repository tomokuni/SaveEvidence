using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary><see cref="EsLabelLayout"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// ラベルの体裁（内側余白・行送り・配置基準）はコントロールに依存しない規則であり、<br/>
/// 各コントロールのコアが同じ値を使う。境界（領域サイズ 0 以下・余白 0・クランプ）を固定する。
/// </remarks>
public class EsLabelLayoutTests
{
    [Fact]
    public void Padding_ラベルの内側余白の既定値は4である()
        => Assert.Equal(4.0, EsLabelLayout.Padding);

    [Fact]
    public void ComputeBox_文字の実寸へ内側余白を四方へ加える()
    {
        var box = EsLabelLayout.ComputeBox(new EsPoint(10, 20), new EsSize(30, 8), paddingX: 4, paddingY: 1);

        Assert.Equal(6, box.X);
        Assert.Equal(19, box.Y);
        Assert.Equal(38, box.Width);
        Assert.Equal(10, box.Height);
    }

    [Fact]
    public void ComputeBoxAtAnchor_基準点を背景の左上に置く()
    {
        var box = EsLabelLayout.ComputeBoxAtAnchor(new EsPoint(10, 20), new EsSize(30, 8), paddingX: 4, paddingY: 4);

        Assert.Equal(10, box.X);
        Assert.Equal(20, box.Y);
        Assert.Equal(38, box.Width);
        Assert.Equal(16, box.Height);
    }

    [Fact]
    public void ComputeOffsetTextOrigin_基準点から内側余白だけ空ける()
    {
        var origin = EsLabelLayout.ComputeOffsetTextOrigin(new EsPoint(10, 20), paddingX: 4, paddingY: 4);

        Assert.Equal(14, origin.X);
        Assert.Equal(24, origin.Y);
    }

    [Fact]
    public void ComputeBoxAtAnchorとComputeOffsetTextOriginの余白は一致する()
    {
        var anchor = new EsPoint(10, 20);
        var textSize = new EsSize(30, 8);

        var box = EsLabelLayout.ComputeBoxAtAnchor(anchor, textSize, paddingX: 4, paddingY: 2);
        var origin = EsLabelLayout.ComputeOffsetTextOrigin(anchor, paddingX: 4, paddingY: 2);

        // 背景の四方の余白が等しく、文字が背景の右端・下端へ接しない
        Assert.Equal(origin.X - box.X, box.Right - (origin.X + textSize.Width));
        Assert.Equal(origin.Y - box.Y, box.Bottom - (origin.Y + textSize.Height));
    }

    [Fact]
    public void ComputeCursorLabelBox_カーソルの右下へオフセットして背景を求める()
    {
        var box = EsLabelLayout.ComputeCursorLabelBox(
            new EsPoint(10, 20), new EsSize(500, 500), new EsSize(32, 14), paddingX: 4, paddingY: 1, offset: 8);

        Assert.Equal(new EsRect(18, 28, 40, 16), box);
    }

    [Fact]
    public void ComputeCursorLabelBox_右端をはみ出す場合は領域内へクランプする()
    {
        var box = EsLabelLayout.ComputeCursorLabelBox(
            new EsPoint(490, 20), new EsSize(500, 500), new EsSize(32, 14), paddingX: 4, paddingY: 1, offset: 8);

        Assert.Equal(460, box.X);
        Assert.Equal(28, box.Y);
    }

    [Fact]
    public void ComputeCursorLabelBox_下端をはみ出す場合は領域内へクランプする()
    {
        var box = EsLabelLayout.ComputeCursorLabelBox(
            new EsPoint(10, 495), new EsSize(500, 500), new EsSize(32, 14), paddingX: 4, paddingY: 1, offset: 8);

        Assert.Equal(18, box.X);
        Assert.Equal(484, box.Y);
    }

    [Fact]
    public void ComputeCursorLabelBox_ラベルが領域より大きい場合は0へクランプする()
    {
        var box = EsLabelLayout.ComputeCursorLabelBox(
            new EsPoint(10, 20), new EsSize(30, 12), new EsSize(32, 14), paddingX: 4, paddingY: 1, offset: 8);

        Assert.Equal(0, box.X);
        Assert.Equal(0, box.Y);
    }

    [Theory]
    [InlineData(0, 500)]
    [InlineData(500, 0)]
    [InlineData(-1, -1)]
    public void ComputeCursorLabelBox_領域サイズが0以下なら軸ごとにクランプしない(double areaWidth, double areaHeight)
    {
        var box = EsLabelLayout.ComputeCursorLabelBox(
            new EsPoint(10, 20), new EsSize(areaWidth, areaHeight), new EsSize(32, 14),
            paddingX: 4, paddingY: 1, offset: 8);

        Assert.Equal(18, box.X);
        Assert.Equal(28, box.Y);
    }

    [Fact]
    public void ComputeTopCenterBox_領域の中央へ背景をセンタリングする()
    {
        var box = EsLabelLayout.ComputeTopCenterBox(
            areaWidth: 160, new EsSize(40, 14), paddingX: 4, paddingY: 1, topMargin: 2);

        // 背景の幅は 40 + 4*2 = 48。中央寄せなので X = (160 - 48) / 2 = 56
        Assert.Equal(56, box.X);
        Assert.Equal(2, box.Y);
        Assert.Equal(48, box.Width);
        Assert.Equal(16, box.Height);
    }

    [Fact]
    public void ComputeTopCenterBox_背景が領域より広い場合は負のXになる()
    {
        var box = EsLabelLayout.ComputeTopCenterBox(
            areaWidth: 30, new EsSize(40, 14), paddingX: 4, paddingY: 1, topMargin: 0);

        Assert.Equal(-9, box.X);
    }

    [Fact]
    public void ComputeTopCenterBoxの文字原点も背景の中央に揃う()
    {
        var box = EsLabelLayout.ComputeTopCenterBox(
            areaWidth: 160, new EsSize(40, 14), paddingX: 4, paddingY: 1, topMargin: 2);
        var origin = EsLabelLayout.ComputeOffsetTextOrigin(box.TopLeft, paddingX: 4, paddingY: 1);

        // 背景の中心と文字の中心が一致する
        Assert.Equal(box.Center.X, origin.X + 40 / 2);
    }

    [Fact]
    public void NextTop_行の高さと内側余白の2倍だけ下がる()
    {
        Assert.Equal(14 + 8, EsLabelLayout.NextTop(top: 0, lineHeight: 14, paddingY: 4));
        Assert.Equal(40 + 12 + 8, EsLabelLayout.NextTop(top: 40, lineHeight: 12, paddingY: 4));
    }
}
