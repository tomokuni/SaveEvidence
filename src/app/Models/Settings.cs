using System.Text.Json;

namespace app.Models;

/// <summary>
/// アプリケーションの設定を管理するクラス
/// </summary>
public sealed class Settings
{
    private static readonly string s_settingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SaveEvidence",
        "settings.json");

    /// <summary>選択スクリーンキャプチャのホットキー</summary>
    public HotKeySetting SelectScreenHotKey { get; set; } = new() { Modifiers = HotKeyModifiers.Control | HotKeyModifiers.Shift, Key = Keys.Q };

    /// <summary>アクティブウィンドウキャプチャのホットキー</summary>
    public HotKeySetting ActiveWindowHotKey { get; set; } = new() { Modifiers = HotKeyModifiers.Control | HotKeyModifiers.Shift, Key = Keys.W };

    /// <summary>範囲選択キャプチャのホットキー</summary>
    public HotKeySetting AreaSelectHotKey { get; set; } = new() { Modifiers = HotKeyModifiers.Control | HotKeyModifiers.Shift, Key = Keys.E };

    /// <summary>ファイル名テンプレート</summary>
    public string FileNameTemplate { get; set; } = "screenshot_{date}_{time}.png";

    /// <summary>保存先フォルダパス</summary>
    public string SaveFolderPath { get; set; } = string.Empty;

    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// 設定をファイルから読み込む
    /// </summary>
    public static Settings Load()
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
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }
        catch
        {
            // 読み込み失敗時はデフォルト設定を使用
        }

        return new Settings();
    }

    /// <summary>
    /// 設定をファイルに保存する
    /// </summary>
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(s_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, s_jsonOptions);
            File.WriteAllText(s_settingsFilePath, json);
        }
        catch
        {
            // 保存失敗時は何もしない
        }
    }
}

/// <summary>
/// ホットキー設定
/// </summary>
public sealed class HotKeySetting
{
    /// <summary>修飾キー</summary>
    public HotKeyModifiers Modifiers { get; set; }

    /// <summary>キー</summary>
    public Keys Key { get; set; }

    /// <summary>
    /// ホットキーの文字列表現を取得する
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotKeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotKeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotKeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotKeyModifiers.Windows)) parts.Add("Win");

        var keyName = Key switch
        {
            Keys.D0 => "0",
            Keys.D1 => "1",
            Keys.D2 => "2",
            Keys.D3 => "3",
            Keys.D4 => "4",
            Keys.D5 => "5",
            Keys.D6 => "6",
            Keys.D7 => "7",
            Keys.D8 => "8",
            Keys.D9 => "9",
            Keys.Oemtilde => "`",
            Keys.OemMinus => "-",
            Keys.Oemplus => "=",
            Keys.OemOpenBrackets => "[",
            Keys.OemCloseBrackets => "]",
            Keys.OemPipe => "\\",
            Keys.OemSemicolon => ";",
            Keys.OemQuotes => "'",
            Keys.Oemcomma => ",",
            Keys.OemPeriod => ".",
            Keys.OemQuestion => "/",
            _ => Key.ToString()
        };

        parts.Add(keyName);
        return string.Join(" + ", parts);
    }
}

/// <summary>
/// ホットキーの修飾キー
/// </summary>
[Flags]
public enum HotKeyModifiers
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008
}
