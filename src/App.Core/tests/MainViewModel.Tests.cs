using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using EsUtil.SaveEvidence.App.Core.ViewModels;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary>
/// MainViewModel の画像保存（SaveImageAsync）と保存済み状態（IsSaved / CanSave）の単体テスト。
/// </summary>
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
    /// <summary>テスト用の ISettingsService モック（設定をメモリ上に保持する簡易実装）。 StateTests からも共有する。</summary>
    internal sealed class MockSettingsService : ISettingsService
    {
        public Settings Current { get; } = new();

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

    /// <summary>テスト用の EsBitmap（10x10）を生成する。</summary>
    /// <returns>生成した EsBitmap。</returns>
    private static EsBitmap CreateBitmap()
    {
        var bmp = EsBitmap.Create(10, 10);
        bmp.Clear(b: 255, g: 255, r: 255, a: 255);
        return bmp;
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
