using Estable.AvaloniaUI.Platform.NoOp;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Xunit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary><see cref="NoOpElementFinder"/> / <see cref="ElementFinderResults"/> の純粋ロジック単体テスト。</summary>
/// <remarks>
/// Windows 以外（Linux / macOS）の既定実装であり、常に未検出を返すことを検証する。<br/>
/// <see cref="NoOpElementFinder"/> は internal のため、自身の Tests（PlatformShared.Tests）から検証する（IVT 規約）。<br/>
/// <see cref="Control"/> を生成しないため、Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// <see cref="Window"/> を生成するテスト（<see cref="NoOpElementFinderTests"/>）のみヘッドレスで直列実行する。
/// </remarks>
public class NoOpElementFinderBehaviorTests
{
    [Fact]
    public void NoOpElementFinder_指定位置ベースも未検出を返す()
    {
        var finder = NoOpElementFinder.Instance;

        Assert.False(finder.FindWindowUnderCursor(new PixelPoint(500, 500)).IsDetected);
        Assert.False(finder.FindScreenUnderCursor(new PixelPoint(500, 500)).IsDetected);
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
        var handle = new DummyHandle();

        // Handle を設定した結果（ウィンドウ検出時）を構築できる
        var result = new ElementFinderResults(
            new PixelRect(10, 10, 100, 80),
            IsDetected: true,
            Title: "Test Window",
            Handle: handle);

        Assert.True(result.IsDetected);
        Assert.Equal("Test Window", result.Title);
        Assert.Same(handle, result.Handle);
    }

    [Fact]
    public void ElementFinderResults_ClassName付きで構築できる()
    {
        var handle = new DummyHandle();

        // クラス名を設定した結果（ウィンドウ検出時。デスクトップ除外の検証用）を構築できる
        var result = new ElementFinderResults(
            new PixelRect(10, 10, 100, 80),
            IsDetected: true,
            Title: "Test Window",
            Handle: handle,
            ClassName: "CabinetWClass");

        Assert.Equal("CabinetWClass", result.ClassName);
        // 省略時は null（後方互換）
        var without = new ElementFinderResults(new PixelRect(0, 0, 10, 10), IsDetected: true);
        Assert.Null(without.ClassName);
    }

    /// <summary>テスト用のダミー IPlatformHandle。</summary>
    private sealed class DummyHandle : IPlatformHandle
    {
        public IntPtr Handle => IntPtr.Zero;
        public string HandleDescriptor => "Dummy";
    }
}

/// <summary><see cref="NoOpCursorOverrideGuard"/> の単体テスト（<see cref="Window"/> を生成するためヘッドレス）。</summary>
/// <remarks>
/// <see cref="Window"/> を生成するため、Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）で直列実行する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class NoOpElementFinderTests
{
    [AvaloniaFact]
    public void NoOpCursorOverrideGuard_アタッチしても何もしない()
    {
        var guard = new NoOpCursorOverrideGuard();
        var window = new Avalonia.Controls.Window();

        // ヘッドレス環境でもアタッチは例外なく完了する（IsAttached は false のまま）
        guard.Attach(window);
        Assert.False(guard.IsAttached);
    }
}
