using EsUtil.SaveEvidence.App.MewUI.Services;
using SkiaSharp;
using Xunit;

namespace EsUtil.SaveEvidence.App.MewUI.Tests;

/// <summary><see cref="MewUIImageWriter"/> のテスト。</summary>
public sealed class MewUIImageWriterTests
{
    /// <summary>拡張子から保存形式が決まり、ファイルが生成されて画素数が保たれることを検証する。</summary>
    /// <param name="extension">保存先の拡張子。</param>
    [Theory]
    [InlineData(".png")]
    [InlineData(".PNG")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".JPG")]
    [InlineData(".bmp")]
    [InlineData(".gif")]
    internal void Save_拡張子に応じた形式で保存する(string extension)
    {
        var path = TestHelper.CreateTempPath(extension);

        try
        {
            new MewUIImageWriter().Save(TestHelper.CreateBitmap(8, 6), path);

            Assert.True(File.Exists(path));
            using var decoded = SKBitmap.Decode(path);
            Assert.NotNull(decoded);
            Assert.Equal(8, decoded.Width);
            Assert.Equal(6, decoded.Height);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Skia がデコードできない形式（TIFF）でも保存できることを検証する。</summary>
    /// <param name="extension">保存先の拡張子。</param>
    [Theory]
    [InlineData(".tif")]
    [InlineData(".tiff")]
    internal void Save_TIFFでも保存できる(string extension)
    {
        var path = TestHelper.CreateTempPath(extension);

        try
        {
            new MewUIImageWriter().Save(TestHelper.CreateBitmap(), path);

            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>未対応の拡張子（WebP・不明・拡張子なし）は例外になることを検証する。</summary>
    /// <param name="extension">保存先の拡張子。</param>
    [Theory]
    [InlineData(".webp")]
    [InlineData(".unknown")]
    [InlineData("")]
    internal void Save_未対応の拡張子は例外(string extension)
    {
        var path = TestHelper.CreateTempPath(extension);

        Assert.Throws<NotSupportedException>(() => new MewUIImageWriter().Save(TestHelper.CreateBitmap(), path));
    }

    /// <summary>保存した画像が元の画素数を保持することを検証する。</summary>
    [Fact]
    internal void Save_画素数を保持する()
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
    internal void Save_既存ファイルを上書きする()
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
    internal void Save_画像がnullなら例外()
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
    internal void Save_パスがnullなら例外()
    {
        Assert.Throws<ArgumentNullException>(() => new MewUIImageWriter().Save(TestHelper.CreateBitmap(), null!));
    }
}
