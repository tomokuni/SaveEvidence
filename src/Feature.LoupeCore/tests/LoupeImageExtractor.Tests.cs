using Estable.Loupe.Imaging;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;
using SizeInt = System.Drawing.Size;

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

    /// <summary>切り出し元は切り出しサイズ・描画先は拡大率倍になることを検証する。</summary>
    /// <param name="captureSize">切り出した領域の一辺（ドメインピクセル）。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <param name="expectedDestination">期待する描画先の一辺（物理ピクセル）。</param>
    [Theory]
    [InlineData(16, 4, 64)]
    [InlineData(25, 4, 100)]
    [InlineData(1, 1, 1)]
    public void ComputeMagnifiedRects_切り出し元は等倍で描画先は拡大率倍になる(
        int captureSize, int zoomLevel, int expectedDestination)
    {
        var (source, destination) = LoupeImageExtractor.ComputeMagnifiedRects(new SizeInt(captureSize, captureSize), zoomLevel);

        // 切り出し元は左上原点の切り出しサイズ、描画先は左上原点の拡大率倍
        Assert.Equal(new EsRect(0, 0, captureSize, captureSize), source);
        Assert.Equal(new EsRect(0, 0, expectedDestination, expectedDestination), destination);
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

    [Fact]
    public void TryExtractRegionAt_ソースと中心が揃っていれば切り出す()
    {
        using var source = EsBitmap.Create(64, 64);

        // pixelSize=32, zoom=4 → 切り出し 8 px
        using var region = LoupeImageExtractor.TryExtractRegionAt(source, Request(new EsPoint(32, 32), 32, 4));

        Assert.NotNull(region);
        Assert.Equal(8, region.Size.Width);
        Assert.Equal(8, region.Size.Height);
    }

    [Fact]
    public void TryExtractRegionAt_ソース未設定ならnullを返す()
    {
        // null（画像なし）は切り出せない → 呼び出し側は市松模様のみを描画する
        Assert.Null(LoupeImageExtractor.TryExtractRegionAt(null, Request(new EsPoint(10, 10), 32, 4)));
    }

    [Fact]
    public void TryExtractRegionAt_中心未設定ならnullを返す()
    {
        using var source = EsBitmap.Create(64, 64);

        Assert.Null(LoupeImageExtractor.TryExtractRegionAt(source, Request(center: null, 32, 4)));
    }

    [Fact]
    public void TryExtractRegionAt_破棄済みソースなら例外にせずnullを返す()
    {
        // 描画中にソースが破棄された場合も描画を継続できる（例外を描画層へ伝播させない）
        var source = EsBitmap.Create(64, 64);
        source.Dispose();

        Assert.Null(LoupeImageExtractor.TryExtractRegionAt(source, Request(new EsPoint(10, 10), 32, 4)));
    }

    [Fact]
    public void TryExtractRegionAt_切り出し位置はExtractRegionAtと一致する()
    {
        // 中心 (10, 10)・pixelSize=16・zoom=4 → 切り出し 4 px、左上 (8, 8)
        using var source = EsBitmap.Create(64, 64);
        source.Clear(b: 0, g: 0, r: 255, a: 255);

        using var viaTry = LoupeImageExtractor.TryExtractRegionAt(source, Request(new EsPoint(10, 10), 16, 4));
        using var viaExtract = LoupeImageExtractor.ExtractRegionAt(source, 10, 10, 4);

        Assert.NotNull(viaTry);
        Assert.Equal(viaExtract.Size, viaTry.Size);
        Assert.Equal(viaExtract.GetPixel(0, 0), viaTry.GetPixel(0, 0));
        Assert.Equal(viaExtract.GetPixel(3, 3), viaTry.GetPixel(3, 3));
    }

    /// <summary>テスト用の描画入力を生成する（外観とスケーリングは既定値）。</summary>
    /// <param name="center">拡大中心（ドメイン座標）。未設定は null。</param>
    /// <param name="pixelSize">ルーペの一辺のサイズ（物理ピクセル）。</param>
    /// <param name="zoomLevel">拡大率。</param>
    /// <returns>描画入力。</returns>
    private static LoupeRenderRequest Request(EsPoint? center, int pixelSize, int zoomLevel)
        => new(center, pixelSize, zoomLevel, new LoupeAppearance(), 1.0);
}
