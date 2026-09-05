using Avalonia;
using Estable.AvaloniaUI.Platform.Imaging;

namespace Estable.AvaloniaUI.Platform.NoOp;

/// <summary>プラットフォーム実装が未提供の場合の NoOp 実装（常にキャプチャ失敗を返す）。</summary>
/// <remarks>
/// Windows 以外（Linux / macOS）で既定実装（<see cref="WinCaptureService"/>）を
/// 生成できない場合に使用する。<br/>
/// 画面/ウィンドウキャプチャは常に null（失敗）を返すため、差し替え前でも例外なく安全に動作する（フォールバック）。<br/>
/// 生成は <see cref="PlatformFactory.CreateCaptureService"/> が唯一の経路。<br/>
/// </remarks>
internal sealed class NoOpCaptureService : ICaptureService
{
    /// <summary>共有シングルトンインスタンス。</summary>
    public static NoOpCaptureService Instance { get; } = new();

    /// <inheritdoc/>
    public UnifiedBitmap? CaptureArea(PixelRect bounds) => null;

    /// <inheritdoc/>
    public UnifiedBitmap? CaptureVirtualScreen(out PixelPoint origin)
    {
        origin = default;
        return null;
    }
}
