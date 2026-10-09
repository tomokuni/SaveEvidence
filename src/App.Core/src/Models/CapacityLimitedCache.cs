using System.Collections.Concurrent;

namespace EsUtil.SaveEvidence.App.Core.Models;

/// <summary>容量制限付きの静的キャッシュ操作を提供するヘルパー。</summary>
/// <remarks>
/// キャッシュの容量管理（上限超過時のリセット）を一元化する。<br/>
/// 各クラスは <see cref="ConcurrentDictionary{TKey, TValue}"/> と容量上限を指定して本ヘルパーへ委譲する。
/// </remarks>
internal static class CapacityLimitedCache
{
    /// <summary>上限判定・破棄・登録を直列化するロック。</summary>
    private static readonly Lock s_trimLock = new();

    /// <summary>キャッシュから値を取得し、無ければ生成して登録する。</summary>
    /// <typeparam name="TKey">キー型。</typeparam>
    /// <typeparam name="TValue">値型。</typeparam>
    /// <param name="cache">対象のキャッシュ。</param>
    /// <param name="capacity">保持数の上限。 上限到達後の新規登録時に全クリアでリセットする。</param>
    /// <param name="key">キー。</param>
    /// <param name="factory">値の生成関数（キーを受け取る）。</param>
    /// <returns>キャッシュされた値。</returns>
    public static TValue GetOrAdd<TKey, TValue>(
        ConcurrentDictionary<TKey, TValue> cache, int capacity, TKey key, Func<TKey, TValue> factory)
        where TKey : notnull
    {
        if (cache.TryGetValue(key, out var value)) return value;

        value = factory(key);

        // 上限判定と破棄・登録は 1 つのロックで行う
        // （背景のサムネイル生成のように複数スレッドから呼ぶと、2 つのスレッドが同時に上限超過を検出し、
        //  同じ値を 2 回破棄する・先に入れた値を上書きして漏らす。 ファクトリの実行はロックの外に置くので、
        //  生成（復号など）は並列のままである）
        lock (s_trimLock)
        {
            // 待っている間に別のスレッドが同じキーを登録していた場合は、その値を返す（作った値は破棄する）
            if (cache.TryGetValue(key, out var registered))
            {
                if (value is IDisposable created)
                {
                    created.Dispose();
                }

                return registered;
            }

            if (cache.Count >= capacity)
            {
                // IDisposable 値（Bitmap 等）は破棄してからクリアする（リソースリーク防止）
                foreach (var cached in cache.Values)
                {
                    if (cached is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                cache.Clear();
            }
            cache[key] = value;
            return value;
        }
    }
}
