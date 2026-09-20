using Estable.SaveEvidence.Models;
using Xunit;

namespace Estable.SaveEvidence.Tests;

/// <summary><see cref="ImageFileExtensions"/> の単体テスト（拡張子判定の境界）。</summary>
/// <remarks>
/// 拡張子の比較が大文字・小文字を区別しないこと、拡張子が無いパスを画像と見なさないことを固定する。
/// </remarks>
public class ImageFileExtensionsTests
{
    [Theory]
    [InlineData("a.png")]
    [InlineData("a.jpg")]
    [InlineData("a.jpeg")]
    [InlineData("a.bmp")]
    [InlineData("a.gif")]
    [InlineData("a.tiff")]
    [InlineData("a.tif")]
    [InlineData("a.webp")]
    [InlineData(@"C:\folder\a.PNG")]
    [InlineData("a.JpEg")]
    [InlineData("name.with.dots.jpg")]
    [InlineData(".png")]
    public void IsImageFile_画像の拡張子はtrue(string path)
        => Assert.True(ImageFileExtensions.IsImageFile(path));

    [Theory]
    [InlineData("a.txt")]
    [InlineData("a")]
    [InlineData("a.")]
    [InlineData(@"C:\folder\a")]
    [InlineData("a.png.txt")]
    [InlineData("")]
    [InlineData(@"C:\folder\")]
    public void IsImageFile_画像以外の拡張子はfalse(string path)
        => Assert.False(ImageFileExtensions.IsImageFile(path));

    [Fact]
    public void All_共有された集合を書き換えられない()
    {
        // IReadOnlyCollection で公開するため、要素を追加する API は公開されない
        Assert.Equal(8, ImageFileExtensions.All.Count);
        Assert.Contains(".png", ImageFileExtensions.All);
        Assert.Contains(".webp", ImageFileExtensions.All);
    }
}
