using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Contract;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Session;
using Xunit;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.Session;

/// <summary><see cref="CaptureOwnerScope"/> の単体テスト（退避の順序・復帰・除外の解除・条件分岐）。</summary>
public class CaptureOwnerScopeTests
{
    [Fact]
    internal void Enter_除外に成功した場合は退避を予約する()
    {
        var log = new List<string>();
        var service = new FakeWindowService(log);
        var owner = new FakeOwner(log);

        using (var scope = CaptureOwnerScope.Enter(service, new EsPlatformHandle(1, "HWND"), owner, hideOwner: true))
        {
            // 除外を設定できた場合は、退避をオーバーレイ表示後まで遅らせる（撮影時にデスクトップを見せない）
            Assert.True(scope.IsExcluded);
            Assert.Equal(["exclude:True"], log);

            // 予約した退避を実行する（2 回目以降は何もしない）
            scope.ApplyPendingHide();
            scope.ApplyPendingHide();
            Assert.Equal(["exclude:True", "hide"], log);
        }

        // 復帰（配置 → 除外解除の順）
        Assert.Equal(["exclude:True", "hide", "restore", "exclude:False"], log);
    }

    [Fact]
    internal void Enter_除外に失敗した場合は撮影の前に退避する()
    {
        var log = new List<string>();
        var service = new FakeWindowService(log) { CanExclude = false };
        var owner = new FakeOwner(log);

        using (var scope = CaptureOwnerScope.Enter(service, new EsPlatformHandle(1, "HWND"), owner, hideOwner: true))
        {
            // 除外できないと自ウィンドウが写るため、予約せずに退避する（ApplyPendingHide は何もしない）
            Assert.False(scope.IsExcluded);
            Assert.Equal(["exclude:True", "hide"], log);

            scope.ApplyPendingHide();
            Assert.Equal(["exclude:True", "hide"], log);
        }

        // 設定できていないため解除も呼ばない（無駄な API 呼び出しを避ける）
        Assert.Equal(["exclude:True", "hide", "restore"], log);
    }

    [Fact]
    internal void Enter_設定が偽の場合は退避しない()
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
    internal void Enter_オーナーが非表示の場合は退避しない()
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
    internal void Dispose_二重に呼んでも復帰は1回だけ()
    {
        var log = new List<string>();
        var service = new FakeWindowService(log);
        var owner = new FakeOwner(log);

        var scope = CaptureOwnerScope.Enter(service, new EsPlatformHandle(1, "HWND"), owner, hideOwner: true);
        scope.ApplyPendingHide();
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

        public void SetBounds(EsPlatformHandle handle, EsRectInt bounds) => log.Add("bounds");

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


