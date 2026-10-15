using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using EsUtil.SaveEvidence.App.WinUI3.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.WinUI3.Tests;

/// <summary><see cref="EditHistory"/> の単体テスト（プレビューと履歴の往復）。</summary>
/// <remarks>
/// 画像はコアの型だけで扱い、ウィンドウを必要としない（ページの生成と同じ経路を検証できる）。
/// </remarks>
public class EditHistoryTests
{
    /// <summary>Undo で直前の画像へ戻ることを検証する。</summary>
    /// <remarks>履歴は画素の複製を保持するため、実体ではなく寸法で照合する（差し替え前の画像は破棄される）。</remarks>
    [Fact]
    internal void Undo_直前の画像へ戻る()
    {
        var services = CreateServices();
        var first = TestHelper.CreateReadOnlyBitmap(2, 2);
        var second = TestHelper.CreateReadOnlyBitmap(4, 4);

        services.Main.SetPreviewImage(first);
        services.UndoHistory.Push(services.Main.PreviewImage);
        services.Main.SetPreviewImage(second);

        EditHistory.Undo(services);

        Assert.NotNull(services.Main.PreviewImage);
        Assert.Equal(2, services.Main.PreviewImage.Width);
        Assert.Equal(2, services.Main.PreviewImage.Height);
    }

    /// <summary>Redo で取り消した画像へ戻ることを検証する。</summary>
    [Fact]
    internal void Redo_取り消した画像へ戻る()
    {
        var services = CreateServices();
        services.Main.SetPreviewImage(TestHelper.CreateReadOnlyBitmap(2, 2));
        services.UndoHistory.Push(services.Main.PreviewImage);
        services.Main.SetPreviewImage(TestHelper.CreateReadOnlyBitmap(4, 4));
        EditHistory.Undo(services);

        EditHistory.Redo(services);

        Assert.NotNull(services.Main.PreviewImage);
        Assert.Equal(4, services.Main.PreviewImage.Width);
        Assert.Equal(4, services.Main.PreviewImage.Height);
    }

    /// <summary>履歴が空の場合はプレビューを変更しないことを検証する。</summary>
    [Fact]
    internal void Undo_履歴が空の場合は何もしない()
    {
        var services = CreateServices();
        var image = TestHelper.CreateReadOnlyBitmap(2, 2);
        services.Main.SetPreviewImage(image);

        EditHistory.Undo(services);
        EditHistory.Redo(services);

        Assert.Same(image, services.Main.PreviewImage);
    }

    /// <summary>復元後の後処理が呼ばれることを検証する。</summary>
    [Fact]
    internal void Undo_復元後の後処理を呼ぶ()
    {
        var services = CreateServices();
        services.Main.SetPreviewImage(TestHelper.CreateReadOnlyBitmap(2, 2));
        services.UndoHistory.Push(services.Main.PreviewImage);
        services.Main.SetPreviewImage(TestHelper.CreateReadOnlyBitmap(4, 4));

        var restored = 0;
        EditHistory.Undo(services, () => restored++);

        Assert.Equal(1, restored);
    }

    /// <summary>テスト用のサービス束を生成する。</summary>
    /// <returns>サービス束。</returns>
    private static AppServices CreateServices()
    {
        var settings = new TestSettingsService();
        var main = new MainViewModel(settings, new EsBitmapImageWriter());
        return new AppServices(main, settings, new UndoHistory(), new FolderViewViewModel());
    }
}