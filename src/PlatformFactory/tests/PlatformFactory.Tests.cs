using Estable.AvaloniaUI.Platform.Win;
using Avalonia.Headless.XUnit;
using System.Runtime.Versioning;
using Xunit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary><see cref="PlatformFactory"/> の単体テスト（実行時プラットフォームに応じた既定実装の生成）。</summary>
/// <remarks>
/// テストは Windows 環境で実行される前提（Avalonia.Headless は Windows 上で動作）。<br/>
/// プラットフォーム分岐（Windows / 他環境）のうち、Windows 側の生成を検証する。<br/>
/// 他環境（Linux / macOS）の分岐は実機検証が必要なため対象外。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class PlatformFactoryTests
{
    [AvaloniaFact]
    public void CreateCursorService_WindowsではWinCursorServiceを返す()
    {
        var service = PlatformFactory.CreateCursorService();

        Assert.NotNull(service);
        // 実行プラットフォーム（Windows）では Win32 実装が生成される
        Assert.IsType<WinCursorService>(service);
    }

    [AvaloniaFact]
    public void CreateElementFinder_WindowsではWinElementFinderを返す()
    {
        var finder = PlatformFactory.CreateElementFinder();

        Assert.NotNull(finder);
        // 実行プラットフォーム（Windows）では Win32 実装が生成される
        Assert.IsType<WinElementFinder>(finder);
    }

    [AvaloniaFact]
    public void CreateCursorOverrideGuard_WindowsではWinCursorOverrideGuardを返す()
    {
        var guard = PlatformFactory.CreateCursorOverrideGuard();

        Assert.NotNull(guard);
        // 実行プラットフォーム（Windows）では Win32 実装が生成される
        Assert.IsType<WinCursorOverrideGuard>(guard);
    }

    [AvaloniaFact]
    [SupportedOSPlatform("windows")]
    public void CreateCaptureService_WindowsではWinCaptureServiceを返す()
    {
        var service = PlatformFactory.CreateCaptureService();

        Assert.NotNull(service);
        // 実行プラットフォーム（Windows）では Win32 実装が生成される
        Assert.IsType<WinCaptureService>(service);
    }

    [AvaloniaFact]
    public void CreateWindowService_WindowsではWinWindowServiceを返す()
    {
        var service = PlatformFactory.CreateWindowService();

        Assert.NotNull(service);
        // 実行プラットフォーム（Windows）では Win32 実装が生成される
        Assert.IsType<WinWindowService>(service);
    }
}
