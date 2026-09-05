using Avalonia;
using Avalonia.Headless.XUnit;
using Estable.AvaloniaUI.Loupe.Imaging;
using Xunit;

namespace Estable.AvaloniaUI.Loupe.Tests;

/// <summary><see cref="LoupeImageExtractor"/> の単体テスト（Bitmap 領域切り出し）。</summary>
/// <remarks>Avalonia ヘッドレス依存のため <see cref="AvaloniaHeadlessCollection"/> 内で直列実行する。</remarks>
[Collection("AvaloniaHeadless")]
public class LoupeImageExtractorTests
{
    [AvaloniaFact]
    public void ExtractRegionAt_中心領域を指定サイズで抽出する()
    {
        var src = TestHelper.CreateSolidBitmap(200, 200);

        using var result = LoupeImageExtractor.ExtractRegionAt(src, 100, 100, 40);

        Assert.Equal(40, result.PixelWidth);
        Assert.Equal(40, result.PixelHeight);
    }

    [AvaloniaFact]
    public void ExtractRegionAt_画像端の中心でも切り出せる()
    {
        var src = TestHelper.CreateSolidBitmap(100, 100);

        // 中心 (0,0) は範囲外パディングを含む
        using var result = LoupeImageExtractor.ExtractRegionAt(src, 0, 0, 20);

        Assert.Equal(20, result.PixelWidth);
        Assert.Equal(20, result.PixelHeight);
    }

    [AvaloniaFact]
    public void ExtractRegionAt_範囲外の中心でも透明パディングで切り出せる()
    {
        var src = TestHelper.CreateSolidBitmap(100, 100);

        // 中心 (200,200) は画像範囲外（完全に透明）
        using var result = LoupeImageExtractor.ExtractRegionAt(src, 200, 200, 20);

        Assert.Equal(20, result.PixelWidth);
        Assert.Equal(20, result.PixelHeight);
    }
}

/// <summary><see cref="LoupeImageExtractor.ComputeClipRect"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 座標計算のみを検証するため、Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// ヘッドレス直列コレクション（<see cref="LoupeImageExtractorTests"/>）から分離することで、テスト全体の高速化を図る。
/// </remarks>
public class LoupeImageExtractorStateTests
{
    [Fact]
    public void ComputeClipRect_画像内の中心は全体をクリップなしで返す()
    {
        var (clip, offset) = LoupeImageExtractor.ComputeClipRect(100, 100, 40, 200, 200);

        Assert.Equal(new PixelRect(80, 80, 40, 40), clip);
        Assert.Equal(0, offset);
    }

    [Fact]
    public void ComputeClipRect_画像端ではクリップしてオフセットを返す()
    {
        // 中心 (0,0)・サイズ 20: 領域は (-10,-10,20,20) → ソース内 (0,0,10,10) にクリップ
        var (clip, offset) = LoupeImageExtractor.ComputeClipRect(0, 0, 20, 100, 100);

        Assert.Equal(new PixelRect(0, 0, 10, 10), clip);
        Assert.Equal(10 * 20 * 4 + 10 * 4, offset);
    }

    [Fact]
    public void ComputeClipRect_画像端で右端もクリップする()
    {
        // 中心 (95,50)・サイズ 20: 領域は (85,40,20,20) → ソース内 (85,40,15,20) にクリップ（右端 105 > 100）
        var (clip, offset) = LoupeImageExtractor.ComputeClipRect(95, 50, 20, 100, 100);

        Assert.Equal(new PixelRect(85, 40, 15, 20), clip);
        Assert.Equal(0, offset);
    }

    [Fact]
    public void ComputeClipRect_完全に範囲外ならnullを返す()
    {
        var (clip, _) = LoupeImageExtractor.ComputeClipRect(200, 200, 20, 100, 100);

        Assert.Null(clip);
    }

    [Fact]
    public void ComputeClipRect_サイズ0は交差なしとみなす()
    {
        var (clip, _) = LoupeImageExtractor.ComputeClipRect(50, 50, 0, 100, 100);

        Assert.Null(clip);
    }
}
