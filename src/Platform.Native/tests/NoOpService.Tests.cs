using EsUtil.Platform.Native.NoOp;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Native.Tests;

/// <summary>NoOp 実装（プラットフォーム非対応のフォールバック）の単体テスト。</summary>
/// <remarks>
/// NoOp 系は内部型（internal）のため、<see cref="PlatformFactory"/> 経由ではなく直接生成して検証する。<br/>
/// いずれも「例外なく安全に動作する（フォールバック）」ことを検証するため、ヘッドレス実行を必要としない。
/// </remarks>
public class NoOpServiceTests
{
    // ====================================================================
    // NoOpCursorService
    // ====================================================================

    [Fact]
    public void NoOpCursorService_位置取得はデフォルトを返す()
    {
        var service = NoOpCursorService.Instance;

        Assert.Equal(default, service.GetCursorScreenPoint());
    }

    [Fact]
    public void NoOpCursorService_位置移動と表示切替は何もしない()
    {
        var service = NoOpCursorService.Instance;

        // 例外なく完了する
        service.SetCursorScreenPoint(new EsPixelPoint(10, 20));
        service.ShowCursor(true);
        service.ShowCursor(false);
    }

    // ====================================================================
    // NoOpCaptureService
    // ====================================================================

    [Fact]
    public void NoOpCaptureService_キャプチャは常にnullを返す()
    {
        var service = NoOpCaptureService.Instance;

        Assert.Null(service.CaptureArea(new EsPixelRect(0, 0, 100, 100)));
        Assert.Null(service.CaptureVirtualScreen(out var origin));
        Assert.Equal(default, origin);
    }

    // ====================================================================
    // NoOpWindowService
    // ====================================================================

    [Fact]
    public void NoOpWindowService_操作は何もしない()
    {
        var service = NoOpWindowService.Instance;
        var handle = new EsPlatformHandle(0x1234, "Dummy");

        service.HideFromTaskSwitcher(handle);
        service.EvacuateWindow(handle);

        // キャプチャ除外は常に false
        Assert.False(service.SetCaptureExclusion(handle, true));
        Assert.False(service.SetCaptureExclusion(handle, false));

        // 位置・サイズの指定も常に false（何もしない）
        Assert.False(service.SetBounds(handle, new EsPixelRect(0, 0, 100, 100)));
    }

    // ====================================================================
    // NoOpCursorOverrideGuard
    // ====================================================================

    [Fact]
    public void NoOpCursorOverrideGuard_アタッチしても抑止されない()
    {
        var guard = new NoOpCursorOverrideGuard();

        // IsAttached は常に false（抑止なし）
        Assert.False(guard.IsAttached);
        guard.Dispose();
    }
}
