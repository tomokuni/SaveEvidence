using app.Models;
using app.ViewModels;
using Xunit;
using MockSettingsService = app.Tests.MainViewModelTests.MockSettingsService;

namespace app.Tests;

/// <summary>MainViewModel / FileNameTemplate の純粋ロジック（UI 非依存）の単体テスト。</summary>
/// <remarks>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存しないため <c>[Fact]</c> で並列実行する。<br/>
/// ヘッドレス直列コレクション（<see cref="MainViewModelTests"/>）から分離することで、テスト全体の高速化を図る。<br/>
/// 対象: ファイル名テンプレートの日時プレースホルダ / 保存先フォルダのプレースホルダ解決 / バリデーション。
/// </remarks>
public class MainViewModelStateTests
{
    // ====================================================================
    // ファイル名テンプレートの日時プレースホルダ（{yyyy}/{MM}/{dd}/{HH}/{mm}/{ss}）
    // ====================================================================

    [Fact]
    public void Generate_日時個別プレースホルダを現在日時に置換する()
    {
        var before = DateTime.Now;
        var name = FileNameTemplate.Generate("img_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}.png");
        var after = DateTime.Now;

        // 生成されたファイル名から値を抽出して、置換前後の現在時刻の範囲内であることを確認する
        // （秒単位の境界で失敗しないよう、前後 1 秒の許容を持つ）
        Assert.Matches(@"^img_(\d{4})-(\d{2})-(\d{2})_(\d{2})-(\d{2})-(\d{2})\.png$", name);

        var parsed = DateTime.ParseExact(
            name.Replace("img_", "").Replace(".png", ""),
            "yyyy-MM-dd_HH-mm-ss",
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(parsed >= before.AddSeconds(-1), $"生成値 {parsed:O} が開始時刻 {before:O} より前");
        Assert.True(parsed <= after.AddSeconds(1), $"生成値 {parsed:O} が終了時刻 {after:O} より後");
    }

    [Fact]
    public void Generate_従来のdateとtimeプレースホルダと併用できる()
    {
        var now = DateTime.Now;
        var name = FileNameTemplate.Generate("s_{date}_{yyyy}_{time}_{HH}_{mm}_{ss}.png");

        Assert.Equal(
            $"s_{now:yyyyMMdd}_{now:yyyy}_{now:HHmmss}_{now:HH}_{now:mm}_{now:ss}.png",
            name);
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
        var name = FileNameTemplate.Generate("{{date}}_{time}.png");

        Assert.Equal($"{{date}}_{DateTime.Now:HHmmss}.png", name);
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
        var now = DateTime.Now;

        // { } 内で複数のトークンを自由に組み合わせられる（yyyyMMdd / HHmm 等）
        Assert.Equal($"abcd{{e}}f{now:yyyyMMdd}-{now:HHmm}", FileNameTemplate.Generate("abcd{{e}}f{yyyyMMdd}-{HHmm}"));
        Assert.Equal($"{now:yyyy}年{now:MM}月{now:dd}日", FileNameTemplate.Generate("{yyyy}年{MM}月{dd}日"));
    }

    [Fact]
    public void Generate_未定義トークンはそのまま残る()
    {
        var now = DateTime.Now;

        // 定義外のトークン（example 等）は置換されず、中括弧が外れてそのまま出力される
        Assert.Equal($"example{now:yyyyMMdd}", FileNameTemplate.Generate("{example}{yyyyMMdd}"));
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
        var vm = new MainViewModel(new MockSettingsService());
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
        var vm = new MainViewModel(new MockSettingsService());
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
        var vm = new MainViewModel(new MockSettingsService());
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        vm.SaveFolderPath = missing;

        Assert.True(vm.HasInvalidSaveFolder);
    }

    [Fact]
    public void HasInvalidSaveFolder_空文字はtrueを返す()
    {
        var vm = new MainViewModel(new MockSettingsService());

        vm.SaveFolderPath = string.Empty;

        Assert.True(vm.HasInvalidSaveFolder);
    }

    [Fact]
    public void HasInvalidSaveFolder_プレースホルダ解決後に判定する()
    {
        var vm = new MainViewModel(new MockSettingsService());

        // {MyPictures} は実在するフォルダへ展開されるため false
        vm.SaveFolderPath = "{MyPictures}";
        Assert.False(vm.HasInvalidSaveFolder);

        // {MyPictures} の下に存在しないサブフォルダを指定すると true
        vm.SaveFolderPath = @"{MyPictures}\__not_exists_placeholder_check__";
        Assert.True(vm.HasInvalidSaveFolder);
    }
}
