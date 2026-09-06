using app.Enum;
using app.Models;
using app.Services;
using app.ViewModels;
using Avalonia.Headless.XUnit;
using Estable.AvaloniaUI.Platform.Imaging;
using Xunit;
using PbBitmap = Estable.Media.EsBitmap;

namespace app.Tests;

/// <summary>
/// MainViewModel（画像保存・ファイル名テンプレートのオートインクリメント）の単体テスト。
/// </summary>
/// <remarks>
/// オートインクリメントの順序（「保存してからインクリメント」）を検証する。<br/>
/// 「screenshot_001.png」と入力されている場合、「screenshot_001.png」で保存してから<br/>
/// テンプレートの表示が「screenshot_002.png」に更新されることを確認する。
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
    public void CanSave_画像と有効なテンプレートがあればtrue()
    {
        var vm = new MainViewModel(new MockSettingsService())
        {
            PreviewImage = CreateBitmap(),
            FileNameTemplateText = "screenshot_001.png",
        };

        Assert.True(vm.CanSave);
    }

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

    [AvaloniaFact]
    public void IncrementRightmostNumber_右端の数値がインクリメントされ桁数が維持される()
    {
        // テンプレートの数値インクリメント（ファイル名テンプレートの純粋関数の動作確認）
        Assert.Equal("screenshot_002.png", FileNameTemplate.IncrementRightmostNumber("screenshot_001.png"));
        Assert.Equal("screenshot_02.png", FileNameTemplate.IncrementRightmostNumber("screenshot_01.png"));
        Assert.Equal("screenshot_100.png", FileNameTemplate.IncrementRightmostNumber("screenshot_099.png"));
        Assert.Equal("a_1_b_2.png", FileNameTemplate.IncrementRightmostNumber("a_1_b_1.png")); // 右端のみ
    }

    [AvaloniaFact]
    public void IncrementRightmostNumber_数値がない場合は元の文字列を返す()
    {
        Assert.Equal("screenshot.png", FileNameTemplate.IncrementRightmostNumber("screenshot.png"));
        Assert.Equal("abc", FileNameTemplate.IncrementRightmostNumber("abc"));
    }

    [AvaloniaFact]
    public void HasInvalidTemplate_有効なテンプレートでfalseになる()
    {
        var vm = new MainViewModel(new MockSettingsService())
        {
            FileNameTemplateText = "screenshot_001.png",
        };

        Assert.True(vm.HasValidTemplate);
        Assert.False(vm.HasInvalidTemplate);
        Assert.Null(vm.ValidationMessage);
    }

    [AvaloniaFact]
    public void HasInvalidTemplate_無効文字を含むテンプレートでtrueになる()
    {
        var vm = new MainViewModel(new MockSettingsService())
        {
            FileNameTemplateText = "screenshot/001.png", // '/' はファイル名に使用できない
        };

        Assert.False(vm.HasValidTemplate);
        Assert.True(vm.HasInvalidTemplate);
        Assert.NotNull(vm.ValidationMessage);
        Assert.Contains("使用できない文字", vm.ValidationMessage);
    }

    [AvaloniaFact]
    public void HasInvalidTemplate_空テンプレートでtrueになる()
    {
        var vm = new MainViewModel(new MockSettingsService())
        {
            FileNameTemplateText = "",
        };

        Assert.False(vm.HasValidTemplate);
        Assert.True(vm.HasInvalidTemplate);
        Assert.NotNull(vm.ValidationMessage);
        Assert.Contains("空です", vm.ValidationMessage);
    }

    [AvaloniaFact]
    public void HasInvalidTemplate_テンプレート変更でPropertyChangedが通知される()
    {
        var vm = new MainViewModel(new MockSettingsService());
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        vm.FileNameTemplateText = "screenshot/001.png"; // 無効 → HasInvalidTemplate が true に変化

        Assert.Contains(nameof(vm.HasInvalidTemplate), notified);
        Assert.Contains(nameof(vm.HasValidTemplate), notified);
        Assert.Contains(nameof(vm.ValidationMessage), notified);
        Assert.True(vm.HasInvalidTemplate);
    }

    // ====================================================================
    // テーマモード（Light / Dark / System）
    // ====================================================================

    [AvaloniaFact]
    public void ThemeMode_既定値はSystemである()
    {
        var vm = new MainViewModel(new MockSettingsService());

        Assert.Equal(ThemeMode.System, vm.Settings.ThemeMode);
    }

    [AvaloniaFact]
    public void ThemeModes_3つの選択肢が定義される()
    {
        var vm = new MainViewModel(new MockSettingsService());

        // Light / Dark / System の 3 択
        Assert.Equal(
            [ThemeMode.Light, ThemeMode.Dark, ThemeMode.System],
            vm.ThemeModes);
    }

    [AvaloniaFact]
    public void ThemeMode_変更するとPropertyChangedが通知される()
    {
        var vm = new MainViewModel(new MockSettingsService());
        var notified = new List<string?>();
        vm.Settings.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        vm.Settings.ThemeMode = ThemeMode.Dark;

        Assert.Contains(nameof(Settings.ThemeMode), notified);
        Assert.Equal(ThemeMode.Dark, vm.Settings.ThemeMode);
    }

    // ====================================================================
    // フォルダビューの表示モード・ソート順（保存/復元用）
    // ====================================================================

    [AvaloniaFact]
    public void FolderViewModeIndex_既定値は大である()
    {
        var vm = new MainViewModel(new MockSettingsService());

        Assert.Equal(1, vm.Settings.FolderViewModeIndex);
    }

    [AvaloniaFact]
    public void FolderViewSortAscending_既定値は昇順である()
    {
        var vm = new MainViewModel(new MockSettingsService());

        Assert.True(vm.Settings.FolderViewSortAscending);
    }

    [AvaloniaFact]
    public void FolderViewSettings_設定を変更するとPropertyChangedが通知される()
    {
        var vm = new MainViewModel(new MockSettingsService());
        var notified = new List<string?>();
        vm.Settings.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        vm.Settings.FolderViewModeIndex = 0;        // 特大
        vm.Settings.FolderViewSortAscending = false; // 降順

        Assert.Contains(nameof(Settings.FolderViewModeIndex), notified);
        Assert.Contains(nameof(Settings.FolderViewSortAscending), notified);
        Assert.Equal(0, vm.Settings.FolderViewModeIndex);
        Assert.False(vm.Settings.FolderViewSortAscending);
    }

    // ====================================================================
    // ラベルの背景色（1〜3 行目）
    // ====================================================================

    [AvaloniaFact]
    public void ラベル背景色の既定値は黒60パーセントである()
    {
        var vm = new MainViewModel(new MockSettingsService());

        // 1〜3 行目の背景色は不透明度を統合した #AARRGGBB（黒 60%）
        Assert.Equal("#99000000", vm.Settings.SizeLabelBackgroundColor);
        Assert.Equal("#99000000", vm.Settings.SubLineBackgroundColor);
        Assert.Equal("#99000000", vm.Settings.InfoLabelBackgroundColor);
    }

    [AvaloniaFact]
    public void ラベル背景色を変更するとPropertyChangedが通知される()
    {
        var vm = new MainViewModel(new MockSettingsService());
        var notified = new List<string?>();
        vm.Settings.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        vm.Settings.SizeLabelBackgroundColor = "DarkBlue";
        vm.Settings.SubLineBackgroundColor = "DarkGreen";
        vm.Settings.InfoLabelBackgroundColor = "DarkRed";

        Assert.Contains(nameof(Settings.SizeLabelBackgroundColor), notified);
        Assert.Contains(nameof(Settings.SubLineBackgroundColor), notified);
        Assert.Contains(nameof(Settings.InfoLabelBackgroundColor), notified);
        Assert.Equal("DarkBlue", vm.Settings.SizeLabelBackgroundColor);
        Assert.Equal("DarkGreen", vm.Settings.SubLineBackgroundColor);
        Assert.Equal("DarkRed", vm.Settings.InfoLabelBackgroundColor);
    }

    // ====================================================================
    // 座標ラベル（カーソル座標・ルーペ座標）
    // ====================================================================

    [AvaloniaFact]
    public void 座標ラベルの既定値が正しい()
    {
        var vm = new MainViewModel(new MockSettingsService());

        Assert.False(vm.Settings.ShowCoordLabel);
        // ルーペ座標ラベルは既定で表示（設定画面の現在値）
        Assert.True(vm.Settings.LoupeShowCoordLabel);
        Assert.Equal("#FFFFFFFF", vm.Settings.CoordLabelColor);
        // 背景色は不透明度を統合した #AARRGGBB（黒 60%）
        Assert.Equal("#99000000", vm.Settings.CoordLabelBackgroundColor);
        Assert.Equal(12.0, vm.Settings.CoordLabelFontSize);
        Assert.Equal("#FFFFFFFF", vm.Settings.LoupeCoordLabelColor);
        Assert.Equal("#99000000", vm.Settings.LoupeCoordLabelBackgroundColor);
        Assert.Equal(12.0, vm.Settings.LoupeCoordLabelFontSize);
    }

    [AvaloniaFact]
    public void 座標ラベル設定を変更するとPropertyChangedが通知される()
    {
        var vm = new MainViewModel(new MockSettingsService());
        var notified = new List<string?>();
        vm.Settings.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        vm.Settings.ShowCoordLabel = true;
        vm.Settings.CoordLabelColor = "Yellow";
        vm.Settings.LoupeShowCoordLabel = false;
        vm.Settings.LoupeCoordLabelColor = "Cyan";

        Assert.Contains(nameof(Settings.ShowCoordLabel), notified);
        Assert.Contains(nameof(Settings.CoordLabelColor), notified);
        Assert.Contains(nameof(Settings.LoupeShowCoordLabel), notified);
        Assert.Contains(nameof(Settings.LoupeCoordLabelColor), notified);
        Assert.True(vm.Settings.ShowCoordLabel);
        Assert.Equal("Yellow", vm.Settings.CoordLabelColor);
        // ルーペ座標ラベルは既定 true のため false へ変更して検証する
        Assert.False(vm.Settings.LoupeShowCoordLabel);
        Assert.Equal("Cyan", vm.Settings.LoupeCoordLabelColor);
    }

    // ====================================================================
    // 外観設定のデフォルト値への復元
    // ====================================================================

    [AvaloniaFact]
    public void ResetAppearanceToDefaults_外観設定がデフォルト値へ戻る()
    {
        var vm = new MainViewModel(new MockSettingsService());
        var s = vm.Settings;

        // 外観設定を変更する
        s.CaptureBorderColor = "Red";
        s.CaptureBorderThickness = 4.0;
        s.MinSelectionSize = 20;
        s.CaptureOverlayColor = "#40000000";
        s.HandleColor = "Blue";
        s.HandleSize = 14.0;
        s.HandleHitRadius = 18.0;
        s.MarkerColor = "Yellow";
        s.MarkerLength = 24.0;
        s.LoupeFrameColor = "Gray";
        s.LoupeFrameWidth = 4;
        s.LoupeCrossColor = "#99FF0000";
        s.LoupeSelectionRectColorA = "#66FFFFFF";
        s.LoupeSelectionRectColorB = "#66000000";
        s.LoupeZoomLevel = 16;
        s.LoupeSize = 320;
        s.LoupeOffset = 60;
        s.PreviewCheckerColorA = "Black";
        s.PreviewCheckerColorB = "#999999";
        s.PreviewCheckerSize = 10;
        s.CaptureTextColor = "Cyan";
        s.SizeLabelBackgroundColor = "#99000000";
        s.SizeLabelFontSize = 16.0;
        s.SubLineColor = "Magenta";
        s.SubLineBackgroundColor = "#99000000";
        s.SubLineFontSize = 16.0;
        s.InfoLabelColor = "Orange";
        s.InfoLabelBackgroundColor = "#99000000";
        s.InfoLabelFontSize = 16.0;
        s.CoordLabelColor = "Lime";
        s.CoordLabelBackgroundColor = "#99000000";
        s.CoordLabelFontSize = 14.0;
        s.LoupeCoordLabelColor = "Pink";
        s.LoupeCoordLabelBackgroundColor = "#99000000";
        s.LoupeCoordLabelFontSize = 14.0;

        // デフォルト値へ戻す
        s.ResetAppearanceToDefaults();

        Assert.Equal("#FFA9A9A9", s.CaptureBorderColor);
        Assert.Equal(2.0, s.CaptureBorderThickness);
        Assert.Equal(6, s.MinSelectionSize);
        Assert.Equal("#80000000", s.CaptureOverlayColor);
        Assert.Equal("#FFFFFFFF", s.HandleColor);
        Assert.Equal(10.0, s.HandleSize);
        Assert.Equal(12.0, s.HandleHitRadius);
        Assert.Equal("#FFFFFFFF", s.MarkerColor);
        Assert.Equal(16.0, s.MarkerLength);
        Assert.Equal("#FFD3D3D3", s.LoupeFrameColor);
        Assert.Equal(2, s.LoupeFrameWidth);
        Assert.Equal("#66FF0000", s.LoupeCrossColor);
        Assert.Equal("#FFFFFFFF", s.LoupeSelectionRectColorA);
        Assert.Equal("#FF000000", s.LoupeSelectionRectColorB);
        Assert.Equal(4, s.LoupeZoomLevel);
        Assert.Equal(160, s.LoupeSize);
        Assert.Equal(20.0, s.LoupeOffset);
        Assert.Equal("#4C808080", s.PreviewCheckerColorA);
        Assert.Equal("#4CA9A9A9", s.PreviewCheckerColorB);
        Assert.Equal(6, s.PreviewCheckerSize);
        Assert.Equal("#FFFFFFFF", s.CaptureTextColor);
        Assert.Equal("#99000000", s.SizeLabelBackgroundColor);
        Assert.Equal(14.0, s.SizeLabelFontSize);
        Assert.Equal("#FFDCDCDC", s.SubLineColor);
        Assert.Equal("#99000000", s.SubLineBackgroundColor);
        Assert.Equal(12.0, s.SubLineFontSize);
        Assert.Equal("#FFFFFFFF", s.InfoLabelColor);
        Assert.Equal("#99000000", s.InfoLabelBackgroundColor);
        Assert.Equal(14.0, s.InfoLabelFontSize);
        Assert.Equal("#FFFFFFFF", s.CoordLabelColor);
        Assert.Equal("#99000000", s.CoordLabelBackgroundColor);
        Assert.Equal(12.0, s.CoordLabelFontSize);
        Assert.Equal("#FFFFFFFF", s.LoupeCoordLabelColor);
        Assert.Equal("#99000000", s.LoupeCoordLabelBackgroundColor);
        Assert.Equal(12.0, s.LoupeCoordLabelFontSize);
    }

    [AvaloniaFact]
    public void ResetAppearanceToDefaults_外観設定以外は変更されない()
    {
        var vm = new MainViewModel(new MockSettingsService());
        var s = vm.Settings;

        // 外観設定以外の値を変更する
        s.FileNameTemplate = "custom_{date}.png";
        s.ThemeMode = ThemeMode.Dark;
        s.LoupeModeValue = 2;
        s.HideMainWindowOnCapture = false;

        s.ResetAppearanceToDefaults();

        // 外観設定以外は維持される
        Assert.Equal("custom_{date}.png", s.FileNameTemplate);
        Assert.Equal(ThemeMode.Dark, s.ThemeMode);
        Assert.Equal(2, s.LoupeModeValue);
        Assert.False(s.HideMainWindowOnCapture);
    }
}
