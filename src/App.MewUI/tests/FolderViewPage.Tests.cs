using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using SkiaSharp;
using Xunit;

namespace EsUtil.SaveEvidence.App.MewUI.Tests;

/// <summary>
/// <see cref="Views.FolderViewPage"/> の起動後の先読み（ページを表示せずに一覧を構築する）のテスト。
/// </summary>
/// <remarks>
/// <b>ウィンドウを必要としない</b>: ページの生成（<c>Build</c>）と表示状態の通知（<c>OnWarmUp</c> /
/// <c>OnActivated</c>）は要素を組み立てるだけで、ホストへ載せなくても動く。<br/>
/// <b>MewUI の要素生成は並列実行できない</b>ため <c>[Collection("MewUI")]</c> で直列化する。
/// </remarks>
[Collection("MewUI")]
public class FolderViewPageTests : IDisposable
{
    /// <summary>テスト用の画像を入れるフォルダ。</summary>
    private readonly string _folder = Directory.CreateTempSubdirectory("SaveEvidenceWarmUp").FullName;

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

    /// <summary>指定サイズの PNG をフォルダへ書き出す。</summary>
    /// <param name="name">ファイル名。</param>
    /// <returns>書き出したファイルのパス。</returns>
    private string WriteImage(string name)
    {
        var path = Path.Combine(_folder, name);
        using (var source = new SKBitmap(32, 24))
        using (var image = SKImage.FromBitmap(source))
        using (var encoded = image.Encode(SKEncodedImageFormat.Png, 100))
        using (var stream = File.Create(path))
        {
            encoded.SaveTo(stream);
        }

        return path;
    }

    /// <summary>テスト用のサービス束を生成する（保存先を一時フォルダへ向ける）。</summary>
    /// <returns>サービス束とフォルダビューの ViewModel。</returns>
    private (AppServices Services, FolderViewViewModel View) CreateServices()
    {
        var settings = new TestSettingsService();
        settings.Current.SaveFolderPath = _folder;

        var view = new FolderViewViewModel();
        var main = new MainViewModel(settings, new EsBitmapImageWriter());
        return (new AppServices(main, settings, new UndoHistory(), view), view);
    }

    /// <summary>先読みで一覧が構築され、その後に表示しても件数が変わらないことを確認する。</summary>
    /// <remarks>
    /// 起動後はページを表示せずに <c>OnWarmUp</c> を呼ぶ（キャプチャページを表示したまま準備する）。
    /// 表示（<c>OnActivated</c>）は同じ一覧に対して行われ、件数は変わらない。
    /// </remarks>
    [Fact]
    internal void 先読みで一覧が構築され表示しても件数が変わらない()
    {
        WriteImage("a.png");
        WriteImage("b.png");
        File.WriteAllText(Path.Combine(_folder, "note.txt"), "画像ではないファイル");

        var (services, view) = CreateServices();
        var page = Views.FolderViewPage.Build(services);

        // 先読み（表示していない状態で一覧を構築し、低負荷で生成を始める）
        Assert.NotNull(page.OnWarmUp);
        page.OnWarmUp!.Invoke();
        Assert.Equal(2, view.ItemCount);

        // 切り替え（同じ一覧を使い、生成を全力へ切り替える）
        Assert.NotNull(page.OnActivated);
        page.OnActivated!.Invoke();
        Assert.Equal(2, view.ItemCount);

        // 離脱しても一覧は保持される（隠れている間も低負荷で生成が続く）
        Assert.NotNull(page.OnDeactivated);
        page.OnDeactivated!.Invoke();
        Assert.Equal(2, view.ItemCount);
    }

    /// <summary>保存先フォルダが無い場合でも先読みが失敗しないことを確認する。</summary>
    [Fact]
    internal void 保存先が存在しなくても先読みは失敗しない()
    {
        var (services, view) = CreateServices();
        Directory.Delete(_folder, recursive: true);

        var page = Views.FolderViewPage.Build(services);
        page.OnWarmUp!.Invoke();

        Assert.Equal(0, view.ItemCount);
    }

    /// <summary>テスト用の設定サービス（ファイルへ書き込まない）。</summary>
    private sealed class TestSettingsService : ISettingsService
    {
        /// <inheritdoc/>
        public Settings Current { get; } = new();

        /// <inheritdoc/>
        public void Save()
        {
            // テストではファイルへ書き込まない
        }
    }
}
