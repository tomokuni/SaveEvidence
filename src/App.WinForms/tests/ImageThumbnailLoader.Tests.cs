using EsUtil.SaveEvidence.App.WinForms.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinForms.Tests;

/// <summary><see cref="ImageThumbnailLoader"/> の縮小読み込みとキャッシュを検証するテスト。</summary>
public class ImageThumbnailLoaderTests
{
    /// <summary>長辺が上限を超えないサムネイルを返すことを検証する。</summary>
    [Fact]
    public void 長辺が上限を超えないサムネイルを返す()
    {
        var path = TestHelper.WriteImageFile(16, 8);
        try
        {
            // 返る画像はキャッシュが所有する（呼び出し側は破棄しない）
            var thumbnail = ImageThumbnailLoader.Load(path, 8, 4);

            Assert.NotNull(thumbnail);
            Assert.True(Math.Max(thumbnail.Width, thumbnail.Height) <= 8);
            Assert.Equal(8, thumbnail.Width);
            Assert.Equal(4, thumbnail.Height);
        }
        finally
        {
            TestHelper.DeleteFile(path);
        }
    }

    /// <summary>上限より小さい画像は等倍で返すことを検証する。</summary>
    [Fact]
    public void 上限より小さい画像は等倍で返す()
    {
        var path = TestHelper.WriteImageFile(4, 2);
        try
        {
            var thumbnail = ImageThumbnailLoader.Load(path, 32, 4);

            Assert.NotNull(thumbnail);
            Assert.Equal(4, thumbnail.Width);
            Assert.Equal(2, thumbnail.Height);
        }
        finally
        {
            TestHelper.DeleteFile(path);
        }
    }

    /// <summary>画像でない拡張子は読み込まないことを検証する。</summary>
    [Fact]
    public void 画像でない拡張子は読み込まない()
    {
        var path = TestHelper.CreateTempPath(".txt");
        try
        {
            File.WriteAllText(path, "not an image");

            Assert.Null(ImageThumbnailLoader.Load(path, 32, 4));
        }
        finally
        {
            TestHelper.DeleteFile(path);
        }
    }

    /// <summary>読み込めないファイルは null を返すことを検証する。</summary>
    [Fact]
    public void 読み込めないファイルはnullを返す()
    {
        var path = TestHelper.CreateTempPath(".png");
        try
        {
            File.WriteAllText(path, "broken");

            Assert.Null(ImageThumbnailLoader.Load(path, 32, 4));
        }
        finally
        {
            TestHelper.DeleteFile(path);
        }
    }

    /// <summary>同じファイルとサイズでは同じ結果を共有することを検証する。</summary>
    [Fact]
    public void 同じ条件では同じ結果を共有する()
    {
        var path = TestHelper.WriteImageFile(16, 16);
        try
        {
            var first = ImageThumbnailLoader.Load(path, 8, 4);
            var second = ImageThumbnailLoader.Load(path, 8, 4);

            Assert.NotNull(first);
            Assert.Same(first, second);
        }
        finally
        {
            TestHelper.DeleteFile(path);
        }
    }

    /// <summary>コアの画像を表示用のビットマップへ縮小できることを検証する。</summary>
    [Fact]
    public void コアの画像を表示用のビットマップへ縮小できる()
    {
        using var source = TestHelper.CreateEsBitmap(32, 8);

        using var bitmap = ImageThumbnailLoader.ToThumbnailBitmap(source, 8);

        Assert.Equal(8, bitmap.Width);
        Assert.Equal(2, bitmap.Height);
    }

    /// <summary>上限より小さい画像は拡大せずに複製することを検証する。</summary>
    [Fact]
    public void 上限より小さい画像は拡大しない()
    {
        using var source = TestHelper.CreateEsBitmap(4, 4);

        using var bitmap = ImageThumbnailLoader.ToThumbnailBitmap(source, 64);

        Assert.Equal(4, bitmap.Width);
        Assert.Equal(4, bitmap.Height);
    }
}
