using EsUtil.SaveEvidence.App.MewUI.Services;
using SkiaSharp;
using Xunit;
using EsUtil.Platform.Core;

namespace EsUtil.SaveEvidence.App.MewUI.Tests;

/// <summary><see cref="ImageThumbnailLoader"/> の単体テスト（縮小デコード・キャッシュ・失敗時の扱い）。</summary>
/// <remarks>
/// 実ファイルへ書き出した画像を読み込み、寸法・アルファ・同一性（キャッシュ）を検証する。<br/>
/// 縮小デコードは <see cref="SKCodec"/> へ縮小後の寸法を渡す実装のため、寸法が要求どおりになることを固定する。
/// </remarks>
public class ImageThumbnailLoaderTests : IDisposable
{
    /// <summary>テストで作成したファイルのパス（終了時に削除する）。</summary>
    private readonly List<string> _files = [];

    /// <summary>テスト用の画像ファイルを書き出す。</summary>
    /// <param name="width">幅（1 以上）。</param>
    /// <param name="height">高さ（1 以上）。</param>
    /// <param name="color">塗りつぶし色。</param>
    /// <param name="extension">拡張子（保存形式を決める）。</param>
    /// <returns>書き出したファイルのパス。</returns>
    private string WriteImage(int width, int height, SKColor color, string extension = ".png")
    {
        var path = TestHelper.CreateTempPath(extension);

        // 所有権は FromSKBitmap へ移るため using を付けない
        var source = new SKBitmap(width, height);
        source.Erase(color);
        using var image = SKImage.FromBitmap(source);
        using var encoded = image.Encode(extension == ".jpg" ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, 100);
        using (var stream = File.Create(path))
        {
            encoded.SaveTo(stream);
        }

        _files.Add(path);
        return path;
    }

    /// <summary>テストで作成した一時ファイルを削除する。</summary>
    public void Dispose()
    {
        foreach (var path in _files)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
            {
                // 一時ファイルの削除失敗はテスト結果に影響しない
            }
        }

        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData(400, 200, 100, 100, 50)]
    [InlineData(200, 400, 100, 50, 100)]
    [InlineData(1000, 1000, 320, 320, 320)]
    internal void Load_長辺が指定サイズへ縮小される(
        int sourceWidth, int sourceHeight, int maxSize, int expectedWidth, int expectedHeight)
    {
        var path = WriteImage(sourceWidth, sourceHeight, SKColors.Red);

        var thumbnail = ImageThumbnailLoader.Load(path, maxSize);

        Assert.NotNull(thumbnail);
        Assert.Equal(expectedWidth, thumbnail!.Width);
        Assert.Equal(expectedHeight, thumbnail.Height);
    }

    [Fact]
    internal void Load_指定サイズ以下の画像は拡大しない()
    {
        var path = WriteImage(40, 20, SKColors.Red);

        var thumbnail = ImageThumbnailLoader.Load(path, 320);

        Assert.NotNull(thumbnail);
        Assert.Equal(40, thumbnail!.Width);
        Assert.Equal(20, thumbnail.Height);
    }

    [Fact]
    internal void Load_半透明PNGのアルファが保持される()
    {
        // 半透明（アルファ 128）の赤を 1x1 で書き出し、デコード後もアルファが残ることを確認する
        var path = TestHelper.CreateTempPath(".png");
        var source = new SKBitmap(new SKImageInfo(1, 1, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        source.SetPixel(0, 0, new SKColor(255, 0, 0, 128));
        using (var image = SKImage.FromBitmap(source))
        using (var encoded = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = File.Create(path))
        {
            encoded.SaveTo(stream);
        }

        _files.Add(path);

        var thumbnail = ImageThumbnailLoader.Load(path, 32);

        Assert.NotNull(thumbnail);
        var (_, _, _, alpha) = thumbnail!.GetPixel(0, 0);
        Assert.InRange(alpha, 120, 136);
    }

    [Fact]
    internal void Load_Jpegのサムネイルも読み込める()
    {
        var path = WriteImage(300, 150, SKColors.Green, ".jpg");

        var thumbnail = ImageThumbnailLoader.Load(path, 150);

        Assert.NotNull(thumbnail);
        Assert.Equal(150, thumbnail!.Width);
        Assert.Equal(75, thumbnail.Height);
    }

    [Fact]
    internal void Load_同じ条件では同一のインスタンスを共有する()
    {
        var path = WriteImage(400, 200, SKColors.Red);

        var first = ImageThumbnailLoader.Load(path, 100);
        var second = ImageThumbnailLoader.Load(path, 100);

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    internal void Load_サイズが異なれば別のインスタンスを返す()
    {
        var path = WriteImage(400, 200, SKColors.Red);

        var small = ImageThumbnailLoader.Load(path, 100);
        var large = ImageThumbnailLoader.Load(path, 200);

        Assert.NotNull(small);
        Assert.NotNull(large);
        Assert.NotSame(small, large);
        Assert.Equal(100, small!.Width);
        Assert.Equal(200, large!.Width);
    }

    [Fact]
    internal void Load_存在しないファイルはnullを返す()
    {
        var path = TestHelper.CreateTempPath(".png");

        Assert.Null(ImageThumbnailLoader.Load(path, 100));
    }

    [Fact]
    internal void Load_画像でないファイルはnullを返す()
    {
        var path = TestHelper.CreateTempPath(".txt");
        File.WriteAllText(path, "これは画像ではありません");
        _files.Add(path);

        Assert.Null(ImageThumbnailLoader.Load(path, 100));
    }

    [Fact]
    internal void Load_壊れた画像データはnullを返す()
    {
        var path = TestHelper.CreateTempPath(".png");
        File.WriteAllBytes(path, [1, 2, 3, 4, 5, 6, 7, 8]);
        _files.Add(path);

        Assert.Null(ImageThumbnailLoader.Load(path, 100));
    }

    [Fact]
    internal void Load_サイズ0以下は1ピクセルとして扱う()
    {
        var path = WriteImage(400, 200, SKColors.Red);

        var thumbnail = ImageThumbnailLoader.Load(path, 0);

        Assert.NotNull(thumbnail);
        Assert.Equal(1, thumbnail!.Width);
        Assert.Equal(1, thumbnail.Height);
    }

    [Fact]
    internal void Load_拡張子が大文字でも読み込める()
    {
        var path = WriteImage(80, 40, SKColors.Red, ".PNG");

        var thumbnail = ImageThumbnailLoader.Load(path, 40);

        Assert.NotNull(thumbnail);
        Assert.Equal(40, thumbnail!.Width);
    }
}
