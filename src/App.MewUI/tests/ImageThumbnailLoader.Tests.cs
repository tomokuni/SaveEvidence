using EsUtil.Platform.Core;
using EsUtil.SaveEvidence.App.MewUI.Services;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace EsUtil.SaveEvidence.App.MewUI.Tests;

/// <summary><see cref="ImageThumbnailLoader"/> の単体テスト（縮小デコード・キャッシュ・失敗時の扱い）。</summary>
/// <remarks>
/// 実ファイルへ書き出した画像を読み込み、寸法・アルファ・同一性（キャッシュ）を検証する。<br/>
/// 寸法・アルファ・同一性（キャッシュ）が要求どおりになることを固定する。
/// </remarks>
public class ImageThumbnailLoaderTests : IDisposable
{
    /// <summary>キャッシュの最大保持数（一覧が表示する件数を渡す想定。 テストでは十分に大きい値を使う）。</summary>
    private const int Capacity = 100;

    /// <summary>テストで作成したファイルのパス（終了時に削除する）。</summary>
    private readonly List<string> _files = [];

    /// <summary>テスト用の画像ファイルを書き出す。</summary>
    /// <param name="width">幅（1 以上）。</param>
    /// <param name="height">高さ（1 以上）。</param>
    /// <param name="color">塗りつぶし色。</param>
    /// <param name="extension">拡張子（保存形式を決める）。</param>
    /// <returns>書き出したファイルのパス。</returns>
    private string WriteImage(int width, int height, System.Drawing.Color color, string extension = ".png")
    {
        var path = TestHelper.CreateTempPath(extension);

        using var source = EsBitmap.Create(width, height, color);
        source.Save(path);

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
        var path = WriteImage(sourceWidth, sourceHeight, System.Drawing.Color.Red);

        var thumbnail = ImageThumbnailLoader.Load(path, maxSize, Capacity);

        Assert.NotNull(thumbnail);
        Assert.Equal(expectedWidth, thumbnail!.Width);
        Assert.Equal(expectedHeight, thumbnail.Height);
    }

    [Fact]
    internal void Load_指定サイズ以下の画像は拡大しない()
    {
        var path = WriteImage(40, 20, System.Drawing.Color.Red);

        var thumbnail = ImageThumbnailLoader.Load(path, 320, Capacity);

        Assert.NotNull(thumbnail);
        Assert.Equal(40, thumbnail!.Width);
        Assert.Equal(20, thumbnail.Height);
    }

    [Fact]
    internal void Load_半透明PNGのアルファが保持される()
    {
        // 半透明（アルファ 128）の赤を 1x1 で書き出し、デコード後もアルファが残ることを確認する
        var path = TestHelper.CreateTempPath(".png");
        using var source = EsBitmap.Create(1, 1);
        source.SetPixel(0, 0, b: 0, g: 0, r: 255, a: 128);
        source.Save(path);

        _files.Add(path);

        var thumbnail = ImageThumbnailLoader.Load(path, 32, Capacity);

        Assert.NotNull(thumbnail);
        var (_, _, _, alpha) = thumbnail!.GetPixel(0, 0);
        Assert.InRange(alpha, 120, 136);
    }

    [Fact]
    internal void Load_Jpegのサムネイルも読み込める()
    {
        var path = WriteImage(300, 150, System.Drawing.Color.Green, ".jpg");

        var thumbnail = ImageThumbnailLoader.Load(path, 150, Capacity);

        Assert.NotNull(thumbnail);
        Assert.Equal(150, thumbnail!.Width);
        Assert.Equal(75, thumbnail.Height);
    }

    [Fact]
    internal void Load_同じ条件では同一のインスタンスを共有する()
    {
        var path = WriteImage(400, 200, System.Drawing.Color.Red);

        var first = ImageThumbnailLoader.Load(path, 100, Capacity);
        var second = ImageThumbnailLoader.Load(path, 100, Capacity);

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    internal void Load_サイズが異なれば別のインスタンスを返す()
    {
        var path = WriteImage(400, 200, System.Drawing.Color.Red);

        var small = ImageThumbnailLoader.Load(path, 100, Capacity);
        var large = ImageThumbnailLoader.Load(path, 200, Capacity);

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

        Assert.Null(ImageThumbnailLoader.Load(path, 100, Capacity));
    }

    [Fact]
    internal void Load_画像でないファイルはnullを返す()
    {
        var path = TestHelper.CreateTempPath(".txt");
        File.WriteAllText(path, "これは画像ではありません");
        _files.Add(path);

        Assert.Null(ImageThumbnailLoader.Load(path, 100, Capacity));
    }

    [Fact]
    internal void Load_壊れた画像データはnullを返す()
    {
        var path = TestHelper.CreateTempPath(".png");
        File.WriteAllBytes(path, [1, 2, 3, 4, 5, 6, 7, 8]);
        _files.Add(path);

        Assert.Null(ImageThumbnailLoader.Load(path, 100, Capacity));
    }

    [Fact]
    internal void Load_サイズ0以下は1ピクセルとして扱う()
    {
        var path = WriteImage(400, 200, System.Drawing.Color.Red);

        var thumbnail = ImageThumbnailLoader.Load(path, 0, Capacity);

        Assert.NotNull(thumbnail);
        Assert.Equal(1, thumbnail!.Width);
        Assert.Equal(1, thumbnail.Height);
    }

    [Fact]
    internal void Load_拡張子が大文字でも読み込める()
    {
        var path = WriteImage(80, 40, System.Drawing.Color.Red, ".PNG");

        var thumbnail = ImageThumbnailLoader.Load(path, 40, Capacity);

        Assert.NotNull(thumbnail);
        Assert.Equal(40, thumbnail!.Width);
    }
}
