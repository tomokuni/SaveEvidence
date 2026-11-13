using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="CaptureLauncher"/> の単体テスト（多重起動の防止と状態の戻し）。</summary>
/// <remarks>
/// <see cref="CaptureLauncher"/> はプロセスで 1 つの状態を持つため、テストは直列に実行する
/// （<see cref="CaptureLauncher.IsCapturing"/> を共有する）。
/// </remarks>
[Collection(nameof(CaptureLauncherTests))]
public class CaptureLauncherTests
{
    /// <summary>撮影中は次の要求を無視することを検証する。</summary>
    /// <returns>非同期テストのタスク。</returns>
    [Fact]
    internal async Task Start_実行中は次の要求を無視する()
    {
        var first = new TaskCompletionSource();
        var started = 0;

        CaptureLauncher.Start(() =>
        {
            started++;
            return first.Task;
        });

        // 実行中の 2 回目は開始しない（ホットキーの連打でオーバーレイを重ねない）
        CaptureLauncher.Start(() =>
        {
            started++;
            return Task.CompletedTask;
        });

        Assert.True(CaptureLauncher.IsCapturing);
        Assert.Equal(1, started);

        first.SetResult();
        await WaitUntilIdleAsync();

        Assert.False(CaptureLauncher.IsCapturing);
        Assert.Equal(1, started);
    }

    /// <summary>完了すると再び開始できることを検証する。</summary>
    /// <returns>非同期テストのタスク。</returns>
    [Fact]
    internal async Task Start_完了後は再び開始できる()
    {
        var started = 0;

        CaptureLauncher.Start(() =>
        {
            started++;
            return Task.CompletedTask;
        });
        await WaitUntilIdleAsync();

        CaptureLauncher.Start(() =>
        {
            started++;
            return Task.CompletedTask;
        });
        await WaitUntilIdleAsync();

        Assert.Equal(2, started);
        Assert.False(CaptureLauncher.IsCapturing);
    }

    /// <summary>失敗しても状態が戻り、例外にならないことを検証する。</summary>
    /// <returns>非同期テストのタスク。</returns>
    /// <remarks>
    /// 撮影の失敗（画面取得不可等）は握って継続する（原因は撮影スタックが記録済み）。
    /// 状態が戻らないと、以降の撮影がすべて無視されて操作できなくなる。
    /// </remarks>
    [Fact]
    internal async Task Start_失敗しても状態が戻る()
    {
        CaptureLauncher.Start(static () => throw new InvalidOperationException("test"));

        await WaitUntilIdleAsync();

        Assert.False(CaptureLauncher.IsCapturing);
    }

    /// <summary>引数が null の場合は例外になることを検証する。</summary>
    [Fact]
    internal void Start_引数がnullの場合は例外になる()
        => Assert.Throws<ArgumentNullException>(() => CaptureLauncher.Start(null!));

    /// <summary>撮影中でなくなるまで待つ（開始したタスクは保持しないため、状態を観測する）。</summary>
    /// <returns>待機が完了したときに完了するタスク。</returns>
    private static async Task WaitUntilIdleAsync()
    {
        for (var i = 0; i < 100 && CaptureLauncher.IsCapturing; i++)
        {
            await Task.Delay(10);
        }
    }
}
