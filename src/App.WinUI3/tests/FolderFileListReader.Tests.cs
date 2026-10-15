using EsUtil.SaveEvidence.App.WinUI3.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinUI3.Tests;

/// <summary><see cref="FolderFileListReader"/> の単体テスト（一覧の抽出と並び順）。</summary>
public class FolderFileListReaderTests : IDisposable
{
    /// <summary>テスト用のファイルを入れるフォルダ。</summary>
    private readonly string _folder = Directory.CreateTempSubdirectory("SaveEvidenceWinUI3List").FullName;

    /// <summary>テストで作成したフォルダを削除する。</summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // 一時フォルダの削除失敗はテストの合否に影響しない
        }
    }

    /// <summary>指定名のファイルを作る（内容は問わない）。</summary>
    /// <param name="name">ファイル名。</param>
    private void Touch(string name) => File.WriteAllText(Path.Combine(_folder, name), "x");

    /// <summary>画像だけを抽出することを検証する。</summary>
    [Fact]
    internal void Read_画像ファイルだけを返す()
    {
        Touch("b.png");
        Touch("a.jpg");
        Touch("note.txt");

        var files = FolderFileListReader.Read(_folder, isSortAscending: true);

        Assert.Equal(2, files.Count);
        Assert.EndsWith("a.jpg", files[0], StringComparison.Ordinal);
        Assert.EndsWith("b.png", files[1], StringComparison.Ordinal);
    }

    /// <summary>降順の並び替えを検証する。</summary>
    [Fact]
    internal void Read_降順では逆順に並ぶ()
    {
        Touch("a.png");
        Touch("b.png");
        Touch("c.png");

        var files = FolderFileListReader.Read(_folder, isSortAscending: false);

        Assert.Equal(3, files.Count);
        Assert.EndsWith("c.png", files[0], StringComparison.Ordinal);
        Assert.EndsWith("a.png", files[2], StringComparison.Ordinal);
    }

    /// <summary>大文字小文字を区別せずに並べることを検証する。</summary>
    [Fact]
    internal void Read_大文字小文字を区別せずに並べる()
    {
        Touch("B.png");
        Touch("a.png");

        var files = FolderFileListReader.Read(_folder, isSortAscending: true);

        Assert.EndsWith("a.png", files[0], StringComparison.Ordinal);
        Assert.EndsWith("B.png", files[1], StringComparison.Ordinal);
    }

    /// <summary>存在しないフォルダ・空のパスでは空の一覧を返すことを検証する。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"C:\no-such-folder-saveevidence")]
    internal void Read_フォルダが無い場合は空を返す(string? folderPath)
        => Assert.Empty(FolderFileListReader.Read(folderPath, isSortAscending: true));
}