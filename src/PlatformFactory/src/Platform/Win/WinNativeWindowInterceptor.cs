using System.Runtime.InteropServices;
using Estable.AvaloniaUI.Platform;

namespace Estable.AvaloniaUI.Platform.Win;

/// <summary>Windows 実装の <see cref="INativeWindowInterceptor"/>。</summary>
/// <remarks>
/// SetWindowLongPtrW / CallWindowProcW Win32 API を使用して WndProc をサブクラス化し、<br/>
/// WM_HOTKEY メッセージを捕捉して専用イベントで配信する。<br/>
/// P/Invoke 呼び出しは <c>Estable.AvaloniaUI.Platform.Win.WinNative</c> に委譲する。<br/>
/// </remarks>
public sealed class WinNativeWindowInterceptor : INativeWindowInterceptor
{
    private IntPtr _hWnd;
    private IntPtr _prevWndProc;
    private WinNative.WndProcDelegate? _wndProcDelegate;
    private bool _disposed;

    /// <inheritdoc/>
    public event Action<int>? HotKeyReceived;

    /// <inheritdoc/>
    public void Attach(IntPtr hWnd)
    {
        Detach();
        _hWnd = hWnd;

        _wndProcDelegate = (hW, msg, wParam, lParam) =>
        {
            if (msg == WinNative.WM_HOTKEY)
            {
                HotKeyReceived?.Invoke((int)wParam);
                return new IntPtr(1);
            }

            return WinNative.CallWindowProc(_prevWndProc, hW, msg, wParam, lParam);
        };

        _prevWndProc = WinNative.SetWindowLongPtr(
            hWnd, WinNative.GWLP_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
    }

    /// <inheritdoc/>
    public void Detach()
    {
        if (_hWnd != IntPtr.Zero && _prevWndProc != IntPtr.Zero)
        {
            WinNative.SetWindowLongPtr(_hWnd, WinNative.GWLP_WNDPROC, _prevWndProc);
            _prevWndProc = IntPtr.Zero;
            _hWnd = IntPtr.Zero;
        }
        _wndProcDelegate = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            Detach();
            _disposed = true;
        }
    }
}
