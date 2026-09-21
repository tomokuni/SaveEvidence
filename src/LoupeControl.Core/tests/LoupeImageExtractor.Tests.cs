using Estable.Loupe.Imaging;
using Estable.Platform.Imaging;
using Xunit;

namespace Estable.Loupe.Tests;

/// <summary><see cref="LoupeImageExtractor"/> の単体テスト（切り出し領域サイズ計算・領域切り出し）。</summary>
/// <remarks>
/// 純粋計算と <see cref="EsBitmap"/> のみで完結するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 描画を伴うテスト（コントロール生成・レンダラー描画）は UI 側のテスト（ヘッドレス直列）が担う。
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

    /// <summary>拡大中心から切り出し領域の左上を計算することを検証する（ExtractRegionAt と同一の整数計算）。</summary>
    [Theory]
    [InlineData(50, 50, 4, 48, 48)]
    [InlineData(0, 0, 4, -2, -2)]
    [InlineData(101, 99, 25, 89, 87)]
    public void ComputeCaptureTopLeft_中心を左上として指定サイズ領域を計算する(
        int centerX, int centerY, int size, int expectedLeft, int expectedTop)
    {
        var topLeft = LoupeImageExtractor.ComputeCaptureTopLeft(centerX, centerY, size);

        Assert.Equal(expectedLeft, (int)topLeft.X);
        Assert.Equal(expectedTop, (int)topLeft.Y);
    }

    /// <summary>中心座標を左上として指定サイズの正方形領域を切り出すことを検証する。</summary>
    [Fact]
    public void ExtractRegionAt_中心を左上として指定サイズで切り出す()
    {
        // ソース 10x10、中心 (5,5)、サイズ 4 → 左上 (3,3) の 4x4 を切り出す
        using var source = EsBitmap.Create(10, 10);

        using var region = LoupeImageExtractor.ExtractRegionAt(source, 5, 5, 4);

        Assert.Equal(4, region.Size.Width);
        Assert.Equal(4, region.Size.Height);
        // 領域 (0,0) はソース (3,3) のピクセルと一致する
        Assert.Equal(source.GetPixel(3, 3), region.GetPixel(0, 0));
    }

    /// <summary>ソースの左端・上端を越える領域では透明パディングされ、位置関係が保たれることを検証する。</summary>
    [Fact]
    public void ExtractRegionAt_ソース端では透明パディングされる()
    {
        // ソース 10x10、中心 (0,0)、サイズ 4 → 左上 (-2,-2)。ソースと交差するのは (0,0)〜(1,1) のみ
        using var source = EsBitmap.Create(10, 10);
        source.Clear(b: 0, g: 0, r: 255, a: 255); // 赤（位置関係を判定できるよう単色で塗る）

        using var region = LoupeImageExtractor.ExtractRegionAt(source, 0, 0, 4);

        Assert.Equal(4, region.Size.Width);
        Assert.Equal(4, region.Size.Height);
        // ソース範囲外（左上の 2x2）は透明（A=0）にパディングされる
        Assert.True(region.GetPixel(0, 0).A == 0, $"範囲外ピクセルが透明でない: A={region.GetPixel(0, 0).A}");
        Assert.True(region.GetPixel(1, 1).A == 0, $"範囲外ピクセルが透明でない: A={region.GetPixel(1, 1).A}");
        // 交差する部分は位置関係を保つ（ソース (0,0) は領域 (2,2) へ写る）
        Assert.Equal(source.GetPixel(0, 0), region.GetPixel(2, 2));
        Assert.Equal(source.GetPixel(1, 1), region.GetPixel(3, 3));
    }
}
