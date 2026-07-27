namespace Estable.AvaloniaUI.Platform;

/// <summary>ネイティブウィンドウプロシージャのサブクラス化（メッセージフック）を抽象化する。</summary>
/// <remarks>
/// ウィンドウメッセージを捕捉するための WndProc サブクラス化を提供する。<br/>
/// Windows 実装では SetWindowLongPtrW / CallWindowProcW Win32 API を使用する。<br/>
/// </remarks>
public interface INativeWindowInterceptor : IDisposable
{
    /// <summary>指定されたウィンドウハンドルにフックを設定する。</summary>
    void Attach(IntPtr hWnd);

    /// <summary>フックを解除し、元のウィンドウプロシージャを復元する。</summary>
    void Detach();

    /// <summary>WM_HOTKEY を受信した時に発生。引数はホットキーID。</summary>
    event Action<int>? HotKeyReceived;
}
