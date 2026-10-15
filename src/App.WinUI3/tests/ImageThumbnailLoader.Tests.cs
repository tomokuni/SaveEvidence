using EsUtil.SaveEvidence.App.WinUI3.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinUI3.Tests;

/// <summary><see cref="ImageThumbnailLoader"/> の単体テスト（縮小デコードと失敗の扱い）。</summary>
/// <remarks>
/// 画像の生成はコアの保存（<c>EsBitmapImageWriter</c>）で行い、SkiaSharp の API をテストへ持ち込まない。
/// </remarks>
public class ImageThumbnailLoaderTests
{
    /// <summary>長辺が指定サイズへ収まることを検証する。</summary>
    /// <param name="maxSize">長辺の上限。</param>
    [Theory]
    [InlineData(16)]
    [InlineData(8)]
    internal void Load_長辺が指定サイズへ収まる(int maxSize)
    {
        var path = TestHelper.WriteImage(TestHelper.CreateTempPath(".png"), width: 64, height: 32);
        try
        {
            var thumbnail = ImageThumbnailLoader.Load(path, maxSize, capacity: 4);

            Assert.NotNull(thumbnail);
            Assert.Equal(maxSize, thumbnail.Width);
            Assert.Equal(maxSize / 2, thumbnail.Height);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>上限より小さい画像は等倍で返ることを検証する。</summary>
    [Fact]
    internal void Load_上限より小さい画像は等倍で返る()
    {
        var path = TestHelper.WriteImage(TestHelper.CreateTempPath(".png"), width: 8, height: 4);
        try
        {
            var thumbnail = ImageThumbnailLoader.Load(path, 64, capacity: 4);

            Assert.NotNull(thumbnail);
            Assert.Equal(8, thumbnail.Width);
            Assert.Equal(4, thumbnail.Height);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>拡張子が画像でないファイルは読み込まないことを検証する。</summary>
    [Fact]
    internal void Load_画像以外の拡張子は読み込まない()
    {
        var path = TestHelper.CreateTempPath(".txt");
        File.WriteAllText(path, "not an image");
        try
        {
            Assert.Null(ImageThumbnailLoader.Load(path, 16, capacity: 4));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>存在しないファイル・破損した画像は null を返すことを検証する。</summary>
    [Fact]
    internal void Load_読み込めないファイルはnullを返す()
    {
        Assert.Null(ImageThumbnailLoader.Load(TestHelper.CreateTempPath(".png"), 16, capacity: 4));

        var broken = TestHelper.CreateTempPath(".png");
        File.WriteAllText(broken, "not an image");
        try
        {
            Assert.Null(ImageThumbnailLoader.Load(broken, 16, capacity: 4));
        }
        finally
        {
            File.Delete(broken);
        }
    }

    /// <summary>同じファイルの 2 回目の読み込みが同じ実体を返すことを検証する（キャッシュ）。</summary>
    [Fact]
    internal void Load_同じ条件では同じ実体を返す()
    {
        var path = TestHelper.WriteImage(TestHelper.CreateTempPath(".png"), width: 32, height: 32);
        try
        {
            var first = ImageThumbnailLoader.Load(path, 16, capacity: 8);
            var second = ImageThumbnailLoader.Load(path, 16, capacity: 8);

            Assert.NotNull(first);
            Assert.Same(first, second);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>長辺の上限に 0 以下を渡しても 1 として扱われることを検証する。</summary>
    [Fact]
    internal void Load_上限が0以下でも読み込める()
    {
        var path = TestHelper.WriteImage(TestHelper.CreateTempPath(".png"), width: 32, height: 16);
        try
        {
            var thumbnail = ImageThumbnailLoader.Load(path, 0, capacity: 4);

            Assert.NotNull(thumbnail);
            Assert.Equal(1, thumbnail.Width);
        }
        finally
        {
            File.Delete(path);
        }
    }
}