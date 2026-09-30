using EsUtil.Platform.Core;
using SystemEnum = System.Enum;

namespace EsUtil.SaveEvidence.Models;

/// <summary>ホットキー文字列を解析する静的ユーティリティ。</summary>
/// <remarks>
/// "Ctrl + Shift + Q", "Ctrl+Shift+Q" 等の形式に対応する。 大文字小文字は区別しない。<br/>
/// View（SettingsPage）から分離した UI 非依存の純粋ロジックであり、ヘッドレスなしの <c>[Fact]</c> で並列テストできる。
/// </remarks>
public static class HotKeyParser
{
    /// <summary>ホットキー文字列を解釈して <see cref="HotKeySetting"/> へ変換する。</summary>
    /// <param name="text">解釈する文字列。</param>
    /// <param name="setting">変換結果。</param>
    /// <returns>解釈に成功した場合は true。</returns>
    /// <remarks>
    /// "Ctrl + Shift + Q", "Ctrl+Shift+Q" 等の形式に対応する。 大文字小文字は区別しない。<br/>
    /// 最低 1 つの修飾キーが必要。 キー名が解釈できない場合は false。
    /// </remarks>
    public static bool TryParse(string text, out HotKeySetting setting)
    {
        setting = new HotKeySetting(HotKeyModifiers.None, EsKey.None);
        if (string.IsNullOrWhiteSpace(text)) return false;

        // "Ctrl + Shift + Q" → ["Ctrl ", " Shift ", " Q"] → trim
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return false;

        var modifiers = HotKeyModifiers.None;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            modifiers |= parts[i].ToLowerInvariant() switch
            {
                "ctrl" or "control" => HotKeyModifiers.Control,
                "shift" => HotKeyModifiers.Shift,
                "alt" => HotKeyModifiers.Alt,
                "win" or "windows" => HotKeyModifiers.Windows,
                _ => HotKeyModifiers.None
            };
        }

        // 最低1つの修飾キーが必要
        if (modifiers == HotKeyModifiers.None) return false;

        var keyName = parts[^1];
        if (SystemEnum.TryParse<EsKey>(keyName, true, out var key) && key != EsKey.None)
        {
            setting = new HotKeySetting(modifiers, key);
            return true;
        }

        return false;
    }
}
