using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Native.NoOp;

/// <summary>プラットフォーム実装が未提供の場合の NoOp 実装（何もしないカーソル操作）。</summary>
/// <remarks>
/// Windows 以外（Linux / macOS）で既定実装（<see cref="WinCursorService"/>）を生成できない場合に使用する。<br/>
/// 位置取得は常に default（0,0）、位置移動・表示切替は何もしない。<br/>
/// 生成は <see cref="PlatformFactory.CreateCursorService"/> が唯一の経路。<br/>
/// </remarks>
internal sealed class NoOpCursorService : ICursorService
{
    /// <summary>共有シングルトンインスタンス。</summary>
    public static NoOpCursorService Instance { get; } = new();

    /// <inheritdoc/>
    public EsPointInt GetCursorScreenPoint() => default;

    /// <inheritdoc/>
    public void SetCursorScreenPoint(EsPointInt point)
    {
    }

    /// <inheritdoc/>
    public void ShowCursor(bool show)
    {
    }
}
