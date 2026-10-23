using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using EsUtil.Platform.Core.Common;
using EsUtil.Platform.Core.Input;
using EsUtil.Platform.Core.Window;
using EsUtil.SaveEvidence.App.Core.Enums;
using EsUtil.SaveEvidence.App.Core.Models;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary>JSON ファイルによる設定管理を提供する。</summary>
/// <remarks>
/// 設定ファイルは <c>%LOCALAPPDATA%\SaveEvidence\settings.json</c> に保存される。<br/>
/// スレッドセーフな読み書きを保証するため <c>Lock</c> による排他制御を行う。<br/>
/// <see cref="Save"/> を明示的に呼び出すまでファイルへの書き込みは行われない。<br/>
/// <br/>
/// <b>トリミング / Native AOT 対応</b>: シリアル化は<b>必ず JSON ソース生成コンテキストを使う</b>（反射ベースでは
/// トリミング・AOT でプロパティが削除されて設定が空になる）。 コンテキストは <see cref="Settings"/> を
/// 参照する側のアセンブリ（例: アプリ本体）で定義する必要がある<br/>
/// （同一コンパイル内では MVVM のソース生成結果を JSON のソース生成が見られないため）。
/// </remarks>
public sealed class SettingsService : ISettingsService
{
    private static readonly string s_settingsFilePath = AppIdentity.SettingsFilePath;

    private readonly Lock _lock = new();

    /// <summary>使用する JSON オプション（書式・変換器はソース生成の有無によらず共通）。</summary>
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>ソース生成された <see cref="Settings"/> の型情報。</summary>
    /// <remarks>
    /// 読み書きは常に本型情報を経由するため、書式・変換器の適用経路は 1 つになる。
    /// </remarks>
    private readonly JsonTypeInfo<Settings> _settingsTypeInfo;

    private readonly Settings _current;
    private string? _lastSavedJson;

    /// <summary>JSON ソース生成コンテキストを指定して設定管理を初期化する。</summary>
    /// <param name="context">ソース生成コンテキスト（<c>Settings</c> を含むもの）。 null 不可。</param>
    /// <remarks>
    /// トリミング / Native AOT で配布する場合は必須である（反射では設定が空になる）。<br/>
    /// 型情報はコンテキストから直接取り出さず、本クラスのオプション（書式・変換器）を経由して取得する。<br/>
    /// コンテキストが持つ型情報はコンテキスト自身のオプションで構築されるため、<br/>
    /// 直接取り出すと書式（インデント）と独自の変換器（色・矩形・enum の名前保存）が適用されない。<br/>
    /// オプションは型情報の取得前に読み取り専用化する（取得後の変更を防ぐ）。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> が null の場合。</exception>
    public SettingsService(JsonSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _jsonOptions = CreateJsonOptions(context);
        _jsonOptions.MakeReadOnly();
        _settingsTypeInfo = (JsonTypeInfo<Settings>)_jsonOptions.GetTypeInfo(typeof(Settings));
        _current = LoadInternal();
    }

    /// <summary>現在の設定オブジェクトを取得する。</summary>
    public Settings Current
    {
        get { lock (_lock) return _current; }
    }

    /// <summary>設定をファイルに保存する。</summary>
    public void Save()
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(_current, _settingsTypeInfo);
            if (string.Equals(json, _lastSavedJson, StringComparison.Ordinal)) return;

            SaveInternal(json);
            _lastSavedJson = json;
        }
    }

    private Settings LoadInternal()
    {
        try
        {
            var dir = Path.GetDirectoryName(s_settingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (File.Exists(s_settingsFilePath))
            {
                var text = File.ReadAllText(s_settingsFilePath);
                var loaded = JsonSerializer.Deserialize(text, _settingsTypeInfo);
                return loaded ?? new Settings();
            }
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 読込失敗時は既定値で続行
        }
        return new Settings();
    }

    /// <summary>JSON の書式と変換器を設定したオプションを生成する。</summary>
    /// <param name="resolver">型情報リゾルバー（ソース生成コンテキスト、またはテスト用の反射リゾルバー）。</param>
    /// <returns>生成したオプション。</returns>
    /// <remarks>
    /// enum は数値ではなく文字列（例: <c>"ThemeMode": "Dark"</c>）で保存する（設定ファイルの可読性向上）。<br/>
    /// 変換器は AOT 対応のジェネリック版を使う（非ジェネリック版は動的コード生成を要求するため）。<br/>
    /// 書式と変換器はソース生成の有無にかかわらず共通とする<br/>
    /// （同一の設定ファイルをソース生成経路と反射経路のどちらでも読み書きできる。 テストが両経路の同一性を検証する）。
    /// </remarks>
    internal static JsonSerializerOptions CreateJsonOptions(IJsonTypeInfoResolver resolver)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            TypeInfoResolver = resolver,
            // enum は名前で保存する。 AOT 対応のためジェネリック版を使う（型ごとに指定する）。
            Converters =
            {
                new EsRectIntJsonConverter(),
                new JsonStringEnumConverter<HotKeyModifiers>(),
                new JsonStringEnumConverter<EsKey>(),
                new JsonStringEnumConverter<EsWindowState>(),
                new JsonStringEnumConverter<ThemeMode>(),
                new JsonStringEnumConverter<StartupPreviewMode>(),
            },
        };

        return options;
    }

    private static void SaveInternal(string json)
    {
        try
        {
            var dir = Path.GetDirectoryName(s_settingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(s_settingsFilePath, json);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 保存失敗時は続行
        }
    }
}
