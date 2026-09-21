using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Native.NoOp;

/// <summary>プラットフォーム実装が未提供の場合の NoOp 実装（常に未検出を返す）。</summary>
/// <remarks>
/// Windows 以外（Linux / macOS）で既定実装（<see cref="WinElementFinder"/>）を生成できない場合に使用する。<br/>
/// 常に未検出（<see cref="ElementFinderResults.IsDetected"/> = false）を返す。<br/>
/// 生成は <see cref="PlatformFactory.CreateElementFinder"/> が唯一の経路。<br/>
/// </remarks>
internal sealed class NoOpElementFinder : IElementFinder
{
    /// <summary>共有シングルトンインスタンス。</summary>
    public static NoOpElementFinder Instance { get; } = new();

    /// <inheritdoc/>
    public ElementFinderResults FindWindowUnderCursor(EsPixelPoint screenPoint)
        => new(default, false);

    /// <inheritdoc/>
    public ElementFinderResults FindScreenUnderCursor(EsPixelPoint screenPoint)
        => new(default, false);

    /// <inheritdoc/>
    public IReadOnlyList<ElementFinderResults> ListScreens()
        => [];

    /// <inheritdoc/>
    public IReadOnlyList<ElementFinderResults> ListWindows()
        => [];
}
