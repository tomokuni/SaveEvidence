namespace Estable.AvaloniaUI.Platform.NoOp;

/// <summary>プラットフォーム実装が未提供の場合の NoOp 実装（ホットキー機能なし）。</summary>
/// <remarks>
/// Windows 以外（Linux / macOS）で既定実装（<see cref="WinGlobalHotKeyService{T}"/>）を
/// 生成できない場合に使用する。<br/>
/// ホットキー登録は何もせず、<see cref="HotKeyPressed"/> は発生しない（安全なフォールバック）。<br/>
/// <see cref="PlatformFactory.CreateGlobalHotKeyService{T}"/> から参照される。
/// </remarks>
/// <typeparam name="T">ホットキーに割り当てるアクションの識別子。</typeparam>
internal sealed class NoOpGlobalHotKeyService<T> : IGlobalHotKeyService<T> where T : notnull
{
    /// <inheritdoc/>
    public event Action<T>? HotKeyPressed
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public void Initialize(IntPtr hWnd)
    {
    }

    /// <inheritdoc/>
    public void RegisterAll(IReadOnlyList<(T Action, HotKeySetting Setting)> registrations)
    {
    }

    /// <inheritdoc/>
    public void UnregisterAll()
    {
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
