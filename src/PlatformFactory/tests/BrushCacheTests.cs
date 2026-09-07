using Avalonia.Media;
using Estable.AvaloniaUI.Platform.Imaging;
using Xunit;

namespace Estable.AvaloniaUI.Platform.Tests;

/// <summary><see cref="BrushCache"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 色 → ブラシの容量制限付きキャッシュのみを検証するため、<br/>
/// Avalonia ヘッドレス（<c>[AvaloniaFact]</c>）に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// <c>SolidColorBrush</c> の生成は UI スレッド非依存（<see cref="StringToBrushConverter"/> と同様）である。<br/>
/// <see cref="LoupeControl"/> / <see cref="DragRectControl"/> で共用する。
/// </remarks>
public class BrushCacheTests
{
    [Fact]
    public void GetOrCreate_同じ色は同じブラシインスタンスを返す()
    {
        var cache = new BrushCache();

        var first = cache.GetOrCreate(Colors.Red);
        var second = cache.GetOrCreate(Colors.Red);

        Assert.Same(first, second);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void GetOrCreate_異なる色は別ブラシインスタンスを返す()
    {
        var cache = new BrushCache();

        var red = cache.GetOrCreate(Colors.Red);
        var blue = cache.GetOrCreate(Colors.Blue);

        Assert.NotSame(red, blue);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void GetOrCreate_容量超過時はクリアされて再登録される()
    {
        var cache = new BrushCache();

        // 容量（MaxCapacity）を超えて再登録する。超過時は全クリアされ無制限の増加を防ぐ。
        for (var i = 0; i < BrushCache.MaxCapacity + 5; i++)
        {
            cache.GetOrCreate(Color.FromRgb((byte)i, 0, 0));
        }

        // 全クリアされても例外なくブラシを返す（クリア後の登録で Count <= MaxCapacity になる）
        Assert.True(cache.Count <= BrushCache.MaxCapacity);
    }

    [Fact]
    public void コンストラクタ_容量が1未満なら例外を投げる()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BrushCache(0));
    }
}
