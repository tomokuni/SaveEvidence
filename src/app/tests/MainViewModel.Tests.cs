using Estable.SaveEvidence.Models;
using Estable.SaveEvidence.Services;
using Estable.SaveEvidence.ViewModels;
using Avalonia.Headless.XUnit;
using Estable.AvaloniaUI.Platform.Imaging;
using Xunit;

namespace Estable.SaveEvidence.Tests;

/// <summary>
/// MainViewModel の画像保存（SaveImageAsync）と保存済み状態（IsSaved / CanSave）の単体テスト。
/// </summary>
/// <remarks>
/// <see cref="SaveImageAsync"/> はプレビュー画像を Avalonia Bitmap へ変換して書き出すため、
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）が必要。<br/>
/// オートインクリメントの順序（「保存してからインクリメント」）を検証する。<br/>
/// 「screenshot_001.png」と入力されている場合、「screenshot_001.png」で保存してから<br/>
/// テンプレートの表示が「screenshot_002.png」に更新されることを確認する。<br/>
/// UI 非依存の純粋ロジック（設定・テンプレート検証等）は <see cref="MainViewModelStateTests"/> で <c>[Fact]</c> 検証する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class MainViewModelTests
{
    /// <summary>テスト用の ISettingsService モック（設定をメモリ上に保持する簡易実装）。StateTests からも共有する。</summary>
    internal sealed class MockSettingsService : ISettingsService
    {
        public Settings Current { get; } = new();

        public void Save() { }

        public void Reload() { }
    }

    /// <summary>テスト用の PbBitmap（10x10）を生成する。</summary>
    /// <returns>生成した PbBitmap。</returns>
    private static PbBitmap CreateBitmap()
    {
        var bmp = PbBitmap.Create(10, 10);
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

    [AvaloniaFact]
    public async Task SaveImageAsync_現在のテンプレートで保存し保存後にインクリメントする()
    {
        using var tempDir = new TempDir();

        var vm = new MainViewModel(new MockSettingsService())
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
        Assert.True(File.Exists(System.IO.Path.Combine(tempDir.Path, "screenshot_001.png")),
            "インクリメント前のファイル名（screenshot_001.png）で保存されること");

        // 2. 保存後にテンプレートの表示が「screenshot_002.png」に更新されること
        Assert.Equal("screenshot_002.png", vm.FileNameTemplateText);

        // 3. 保存済みファイル名が「screenshot_001.png」であること（_lastSavedFileName）
        Assert.Equal("screenshot_001.png", vm.SavedFileName);
        Assert.True(vm.IsSaved);
    }

    [AvaloniaFact]
    public async Task SaveImageAsync_プレビュー画像がnullの場合は保存せずインクリメントしない()
    {
        using var tempDir = new TempDir();

        var vm = new MainViewModel(new MockSettingsService())
        {
            FileNameTemplateText = "screenshot_001.png",
            SaveFolderPath = tempDir.Path,
        };

        // PreviewImage が null のまま保存実行
        var result = await vm.SaveImageAsync();

        // 保存失敗（false）で、インクリメントもされない
        Assert.False(result);
        Assert.Equal("screenshot_001.png", vm.FileNameTemplateText);
        Assert.False(File.Exists(System.IO.Path.Combine(tempDir.Path, "screenshot_001.png")));
    }

    [AvaloniaFact]
    public async Task SaveImageAsync_同名ファイル存在時に上書きを拒否するとインクリメントしない()
    {
        using var tempDir = new TempDir();

        // 同名ファイルを先に作成しておく
        await File.WriteAllTextAsync(System.IO.Path.Combine(tempDir.Path, "screenshot_001.png"), "existing");

        var vm = new MainViewModel(new MockSettingsService())
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
    }

    [AvaloniaFact]
    public async Task SaveImageAsync_同名ファイル存在時に上書き許可すると保存後にインクリメントする()
    {
        using var tempDir = new TempDir();

        // 同名ファイルを先に作成しておく
        await File.WriteAllTextAsync(System.IO.Path.Combine(tempDir.Path, "screenshot_001.png"), "existing");

        var vm = new MainViewModel(new MockSettingsService())
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
    }

    // ====================================================================
    // 保存可否（CanSave）と保存済み状態（IsSaved）
    // ====================================================================

    [AvaloniaFact]
    public void CanSave_保存済みの場合はfalseになる()
    {
        using var tempDir = new TempDir();
        var vm = new MainViewModel(new MockSettingsService())
        {
            PreviewImage = CreateBitmap(),
            FileNameTemplateText = "screenshot_001.png",
            SaveFolderPath = tempDir.Path,
        };

        // 保存済みにする（IsSaved = true になる）
        Assert.True(vm.SaveImageAsync().Result);

        // 保存済みの間は保存不可
        Assert.True(vm.IsSaved);
        Assert.False(vm.CanSave);
    }

    [AvaloniaFact]
    public void CanSave_画像を変更すると保存済みが解除されtrueに戻る()
    {
        using var tempDir = new TempDir();
        var vm = new MainViewModel(new MockSettingsService())
        {
            PreviewImage = CreateBitmap(),
            FileNameTemplateText = "screenshot_001.png",
            SaveFolderPath = tempDir.Path,
        };

        // 保存済みにする
        Assert.True(vm.SaveImageAsync().Result);
        Assert.False(vm.CanSave);

        // 画像を変更すると保存済みが解除され、保存可能に戻る
        vm.SetPreviewImage(CreateBitmap());

        Assert.False(vm.IsSaved);
        Assert.True(vm.CanSave);
    }
}
