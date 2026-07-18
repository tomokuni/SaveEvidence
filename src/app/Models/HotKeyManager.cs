using System.Runtime.InteropServices;

namespace app.Models;

/// <summary>
/// グローバルホットキーを管理するクラス
/// </summary>
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
        var key = (int)setting.Key;

        if (RegisterHotKey(_hWnd, id, modifiers, (uint)key))
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
    /// WM_HOTKEY メッセージを処理して、対応するCaptureTypeを返す
    /// </summary>
    public CaptureType? ProcessHotKeyMessage(Message msg)
    {
        if (msg.Msg != WM_HOTKEY)
        {
            return null;
        }

        var id = (int)msg.WParam;
        if (_registeredHotKeys.TryGetValue(id, out var entry))
        {
            return entry.CaptureType;
        }

        return null;
    }

    /// <summary>
    /// 設定に基づいて全てのホットキーを再登録する
    /// </summary>
    public void RegisterAll(Settings settings)
    {
        UnregisterAll();
        Register(settings.SelectScreenHotKey, CaptureType.SelectScreen);
        Register(settings.ActiveWindowHotKey, CaptureType.ActiveWindow);
        Register(settings.AreaSelectHotKey, CaptureType.AreaSelect);
    }

    public void Dispose()
    {
        UnregisterAll();
    }

    private const int WM_HOTKEY = 0x0312;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}

/// <summary>
/// キャプチャの種類
/// </summary>
public enum CaptureType
{
    /// <summary>スクリーン選択</summary>
    SelectScreen,

    /// <summary>アクティブウィンドウ</summary>
    ActiveWindow,

    /// <summary>範囲選択</summary>
    AreaSelect
}
