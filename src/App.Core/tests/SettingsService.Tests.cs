using System.Text.Json;
using System.Text.Json.Serialization;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.SaveEvidence.App.Core.Enum;
using EsUtil.SaveEvidence.App.Core.Models;
using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="SettingsService"/> の JSON 書式（ソース生成と反射の互換性）の単体テスト。</summary>
/// <remarks>
/// <b>ファイル入出力は検証しない</b>: 保存先は <c>%LOCALAPPDATA%\SaveEvidence\settings.json</c> に固定されており、<br/>
/// テストから書き込むとユーザーの実設定を破壊するため、オプションと型情報の検証に限定する。<br/>
/// <b>検証の目的</b>: Native AOT / トリミング用のソース生成経路が、反射経路と同一の JSON を生成すること<br/>
/// （書式・enum の名前保存・矩形の 4 項目保存）。
/// </remarks>
public partial class SettingsServiceJsonTests
{
    /// <summary>ソース生成の有無にかかわらず同一の JSON を生成することを検証する。</summary>
    /// <remarks>
    /// 両経路の出力が一致しなければ、AOT 版と通常版で設定ファイルの互換性が崩れる。
    /// </remarks>
    [Fact]
    internal void 直列化_ソース生成と反射で同一のJSONになる()
    {
        var settings = CreateSettings();

        var reflection = Serialize(settings, context: null);
        var generated = Serialize(settings, TestSettingsJsonContext.Default);

        Assert.Equal(reflection, generated);
    }

    /// <summary>enum は数値ではなく名前で保存されることを検証する。</summary>
    /// <remarks>数値で保存されると、enum の並びを変えた場合に既存の設定ファイルが別の意味になる。</remarks>
    [Fact]
    internal void 直列化_enumは名前で保存される()
    {
        var settings = CreateSettings();
        settings.ThemeMode = ThemeMode.Dark;
        settings.MainWindowState = EsWindowState.Maximized;

        var json = Serialize(settings, TestSettingsJsonContext.Default);

        Assert.Contains("\"ThemeMode\": \"Dark\"", json, StringComparison.Ordinal);
        Assert.Contains("\"MainWindowState\": \"Maximized\"", json, StringComparison.Ordinal);
    }

    /// <summary>ウィンドウ矩形は X / Y / Width / Height の 4 項目のみで保存されることを検証する。</summary>
    /// <remarks>
    /// 派生プロパティ（Right / Bottom / Center 等）が保存されると、読み込み時に無視される項目が増え、<br/>
    /// ファイルが冗長になる（専用の変換器が単一所有する書式）。
    /// </remarks>
    [Fact]
    internal void 直列化_ウィンドウ矩形は4項目のみで保存される()
    {
        var settings = CreateSettings();
        settings.MainWindowBounds = new EsRectInt(150, 180, 900, 600);

        var json = Serialize(settings, TestSettingsJsonContext.Default);

        Assert.Contains("\"X\": 150", json, StringComparison.Ordinal);
        Assert.Contains("\"Y\": 180", json, StringComparison.Ordinal);
        Assert.Contains("\"Width\": 900", json, StringComparison.Ordinal);
        Assert.Contains("\"Height\": 600", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Right\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Center\"", json, StringComparison.Ordinal);
    }

    /// <summary>ソース生成経路の往復で値が保持されることを検証する。</summary>
    [Fact]
    internal void 直列化_ソース生成経路の往復で値が保持される()
    {
        var settings = CreateSettings();
        var json = Serialize(settings, TestSettingsJsonContext.Default);

        var options = SettingsService.CreateJsonOptions(TestSettingsJsonContext.Default);
        var restored = JsonSerializer.Deserialize(json, (System.Text.Json.Serialization.Metadata.JsonTypeInfo<Settings>)options.GetTypeInfo(typeof(Settings)));

        Assert.NotNull(restored);
        Assert.Equal(settings.FileNameTemplate, restored!.FileNameTemplate);
        Assert.Equal(settings.MainWindowBounds, restored.MainWindowBounds);
        Assert.Equal(settings.ThemeMode, restored.ThemeMode);
        Assert.Equal(settings.SelectScreenHotKey, restored.SelectScreenHotKey);
    }

    /// <summary>検証用の設定（既定値＋代表的な非既定値）を生成する。</summary>
    /// <returns>生成した設定。</returns>
    private static Settings CreateSettings()
    {
        var settings = new Settings
        {
            MainWindowBounds = new EsRectInt(150, 180, 900, 600),
            ThemeMode = ThemeMode.Dark,
            MainWindowState = EsWindowState.Normal,
            SelectScreenHotKey = new HotKeySetting(HotKeyModifiers.Control | HotKeyModifiers.Shift, EsKey.Q),
        };

        return settings;
    }

    /// <summary>指定された経路で設定を JSON へ直列化する。</summary>
    /// <param name="settings">対象の設定。</param>
    /// <param name="context">ソース生成コンテキスト。 null の場合は反射ベース。</param>
    /// <returns>生成した JSON。</returns>
    private static string Serialize(Settings settings, JsonSerializerContext? context)
    {
        var options = SettingsService.CreateJsonOptions(context);
        options.MakeReadOnly();

        var typeInfo = (System.Text.Json.Serialization.Metadata.JsonTypeInfo<Settings>)options.GetTypeInfo(typeof(Settings));
        return JsonSerializer.Serialize(settings, typeInfo);
    }
}

/// <summary>テスト用の JSON ソース生成コンテキスト。</summary>
/// <remarks>
/// 本コンテキストは <c>Settings</c> を参照するアセンブリに置く必要がある（同一アセンブリ内では<br/>
/// MVVM のソース生成結果を JSON のソース生成が見られないため）。 ここではテストアセンブリが該当する。
/// </remarks>
[JsonSerializable(typeof(Settings))]
internal sealed partial class TestSettingsJsonContext : JsonSerializerContext;
