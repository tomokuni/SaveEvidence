using System.Collections.Concurrent;
using EsUtil.SaveEvidence.App.Core.Models;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="CapacityLimitedCache"/> の容量管理を検証するテスト。</summary>
/// <remarks>
/// <b>複数スレッドの競合</b>（同じキーを同時に生成した場合に片方を破棄すること）は、待ち合わせの<br/>
/// タイミングに依存するため自動テストでは再現しない（直列化はロックが単一所有する）。
/// </remarks>
public class CapacityLimitedCacheTests
{
    /// <summary>同じキーではファクトリを 1 回だけ実行することを検証する。</summary>
    [Fact]
    public void 同じキーではファクトリを一度だけ実行する()
    {
        var cache = new ConcurrentDictionary<string, DisposableValue>();
        var calls = 0;

        var first = CapacityLimitedCache.GetOrAdd(cache, 4, "a", _ =>
        {
            calls++;
            return new DisposableValue();
        });
        var second = CapacityLimitedCache.GetOrAdd(cache, 4, "a", _ =>
        {
            calls++;
            return new DisposableValue();
        });

        Assert.Equal(1, calls);
        Assert.Same(first, second);
    }

    /// <summary>上限ちょうどまでは既存の値を保持することを検証する。</summary>
    [Fact]
    public void 上限ちょうどまでは既存の値を保持する()
    {
        var cache = new ConcurrentDictionary<int, DisposableValue>();
        var first = CapacityLimitedCache.GetOrAdd(cache, 2, 1, _ => new DisposableValue());
        var second = CapacityLimitedCache.GetOrAdd(cache, 2, 2, _ => new DisposableValue());

        Assert.False(first.IsDisposed);
        Assert.False(second.IsDisposed);
        Assert.Equal(2, cache.Count);
    }

    /// <summary>上限を超える新規登録で既存の値を破棄してから登録することを検証する。</summary>
    [Fact]
    public void 上限を超えると既存の値を破棄する()
    {
        var cache = new ConcurrentDictionary<int, DisposableValue>();
        var first = CapacityLimitedCache.GetOrAdd(cache, 2, 1, _ => new DisposableValue());
        _ = CapacityLimitedCache.GetOrAdd(cache, 2, 2, _ => new DisposableValue());
        var third = CapacityLimitedCache.GetOrAdd(cache, 2, 3, _ => new DisposableValue());

        Assert.True(first.IsDisposed);
        Assert.Single(cache);
        Assert.False(third.IsDisposed);
    }

    /// <summary>容量が 0 以下の場合も直近の 1 件だけを保持することを検証する。</summary>
    [Fact]
    public void 容量が0以下の場合も直近の1件だけを保持する()
    {
        var cache = new ConcurrentDictionary<int, DisposableValue>();
        var first = CapacityLimitedCache.GetOrAdd(cache, 0, 1, _ => new DisposableValue());
        var second = CapacityLimitedCache.GetOrAdd(cache, 0, 2, _ => new DisposableValue());

        Assert.True(first.IsDisposed);
        Assert.Single(cache);
        Assert.False(second.IsDisposed);
    }

    /// <summary>破棄可能なテスト用の値。</summary>
    private sealed class DisposableValue : IDisposable
    {
        /// <summary>破棄済みかどうかを取得する。</summary>
        public bool IsDisposed { get; private set; }

        /// <inheritdoc />
        public void Dispose() => IsDisposed = true;
    }
}
