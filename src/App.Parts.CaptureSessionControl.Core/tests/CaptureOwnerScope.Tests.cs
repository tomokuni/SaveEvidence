using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Native;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CaptureSession.Tests;

/// <summary><see cref="CaptureOwnerScope"/> の単体テスト（退避の順序・復帰・除外の解除・条件分岐）。</summary>
public class CaptureOwnerScopeTests
{
    [Fact]
    public void Enter_除外を設定してから退避する()
    {
        var log = new List<string>();
        var service = new FakeWindowService(log);
        var owner = new FakeOwner(log);

        using (CaptureOwnerScope.Enter(service, new EsPlatformHandle(1, "HWND"), owner, hideOwner: true))
        {
            // 撮影中の状態
            Assert.Equal(["exclude:True", "hide"], log);
        }

        // 復帰（配置 → 除外解除の順）
        Assert.Equal(["exclude:True", "hide", "restore", "exclude:False"], log);
    }

    [Fact]
    public void Enter_設定が偽の場合は退避しない()
    {
        var log = new List<string>();
        var service = new FakeWindowService(log);
        var owner = new FakeOwner(log);

        using (CaptureOwnerScope.Enter(service, new EsPlatformHandle(1, "HWND"), owner, hideOwner: false))
        {
            Assert.Empty(log);
        }

        Assert.Empty(log);
    }

    [Fact]
    public void Enter_オーナーが非表示の場合は退避しない()
    {
        var log = new List<string>();
        var service = new FakeWindowService(log);
        var owner = new FakeOwner(log) { IsVisible = false };

        using (CaptureOwnerScope.Enter(service, new EsPlatformHandle(1, "HWND"), owner, hideOwner: true))
        {
            Assert.Empty(log);
        }

        Assert.Empty(log);
    }

    [Fact]
    public void Dispose_除外の設定に失敗した場合は解除しない()
    {
        var log = new List<string>();
        var service = new FakeWindowService(log) { CanExclude = false };
        var owner = new FakeOwner(log);

        using (CaptureOwnerScope.Enter(service, new EsPlatformHandle(1, "HWND"), owner, hideOwner: true))
        {
            Assert.Equal(["exclude:True", "hide"], log);
        }

        // 設定できていないため解除も呼ばない（無駄な API 呼び出しを避ける）
        Assert.Equal(["exclude:True", "hide", "restore"], log);
    }

    [Fact]
    public void Dispose_二重に呼んでも復帰は1回だけ()
    {
        var log = new List<string>();
        var service = new FakeWindowService(log);
        var owner = new FakeOwner(log);

        var scope = CaptureOwnerScope.Enter(service, new EsPlatformHandle(1, "HWND"), owner, hideOwner: true);
        scope.Dispose();
        scope.Dispose();

        Assert.Equal(["exclude:True", "hide", "restore", "exclude:False"], log);
    }

    /// <summary>ウィンドウ操作サービスのフェイク（呼び出し順を記録する）。</summary>
    private sealed class FakeWindowService(List<string> log) : IWindowService
    {
        /// <summary>キャプチャ除外を成功させるかどうか。</summary>
        public bool CanExclude { get; init; } = true;

        public void HideFromTaskSwitcher(EsPlatformHandle handle) => log.Add("task-switcher");

        public bool SetCaptureExclusion(EsPlatformHandle handle, bool exclude)
        {
            log.Add($"exclude:{exclude}");
            return CanExclude;
        }

        public bool SetBounds(EsPlatformHandle handle, EsRectInt bounds) => true;

        public void EvacuateWindow(EsPlatformHandle handle) => log.Add("evacuate");
    }

    /// <summary>オーナーウィンドウのフェイク（呼び出し順を記録する）。</summary>
    private sealed class FakeOwner(List<string> log) : ICaptureOwnerWindow
    {
        /// <inheritdoc/>
        public bool IsVisible { get; init; } = true;

        /// <inheritdoc/>
        public void HideOffScreen() => log.Add("hide");

        /// <inheritdoc/>
        public void RestorePlacement() => log.Add("restore");
    }
}
