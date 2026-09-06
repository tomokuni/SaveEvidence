using Estable.AvaloniaUI.Loupe.Imaging;
using Estable.AvaloniaUI.Platform.Imaging;
using Xunit;

namespace Estable.AvaloniaUI.Loupe.Tests;

/// <summary><see cref="LoupeImageExtractor"/> の単体テスト（切り出し領域サイズ計算・領域切り出し）。</summary>
/// <remarks>
/// <c>ComputeCaptureSize</c> / <c>ExtractRegionAt</c> は純粋計算・<see cref="PbBitmap"/> のみで完結するため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する（テスト全体の高速化）。<br/>
/// ヘッドレス必須のテスト（コントロール生成・<see cref="LoupeRenderer"/> 描画）は <see cref="LoupeControlTests"/> が直列コレクションで検証する。
/// </remarks>
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

    /// <summary>中心座標を左上として指定サイズの正方形領域を切り出すことを検証する。</summary>
    [Fact]
    public void ExtractRegionAt_中心を左上として指定サイズで切り出す()
    {
        // ソース 10x10、中心 (5,5)、サイズ 4 → 左上 (3,3) の 4x4 を切り出す
        using var source = PbBitmap.Create(10, 10);

        using var region = LoupeImageExtractor.ExtractRegionAt(source, 5, 5, 4);

        Assert.Equal(4, region.Size.Width);
        Assert.Equal(4, region.Size.Height);
        // 領域 (0,0) はソース (3,3) のピクセルと一致する
        Assert.Equal(source.GetPixel(3, 3), region.GetPixel(0, 0));
    }

    /// <summary>ソース端を越える領域では透明パディングされることを検証する。</summary>
    [Fact]
    public void ExtractRegionAt_ソース端では透明パディングされる()
    {
        // ソース 10x10、中心 (0,0)、サイズ 4 → 左上 (-2,-2)。ソースと交差するのは (0,0)〜(1,1) のみ
        using var source = PbBitmap.Create(10, 10);

        using var region = LoupeImageExtractor.ExtractRegionAt(source, 0, 0, 4);

        Assert.Equal(4, region.Size.Width);
        Assert.Equal(4, region.Size.Height);
        // 領域 (0,0) はソース (0,0) のピクセルと一致する
        Assert.Equal(source.GetPixel(0, 0), region.GetPixel(0, 0));
        // ソース範囲外のピクセルは透明（A=0）にパディングされる
        var transparent = region.GetPixel(3, 3);
        Assert.True(transparent.A == 0, $"範囲外ピクセルが透明でない: A={transparent.A}");
    }
}
