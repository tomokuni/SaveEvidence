using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="LogLevelText"/> の仕様テスト（レベルの 3 文字表記）。</summary>
/// <remarks>
/// 表記はログの行の先頭に出るため、4 つの UI 版で同じ値にする必要がある（規則の所有者は
/// <c>.github/ログ規約.md</c> §4.1）。 Serilog の <c>{Level:u3}</c> は <c>Verbose</c> を <c>VRB</c> と表すため、
/// 写像の結果（特に <c>TRC</c>）をここで固定する。
/// </remarks>
public class LogLevelTextTests
{
    [Theory]
    [InlineData("Trace", "TRC")]
    [InlineData("Verbose", "TRC")]
    [InlineData("Debug", "DBG")]
    [InlineData("Information", "INF")]
    [InlineData("Warning", "WRN")]
    [InlineData("Error", "ERR")]
    [InlineData("Critical", "CRT")]
    [InlineData("Fatal", "CRT")]
    internal void Abbreviate_は3文字の表記を返す(string levelName, string expected)
        => Assert.Equal(expected, LogLevelText.Abbreviate(levelName));

    [Fact]
    internal void 表記_はすべて3文字()
    {
        string[] names = ["Trace", "Debug", "Information", "Warning", "Error", "Critical"];

        Assert.All(names, name => Assert.Equal(3, LogLevelText.Abbreviate(name).Length));
    }

    [Fact]
    internal void 未知のレベル_は先頭3文字を大文字にする()
        => Assert.Equal("NOT", LogLevelText.Abbreviate("Notice"));

    [Fact]
    internal void 空のレベル_は問い符3つ()
        => Assert.Equal("???", LogLevelText.Abbreviate(""));

    [Fact]
    internal void プロパティ名_はテンプレートと一致する()
        => Assert.Equal("LogLevelText", LogLevelText.PropertyName);
}
