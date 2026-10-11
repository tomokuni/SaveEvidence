using System.Runtime.Versioning;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary>
/// <see cref="EsBitmapImageWriter"/>（<see cref="IImageWriter"/> の唯一の本番実装）のテスト。
/// </summary>
/// <remarks>
/// <b>保存形式ごとの細かい検証はコアが単一所有する</b>: 形式の判別とエンコードは
/// <see cref="EsBitmapExtensions.Save(EsReadOnlyBitmap, string)"/> / <see cref="EsReadOnlyBitmap.Load(string)"/>（<c>EsUtil.Platform.Core.Tests</c>）が検証済みである。<br/>
/// 本テストは<b>アプリ側の契約</b>だけを検証する（コアの保存へ委譲されること・引数の検証・上書き）。<br/>
/// 保存は Windows 専用の GDI+ を呼ぶため、本クラスは
/// <see cref="SupportedOSPlatformAttribute"/> で Windows に限定する（コアの保存テストと同じ方針）。
/// </remarks>
[SupportedOSPlatform("windows")]
public class EsBitmapImageWriterTests
{
    /// <summary>一時ファイルのパスを作る（ファイルは作成しない）。</summary>
    /// <param name="extension">拡張子（例: ".png"）。</param>
    /// <returns>一時ディレクトリ配下の一意なパス。</returns>
    private static string CreateTempPath(string extension)
        => Path.Combine(Path.GetTempPath(), $"saveevidence-writer-test-{Guid.NewGuid():N}{extension}");

    /// <summary>単色の書き換え可能な画像を生成する。</summary>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <returns>生成した画像。 所有権は呼び出し側。</returns>
    private static EsBitmap CreateBitmap(int width = 4, int height = 4)
        => EsBitmap.Create(width, height, System.Drawing.Color.CornflowerBlue);

    [Fact]
    internal void Save_コアの保存へ委譲する()
    {
        // 拡張子が形式を決める（コアの対応形式は .bmp / .png / .jpg / .jpeg / .gif / .tif / .tiff）
        foreach (var extension in EsReadOnlyBitmap.SupportedFileExtensions)
        {
            var path = CreateTempPath(extension);
            try
            {
                new EsBitmapImageWriter().Save(CreateBitmap(8, 6), path);

                Assert.True(File.Exists(path), $"{extension} へ保存されていない");

                // コアの読み込みで往復でき、画素数が保たれる
                using var loaded = EsReadOnlyBitmap.Load(path);
                Assert.Equal(8, loaded.Width);
                Assert.Equal(6, loaded.Height);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }

    [Theory]
    [InlineData(".webp")]
    [InlineData(".unknown")]
    [InlineData("")]
    internal void Save_未対応の拡張子はコアと同じ例外(string extension)
    {
        // 未対応の拡張子を PNG 等へ黙って差し替えない（コアの契約をそのまま公開する）
        using var image = CreateBitmap();

        Assert.Throws<NotSupportedException>(() => new EsBitmapImageWriter().Save(image, CreateTempPath(extension)));
    }

    [Fact]
    internal void Save_既存ファイルを上書きする()
    {
        var path = CreateTempPath(".png");

        try
        {
            File.WriteAllText(path, "既存の内容");

            new EsBitmapImageWriter().Save(CreateBitmap(10, 5), path);

            using var loaded = EsReadOnlyBitmap.Load(path);
            Assert.Equal(10, loaded.Width);
            Assert.Equal(5, loaded.Height);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    internal void Save_画素数を保持する()
    {
        var path = CreateTempPath(".png");

        try
        {
            new EsBitmapImageWriter().Save(CreateBitmap(10, 5), path);

            using var loaded = EsReadOnlyBitmap.Load(path);
            Assert.Equal(10, loaded.Width);
            Assert.Equal(5, loaded.Height);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    internal void Save_画像がnullならArgumentNullExceptionを投げる()
        => Assert.Throws<ArgumentNullException>(() => new EsBitmapImageWriter().Save(null!, CreateTempPath(".png")));

    [Fact]
    internal void Save_パスがnullならArgumentNullExceptionを投げる()
    {
        using var image = CreateBitmap();

        Assert.Throws<ArgumentNullException>(() => new EsBitmapImageWriter().Save(image, null!));
    }
}
