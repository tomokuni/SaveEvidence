using System.Runtime.InteropServices;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>Windows 実装の <see cref="IGlobalHotKeyService{T}"/>。</summary>
/// <remarks>
/// RegisterHotKey / UnregisterHotKey Win32 API でホットキーを登録管理し、<br/>
/// WndProc サブクラス化で WM_HOTKEY メッセージを捕捉して <see cref="HotKeyPressed"/> で配信する。<br/>
/// P/Invoke 呼び出しは <c>EsUtil.Platform.Core.Native.Win.WinNative</c> に委譲する。<br/>
/// デリゲートはフィールドに保持し、WndProc の呼び出し中に GC されないようにする。<br/>
/// </remarks>
/// <typeparam name="T">ホットキーに割り当てるアクションの識別子。</typeparam>
public sealed class WinGlobalHotKeyService<T> : IGlobalHotKeyService<T> where T : notnull
{
    private IntPtr _hWnd;
    private IntPtr _originalWndProc;
    private WinNative.WndProcDelegate? _wndProcDelegate;
    private int _currentId;
    private readonly Dictionary<int, T> _registeredHotKeys = [];
    private bool _disposed;

    /// <inheritdoc/>
    public event Action<T>? HotKeyPressed;

    /// <inheritdoc/>
    public void Initialize(IntPtr hWnd)
    {
        Detach();
        _hWnd = hWnd;
        if (_hWnd == IntPtr.Zero) return;

        _wndProcDelegate = WndProc;
        _originalWndProc = WinNative.SetWindowLongPtr(
            hWnd, WinNative.GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
    }

    /// <inheritdoc/>
    public void RegisterAll(IReadOnlyList<(T Action, HotKeySetting Setting)> registrations)
    {
        // 全解除は一度だけ行う（RegisterCore 内では解除しない。 空辞書の無駄なスキャンを回避）
        UnregisterAll();
        foreach (var (action, setting) in registrations)
        {
            RegisterCore(action, setting);
        }
    }

    /// <inheritdoc/>
    public void UnregisterAll()
    {
        foreach (var id in _registeredHotKeys.Keys)
        {
            WinNative.UnregisterHotKey(_hWnd, id);
        }
        _registeredHotKeys.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            UnregisterAll();
            Detach();
            _disposed = true;
        }
    }

    /// <summary>WndProc サブクラス化を解除し、元の WndProc を復元する。</summary>
    private void Detach()
    {
        if (_hWnd != IntPtr.Zero && _originalWndProc != IntPtr.Zero)
        {
            WinNative.SetWindowLongPtr(_hWnd, WinNative.GWLP_WNDPROC, _originalWndProc);
            _originalWndProc = IntPtr.Zero;
            _hWnd = IntPtr.Zero;
        }
        _wndProcDelegate = null;
    }

    /// <summary>サブクラス化した WndProc。 WM_HOTKEY メッセージを捕捉して対応するアクションを配信する。</summary>
    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WinNative.WM_HOTKEY)
        {
            var id = (int)wParam;
            if (_registeredHotKeys.TryGetValue(id, out var action))
                HotKeyPressed?.Invoke(action);
            return new IntPtr(1);
        }
        return WinNative.CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }

    /// <summary>1件のホットキーを登録する。</summary>
    /// <remarks><see cref="RegisterAll"/> が事前に全解除しているため、個別解除は行わない。</remarks>
    private void RegisterCore(T action, HotKeySetting setting)
    {
        var id = ++_currentId;
        var modifiers = (int)setting.Modifiers;
        var key = WinNative.KeyToWin32VirtualKey(setting.Key);
        if (WinNative.RegisterHotKey(_hWnd, id, modifiers, key))
            _registeredHotKeys[id] = action;
    }
}
