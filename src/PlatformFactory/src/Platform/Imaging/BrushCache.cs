using System.Collections.Concurrent;
using Avalonia.Media;

namespace Estable.AvaloniaUI.Platform.Imaging;

/// <summary>色に対応するブラシの容量制限付きキャッシュ（Flyweight・スレッド安全）。</summary>
/// <remarks>
/// <see cref="Color"/> をキーに <see cref="SolidColorBrush"/> を共有し、容量超過時は全クリアして<br/>
/// 無制限のメモリ増加を防ぐ。<br/>
/// <see cref="LoupeControl"/> / <see cref="DragRectControl"/> の描画で共用する（重複排除・DRY）。<br/>
/// UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
public sealed class BrushCache
{
    /// <summary>ブラシキャッシュの最大保持数。</summary>
    public const int MaxCapacity = 128;

    private readonly ConcurrentDictionary<Color, SolidColorBrush> _cache = [];
    private readonly int _capacity;

    /// <summary>既定容量（<see cref="MaxCapacity"/>）のキャッシュを生成する。</summary>
    public BrushCache() : this(MaxCapacity) { }

    /// <summary>指定した容量のキャッシュを生成する。</summary>
    /// <param name="capacity">キャッシュの最大保持数（正の値）。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> が 1 未満の場合。</exception>
    public BrushCache(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
    }

    /// <summary>指定された色に対応するブラシを返す（未登録なら生成して登録・容量超過時は全クリア）。</summary>
    /// <param name="color">ブラシの色。</param>
    /// <returns>色に対応する <see cref="SolidColorBrush"/>。</returns>
    public SolidColorBrush GetOrCreate(Color color)
    {
        if (_cache.TryGetValue(color, out var cached))
        {
            return cached;
        }

        var brush = new SolidColorBrush(color);
        if (_cache.Count >= _capacity)
        {
            _cache.Clear();
        }
        _cache[color] = brush;
        return brush;
    }

    /// <summary>キャッシュ内のブラシ数を取得する（テスト用）。</summary>
    public int Count => _cache.Count;
}
