using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="EditHistory"/> の単体テスト（プレビューと履歴の往復）。</summary>
/// <remarks>
/// 画像はコアの型だけで扱う（UI フレームワークを必要としない）。
/// </remarks>
public class EditHistoryTests
{
    /// <summary>Undo で直前の画像へ戻ることを検証する。</summary>
    /// <remarks>履歴は画素の複製を保持するため、実体ではなく寸法で照合する（差し替え前の画像は破棄される）。</remarks>
    [Fact]
    internal void Undo_直前の画像へ戻る()
    {
        var (main, history) = CreateTarget();
        var first = TestBitmap(2, 2);
        var second = TestBitmap(4, 4);

        main.SetPreviewImage(first);
        history.Push(main.PreviewImage);
        main.SetPreviewImage(second);

        EditHistory.Undo(main, history);

        Assert.NotNull(main.PreviewImage);
        Assert.Equal(2, main.PreviewImage.Width);
        Assert.Equal(2, main.PreviewImage.Height);
    }

    /// <summary>Redo で取り消した画像へ戻ることを検証する。</summary>
    [Fact]
    internal void Redo_取り消した画像へ戻る()
    {
        var (main, history) = CreateTarget();
        main.SetPreviewImage(TestBitmap(2, 2));
        history.Push(main.PreviewImage);
        main.SetPreviewImage(TestBitmap(4, 4));
        EditHistory.Undo(main, history);

        EditHistory.Redo(main, history);

        Assert.NotNull(main.PreviewImage);
        Assert.Equal(4, main.PreviewImage.Width);
        Assert.Equal(4, main.PreviewImage.Height);
    }

    /// <summary>履歴が空の場合はプレビューを変更しないことを検証する。</summary>
    [Fact]
    internal void Undo_履歴が空の場合は何もしない()
    {
        var (main, history) = CreateTarget();
        var image = TestBitmap(2, 2);
        main.SetPreviewImage(image);

        EditHistory.Undo(main, history);
        EditHistory.Redo(main, history);

        Assert.Same(image, main.PreviewImage);
    }

    /// <summary>復元後の後処理が呼ばれることを検証する。</summary>
    [Fact]
    internal void Undo_復元後の後処理を呼ぶ()
    {
        var (main, history) = CreateTarget();
        main.SetPreviewImage(TestBitmap(2, 2));
        history.Push(main.PreviewImage);
        main.SetPreviewImage(TestBitmap(4, 4));

        var restored = 0;
        EditHistory.Undo(main, history, () => restored++);

        Assert.Equal(1, restored);
    }

    /// <summary>引数が null の場合は例外になることを検証する。</summary>
    [Fact]
    internal void Undo_引数がnullの場合は例外になる()
    {
        var (main, history) = CreateTarget();

        Assert.Throws<ArgumentNullException>(() => EditHistory.Undo(null!, history));
        Assert.Throws<ArgumentNullException>(() => EditHistory.Undo(main, null!));
    }

    /// <summary>テスト用の対象（メイン ViewModel と履歴）を生成する。</summary>
    /// <returns>メイン ViewModel と空の履歴。</returns>
    private static (MainViewModel Main, UndoHistory History) CreateTarget()
        => (new MainViewModel(new MainViewModelTests.MockSettingsService(), new EsBitmapImageWriter()), new UndoHistory());

    /// <summary>テスト用の画像を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した画像（読み取りの面）。 破棄は呼び出し側。</returns>
    private static EsReadOnlyBitmap TestBitmap(int width, int height)
    {
        using var bitmap = EsBitmap.Create(width, height);
        bitmap.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(bitmap);
    }
}
