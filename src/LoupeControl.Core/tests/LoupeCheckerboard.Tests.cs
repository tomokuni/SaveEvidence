using Xunit;

namespace Estable.Loupe.Tests;

/// <summary><see cref="LoupeCheckerboard"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 市松模様の明暗判定のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 本判定は各 UI（Avalonia / MewUI）の市松模様描画で共用する。
/// </remarks>
public class LoupeCheckerboardTests
{
    /// <summary>交互パターン（列と行の和が奇数）で暗色を判定することを検証する。</summary>
    [Fact]
    public void IsDarkTile_交互パターンで暗色を判定する()
    {
        Assert.True(LoupeCheckerboard.IsDarkTile(0, 1));
        Assert.True(LoupeCheckerboard.IsDarkTile(1, 0));
        Assert.False(LoupeCheckerboard.IsDarkTile(0, 0));
        Assert.False(LoupeCheckerboard.IsDarkTile(1, 1));
    }

    /// <summary>列と行の和の偶奇で明暗が決まることを検証する。</summary>
    [Fact]
    public void IsDarkTile_列と行の和が奇数なら暗色になる()
    {
        // (2 + 3) = 5（奇数）→ 暗色
        Assert.True(LoupeCheckerboard.IsDarkTile(2, 3));
        // (4 + 2) = 6（偶数）→ 明色
        Assert.False(LoupeCheckerboard.IsDarkTile(4, 2));
    }
}
