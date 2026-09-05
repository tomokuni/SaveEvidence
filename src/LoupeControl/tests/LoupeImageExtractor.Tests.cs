using Estable.AvaloniaUI.Loupe.Imaging;
using Xunit;

namespace Estable.AvaloniaUI.Loupe.Tests;

/// <summary><see cref="LoupeImageExtractor"/> の単体テスト（切り出し領域サイズ計算）。</summary>
/// <remarks>純粋計算のためヘッドレス不要（<c>[Fact]</c>・並列実行可能）。</remarks>
public class LoupeImageExtractorTests
{
    [Theory]
    [InlineData(100, 4, 25)]
    [InlineData(100, 3, 34)]
    [InlineData(64, 4, 16)]
    [InlineData(10, 64, 1)]
    public void ComputeCaptureSize_切り上げてドメインピクセルを返す(int pixelSize, int zoomLevel, int expected)
    {
        Assert.Equal(expected, LoupeImageExtractor.ComputeCaptureSize(pixelSize, zoomLevel));
    }
}
