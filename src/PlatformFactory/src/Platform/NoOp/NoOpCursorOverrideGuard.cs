using Avalonia.Controls;

namespace Estable.AvaloniaUI.Platform.NoOp;

/// <summary>プラットフォーム実装が未提供の場合の NoOp 実装（カーソル上書き抑止なし）。</summary>
/// <remarks>
/// Windows 以外（Linux / macOS）で既定実装（<see cref="WinCursorOverrideGuard"/>）を
/// 生成できない場合に使用する。<br/>
/// macOS は NSCursor がビュー階層で制御され WM_SETCURSOR 相当の上書きが発生しないため、<br/>
/// X11 はカーソル上書きがウィンドウマネージャー依存のため、抑止は実質不要である。<br/>
/// アタッチは何もせず <see cref="IsAttached"/> = false のまま動作する（安全なフォールバック）。<br/>
/// <see cref="PlatformFactory.CreateCursorOverrideGuard"/> から参照される。
/// </remarks>
internal sealed class NoOpCursorOverrideGuard : ICursorOverrideGuard
{
    /// <inheritdoc/>
    public bool IsAttached => false;

    /// <inheritdoc/>
    public void Attach(TopLevel window)
    {
        // 抑止を実現しない環境（Linux / macOS）では何もしない。
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
