using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary>MainViewModel の画像保存（SaveImageAsync）と保存済み状態（IsSaved / CanSave）の単体テスト。</summary>
/// <remarks>
/// <see cref="MainViewModel.SaveImageAsync"/> の画像エンコード・ファイル書き込みは <see cref="IImageWriter"/> に委譲するため、
/// テストでは <see cref="MockImageWriter"/> を注入して Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）を不要にする。<br/>
/// オートインクリメントの順序（「保存してからインクリメント」）を検証する。<br/>
/// 「screenshot_001.png」と入力されている場合、「screenshot_001.png」で保存してから<br/>
/// テンプレートの表示が「screenshot_002.png」に更新されることを確認する。<br/>
/// UI 非依存の純粋ロジック（設定・テンプレート検証等）は <see cref="MainViewModelStateTests"/> で <c>[Fact]</c> 検証する。
/// </remarks>
public class MainViewModelTests
{
    /// <summary>テスト用の ISettingsService モック（設定をメモリ上に保持する簡易実装）。 MainViewModelStateTests からも共有する。</summary>
    internal sealed class MockSettingsService : ISettingsService
    {
        public Settings Current { get; } = new();

        /// <inheritdoc/>
        public event EventHandler<SaveFailedEventArgs>? SaveFailed
        {
            add { }
            remove { }
        }

        public void Save() { }

        public static void Reload() { }
    }

    /// <summary>テスト用の IImageWriter モック（要求された画像をダミーデータでファイルへ書き出す）。</summary>
    /// <remarks>
    /// Avalonia Skia（<c>WriteableBitmap</c>）に依存せず、ヘッドレス不要で <see cref="MainViewModel.SaveImageAsync"/> の
    /// 保存フロー（インクリメント・状態更新）を検証する。<br/>
    /// 実ファイルへの書き出し（<see cref="File.Exists"/> 検証用）はダミーデータで行う。
    /// </remarks>
    internal sealed class MockImageWriter : IImageWriter
    {
        /// <summary>保存されたファイルパスの一覧。</summary>
        public List<string> SavedPaths { get; } = [];

        /// <summary>保存対象として渡された画像の一覧。</summary>
        public List<EsReadOnlyBitmap> SavedImages { get; } = [];

        /// <summary>書き出したダミーデータ（中身は検証しない）。</summary>
        private static readonly byte[] s_dummy = [1, 2, 3, 4];

        /// <inheritdoc/>
        public void Save(EsReadOnlyBitmap image, string filePath)
        {
            SavedPaths.Add(filePath);
            SavedImages.Add(image);
            File.WriteAllBytes(filePath, s_dummy);
        }
    }

    /// <summary>テスト用の IImageWriter モック（常に例外を投げる）。</summary>
    /// <remarks>保存の失敗が <see cref="MainViewModel.SaveFailed"/> で通知されることを検証するために使う。</remarks>
    private sealed class FailingImageWriter : IImageWriter
    {
        /// <inheritdoc/>
        /// <exception cref="IOException">常に投げる（テスト用の保存失敗）。</exception>
        public void Save(EsReadOnlyBitmap image, string filePath)
            => throw new IOException("テスト用の保存失敗");
    }

    /// <summary>テスト用の画像（10x10・読み取りの面）を生成する。</summary>
    /// <returns>生成した画像（読み取りの面）。 破棄は呼び出し側。</returns>
    /// <remarks>プレビューは読み取りの面で保持されるため、書き込みの済んだ画像を凍結して 0 コピーで渡す。</remarks>
    private static EsReadOnlyBitmap CreateBitmap()
    {
        using var bmp = EsBitmap.Create(10, 10);
        bmp.Clear(b: 255, g: 255, r: 255, a: 255);
        bmp.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(bmp);
    }

    /// <summary>一時フォルダを生成し、テスト終了時に削除する（IDisposable）。</summary>
    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "SaveEvidenceTest_" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    [Fact]
    internal async Task SaveImageAsync_現在のテンプレートで保存し保存後にインクリメントする()
    {
        using var tempDir = new TempDir();
        var writer = new MockImageWriter();

        var vm = new MainViewModel(new MockSettingsService(), writer)
        {
            PreviewImage = CreateBitmap(),
            FileNameTemplateText = "screenshot_001.png",
            SaveFolderPath = tempDir.Path,
        };

        // 保存実行
        var result = await vm.SaveImageAsync();

        // 保存成功
        Assert.True(result);

        // 1. 保存されたファイル名が「screenshot_001.png」（インクリメント前の現在の値）であること
        Assert.True(File.Exists(Path.Combine(tempDir.Path, "screenshot_001.png")),
            "インクリメント前のファイル名（screenshot_001.png）で保存されること");

        // 2. 保存後にテンプレートの表示が「screenshot_002.png」に更新されること
        Assert.Equal("screenshot_002.png", vm.FileNameTemplateText);

        // 3. 保存済みファイル名が「screenshot_001.png」であること（_lastSavedFileName）
        Assert.Equal("screenshot_001.png", vm.SavedFileName);
        Assert.True(vm.IsSaved);

        // 4. 書き込みが画像を伴って要求されたこと（モック活用検証）
        var saved = Assert.Single(writer.SavedPaths);
        Assert.EndsWith("screenshot_001.png", saved, StringComparison.Ordinal);
        Assert.Single(writer.SavedImages);

        // 5. ステータスバーの通知が「保存しました（…）」に置き換わること（撮影の通知が残らない）
        Assert.StartsWith("保存しました（", vm.StatusMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("キャプチャしました", vm.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    internal async Task SaveImageAsync_画像が変わっていない場合は保存しない()
    {
        using var tempDir = new TempDir();
        var writer = new MockImageWriter();

        var vm = new MainViewModel(new MockSettingsService(), writer)
        {
            PreviewImage = CreateBitmap(),
            FileNameTemplateText = "screenshot_001.png",
            SaveFolderPath = tempDir.Path,
        };

        Assert.True(await vm.SaveImageAsync());

        // 画像が変わっていないため保存済みのまま。 2 回目は何もしない（キー リピート・連打で増やさない）
        Assert.False(vm.CanSave);
        Assert.False(await vm.SaveImageAsync());

        Assert.Single(writer.SavedPaths);
        Assert.Equal("screenshot_002.png", vm.FileNameTemplateText);
    }

    [Fact]
    internal async Task SaveImageAsync_画像を変えて再保存するとSavedFileNameが更新される()
    {
        using var tempDir = new TempDir();
        var writer = new MockImageWriter();

        var vm = new MainViewModel(new MockSettingsService(), writer)
        {
            PreviewImage = CreateBitmap(),
            FileNameTemplateText = "screenshot_001.png",
            SaveFolderPath = tempDir.Path,
        };

        Assert.True(await vm.SaveImageAsync());
        Assert.Equal("screenshot_001.png", vm.SavedFileName);

        // 画像を差し替えると保存済みが解除され、次の保存で新しい名前になる
        vm.SetPreviewImage(CreateBitmap());
        Assert.True(await vm.SaveImageAsync());

        Assert.Equal("screenshot_002.png", vm.SavedFileName);
    }

    [Fact]
    internal void CanSave_初期画像の場合はfalseになり通常の画像でtrueに戻る()
    {
        using var tempDir = new TempDir();

        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter())
        {
            FileNameTemplateText = "screenshot_001.png",
            SaveFolderPath = tempDir.Path,
        };

        // 起動直後の初期画像は利用者の画像ではないため保存できない
        using (var startup = EsBitmap.Create(4, 4))
        {
            vm.SetStartupPreviewImage(startup);
        }

        Assert.True(vm.IsStartupImage);
        Assert.False(vm.CanSave);

        // 撮影等で差し替えると保存できる
        vm.SetPreviewImage(CreateBitmap());
        Assert.False(vm.IsStartupImage);
        Assert.True(vm.CanSave);
    }

    [Fact]
    internal void AutoSave_初期画像は自動保存しない()
    {
        using var tempDir = new TempDir();
        var writer = new MockImageWriter();

        var vm = new MainViewModel(new MockSettingsService(), writer)
        {
            FileNameTemplateText = "screenshot_001.png",
            SaveFolderPath = tempDir.Path,
            AutoSave = true,
        };

        // 起動直後の初期画像は利用者の画像ではないため、自動保存の対象外
        using (var startup = EsBitmap.Create(4, 4))
        {
            vm.SetStartupPreviewImage(startup);
        }

        Assert.True(vm.IsStartupImage);
        Assert.Empty(writer.SavedPaths);
    }

    [Fact]
    internal async Task AutoSave_有効な場合は画像の変更で自動保存する()
    {
        using var tempDir = new TempDir();
        var writer = new MockImageWriter();

        var vm = new MainViewModel(new MockSettingsService(), writer)
        {
            FileNameTemplateText = "screenshot_001.png",
            SaveFolderPath = tempDir.Path,
            AutoSave = true,
        };

        // 画像を設定すると（変更通知の後処理を待って）自動で保存される
        vm.SetPreviewImage(CreateBitmap());

        var path = Path.Combine(tempDir.Path, "screenshot_001.png");
        for (var i = 0; i < 100 && !File.Exists(path); i++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.True(File.Exists(path), "自動保存でファイルが作られること");
        Assert.True(vm.IsSaved);
    }

    [Fact]
    internal async Task CanSave_保存後にテンプレートを変えると保存できる()
    {
        using var tempDir = new TempDir();
        var writer = new MockImageWriter();

        var vm = new MainViewModel(new MockSettingsService(), writer)
        {
            PreviewImage = CreateBitmap(),
            FileNameTemplateText = "screenshot_001.png",
            SaveFolderPath = tempDir.Path,
        };

        Assert.True(await vm.SaveImageAsync());

        // 保存直後（自動で +1 されただけ）は変化とみなさず保存できない
        Assert.Equal("screenshot_002.png", vm.FileNameTemplateText);
        Assert.False(vm.CanSave);

        // 番号を戻すと保存できる
        vm.FileNameTemplateText = "screenshot_001.png";
        Assert.True(vm.CanSave);

        // さらに進めた番号でも保存できる
        vm.FileNameTemplateText = "screenshot_003.png";
        Assert.True(vm.CanSave);
    }

    [Fact]
    internal async Task PasteFromClipboardAsync_はUndoの対象になる()
    {
        using var tempDir = new TempDir();
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter())
        {
            FileNameTemplateText = "screenshot_001.png",
            SaveFolderPath = tempDir.Path,
            PreviewImage = CreateBitmap(),
        };

        var pushed = 0;
        vm.BeforeEditAction = () => pushed++;

        vm.PasteFromClipboardCallback = () =>
        {
            using var bmp = EsBitmap.Create(4, 4);
            bmp.SetReadOnly();
            return Task.FromResult<EsBitmap?>(EsBitmap.FromReadOnlyBitmap(EsReadOnlyBitmap.FromBitmap(bmp)));
        };

        Assert.True(await vm.PasteFromClipboardAsync());
        Assert.Equal(1, pushed);
    }

    [Fact]
    internal async Task SaveImageAsync_プレビュー画像がnullの場合は保存せずインクリメントしない()
    {
        using var tempDir = new TempDir();
        var writer = new MockImageWriter();

        var vm = new MainViewModel(new MockSettingsService(), writer)
        {
            FileNameTemplateText = "screenshot_001.png",
            SaveFolderPath = tempDir.Path,
        };

        // PreviewImage が null のまま保存実行
        var result = await vm.SaveImageAsync();

        // 保存失敗（false）で、インクリメントもされない
        Assert.False(result);
        Assert.Equal("screenshot_001.png", vm.FileNameTemplateText);
        Assert.False(File.Exists(Path.Combine(tempDir.Path, "screenshot_001.png")));
        Assert.Empty(writer.SavedPaths);
        Assert.Empty(writer.SavedImages);
    }

    /// <summary>保存の例外が <see cref="MainViewModel.SaveFailed"/> で通知されることを検証する。</summary>
    /// <remarks>
    /// 通知はホスト（アプリ）がログへ記録するための経路であり、失敗しても操作は継続する<br/>
    /// （[`.github/例外処理規約.md`](../../../../.github/例外処理規約.md) §4）。<br/>
    /// 通知には保存先のパスと例外が入る（原因をログだけで追跡できる）。
    /// </remarks>
    [Fact]
    internal async Task SaveImageAsync_保存に失敗したらSaveFailedで通知する()
    {
        using var tempDir = new TempDir();

        var vm = new MainViewModel(new MockSettingsService(), new FailingImageWriter())
        {
            PreviewImage = CreateBitmap(),
            FileNameTemplateText = "screenshot_001.png",
            SaveFolderPath = tempDir.Path,
        };

        SaveFailedEventArgs? notified = null;
        vm.SaveFailed += (_, e) => notified = e;

        var result = await vm.SaveImageAsync();

        Assert.False(result);
        Assert.NotNull(notified);
        Assert.IsType<IOException>(notified.Error);
        Assert.Equal(Path.Combine(tempDir.Path, "screenshot_001.png"), notified.FilePath);
        Assert.Equal(StatusMessages.SaveFailed(), vm.StatusMessage);
    }

    [Fact]
    internal async Task SaveImageAsync_同名ファイル存在時に上書きを拒否するとインクリメントしない()
    {
        using var tempDir = new TempDir();
        var writer = new MockImageWriter();

        // 同名ファイルを先に作成しておく
        await File.WriteAllTextAsync(
            Path.Combine(tempDir.Path, "screenshot_001.png"), "existing", TestContext.Current.CancellationToken);

        var vm = new MainViewModel(new MockSettingsService(), writer)
        {
            PreviewImage = CreateBitmap(),
            FileNameTemplateText = "screenshot_001.png",
            SaveFolderPath = tempDir.Path,
            // 上書き確認で「拒否」を返す
            ConfirmOverwriteAsync = _ => Task.FromResult(false),
        };

        var result = await vm.SaveImageAsync();

        // 保存キャンセル（false）で、インクリメントもされない
        Assert.False(result);
        Assert.Equal("screenshot_001.png", vm.FileNameTemplateText);
        Assert.Empty(writer.SavedPaths);
    }

    [Fact]
    internal async Task SaveImageAsync_同名ファイル存在時に上書き許可すると保存後にインクリメントする()
    {
        using var tempDir = new TempDir();
        var writer = new MockImageWriter();

        // 同名ファイルを先に作成しておく
        await File.WriteAllTextAsync(
            Path.Combine(tempDir.Path, "screenshot_001.png"), "existing", TestContext.Current.CancellationToken);

        var vm = new MainViewModel(new MockSettingsService(), writer)
        {
            PreviewImage = CreateBitmap(),
            FileNameTemplateText = "screenshot_001.png",
            SaveFolderPath = tempDir.Path,
            // 上書き確認で「許可」を返す
            ConfirmOverwriteAsync = _ => Task.FromResult(true),
        };

        var result = await vm.SaveImageAsync();

        // 保存成功 → インクリメント
        Assert.True(result);
        Assert.Equal("screenshot_002.png", vm.FileNameTemplateText);
        Assert.Single(writer.SavedPaths);
    }

    // ====================================================================
    // 保存可否（CanSave）と保存済み状態（IsSaved）
    // ====================================================================

    [Fact]
    internal async Task CanSave_保存済みの場合はfalseになる()
    {
        using var tempDir = new TempDir();
        var writer = new MockImageWriter();
        var vm = new MainViewModel(new MockSettingsService(), writer)
        {
            PreviewImage = CreateBitmap(),
            FileNameTemplateText = "screenshot_001.png",
            SaveFolderPath = tempDir.Path,
        };

        // 保存済みにする（IsSaved = true になる）
        Assert.True(await vm.SaveImageAsync());

        // 保存済みの間は保存不可
        Assert.True(vm.IsSaved);
        Assert.False(vm.CanSave);
    }

    [Fact]
    internal async Task CanSave_画像を変更すると保存済みが解除されtrueに戻る()
    {
        using var tempDir = new TempDir();
        var writer = new MockImageWriter();
        var vm = new MainViewModel(new MockSettingsService(), writer)
        {
            PreviewImage = CreateBitmap(),
            FileNameTemplateText = "screenshot_001.png",
            SaveFolderPath = tempDir.Path,
        };

        // 保存済みにする
        Assert.True(await vm.SaveImageAsync());
        Assert.False(vm.CanSave);

        // 画像を変更すると保存済みが解除され、保存可能に戻る
        vm.SetPreviewImage(CreateBitmap());

        Assert.False(vm.IsSaved);
        Assert.True(vm.CanSave);
    }

    /// <summary>切り出しコマンドの引数型がコア型（<see cref="EsRectInt"/>）であることを検証する。</summary>
    /// <remarks>
    /// <see cref="CommunityToolkit.Mvvm.Input.RelayCommand{T}"/> は引数の型が一致しないと <see cref="ArgumentException"/> を投げる。<br/>
    /// 切り出しの確定は UI 側で UI 型（例: Avalonia の <c>PixelRect</c>）を生成しやすいため、<br/>
    /// <b>境界（UI 層）でコア型へ変換してからコマンドへ渡す</b>契約をここで固定する<br/>
    /// （UI 型をそのまま渡すと切り出しが例外で失敗する）。
    /// </remarks>
    [Fact]
    internal void CropPreviewCommand_引数はコア型の矩形のみ受け付ける()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());

        // コア型（UI 非依存の画面物理矩形）は受け付ける
        var exception = Record.Exception(() => vm.CropPreviewCommand.Execute(new EsRectInt(0, 0, 10, 10)));
        Assert.Null(exception);

        // UI 型（コア型ではない別の構造体）を渡すと例外になる
        // （境界でコア型へ変換せずに渡した場合の実害を固定する）
        var uiTypeException = Record.Exception(
            () => vm.CropPreviewCommand.Execute(new System.Drawing.Rectangle(0, 0, 10, 10)));
        Assert.IsType<ArgumentException>(uiTypeException);
    }
}
