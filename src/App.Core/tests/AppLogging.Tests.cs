using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.Logging;
using EsUtil.SaveEvidence.App.Core.Models;
using Serilog.Events;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="AppLogging"/> の出力レベルの反映の仕様テスト。</summary>
/// <remarks>
/// 設定（<see cref="Settings.LogOutputLevel"/>）から Serilog の下限への写像は、名前の違う 2 つ
/// （<c>Trace</c> → <c>Verbose</c> / <c>Critical</c> → <c>Fatal</c>）を取り違えると 1 段ずれるため固定する。<br/>
/// 規則の所有者は <c>.github/ログ規約.md</c> §2。
/// </remarks>
public class AppLoggingTests
{
    [Theory]
    [InlineData(LogOutputLevel.Trace, LogEventLevel.Verbose)]
    [InlineData(LogOutputLevel.Debug, LogEventLevel.Debug)]
    [InlineData(LogOutputLevel.Information, LogEventLevel.Information)]
    [InlineData(LogOutputLevel.Warning, LogEventLevel.Warning)]
    [InlineData(LogOutputLevel.Error, LogEventLevel.Error)]
    [InlineData(LogOutputLevel.Critical, LogEventLevel.Fatal)]
    internal void 出力レベルの写像_は重大度の順を保つ(LogOutputLevel level, LogEventLevel expected)
        => Assert.Equal(expected, AppLogging.ToLogEventLevel(level));

    /// <summary>設定の出力レベルを変えると、下限が即時に切り替わることを検証する。</summary>
    /// <remarks>
    /// 設定ページの「出力ログレベル」は <see cref="AppLogging.ApplySettings"/> の購読だけで反映されるため、<br/>
    /// UI 版ごとの切り替え処理が無くても効くことをここで確かめる。<br/>
    /// <b>本テストはプロセス全体の状態（Serilog の水準）を変える</b>ため、<c>try</c> / <c>finally</c> で既定へ戻す。
    /// </remarks>
    [Fact]
    internal void 設定の出力レベル_は変更すると即時に反映される()
    {
        var settings = new Settings();

        try
        {
            AppLogging.ApplySettings(settings);
            Assert.Equal(LogEventLevel.Verbose, AppLogging.MinimumLevel);

            settings.LogOutputLevel = LogOutputLevel.Error;
            Assert.Equal(LogEventLevel.Error, AppLogging.MinimumLevel);

            settings.LogOutputLevel = LogOutputLevel.Critical;
            Assert.Equal(LogEventLevel.Fatal, AppLogging.MinimumLevel);
        }
        finally
        {
            AppLogging.SetMinimumLevel(LogOutputLevel.Trace);
        }
    }
}
