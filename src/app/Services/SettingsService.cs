using System.Text.Json;
using app.Models;

namespace app.Services;

/// <summary>
/// JSON ファイルによる設定管理を提供する <see cref="ISettingsService"/> の既定実装。
/// </summary>
/// <remarks>
/// 設定ファイルは <c>%LOCALAPPDATA%\SaveEvidence\settings.json</c> に保存される。<br/>
/// スレッドセーフな読み書きを保証するため、<c>Lock</c> による排他制御を行う。<br/>
/// 設定変更後は <see cref="Save"/> を明示的に呼び出す必要がある。<br/>
/// </remarks>
public sealed class SettingsService : ISettingsService
{
    /// <summary>設定ファイルのフルパス（%LOCALAPPDATA%\SaveEvidence\settings.json）</summary>
    private static readonly string s_settingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SaveEvidence", "settings.json");

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly Lock _lock = new();
    private Settings _current;

    /// <summary>
    /// SettingsService の新しいインスタンスを初期化する。
    /// </summary>
    /// <remarks>
    /// コンストラクタで設定ファイルを読み込み、既定値で初期化する。<br/>
    /// ファイルが存在しない場合や読み込みエラーの場合は、既定の設定が使用される。<br/>
    /// </remarks>
    public SettingsService()
    {
        _current = LoadInternal();
    }

    /// <inheritdoc/>
    public Settings Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    /// <inheritdoc/>
    public void Save()
    {
        lock (_lock)
        {
            SaveInternal(_current);
        }
    }

    /// <inheritdoc/>
    public void Reload()
    {
        lock (_lock)
        {
            _current = LoadInternal();
        }
    }

    /// <summary>
    /// 設定ファイルから設定を読み込む。
    /// </summary>
    /// <returns>読み込まれた設定オブジェクト。エラー時は既定値。</returns>
    private static Settings LoadInternal()
    {
        try
        {
            var directory = Path.GetDirectoryName(s_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(s_settingsFilePath))
            {
                var json = File.ReadAllText(s_settingsFilePath);
                return JsonSerializer.Deserialize<Settings>(json, s_jsonOptions) ?? new Settings();
            }
        }
        catch
        {
            // 設定ファイルの読み込みに失敗しても、既定値で動作を続行するため例外を無視する
        }

        return new Settings();
    }

    /// <summary>
    /// 設定オブジェクトをファイルに保存する。
    /// </summary>
    /// <param name="settings">保存する設定オブジェクト</param>
    private static void SaveInternal(Settings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(s_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, s_jsonOptions);
            File.WriteAllText(s_settingsFilePath, json);
        }
        catch
        {
            // 設定ファイルの保存に失敗しても、アプリケーションの動作を続行するため例外を無視する
        }
    }
}
