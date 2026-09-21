using Xunit;

namespace Estable.CaptureSession.Tests;

/// <summary><see cref="ImageDrawMath"/> の単体テスト（Uniform 収め・Fill・空の扱い）。</summary>
/// <remarks>
/// 画像の配置はサムネイル（Uniform）とキャプチャのオーバーレイ（Fill）で共用するため、境界値を固定する。
/// </remarks>
public class ImageDrawMathTests
{
    [Fact]
    public void ComputeDestination_Uniformは縦横比を保って中央へ配置する()
    {
        // 400x200 の画像を 100x100 の領域へ収める → 100x50（上下に 25 の余白）
        var rect = ImageDrawMath.ComputeDestination(400, 200, 100, 100, uniform: true);

        Assert.Equal(0f, rect.Left);
        Assert.Equal(25f, rect.Top);
        Assert.Equal(100f, rect.Right);
        Assert.Equal(75f, rect.Bottom);
    }

    [Fact]
    public void ComputeDestination_Uniformは縦長の画像を高さ基準で収める()
    {
        // 200x400 の画像を 100x100 の領域へ収める → 50x100（左右に 25 の余白）
        var rect = ImageDrawMath.ComputeDestination(200, 400, 100, 100, uniform: true);

        Assert.Equal(25f, rect.Left);
        Assert.Equal(0f, rect.Top);
        Assert.Equal(75f, rect.Right);
        Assert.Equal(100f, rect.Bottom);
    }

    [Fact]
    public void ComputeDestination_縦横比が一致すれば余白なしで収まる()
    {
        var rect = ImageDrawMath.ComputeDestination(1920, 1080, 960, 540, uniform: true);

        Assert.Equal(0f, rect.Left);
        Assert.Equal(0f, rect.Top);
        Assert.Equal(960f, rect.Right);
        Assert.Equal(540f, rect.Bottom);
    }

    [Fact]
    public void ComputeDestination_Fillは領域全体へ引き伸ばす()
    {
        var rect = ImageDrawMath.ComputeDestination(400, 200, 100, 100, uniform: false);

        Assert.Equal(0f, rect.Left);
        Assert.Equal(0f, rect.Top);
        Assert.Equal(100f, rect.Right);
        Assert.Equal(100f, rect.Bottom);
    }

    [Theory]
    [InlineData(0, 100, 100, 100)]
    [InlineData(100, 0, 100, 100)]
    [InlineData(100, 100, 0, 100)]
    [InlineData(100, 100, 100, 0)]
    [InlineData(-10, 100, 100, 100)]
    [InlineData(100, 100, -10, 100)]
    public void ComputeDestination_空の入力は空の矩形を返す(
        int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        Assert.True(ImageDrawMath.ComputeDestination(sourceWidth, sourceHeight, targetWidth, targetHeight, uniform: true).IsEmpty);
        Assert.True(ImageDrawMath.ComputeDestination(sourceWidth, sourceHeight, targetWidth, targetHeight, uniform: false).IsEmpty);
    }

    [Fact]
    public void ComputeDestination_元画像より大きい領域へはみ出さない()
    {
        // 40x20 の画像を 200x200 の領域へ Uniform で収める → 200x100（拡大はされる）
        var rect = ImageDrawMath.ComputeDestination(40, 20, 200, 200, uniform: true);

        Assert.Equal(0f, rect.Left);
        Assert.Equal(50f, rect.Top);
        Assert.Equal(200f, rect.Right);
        Assert.Equal(150f, rect.Bottom);
    }
}
