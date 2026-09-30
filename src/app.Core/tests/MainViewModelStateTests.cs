using EsUtil.SaveEvidence.LineDetection;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Enum;
using EsUtil.SaveEvidence.Models;
using EsUtil.SaveEvidence.ViewModels;
using Xunit;
using MockSettingsService = EsUtil.SaveEvidence.Tests.MainViewModelTests.MockSettingsService;
using MockImageWriter = EsUtil.SaveEvidence.Tests.MainViewModelTests.MockImageWriter;

namespace EsUtil.SaveEvidence.Tests;

/// <summary>MainViewModel / FileNameTemplate の純粋ロジック（UI 非依存）の単体テスト。</summary>
/// <remarks>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存しないため <c>[Fact]</c> で並列実行する。<br/>
/// ヘッドレス直列コレクション（<see cref="MainViewModelTests"/>）から分離することで、テスト全体の高速化を図る。<br/>
/// 対象: ファイル名テンプレートの日時プレースホルダ / 保存先フォルダのプレースホルダ解決 / バリデーション /
/// 設定の既定値と変更通知（テーマ・フォルダビュー・ラベル色・座標ラベル） / 外観設定のデフォルト値への復元。
/// </remarks>
public class MainViewModelStateTests
{
    /// <summary>テスト用の EsBitmap（10x10）を生成する。</summary>
    /// <returns>生成した EsBitmap。</returns>
    private static EsBitmap CreateBitmap()
    {
        var bmp = EsBitmap.Create(10, 10);
        bmp.Clear(b: 255, g: 255, r: 255, a: 255);
        return bmp;
    }

    // ====================================================================
    // ファイル名テンプレートの日時プレースホルダ（{yyyy}/{MM}/{dd}/{HH}/{mm}/{ss}）
    // ====================================================================

    /// <summary>検証に使う固定日時（秒の境界に依存しないよう、テストは本値を注入する）。</summary>
    private static readonly DateTime s_now = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Local);

    [Fact]
    public void Generate_日時個別プレースホルダを指定日時に置換する()
    {
        var name = FileNameTemplate.Generate("img_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}.png", s_now);

        Assert.Equal("img_2026-03-04_05-06-07.png", name);
    }

    [Fact]
    public void Generate_日時を省略すると現在日時を使う()
    {
        var before = DateTime.Now;
        var name = FileNameTemplate.Generate("img_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}.png");
        var after = DateTime.Now;

        // 既定（now 指定なし）は現在日時を使う。 秒単位の境界で失敗しないよう前後 1 秒の許容を持つ
        Assert.Matches(@"^img_(\d{4})-(\d{2})-(\d{2})_(\d{2})-(\d{2})-(\d{2})\.png$", name);

        var parsed = DateTime.ParseExact(
            name.Replace("img_", "").Replace(".png", ""),
            "yyyy-MM-dd_HH-mm-ss",
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.InRange(parsed, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public void Generate_従来のdateとtimeプレースホルダと併用できる()
    {
        var name = FileNameTemplate.Generate("s_{date}_{yyyy}_{time}_{HH}_{mm}_{ss}.png", s_now);

        Assert.Equal("s_20260304_2026_050607_05_06_07.png", name);
    }

    [Fact]
    public void IsValidTemplate_日時個別プレースホルダは有効と判定される()
    {
        Assert.True(FileNameTemplate.IsValidTemplate("img_{yyyy}_{MM}_{dd}_{HH}_{mm}_{ss}.png", out var message));
        Assert.Null(message);
    }

    [Fact]
    public void Generate_エスケープの中括弧はリテラルとして解決される()
    {
        // {{ は {、}} は } に解決される（プレースホルダとして誤置換されない）
        var name = FileNameTemplate.Generate("{{date}}_{time}.png", s_now);

        Assert.Equal("{date}_050607.png", name);
    }

    [Fact]
    public void Generate_エスケープ中のプレースホルダは置換されない()
    {
        // {{date}} は {date} というリテラル文字列になる（date は置換されない）
        var name = FileNameTemplate.Generate("img_{{date}}.png");

        Assert.Equal("img_{date}.png", name);
    }

    [Fact]
    public void Generate_中括弧内のトークンは組み合わせて置換される()
    {
        // { } 内で複数のトークンを自由に組み合わせられる（yyyyMMdd / HHmm 等）
        Assert.Equal("abcd{e}f20260304-0506", FileNameTemplate.Generate("abcd{{e}}f{yyyyMMdd}-{HHmm}", s_now));
        Assert.Equal("2026年03月04日", FileNameTemplate.Generate("{yyyy}年{MM}月{dd}日", s_now));
    }

    [Fact]
    public void Generate_未定義トークンはそのまま残る()
    {
        // 定義外のトークン（example 等）は置換されず、中括弧が外れてそのまま出力される
        Assert.Equal("example20260304", FileNameTemplate.Generate("{example}{yyyyMMdd}", s_now));
    }

    [Fact]
    public void IsValidTemplate_エスケープが解決されて検証される()
    {
        // {{ / }} はリテラル { / } に解決される（{ } は Windows のファイル名で使用可能なため有効と判定）
        Assert.True(FileNameTemplate.IsValidTemplate("img_{{date}}.png", out var message));
        Assert.Null(message);
    }

    [Fact]
    public void IsValidTemplate_複合トークンは解決されて検証される()
    {
        // {yyyyMMdd} は数字へ解決されるため有効
        Assert.True(FileNameTemplate.IsValidTemplate("img_{yyyyMMdd}-{HHmm}.png", out var message));
        Assert.Null(message);
    }

    // ====================================================================
    // 保存先フォルダのプレースホルダ解決（{MyPictures}）
    // ====================================================================

    [Fact]
    public void ResolveSaveFolderPath_MyPicturesプレースホルダを展開する()
    {
        var expected = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        Assert.Equal(expected, MainViewModel.ResolveSaveFolderPath("{MyPictures}"));
    }

    [Fact]
    public void ResolveSaveFolderPath_大文字小文字を区別しない()
    {
        var expected = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        Assert.Equal(expected, MainViewModel.ResolveSaveFolderPath("{mypictures}"));
        Assert.Equal(expected, MainViewModel.ResolveSaveFolderPath("{MYPICTURES}"));
    }

    [Fact]
    public void ResolveSaveFolderPath_プレースホルダなしは入力をそのまま返す()
    {
        Assert.Equal(@"C:\temp\images", MainViewModel.ResolveSaveFolderPath(@"C:\temp\images"));
    }

    [Fact]
    public void ResolveSaveFolderPath_パスの一部に含まれるプレースホルダも展開する()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SaveEvidence");

        Assert.Equal(expected, MainViewModel.ResolveSaveFolderPath(@"{MyPictures}\SaveEvidence"));
    }

    [Fact]
    public void ResolveSaveFolderPath_nullや空文字はそのまま返す()
    {
        Assert.Equal(string.Empty, MainViewModel.ResolveSaveFolderPath(""));
        // 空白のみの文字列は null/空判定に掛からず、そのまま返る
        Assert.Equal("   ", MainViewModel.ResolveSaveFolderPath("   "));
    }

    [Fact]
    public void SaveFolderPathResolved_設定値のプレースホルダを展開する()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());
        var expected = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        vm.Settings.SaveFolderPath = "{MyPictures}";
        vm.SaveFolderPath = "{MyPictures}";

        Assert.Equal(expected, vm.SaveFolderPathResolved);
    }

    // ====================================================================
    // 保存先フォルダのバリデーション（HasInvalidSaveFolder）
    // ====================================================================

    [Fact]
    public void HasInvalidSaveFolder_存在するフォルダはfalseを返す()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());
        var dir = Directory.CreateTempSubdirectory();

        try
        {
            vm.SaveFolderPath = dir.FullName;

            Assert.False(vm.HasInvalidSaveFolder);
        }
        finally
        {
            Directory.Delete(dir.FullName, recursive: true);
        }
    }

    [Fact]
    public void HasInvalidSaveFolder_存在しないフォルダはtrueを返す()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        vm.SaveFolderPath = missing;

        Assert.True(vm.HasInvalidSaveFolder);
    }

    // ====================================================================
    // 自動切り出し（LineDetection 矩形復元）
    // ====================================================================

    /// <summary>垂直線を生成するヘルパー。</summary>
    /// <param name="x">垂直線の X 座標。</param>
    /// <returns>垂直線の検出結果。</returns>
    private static LineDetectionResult VerticalLine(double x)
        => new(new EsPoint(x, 0), new EsPoint(x, 1000), LineOrientation.Vertical, 1000);

    /// <summary>水平線を生成するヘルパー。</summary>
    /// <param name="y">水平線の Y 座標。</param>
    /// <returns>水平線の検出結果。</returns>
    private static LineDetectionResult HorizontalLine(double y)
        => new(new EsPoint(0, y), new EsPoint(1000, y), LineOrientation.Horizontal, 1000);

    [Fact]
    public void TryReconstructRects_垂直2本と水平2本から外側の境界を復元する()
    {
        // 垂直 X=100, 300 / 水平 Y=50, 200 の矩形
        IReadOnlyList<LineDetectionResult> results =
        [
            VerticalLine(100), VerticalLine(300),
            HorizontalLine(50), HorizontalLine(200),
        ];

        var rects = MainViewModel.TryReconstructRects(results);

        var rect = Assert.Single(rects);
        Assert.Equal(100, rect.X);      // 左端 = 最小垂直 X
        Assert.Equal(50, rect.Y);       // 上端 = 最小水平 Y
        Assert.Equal(200, rect.Width);  // 幅 = 最大垂直 X − 最小垂直 X
        Assert.Equal(150, rect.Height); // 高さ = 最大水平 Y − 最小水平 Y
    }

    [Fact]
    public void TryReconstructRects_垂直または水平が2本未満は空を返す()
    {
        // 垂直のみ 2 本（水平なし）は復元できない
        Assert.Empty(MainViewModel.TryReconstructRects([VerticalLine(100), VerticalLine(300), HorizontalLine(50)]));

        // 水平のみ 2 本（垂直なし）は復元できない
        Assert.Empty(MainViewModel.TryReconstructRects([VerticalLine(100), HorizontalLine(50), HorizontalLine(200)]));

        // 線分が空
        Assert.Empty(MainViewModel.TryReconstructRects([]));
    }

    [Fact]
    public void TryReconstructRects_重複座標は統合して外側の境界を復元する()
    {
        // 同じ X/Y に複数の線分があっても Distinct で統合される
        IReadOnlyList<LineDetectionResult> results =
        [
            VerticalLine(100), VerticalLine(100), VerticalLine(300),
            HorizontalLine(50), HorizontalLine(50), HorizontalLine(200),
        ];

        var rects = MainViewModel.TryReconstructRects(results);
        var rect = Assert.Single(rects);
        Assert.Equal(100, rect.X);
        Assert.Equal(50, rect.Y);
    }

    [Fact]
    public void HasInvalidSaveFolder_空文字はtrueを返す()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());

        vm.SaveFolderPath = string.Empty;

        Assert.True(vm.HasInvalidSaveFolder);
    }

    [Fact]
    public void HasInvalidSaveFolder_プレースホルダ解決後に判定する()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());

        // {MyPictures} は実在するフォルダへ展開されるため false
        vm.SaveFolderPath = "{MyPictures}";
        Assert.False(vm.HasInvalidSaveFolder);

        // {MyPictures} の下に存在しないサブフォルダを指定すると true
        vm.SaveFolderPath = @"{MyPictures}\__not_exists_placeholder_check__";
        Assert.True(vm.HasInvalidSaveFolder);
    }

    // ====================================================================
    // 保存可否（CanSave）と保存済み状態（IsSaved）の純粋検証
    // ====================================================================

    [Fact]
    public void CanSave_画像と有効なテンプレートがあればtrue()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter())
        {
            PreviewImage = CreateBitmap(),
            FileNameTemplateText = "screenshot_001.png",
        };

        Assert.True(vm.CanSave);
    }

    // ====================================================================
    // ファイル名テンプレートのオートインクリメント（純粋関数）
    // ====================================================================

    [Fact]
    public void IncrementRightmostNumber_右端の数値がインクリメントされ桁数が維持される()
    {
        // テンプレートの数値インクリメント（ファイル名テンプレートの純粋関数の動作確認）
        Assert.Equal("screenshot_002.png", FileNameTemplate.IncrementRightmostNumber("screenshot_001.png"));
        Assert.Equal("screenshot_02.png", FileNameTemplate.IncrementRightmostNumber("screenshot_01.png"));
        Assert.Equal("screenshot_100.png", FileNameTemplate.IncrementRightmostNumber("screenshot_099.png"));
        Assert.Equal("a_1_b_2.png", FileNameTemplate.IncrementRightmostNumber("a_1_b_1.png")); // 右端のみ
    }

    [Fact]
    public void IncrementRightmostNumber_数値がない場合は元の文字列を返す()
    {
        Assert.Equal("screenshot.png", FileNameTemplate.IncrementRightmostNumber("screenshot.png"));
        Assert.Equal("abc", FileNameTemplate.IncrementRightmostNumber("abc"));
    }

    // ====================================================================
    // ファイル名テンプレートのバリデーション（HasValidTemplate / HasInvalidTemplate）
    // ====================================================================

    [Fact]
    public void HasInvalidTemplate_有効なテンプレートでfalseになる()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter())
        {
            FileNameTemplateText = "screenshot_001.png",
        };

        Assert.True(vm.HasValidTemplate);
        Assert.False(vm.HasInvalidTemplate);
        Assert.Null(vm.ValidationMessage);
    }

    [Fact]
    public void HasInvalidTemplate_無効文字を含むテンプレートでtrueになる()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter())
        {
            FileNameTemplateText = "screenshot/001.png", // '/' はファイル名に使用できない
        };

        Assert.False(vm.HasValidTemplate);
        Assert.True(vm.HasInvalidTemplate);
        Assert.NotNull(vm.ValidationMessage);
        Assert.Contains("使用できない文字", vm.ValidationMessage);
    }

    [Fact]
    public void HasInvalidTemplate_空テンプレートでtrueになる()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter())
        {
            FileNameTemplateText = "",
        };

        Assert.False(vm.HasValidTemplate);
        Assert.True(vm.HasInvalidTemplate);
        Assert.NotNull(vm.ValidationMessage);
        Assert.Contains("空です", vm.ValidationMessage);
    }

    [Fact]
    public void HasInvalidTemplate_テンプレート変更でPropertyChangedが通知される()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());
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

    [Fact]
    public void ThemeMode_既定値はSystemである()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());

        Assert.Equal(ThemeMode.System, vm.Settings.ThemeMode);
    }

    [Fact]
    public void ThemeModes_3つの選択肢が定義される()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());

        // Light / Dark / System の 3 択
        Assert.Equal(
            [ThemeMode.Light, ThemeMode.Dark, ThemeMode.System],
            vm.ThemeModes);
    }

    [Fact]
    public void ThemeMode_変更するとPropertyChangedが通知される()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());
        var notified = new List<string?>();
        vm.Settings.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        vm.Settings.ThemeMode = ThemeMode.Dark;

        Assert.Contains(nameof(Settings.ThemeMode), notified);
        Assert.Equal(ThemeMode.Dark, vm.Settings.ThemeMode);
    }

    // ====================================================================
    // フォルダビューの表示モード・ソート順（保存/復元用）
    // ====================================================================

    [Fact]
    public void FolderViewModeIndex_既定値は大である()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());

        Assert.Equal(1, vm.Settings.FolderViewModeIndex);
    }

    [Fact]
    public void FolderViewSortAscending_既定値は昇順である()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());

        Assert.True(vm.Settings.FolderViewSortAscending);
    }

    [Fact]
    public void FolderViewSettings_設定を変更するとPropertyChangedが通知される()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());
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

    [Fact]
    public void ラベル背景色の既定値は黒60パーセントである()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());

        // 1〜3 行目の背景色は不透明度を統合した #AARRGGBB（黒 60%）
        Assert.Equal("#99000000", vm.Settings.SizeLabelBackgroundColor);
        Assert.Equal("#99000000", vm.Settings.SubLineBackgroundColor);
        Assert.Equal("#99000000", vm.Settings.InfoLabelBackgroundColor);
    }

    [Fact]
    public void ラベル背景色を変更するとPropertyChangedが通知される()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());
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

    [Fact]
    public void 座標ラベルの既定値が正しい()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());

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

    [Fact]
    public void 座標ラベル設定を変更するとPropertyChangedが通知される()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());
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

    [Fact]
    public void ResetAppearanceToDefaults_外観設定がデフォルト値へ戻る()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());
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

    [Fact]
    public void ResetAppearanceToDefaults_外観設定以外は変更されない()
    {
        var vm = new MainViewModel(new MockSettingsService(), new MockImageWriter());
        var s = vm.Settings;

        // 外観設定以外の値を変更する
        s.FileNameTemplate = "custom_{date}.png";
        s.ThemeMode = ThemeMode.Dark;
        s.HideMainWindowOnCapture = false;

        s.ResetAppearanceToDefaults();

        // 外観設定以外は維持される
        Assert.Equal("custom_{date}.png", s.FileNameTemplate);
        Assert.Equal(ThemeMode.Dark, s.ThemeMode);
        Assert.False(s.HideMainWindowOnCapture);
    }
}
