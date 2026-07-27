using Avalonia.Platform;

namespace Estable.AvaloniaUI.Platform.NoOp;

/// <summary>プラットフォーム実装が未提供の場合の NoOp 実装（何もしないウィンドウ操作）。</summary>
/// <remarks>
/// Windows 以外（Linux / macOS）で既定実装（<see cref="WinWindowService"/>）を
/// 生成できない場合に使用する。<br/>
/// タスクバー非表示・ハング時退避は何もしない（プラットフォーム非対応のフォールバック）。<br/>
/// 生成は <see cref="PlatformFactory.CreateWindowService"/> が唯一の経路。<br/>
/// </remarks>
internal sealed class NoOpWindowService : IWindowService
{
    /// <summary>共有シングルトンインスタンス。</summary>
    public static NoOpWindowService Instance { get; } = new();

    /// <inheritdoc/>
    public void HideFromTaskSwitcher(IPlatformHandle handle)
    {
        // 非対応プラットフォーム（Linux / macOS）では何もしない。
    }

    /// <inheritdoc/>
    public bool SetCaptureExclusion(IPlatformHandle handle, bool exclude) => false;

    /// <inheritdoc/>
    public void EvacuateWindow(IPlatformHandle handle)
    {
        // 非対応プラットフォーム（Linux / macOS）では何もしない。
    }
}
