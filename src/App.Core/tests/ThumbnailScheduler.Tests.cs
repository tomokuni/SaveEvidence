using System.Diagnostics;
using EsUtil.SaveEvidence.App.Core.Models;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary>
/// <see cref="ThumbnailScheduler"/> の単体テスト（表示状態による負荷の切り替えと中断）。
/// </summary>
/// <remarks>
/// <b>待ち時間に依存しない書き方</b>: 同時実行の検証は「開始した件数」を生成処理から記録し、
/// <b>ブロックしたまま待つ</b>ことで行う（時間の経過で判定しない。 待機は「起きるのを待つ」向きにだけ使う）。
/// 中断の検証は、記録したトークンの状態だけを見る。
/// </remarks>
public class ThumbnailSchedulerTests
{
    /// <summary>記録が条件を満たすまで待つ（満たさない場合は失敗する）。</summary>
    /// <param name="condition">条件。</param>
    /// <param name="timeoutSeconds">待つ上限（秒）。</param>
    /// <returns>条件が満たされた場合は true。</returns>
    private static async Task<bool> WaitUntilAsync(Func<bool> condition, double timeoutSeconds = 5)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        return condition();
    }

    [Fact]
    internal async Task 非表示では1件ずつ実行する()
    {
        var started = new List<int>();
        var gate = new SemaphoreSlim(0);
        using var scheduler = new ThumbnailScheduler(static _ => { }, idleInterval: TimeSpan.Zero);
        scheduler.IsActive = false;

        for (var i = 0; i < 2; i++)
        {
            var index = i;
            scheduler.Enqueue(async token =>
            {
                lock (started)
                {
                    started.Add(index);
                }

                await gate.WaitAsync(token);
            });
        }

        // 1 件目が開始し、2 件目は 1 件目が終わるまで開始しない
        Assert.True(await WaitUntilAsync(() => { lock (started) { return started.Count == 1; } }));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        lock (started)
        {
            Assert.Equal([0], started);
        }

        gate.Release(2);
        Assert.True(await WaitUntilAsync(() => { lock (started) { return started.Count == 2; } }));
    }

    [Fact]
    internal async Task 表示中は同時実行の上限まで並列に実行する()
    {
        var started = 0;
        var gate = new SemaphoreSlim(0);
        using var scheduler = new ThumbnailScheduler(static _ => { }, maxParallelism: 3);
        scheduler.IsActive = true;

        for (var i = 0; i < 4; i++)
        {
            scheduler.Enqueue(async token =>
            {
                Interlocked.Increment(ref started);
                await gate.WaitAsync(token);
            });
        }

        // 上限（3 件）まで並列に開始し、4 件目は空きを待つ
        Assert.True(await WaitUntilAsync(() => Volatile.Read(ref started) == 3));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(3, Volatile.Read(ref started));

        gate.Release(4);
        Assert.True(await WaitUntilAsync(() => Volatile.Read(ref started) == 4));
    }

    [Fact]
    internal async Task 表示へ切り替わると待機を中断して次を開始する()
    {
        // 非表示の待機を 5 秒にして、切り替えが無ければ次が始まらない状況を作る
        var secondStarted = false;
        using var scheduler = new ThumbnailScheduler(static _ => { }, idleInterval: TimeSpan.FromSeconds(5));
        scheduler.IsActive = false;

        scheduler.Enqueue(static _ => ValueTask.CompletedTask);
        scheduler.Enqueue(token =>
        {
            Volatile.Write(ref secondStarted, true);
            return ValueTask.CompletedTask;
        });

        // 1 件目の完了後は非表示の待機に入る
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False(Volatile.Read(ref secondStarted));

        // 表示へ切り替えると待機の残りを待たずに次が始まる
        scheduler.IsActive = true;
        Assert.True(await WaitUntilAsync(() => Volatile.Read(ref secondStarted), timeoutSeconds: 1));
    }

    [Fact]
    internal async Task Clearは未実行の要求を捨てる()
    {
        var started = new List<int>();
        var gate = new SemaphoreSlim(0);
        using var scheduler = new ThumbnailScheduler(static _ => { }, idleInterval: TimeSpan.Zero);
        scheduler.IsActive = false;

        // 1 件目をブロックして、以降を待ち行列へ積む
        scheduler.Enqueue(async token =>
        {
            lock (started)
            {
                started.Add(0);
            }

            await gate.WaitAsync(token);
        });
        Assert.True(await WaitUntilAsync(() => { lock (started) { return started.Count == 1; } }));

        for (var i = 1; i <= 3; i++)
        {
            var index = i;
            scheduler.Enqueue(token =>
            {
                lock (started)
                {
                    started.Add(index);
                }

                return ValueTask.CompletedTask;
            });
        }

        Assert.Equal(3, scheduler.PendingCount);

        scheduler.Clear();
        Assert.Equal(0, scheduler.PendingCount);

        // 1 件目を終わらせても、捨てた要求は実行されない
        gate.Release();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        lock (started)
        {
            Assert.Equal([0], started);
        }
    }

    [Fact]
    internal async Task Clearは実行中の要求へ中断を伝える()
    {
        var cancelled = false;
        using var scheduler = new ThumbnailScheduler(static _ => { }, idleInterval: TimeSpan.Zero);
        scheduler.IsActive = true;

        scheduler.Enqueue(async token =>
        {
            using var registration = token.Register(() => Volatile.Write(ref cancelled, true));
            await Task.Delay(Timeout.Infinite, token);
        });

        await Task.Delay(50, TestContext.Current.CancellationToken);
        scheduler.Clear();

        Assert.True(await WaitUntilAsync(() => Volatile.Read(ref cancelled)));
    }

    [Fact]
    internal void 破棄後は要求を受け付けない()
    {
        using var scheduler = new ThumbnailScheduler(static _ => { });
        scheduler.Dispose();

        scheduler.Enqueue(static _ => ValueTask.CompletedTask);

        Assert.Equal(0, scheduler.PendingCount);
    }

    [Fact]
    internal async Task 失敗は通知され次の要求は実行される()
    {
        var errors = new List<Exception>();
        var nextRan = false;
        using var scheduler = new ThumbnailScheduler(ex => { lock (errors) { errors.Add(ex); } }, idleInterval: TimeSpan.Zero);
        scheduler.IsActive = true;

        scheduler.Enqueue(static _ => throw new InvalidOperationException("生成に失敗した"));
        scheduler.Enqueue(token =>
        {
            Volatile.Write(ref nextRan, true);
            return ValueTask.CompletedTask;
        });

        Assert.True(await WaitUntilAsync(() => { lock (errors) { return errors.Count == 1; } }));
        Assert.IsType<InvalidOperationException>(errors[0]);
        Assert.True(await WaitUntilAsync(() => Volatile.Read(ref nextRan)));
    }

    [Fact]
    internal async Task 見えている項目を先に実行する()
    {
        var started = new List<string>();
        var gate = new SemaphoreSlim(0);
        using var scheduler = new ThumbnailScheduler(static _ => { }, maxParallelism: 1);
        scheduler.IsActive = true;

        // 1 件目を止めている間に、見えていない項目と見えている項目を積む
        _ = EnqueueBlocking(scheduler, "背景1", gate, started);
        Assert.True(await WaitUntilAsync(() => { lock (started) { return started.Count == 1; } }));

        Enqueue(scheduler, ThumbnailPriority.Background(1), "背景2", started);
        Enqueue(scheduler, ThumbnailPriority.Background(2), "背景3", started);
        Enqueue(scheduler, ThumbnailPriority.Visible(3), "表示", started);

        gate.Release();
        Assert.True(await WaitUntilAsync(() => { lock (started) { return started.Count == 4; } }, timeoutSeconds: 10));

        // 表示中の項目（帯 0）が、見えていない項目（帯 1）より先に実行される
        lock (started)
        {
            Assert.Equal(["背景1", "表示", "背景2", "背景3"], started);
        }
    }

    [Fact]
    internal async Task 見えている中では上方を先に実行する()
    {
        var started = new List<string>();
        var gate = new SemaphoreSlim(0);
        using var scheduler = new ThumbnailScheduler(static _ => { }, maxParallelism: 1);
        scheduler.IsActive = true;

        _ = EnqueueBlocking(scheduler, "止める", gate, started);
        Assert.True(await WaitUntilAsync(() => { lock (started) { return started.Count == 1; } }));

        // 上方（位置 0）を最後に積んでも、位置の小さい順に実行される
        Enqueue(scheduler, ThumbnailPriority.Visible(2), "3番目", started);
        Enqueue(scheduler, ThumbnailPriority.Visible(0), "1番目", started);
        Enqueue(scheduler, ThumbnailPriority.Visible(1), "2番目", started);

        gate.Release();
        Assert.True(await WaitUntilAsync(() => { lock (started) { return started.Count == 4; } }, timeoutSeconds: 10));

        lock (started)
        {
            Assert.Equal(["止める", "1番目", "2番目", "3番目"], started);
        }
    }

    [Fact]
    internal async Task 優先度を変えると次に実行される()
    {
        var started = new List<string>();
        var gate = new SemaphoreSlim(0);
        using var scheduler = new ThumbnailScheduler(static _ => { }, maxParallelism: 1);
        scheduler.IsActive = true;

        _ = EnqueueBlocking(scheduler, "止める", gate, started);
        Assert.True(await WaitUntilAsync(() => { lock (started) { return started.Count == 1; } }));

        var second = Enqueue(scheduler, ThumbnailPriority.Background(1), "背景2", started);
        var third = Enqueue(scheduler, ThumbnailPriority.Background(2), "背景3", started);

        // 3 番目として積んだ項目を「見えている（最上位）」へ変える
        Assert.True(scheduler.Reprioritize(third, ThumbnailPriority.Visible(0)));

        gate.Release();
        Assert.True(await WaitUntilAsync(() => { lock (started) { return started.Count == 3; } }, timeoutSeconds: 10));

        lock (started)
        {
            Assert.Equal(["止める", "背景3", "背景2"], started);
        }

        // 実行が始まった後・同じ優先度の場合は変えない
        Assert.False(scheduler.Reprioritize(second, ThumbnailPriority.Visible(1)));
        Assert.False(scheduler.Reprioritize(third, ThumbnailPriority.Visible(0)));
    }

    /// <summary>指定の優先度で要求を積み、開始時に名前を記録する。</summary>
    /// <param name="scheduler">対象のスケジューラ。</param>
    /// <param name="priority">優先度。</param>
    /// <param name="name">記録する名前。</param>
    /// <param name="started">記録先。</param>
    /// <returns>積んだ要求。</returns>
    private static ThumbnailScheduler.ThumbnailRequest Enqueue(
        ThumbnailScheduler scheduler, ThumbnailPriority priority, string name, List<string> started)
        => scheduler.Enqueue(priority, token =>
        {
            lock (started)
            {
                started.Add(name);
            }

            return ValueTask.CompletedTask;
        });

    /// <summary>開始を記録してゲートで待つ生成処理を積む（後続の実行を止めておくために使う）。</summary>
    /// <param name="scheduler">対象のスケジューラ。</param>
    /// <param name="name">記録する名前。</param>
    /// <param name="gate">待つゲート。</param>
    /// <param name="started">記録先。</param>
    /// <returns>積んだ要求。</returns>
    private static ThumbnailScheduler.ThumbnailRequest EnqueueBlocking(
        ThumbnailScheduler scheduler, string name, SemaphoreSlim gate, List<string> started)
        => scheduler.Enqueue(ThumbnailPriority.Background(0), async token =>
        {
            lock (started)
            {
                started.Add(name);
            }

            await gate.WaitAsync(token).ConfigureAwait(false);
        });
}
