using app.Enum;

namespace app.Models;

/// <summary>ホットキーの組合せ（修飾キー＋キー）を表す値オブジェクト。</summary>
/// <remarks>
/// <see cref="Modifiers"/> と <see cref="Key"/> の組合せで一意のホットキーを定義する。<br/>
/// <c>ToString()</c> で "Ctrl + Shift + Q" のようなユーザー可読な文字列を返す。
/// </remarks>
/// <param name="Modifiers">修飾キー（Alt / Control / Shift / Windows の組合せ）</param>
/// <param name="Key">ホットキーとして割り当てるキー</param>
public sealed record HotKeySetting(HotKeyModifiers Modifiers, Keys Key)
{
    /// <summary>ホットキーのユーザー可読な文字列表現（例: "Ctrl + Shift + Q"）を取得する。</summary>
    public override string ToString()
    {
        var parts = new List<string>(4);
        if (Modifiers.HasFlag(HotKeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotKeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotKeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotKeyModifiers.Windows)) parts.Add("Win");
        parts.Add(KeyToSymbolString(Key));
        return string.Join(" + ", parts);
    }

    private static string KeyToSymbolString(Keys key) => key switch
    {
        Keys.D0 => "0", Keys.D1 => "1", Keys.D2 => "2", Keys.D3 => "3", Keys.D4 => "4",
        Keys.D5 => "5", Keys.D6 => "6", Keys.D7 => "7", Keys.D8 => "8", Keys.D9 => "9",
        Keys.Oemtilde => "`", Keys.OemMinus => "-", Keys.Oemplus => "=",
        Keys.OemOpenBrackets => "[", Keys.OemCloseBrackets => "]", Keys.OemPipe => "\\",
        Keys.OemSemicolon => ";", Keys.OemQuotes => "'",
        Keys.Oemcomma => ",", Keys.OemPeriod => ".", Keys.OemQuestion => "/",
        _ => key.ToString()
    };
}
