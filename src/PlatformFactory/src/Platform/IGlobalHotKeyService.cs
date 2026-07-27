namespace Estable.AvaloniaUI.Platform;

/// <summary>グローバルホットキーの登録・解除・管理を抽象化する。</summary>
/// <remarks>
/// アプリが非アクティブな状態でも機能するシステムワイドなホットキーを管理する。<br/>
/// Windows 実装では RegisterHotKey / UnregisterHotKey Win32 API を使用する。<br/>
/// ホットキーとアクション（<typeparamref name="T"/>）の対応はジェネリック型パラメータで保持し、<br/>
/// アプリケーション固有の型（CaptureType 等）への依存を持たない（DIP）。<br/>
/// </remarks>
/// <typeparam name="T">ホットキーに割り当てるアクションの識別子（アプリが定義する列挙型等）。</typeparam>
public interface IGlobalHotKeyService<T> : IDisposable where T : notnull
{
    /// <summary>初期化する（ウィンドウハンドルを設定）。</summary>
    void Initialize(IntPtr hWnd);

    /// <summary>ホットキーとアクションの対応を登録する。</summary>
    /// <param name="registrations">ホットキー設定とアクションの対応一覧。</param>
    void RegisterAll(IReadOnlyList<(T Action, HotKeySetting Setting)> registrations);

    /// <summary>特定のアクションに割り当てたホットキーを解除する。</summary>
    void Unregister(T action);

    /// <summary>全ホットキーを解除する。</summary>
    void UnregisterAll();

    /// <summary>WM_HOTKEY メッセージを処理して対応するアクションを返す。</summary>
    T? ProcessHotKeyMessage(uint msg, nint wParam);
}
