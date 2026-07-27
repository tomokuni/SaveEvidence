using Estable.AvaloniaUI.Platform.Win;

namespace Estable.AvaloniaUI.Platform;

/// <summary>Windows 実装の <see cref="IGlobalHotKeyService{T}"/>。</summary>
/// <remarks>
/// RegisterHotKey / UnregisterHotKey Win32 API をラップし、<br/>
/// 複数ホットキーの登録管理と WM_HOTKEY メッセージ処理を提供する。<br/>
/// P/Invoke 呼び出しは <c>Estable.AvaloniaUI.Platform.Win.WinNative</c> に委譲する。<br/>
/// </remarks>
/// <typeparam name="T">ホットキーに割り当てるアクションの識別子。</typeparam>
public sealed class WinGlobalHotKeyService<T> : IGlobalHotKeyService<T> where T : notnull
{
    private IntPtr _hWnd;
    private int _currentId;
    private readonly Dictionary<int, T> _registeredHotKeys = [];
    private bool _disposed;

    /// <inheritdoc/>
    public void Initialize(IntPtr hWnd)
    {
        _hWnd = hWnd;
    }

    /// <inheritdoc/>
    public void RegisterAll(IReadOnlyList<(T Action, HotKeySetting Setting)> registrations)
    {
        UnregisterAll();
        foreach (var (action, setting) in registrations)
        {
            RegisterCore(action, setting);
        }
    }

    /// <inheritdoc/>
    public void Unregister(T action)
    {
        var keysToRemove = _registeredHotKeys
            .Where(kvp => EqualityComparer<T>.Default.Equals(kvp.Value, action))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in keysToRemove)
        {
            WinNative.UnregisterHotKey(_hWnd, id);
            _registeredHotKeys.Remove(id);
        }
    }

    /// <inheritdoc/>
    public void UnregisterAll()
    {
        foreach (var id in _registeredHotKeys.Keys)
            WinNative.UnregisterHotKey(_hWnd, id);
        _registeredHotKeys.Clear();
    }

    /// <inheritdoc/>
    public T? ProcessHotKeyMessage(uint msg, nint wParam)
    {
        if (msg != WinNative.WM_HOTKEY) return default;
        var id = (int)wParam;
        return _registeredHotKeys.TryGetValue(id, out var action) ? action : default;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            UnregisterAll();
            _disposed = true;
        }
    }

    /// <summary>1件のホットキーを登録する。</summary>
    private void RegisterCore(T action, HotKeySetting setting)
    {
        Unregister(action);
        var id = ++_currentId;
        var modifiers = (int)setting.Modifiers;
        var key = WinNative.KeyToWin32VirtualKey(setting.Key);
        if (WinNative.RegisterHotKey(_hWnd, id, modifiers, key))
            _registeredHotKeys[id] = action;
    }
}
