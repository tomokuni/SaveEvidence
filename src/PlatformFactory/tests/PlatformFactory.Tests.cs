using Estable.AvaloniaUI.Platform.Win;
using System.Runtime.Versioning;
using Xunit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary><see cref="PlatformFactory"/> の単体テスト（実行時プラットフォームに応じた既定実装の生成）。</summary>
/// <remarks>
/// テストは Windows 環境で実行される前提。<br/>
/// <see cref="PlatformFactory.CreateCursorService"/> 等は <see cref="OperatingSystem.IsWindows"/> の分岐で実装を生成するだけであり、<br/>
/// Avalonia ヘッドレス（UI スレッド）を必要としないため <c>[Fact]</c> で並列実行する。<br/>
/// プラットフォーム分岐（Windows / 他環境）のうち、Windows 側の生成を検証する。<br/>
/// 他環境（Linux / macOS）の分岐は実機検証が必要なため対象外。
/// </remarks>
public class PlatformFactoryTests
{
    [Fact]
    public void CreateCursorService_WindowsではWinCursorServiceを返す()
    {
        var service = PlatformFactory.CreateCursorService();

        Assert.NotNull(service);
        // 実行プラットフォーム（Windows）では Win32 実装が生成される
        Assert.IsType<WinCursorService>(service);
    }

    [Fact]
    public void CreateElementFinder_WindowsではWinElementFinderを返す()
    {
        var finder = PlatformFactory.CreateElementFinder();

        Assert.NotNull(finder);
        // 実行プラットフォーム（Windows）では Win32 実装が生成される
        Assert.IsType<WinElementFinder>(finder);
    }

    [Fact]
    public void CreateCursorOverrideGuard_WindowsではWinCursorOverrideGuardを返す()
    {
        var guard = PlatformFactory.CreateCursorOverrideGuard();

        Assert.NotNull(guard);
        // 実行プラットフォーム（Windows）では Win32 実装が生成される
        Assert.IsType<WinCursorOverrideGuard>(guard);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void CreateCaptureService_WindowsではWinCaptureServiceを返す()
    {
        var service = PlatformFactory.CreateCaptureService();

        Assert.NotNull(service);
        // 実行プラットフォーム（Windows）では Win32 実装が生成される
        Assert.IsType<WinCaptureService>(service);
    }

    [Fact]
    public void CreateWindowService_WindowsではWinWindowServiceを返す()
    {
        var service = PlatformFactory.CreateWindowService();

        Assert.NotNull(service);
        // 実行プラットフォーム（Windows）では Win32 実装が生成される
        Assert.IsType<WinWindowService>(service);
    }
}
