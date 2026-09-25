using EsUtil.Platform.Core;
using EsUtil.Platform.Native.NoOp;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Native.Tests;

/// <summary><see cref="NoOpElementFinder"/> / <see cref="ElementFinderResults"/> の純粋ロジック単体テスト。</summary>
/// <remarks>
/// Windows 以外（Linux / macOS）の既定実装であり、常に未検出を返すことを検証する。<br/>
/// <see cref="NoOpElementFinder"/> は internal のため、自身の Tests（Platform.Native.Tests）から検証する（IVT 規約）。<br/>
/// UI フレームワークを参照しないプロジェクトのため、UI 初期化に依存しない <c>[Fact]</c> で並列実行する。
/// </remarks>
public class NoOpElementFinderBehaviorTests
{
    [Fact]
    public void NoOpElementFinder_指定位置ベースも未検出を返す()
    {
        var finder = NoOpElementFinder.Instance;

        Assert.False(finder.FindWindowUnderCursor(new EsPointInt(500, 500)).IsDetected);
        Assert.False(finder.FindScreenUnderCursor(new EsPointInt(500, 500)).IsDetected);
    }

    [Fact]
    public void NoOpElementFinder_ListScreensは空リストを返す()
    {
        var finder = NoOpElementFinder.Instance;

        var result = finder.ListScreens();

        Assert.Empty(result);
    }

    [Fact]
    public void NoOpElementFinder_ListWindowsは空リストを返す()
    {
        var finder = NoOpElementFinder.Instance;

        var result = finder.ListWindows();

        Assert.Empty(result);
    }

    [Fact]
    public void ElementFinderResults_Handle付きで構築できる()
    {
        var handle = new EsPlatformHandle(0x1234, "Dummy");

        // Handle を設定した結果（ウィンドウ検出時）を構築できる
        var result = new ElementFinderResults(
            new EsRectInt(10, 10, 100, 80),
            IsDetected: true,
            Title: "Test Window",
            Handle: handle);

        Assert.True(result.IsDetected);
        Assert.Equal("Test Window", result.Title);
        Assert.Equal(handle, result.Handle);
    }

    [Fact]
    public void ElementFinderResults_ClassName付きで構築できる()
    {
        var handle = new EsPlatformHandle(0x1234, "Dummy");

        // クラス名を設定した結果（ウィンドウ検出時。デスクトップ除外の検証用）を構築できる
        var result = new ElementFinderResults(
            new EsRectInt(10, 10, 100, 80),
            IsDetected: true,
            Title: "Test Window",
            Handle: handle,
            ClassName: "CabinetWClass");

        Assert.Equal("CabinetWClass", result.ClassName);
        // 省略時は null（後方互換）
        var without = new ElementFinderResults(new EsRectInt(0, 0, 10, 10), IsDetected: true);
        Assert.Null(without.ClassName);
    }
}

/// <summary><see cref="NoOpCursorOverrideGuard"/> の単体テスト。</summary>
/// <remarks>
/// UI フレームワーク非依存のハンドルを受け取るオーバーロードを直接検証する（UI 初期化不要・並列実行）。<br/>
/// Avalonia の <c>TopLevel</c> からハンドルを取得する拡張メソッドのテストは
/// <c>PlatformFactory.Tests</c>（Avalonia 側）が担う。
/// </remarks>
public class NoOpCursorOverrideGuardTests
{
    [Fact]
    public void NoOpCursorOverrideGuard_アタッチしても抑止しない()
    {
        var guard = new NoOpCursorOverrideGuard();

        // 例外なく完了し、IsAttached は false のまま（抑止なし）
        guard.Attach(new EsPlatformHandle(0x1234, "Dummy"));
        Assert.False(guard.IsAttached);
    }

    [Fact]
    public void NoOpCursorOverrideGuard_破棄しても例外にならない()
    {
        var guard = new NoOpCursorOverrideGuard();

        guard.Dispose();
        Assert.False(guard.IsAttached);
    }
}
