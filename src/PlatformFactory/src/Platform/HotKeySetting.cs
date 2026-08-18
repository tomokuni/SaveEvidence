using Avalonia.Input;

namespace Estable.AvaloniaUI.Platform;

/// <summary>ホットキーの組合せ（修飾キー＋キー）を表す値オブジェクト。</summary>
/// <remarks>
/// <see cref="Modifiers"/> と <see cref="Key"/> の組合せで一意のホットキーを定義する。<br/>
/// <c>ToString()</c> で "Ctrl + Shift + Q" のようなユーザー可読な文字列を返す。<br/>
/// Win32 API の RegisterHotKey に渡すため、<see cref="Key"/> は <see cref="Avalonia.Input.Key"/> を使用する。<br/>
/// </remarks>
/// <param name="Modifiers">修飾キー（Alt / Control / Shift / Windows の組合せ）</param>
/// <param name="Key">ホットキーとして割り当てるキー（Avalonia.Input.Key）</param>
public sealed record HotKeySetting(HotKeyModifiers Modifiers, Key Key)
{
    /// <summary>ホットキーのユーザー可読な文字列表現（例: "Ctrl + Shift + Q"）を取得する。</summary>
    public override string ToString()
    {
        var parts = new List<string>(4);
        if ((Modifiers & HotKeyModifiers.Control) != 0) parts.Add("Ctrl");
        if ((Modifiers & HotKeyModifiers.Alt) != 0) parts.Add("Alt");
        if ((Modifiers & HotKeyModifiers.Shift) != 0) parts.Add("Shift");
        if ((Modifiers & HotKeyModifiers.Windows) != 0) parts.Add("Win");
        parts.Add(KeyToSymbolString(Key));
        return string.Join(" + ", parts);
    }

    /// <summary>Win32 RegisterHotKey 用の仮想キーコードを取得する。</summary>
    public uint GetVirtualKeyCode() => (uint)Key;

    private static string KeyToSymbolString(Key key) => key switch
    {
        Key.D0 => "0", Key.D1 => "1", Key.D2 => "2", Key.D3 => "3", Key.D4 => "4",
        Key.D5 => "5", Key.D6 => "6", Key.D7 => "7", Key.D8 => "8", Key.D9 => "9",
        Key.OemTilde => "`", Key.OemMinus => "-", Key.OemPlus => "=",
        Key.OemOpenBrackets => "[", Key.OemCloseBrackets => "]", Key.OemPipe => "\\",
        Key.OemSemicolon => ";",
        Key.OemComma => ",", Key.OemPeriod => ".", Key.OemQuestion => "/",
        _ => key.ToString()
    };
}
