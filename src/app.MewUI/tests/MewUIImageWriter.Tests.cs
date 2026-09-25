using EsUtil.SaveEvidence.MewUI.Services;
using SkiaSharp;
using Xunit;

namespace EsUtil.SaveEvidence.MewUI.Tests;

/// <summary><see cref="MewUIImageWriter"/> のテスト。</summary>
public sealed class MewUIImageWriterTests
{
    /// <summary>拡張子から保存形式が決まり、判別できない拡張子は PNG になることを検証する。</summary>
    /// <param name="extension">保存先の拡張子。</param>
    /// <param name="expected">期待する保存形式。</param>
    [Theory]
    [InlineData(".png", SKEncodedImageFormat.Png)]
    [InlineData(".PNG", SKEncodedImageFormat.Png)]
    [InlineData(".jpg", SKEncodedImageFormat.Jpeg)]
    [InlineData(".jpeg", SKEncodedImageFormat.Jpeg)]
    [InlineData(".JPG", SKEncodedImageFormat.Jpeg)]
    [InlineData(".webp", SKEncodedImageFormat.Webp)]
    [InlineData(".bmp", SKEncodedImageFormat.Png)]
    [InlineData(".unknown", SKEncodedImageFormat.Png)]
    [InlineData("", SKEncodedImageFormat.Png)]
    public void Save_拡張子に応じた形式で保存する(string extension, SKEncodedImageFormat expected)
    {
        var path = TestHelper.CreateTempPath(extension);

        try
        {
            new MewUIImageWriter().Save(TestHelper.CreateBitmap(8, 6), path);

            Assert.True(File.Exists(path));
            Assert.Equal(expected, TestHelper.ReadFormat(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>保存した画像が元の画素数を保持することを検証する。</summary>
    [Fact]
    public void Save_画素数を保持する()
    {
        var path = TestHelper.CreateTempPath(".png");

        try
        {
            new MewUIImageWriter().Save(TestHelper.CreateBitmap(10, 5), path);

            using var decoded = SKBitmap.Decode(path);

            Assert.NotNull(decoded);
            Assert.Equal(10, decoded.Width);
            Assert.Equal(5, decoded.Height);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>既存ファイルを上書きできることを検証する。</summary>
    [Fact]
    public void Save_既存ファイルを上書きする()
    {
        var path = TestHelper.CreateTempPath(".png");

        try
        {
            File.WriteAllText(path, "既存の内容");

            new MewUIImageWriter().Save(TestHelper.CreateBitmap(), path);

            Assert.Equal(SKEncodedImageFormat.Png, TestHelper.ReadFormat(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>null 引数を拒否することを検証する。</summary>
    [Fact]
    public void Save_画像がnullなら例外()
    {
        var path = TestHelper.CreateTempPath(".png");

        try
        {
            Assert.Throws<ArgumentNullException>(() => new MewUIImageWriter().Save(null!, path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>null のパスを拒否することを検証する。</summary>
    [Fact]
    public void Save_パスがnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() => new MewUIImageWriter().Save(TestHelper.CreateBitmap(), null!));
    }
}
