using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.Common.Imaging;
using Xunit;

namespace EsUtil.SaveEvidence.Common.Tests.Imaging;

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

    [Theory]
    [InlineData(EsLabelAlignX.Left, 10)]
    [InlineData(EsLabelAlignX.Center, -9)]
    [InlineData(EsLabelAlignX.Right, -28)]
    public void ComputeAlignedBox_水平の基準位置を合わせる(EsLabelAlignX horizontal, double expectedX)
    {
        var box = EsLabelLayout.ComputeAlignedBox(
            new EsPoint(10, 20), new EsSize(0, 0), new EsSize(30, 8),
            horizontal, EsLabelAlignY.Top, paddingX: 4, paddingY: 4);

        Assert.Equal(expectedX, box.X);
        Assert.Equal(20, box.Y);
        Assert.Equal(38, box.Width);
        Assert.Equal(16, box.Height);
    }

    [Theory]
    [InlineData(EsLabelAlignY.Top, 20)]
    [InlineData(EsLabelAlignY.Middle, 12)]
    [InlineData(EsLabelAlignY.Bottom, 4)]
    public void ComputeAlignedBox_垂直の基準位置を合わせる(EsLabelAlignY vertical, double expectedY)
    {
        var box = EsLabelLayout.ComputeAlignedBox(
            new EsPoint(10, 20), new EsSize(0, 0), new EsSize(30, 8),
            EsLabelAlignX.Left, vertical, paddingX: 4, paddingY: 4);

        Assert.Equal(10, box.X);
        Assert.Equal(expectedY, box.Y);
    }

    [Fact]
    public void ComputeAlignedBox_上端中央を基準にできる()
    {
        // ルーペの中心座標ラベル（領域の上端中央）の指定
        var box = EsLabelLayout.ComputeAlignedBox(
            new EsPoint(80, 2), new EsSize(0, 0), new EsSize(40, 14),
            EsLabelAlignX.Center, EsLabelAlignY.Top, paddingX: 4, paddingY: 1);

        Assert.Equal(56, box.X);
        Assert.Equal(2, box.Y);
        Assert.Equal(48, box.Width);
        Assert.Equal(16, box.Height);
    }

    [Fact]
    public void ComputeAlignedBox_領域の右端と下端をはみ出す場合はクランプする()
    {
        var box = EsLabelLayout.ComputeAlignedBox(
            new EsPoint(490, 495), new EsSize(500, 500), new EsSize(32, 14),
            EsLabelAlignX.Left, EsLabelAlignY.Top, paddingX: 4, paddingY: 1);

        Assert.Equal(460, box.X);
        Assert.Equal(484, box.Y);
    }

    [Fact]
    public void ComputeAlignedBox_ラベルが領域より大きい場合は0へクランプする()
    {
        var box = EsLabelLayout.ComputeAlignedBox(
            new EsPoint(10, 20), new EsSize(30, 12), new EsSize(32, 14),
            EsLabelAlignX.Left, EsLabelAlignY.Top, paddingX: 4, paddingY: 1);

        Assert.Equal(0, box.X);
        Assert.Equal(0, box.Y);
    }

    [Theory]
    [InlineData(0, 500)]
    [InlineData(500, 0)]
    [InlineData(-1, -1)]
    public void ComputeAlignedBox_領域サイズが0以下なら軸ごとにクランプしない(double areaWidth, double areaHeight)
    {
        var box = EsLabelLayout.ComputeAlignedBox(
            new EsPoint(10, 20), new EsSize(areaWidth, areaHeight), new EsSize(32, 14),
            EsLabelAlignX.Left, EsLabelAlignY.Top, paddingX: 4, paddingY: 1);

        Assert.Equal(10, box.X);
        Assert.Equal(20, box.Y);
    }

    [Fact]
    public void ComputeOffsetTextOrigin_背景の左上から内側余白だけ空ける()
    {
        var origin = EsLabelLayout.ComputeOffsetTextOrigin(new EsPoint(10, 20), paddingX: 4, paddingY: 4);

        Assert.Equal(14, origin.X);
        Assert.Equal(24, origin.Y);
    }

    [Fact]
    public void ComputeAlignedBoxとComputeOffsetTextOriginの余白は一致する()
    {
        var anchor = new EsPoint(10, 20);
        var textSize = new EsSize(30, 8);

        var box = EsLabelLayout.ComputeAlignedBox(
            anchor, new EsSize(0, 0), textSize, EsLabelAlignX.Left, EsLabelAlignY.Top, paddingX: 4, paddingY: 2);
        var origin = EsLabelLayout.ComputeOffsetTextOrigin(box.TopLeft, paddingX: 4, paddingY: 2);

        // 背景の四方の余白が等しく、文字が背景の右端・下端へ接しない
        Assert.Equal(origin.X - box.X, box.Right - (origin.X + textSize.Width));
        Assert.Equal(origin.Y - box.Y, box.Bottom - (origin.Y + textSize.Height));
    }

    [Fact]
    public void NextTop_行の高さと内側余白の2倍だけ下がる()
    {
        Assert.Equal(14 + 8, EsLabelLayout.NextTop(top: 0, lineHeight: 14, paddingY: 4));
        Assert.Equal(40 + 12 + 8, EsLabelLayout.NextTop(top: 40, lineHeight: 12, paddingY: 4));
    }
}
