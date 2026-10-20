using System.Reflection;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.WinForms.Views;
using EsUtil.Ui.WinForms.Thumbnail;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinForms.Tests;

/// <summary>フォルダビューの一覧が実際にサムネイルを描くことを検証するテスト。</summary>
/// <remarks>
/// <b>非表示のウィンドウへホストする</b>: フォルダの列挙は背景で行い、結果を UI スレッドへ戻すため、<br/>
/// テストでも所有ウィンドウのハンドルとメッセージ キューを用意する。 描画は
/// <see cref="ThumbnailControl"/> の描画処理を直接呼び、画素を検査する。<br/>
/// 背景の生成は並列に走るため、画像が反映されるまで UI メッセージを処理しながら待つ。
/// </remarks>
public class FolderViewPageTests : IDisposable
{
    /// <summary>テスト用の画像を入れるフォルダ。</summary>
    private readonly string _folder = Directory.CreateTempSubdirectory("SaveEvidenceFolderView").FullName;

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

    /// <summary>赤で塗りつぶした PNG をフォルダへ書き出す。</summary>
    /// <param name="name">ファイル名。</param>
    private void WriteImage(string name)
    {
        var path = Path.Combine(_folder, name);
        using var bitmap = new Bitmap(64, 48);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.FromArgb(255, 255, 0, 0));
        }

        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    /// <summary>テスト用のサービス束を生成する（保存先を一時フォルダへ向ける）。</summary>
    /// <returns>サービス束とフォルダビューの ViewModel。</returns>
    private (AppServices Services, FolderViewViewModel View) CreateServices()
    {
        var settings = new TestSettingsService();
        settings.Current.SaveFolderPath = _folder;

        var view = new FolderViewViewModel();
        var main = new MainViewModel(settings, new EsBitmapImageWriter());
        return (new AppServices(main, settings, new UndoHistory(), view, new UndoViewModel()), view);
    }

    /// <summary>表示した一覧へファイルのサムネイルが反映されることを検証する。</summary>
    /// <remarks>
    /// 一覧の項目が生成した画像を保持することを確かめる（描画そのものは
    /// <c>EsUtil.Ui.WinForms.Thumbnail.Tests</c> が画素で検証する）。<br/>
    /// <b>添字の取り違えを検出する</b>: 生成はループの後に走るため、ループ変数を捕まえると
    /// すべての生成が最後の添字を指し、一覧の範囲外になって画像が反映されない（実測）。
    /// </remarks>
    [Fact]
    public void 一覧へファイルのサムネイルが反映される()
    {
        WriteImage("a.png");
        WriteImage("b.png");

        var (services, view) = CreateServices();
        var page = FolderViewPage.Build(services);
        using var form = new Form();
        form.Controls.Add(page.Content);
        _ = form.Handle;
        page.OnActivated?.Invoke();

        for (var attempt = 0; attempt < 100 && view.ItemCount != 2; attempt++)
        {
            Application.DoEvents();
            Thread.Sleep(50);
        }

        Assert.Equal(2, view.ItemCount);

        var list = FindList(page.Content);
        Assert.NotNull(list);

        // 背景の生成が終わるまで待つ（生成できた 1 件から反映される）
        IReadOnlyList<string> images = [];
        for (var attempt = 0; attempt < 100; attempt++)
        {
            Application.DoEvents();
            images = ReadItemImages(list!);
            if (images.Count > 0 && images.All(static text => text != "null"))
            {
                break;
            }

            Thread.Sleep(50);
        }

        Assert.Equal(["64x48", "64x48"], images);
    }

    /// <summary>一覧の項目が保持する画像を読む（診断用の文字列で返す）。</summary>
    /// <param name="list">対象の一覧。</param>
    /// <returns>項目ごとの画像（未設定は <c>null</c>）。</returns>
    private static IReadOnlyList<string> ReadItemImages(ThumbnailControl list)
    {
        var items = (System.Collections.IList)typeof(ThumbnailControl)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(list)!;

        var images = new List<string>(items.Count);
        foreach (var item in items)
        {
            var image = item!.GetType().GetProperty("Image")!.GetValue(item) as Image;
            images.Add(image is null
                ? "null"
                : $"{image.Width.ToString(System.Globalization.CultureInfo.InvariantCulture)}x"
                    + image.Height.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return images;
    }

    /// <summary>ページの内容からサムネイル一覧を探す。</summary>
    /// <param name="content">ページの内容。</param>
    /// <returns>見つかった一覧。 無い場合は null。</returns>
    private static ThumbnailControl? FindList(Control content)
    {
        if (content is ThumbnailControl list)
        {
            return list;
        }

        foreach (Control child in content.Controls)
        {
            if (FindList(child) is { } found)
            {
                return found;
            }
        }

        return null;
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
