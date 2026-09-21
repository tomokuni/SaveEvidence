using System.Text.Json;
using Estable.SaveEvidence.Enum;
using Estable.SaveEvidence.Models;
using Estable.SaveEvidence.Services;
using Xunit;

namespace Estable.SaveEvidence.Tests;

/// <summary>設定（<see cref="Settings"/>）の JSON ソース生成コンテキストの単体テスト。</summary>
/// <remarks>
/// <b>目的</b>: Native AOT / トリミング配布で設定が空（<c>{}</c>）にならないことを守る。<br/>
/// アプリ本体（<c>Program</c>）は <see cref="SettingsJsonContext"/> を <c>SettingsService</c> へ注入しており、<br/>
/// 本テストはそのコンテキストが<b>設定のプロパティのメタデータを生成できている</b>ことを検証する<br/>
/// （コンテキストを消す / <c>[JsonSerializable]</c> を外す / プロパティが生成されないと失敗する）。<br/>
/// <b>ファイル入出力は検証しない</b>: 保存先は <c>%LOCALAPPDATA%</c> に固定されており、テストから書き込むと実設定を壊すため。
/// </remarks>
public class SettingsJsonContextTests
{
    /// <summary>設定のプロパティ数の下限（目安。設定項目を減らすと気づけるよう緩めに固定する）。</summary>
    /// <remarks>
    /// <c>Settings</c> は 60 個のプロパティを持つ（実測）。ここでは「生成されている」ことの確認が目的のため、<br/>
    /// 少数の欠落で失敗しないよう余裕を持った下限にする（0 個・数個になれば AOT / トリミングで壊れている）。
    /// </remarks>
    private const int MinimumPropertyCount = 40;

    /// <summary>ソース生成コンテキストが設定のプロパティのメタデータを生成していることを検証する。</summary>
    /// <remarks>
    /// メタデータが無い（または空の）コンテキストで保存すると、設定ファイルが <c>{}</c> になり全損する。<br/>
    /// 生成されたメタデータはコンパイル時に埋め込まれるため、AOT 実行時も同じ件数になる。
    /// </remarks>
    [Fact]
    public void メタデータ_設定のプロパティが生成されている()
    {
        var typeInfo = SettingsJsonContext.Default.GetTypeInfo(typeof(Settings));

        Assert.NotNull(typeInfo);
        Assert.True(
            typeInfo!.Properties.Count >= MinimumPropertyCount,
            $"設定のプロパティが {typeInfo.Properties.Count} 個しか生成されていない（{MinimumPropertyCount} 個以上が必要）");

        // 代表的なプロパティが含まれること（設定ファイルの主要項目）
        var names = typeInfo.Properties.Select(static p => p.Name).ToList();
        Assert.Contains(nameof(Settings.FileNameTemplate), names);
        Assert.Contains(nameof(Settings.ThemeMode), names);
        Assert.Contains(nameof(Settings.MainWindowBounds), names);
    }

    /// <summary>コンテキスト経由の往復で値が保持されることを検証する。</summary>
    [Fact]
    public void 直列化_往復で値が保持される()
    {
        var settings = new Settings
        {
            FileNameTemplate = "capture_{time}.png",
            ThemeMode = ThemeMode.Dark,
        };

        var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.Settings);
        var restored = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.Settings);

        Assert.NotEqual("{}", json);
        Assert.NotNull(restored);
        Assert.Equal(settings.FileNameTemplate, restored!.FileNameTemplate);
        Assert.Equal(settings.ThemeMode, restored.ThemeMode);
    }
}
