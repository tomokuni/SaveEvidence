using System.Collections.Concurrent;

namespace EsUtil.SaveEvidence.App.Core.Models;

/// <summary>容量制限付きの静的キャッシュ操作を提供するヘルパー。</summary>
/// <remarks>
/// キャッシュの容量管理（上限超過時のリセット）を一元化する。<br/>
/// 各クラスは <see cref="ConcurrentDictionary{TKey, TValue}"/> と容量上限を指定して本ヘルパーへ委譲する。
/// </remarks>
internal static class CapacityLimitedCache
{
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
