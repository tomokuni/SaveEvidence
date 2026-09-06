using System.Text.Json;
using System.Text.Json.Serialization;
using Estable.SaveEvidence.Models;

namespace Estable.SaveEvidence.Services;

/// <summary>JSON ファイルによる設定管理を提供する。</summary>
/// <remarks>
/// 設定ファイルは <c>%LOCALAPPDATA%\SaveEvidence\settings.json</c> に保存される。<br/>
/// スレッドセーフな読み書きを保証するため <c>Lock</c> による排他制御を行う。<br/>
/// <see cref="Save"/> を明示的に呼び出すまでファイルへの書き込みは行われない。<br/>
/// </remarks>
public sealed class SettingsService : ISettingsService
{
    private static readonly string s_settingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SaveEvidence", "settings.json");

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        // enum は数値ではなく文字列（例: "ThemeMode": "Dark"）で保存する（設定ファイルの可読性向上）
        Converters =
        {
            new PixelRectConverter(),
            new JsonStringEnumConverter(),
        },
    };

    private readonly Lock _lock = new();
    private Settings _current;
    private string? _lastSavedJson;

    public SettingsService() => _current = LoadInternal();

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
            var json = JsonSerializer.Serialize(_current, s_jsonOptions);
            if (json == _lastSavedJson) return;

            SaveInternal(json);
            _lastSavedJson = json;
        }
    }

    private static Settings LoadInternal()
    {
        try
        {
            var dir = Path.GetDirectoryName(s_settingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (File.Exists(s_settingsFilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(s_settingsFilePath), s_jsonOptions) ?? new Settings();
        }
        catch
        {
            // 読込失敗時は既定値で続行
        }
        return new Settings();
    }

    private static void SaveInternal(string json)
    {
        try
        {
            var dir = Path.GetDirectoryName(s_settingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(s_settingsFilePath, json);
        }
        catch
        {
            // 保存失敗時は続行
        }
    }
}
