using app.Enum;
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
