using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="AppIdentity"/> の仕様テスト（名前・識別子・保存先の共通定義）。</summary>
/// <remarks>
/// 4 つの UI 版が同じ値を使うため、値そのものを固定する<br/>
/// （UI 版ごとに文字列を書くと、設定・ログ・タスク バーのグループ分けがずれる）。
/// </remarks>
public class AppIdentityTests
{
    [Fact]
    internal void 製品名_は4つのUI版で共通()
        => Assert.Equal("SaveEvidence", AppIdentity.ProductName);

    [Fact]
    internal void タスクバーの識別子_は逆ドメイン形式()
    {
        Assert.Equal("EsUtil.SaveEvidence", AppIdentity.AppUserModelId);
        Assert.Contains('.', AppIdentity.AppUserModelId);
        Assert.DoesNotContain(' ', AppIdentity.AppUserModelId);
    }

    [Fact]
    internal void アイコン_は共有のFluentアイコン()
        => Assert.Equal("ic_fluent_scan_table_24_regular.svg", AppIdentity.IconFileName);

    [Fact]
    internal void 保存先_はLOCALAPPDATA配下の製品フォルダ()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.Equal(Path.Combine(localAppData, AppIdentity.ProductName), AppIdentity.LocalAppDataFolder);
        Assert.Equal(Path.Combine(AppIdentity.LocalAppDataFolder, "settings.json"), AppIdentity.SettingsFilePath);
        Assert.Equal(Path.Combine(AppIdentity.LocalAppDataFolder, "logs"), AppIdentity.LogsFolder);
        Assert.Equal(
            Path.Combine(AppIdentity.LogsFolder, AppIdentity.ProductName + ".log"),
            AppIdentity.LogFilePath);
    }

    [Fact]
    internal void 一時フォルダ_はTEMP配下の製品フォルダ()
        => Assert.Equal(
            Path.Combine(Path.GetTempPath(), AppIdentity.ProductName),
            AppIdentity.TempFolder);

    [Fact]
    internal void タスクバーの識別子の設定_は例外にしない()
    {
        // 同じ値を 2 回設定しても失敗しない（起動時に 1 回、呼び直しを許す）
        Assert.Equal(TaskbarIdentity.Apply(AppIdentity.AppUserModelId), TaskbarIdentity.Apply(AppIdentity.AppUserModelId));
    }
}
