using app.Enum;
using Avalonia.Input;
using System.Runtime.InteropServices;

namespace app.Models;

/// <summary>
/// グローバルホットキーの登録・解除・管理を行うクラス。
/// </summary>
/// <remarks>
/// RegisterHotKey / UnregisterHotKey Win32 API を使用する。<br/>
/// ホットキーメッセージ（WM_HOTKEY）の処理もこのクラスが担当する。<br/>
/// 破棄時に全てのホットキーを自動解除するため、<see cref="IDisposable"/> を実装する。<br/>
/// </remarks>
public sealed partial class HotKeyManager(IntPtr hWnd) : IDisposable
{
    private readonly IntPtr _hWnd = hWnd;
    private int _currentId;
    private readonly Dictionary<int, (HotKeySetting Setting, CaptureType CaptureType)> _registeredHotKeys = [];

    /// <summary>
    /// ホットキーを登録する
    /// </summary>
    public void Register(HotKeySetting setting, CaptureType captureType)
    {
        Unregister(captureType);

        var id = ++_currentId;
        var modifiers = (int)setting.Modifiers;
        var key = KeyToWin32VirtualKey(setting.Key);

        if (RegisterHotKey(_hWnd, id, modifiers, key))
        {
            _registeredHotKeys[id] = (setting, captureType);
        }
    }

    /// <summary>
    /// 特定のキャプチャタイプのホットキーを解除する
    /// </summary>
    public void Unregister(CaptureType captureType)
    {
        var keysToRemove = _registeredHotKeys
            .Where(kvp => kvp.Value.CaptureType == captureType)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in keysToRemove)
        {
            UnregisterHotKey(_hWnd, id);
            _registeredHotKeys.Remove(id);
        }
    }

    /// <summary>
    /// 全ホットキーを解除する
    /// </summary>
    public void UnregisterAll()
    {
        foreach (var id in _registeredHotKeys.Keys)
        {
            UnregisterHotKey(_hWnd, id);
        }

        _registeredHotKeys.Clear();
    }

    /// <summary>
    /// WM_HOTKEY メッセージを処理して、対応するCaptureTypeを返す。
    /// </summary>
    /// <param name="msg">ウィンドウメッセージID（WM_HOTKEY=0x0312以外の場合はnullを返す）</param>
    /// <param name="wParam">メッセージのwParam（ホットキーIDとして解釈）</param>
    /// <returns>対応するCaptureType、または該当なしの場合はnull</returns>
    public CaptureType? ProcessHotKeyMessage(uint msg, nint wParam)
    {
        if (msg != WM_HOTKEY)
        {
            return null;
        }

        var id = (int)wParam;
        if (_registeredHotKeys.TryGetValue(id, out var entry))
        {
            return entry.CaptureType;
        }

        return null;
    }

    /// <summary>
    /// 設定オブジェクトの内容に基づいて、3種類のキャプチャ用ホットキーを全て再登録する。
    /// </summary>
    /// <param name="settings">ホットキー設定を含む設定オブジェクト</param>
    public void RegisterAll(Settings settings)
    {
        UnregisterAll();
        Register(settings.SelectScreenHotKey, CaptureType.SelectScreen);
        Register(settings.WindowSelectHotKey, CaptureType.WindowSelect);
        Register(settings.AreaSelectHotKey, CaptureType.AreaSelect);
    }

    /// <summary>使用中のリソースを解放し、全ホットキーの登録を解除する。</summary>
    public void Dispose()
    {
        UnregisterAll();
    }

    /// <summary>
    /// Avalonia.Input.Key を Win32 仮想キーコードに変換する。
    /// </summary>
    /// <param name="key">変換するAvaloniaキー</param>
    /// <returns>Win32 仮想キーコード</returns>
    private static uint KeyToWin32VirtualKey(Key key) => key switch
    {
        Key.LeftShift => 0xA0,
        Key.RightShift => 0xA1,
        Key.LeftCtrl => 0xA2,
        Key.RightCtrl => 0xA3,
        Key.LeftAlt => 0xA4,
        Key.RightAlt => 0xA5,
        Key.LWin => 0x5B,
        Key.RWin => 0x5C,
        Key.Back => 0x08,
        Key.Tab => 0x09,
        Key.Clear => 0x0C,
        Key.Return => 0x0D,
        Key.Pause => 0x13,
        Key.Capital => 0x14,
        Key.Escape => 0x1B,
        Key.Space => 0x20,
        Key.PageUp => 0x21,
        Key.PageDown => 0x22,
        Key.End => 0x23,
        Key.Home => 0x24,
        Key.Left => 0x25,
        Key.Up => 0x26,
        Key.Right => 0x27,
        Key.Down => 0x28,
        Key.Select => 0x29,
        Key.Print => 0x2A,
        Key.Execute => 0x2B,
        Key.Snapshot => 0x2C,
        Key.Insert => 0x2D,
        Key.Delete => 0x2E,
        Key.Help => 0x2F,
        Key.D0 => 0x30, Key.D1 => 0x31, Key.D2 => 0x32, Key.D3 => 0x33,
        Key.D4 => 0x34, Key.D5 => 0x35, Key.D6 => 0x36, Key.D7 => 0x37,
        Key.D8 => 0x38, Key.D9 => 0x39,
        Key.A => 0x41, Key.B => 0x42, Key.C => 0x43, Key.D => 0x44,
        Key.E => 0x45, Key.F => 0x46, Key.G => 0x47, Key.H => 0x48,
        Key.I => 0x49, Key.J => 0x4A, Key.K => 0x4B, Key.L => 0x4C,
        Key.M => 0x4D, Key.N => 0x4E, Key.O => 0x4F, Key.P => 0x50,
        Key.Q => 0x51, Key.R => 0x52, Key.S => 0x53, Key.T => 0x54,
        Key.U => 0x55, Key.V => 0x56, Key.W => 0x57, Key.X => 0x58,
        Key.Y => 0x59, Key.Z => 0x5A,
        Key.NumPad0 => 0x60, Key.NumPad1 => 0x61, Key.NumPad2 => 0x62, Key.NumPad3 => 0x63,
        Key.NumPad4 => 0x64, Key.NumPad5 => 0x65, Key.NumPad6 => 0x66, Key.NumPad7 => 0x67,
        Key.NumPad8 => 0x68, Key.NumPad9 => 0x69,
        Key.Multiply => 0x6A, Key.Add => 0x6B, Key.Separator => 0x6C,
        Key.Subtract => 0x6D, Key.Decimal => 0x6E, Key.Divide => 0x6F,
        Key.F1 => 0x70, Key.F2 => 0x71, Key.F3 => 0x72, Key.F4 => 0x73,
        Key.F5 => 0x74, Key.F6 => 0x75, Key.F7 => 0x76, Key.F8 => 0x77,
        Key.F9 => 0x78, Key.F10 => 0x79, Key.F11 => 0x7A, Key.F12 => 0x7B,
        Key.F13 => 0x7C, Key.F14 => 0x7D, Key.F15 => 0x7E, Key.F16 => 0x7F,
        Key.F17 => 0x80, Key.F18 => 0x81, Key.F19 => 0x82, Key.F20 => 0x83,
        Key.F21 => 0x84, Key.F22 => 0x85, Key.F23 => 0x86, Key.F24 => 0x87,
        Key.NumLock => 0x90, Key.Scroll => 0x91,
        Key.OemSemicolon => 0xBA,
        Key.OemPlus => 0xBB, Key.OemComma => 0xBC,
        Key.OemMinus => 0xBD, Key.OemPeriod => 0xBE,
        Key.OemQuestion => 0xBF, Key.OemTilde => 0xC0,
        Key.OemOpenBrackets => 0xDB, Key.OemPipe => 0xDC,
        Key.OemCloseBrackets => 0xDD, // OemQuote/OemBacktab は Avalonia 11 の Key 列挙型に存在しないため削除
        _ => (uint)key
    };

    /// <summary>WM_HOTKEY メッセージID。</summary>
    private const int WM_HOTKEY = 0x0312;

    /// <summary>RegisterHotKey Win32 API。グローバルホットキーを登録する。</summary>
    /// <param name="hWnd">ホットキー通知を受け取るウィンドウハンドル</param>
    /// <param name="id">ホットキーの一意識別子</param>
    /// <param name="fsModifiers">修飾キーフラグの組み合わせ</param>
    /// <param name="vk">仮想キーコード</param>
    /// <returns>登録が成功した場合は true</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, uint vk);

    /// <summary>UnregisterHotKey Win32 API。グローバルホットキーの登録を解除する。</summary>
    /// <param name="hWnd">ホットキーを登録したウィンドウハンドル</param>
    /// <param name="id">解除するホットキーの識別子</param>
    /// <returns>解除が成功した場合は true</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}
