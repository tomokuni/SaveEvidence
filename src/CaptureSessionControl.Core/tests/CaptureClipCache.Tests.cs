using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;

namespace Estable.CaptureSession.Tests;

/// <summary><see cref="CaptureClipCache"/> の仕様テスト（UI 非依存・並列実行）。</summary>
/// <remarks>同一判定・所有権の受け渡し・旧値の破棄順序を固定する。</remarks>
public class CaptureClipCacheTests
{
    /// <summary>検証用の画像を生成する。</summary>
    /// <returns>4x3 の画像。</returns>
    private static EsBitmap CreateImage() => EsBitmap.Create(4, 3);

    [Fact]
    public void Store_格納した矩形と画像を保持する()
    {
        using var cache = new CaptureClipCache();
        cache.Store(new EsPixelRect(10, 20, 41, 31), CreateImage());

        Assert.True(cache.HasCached(new EsPixelRect(10, 20, 41, 31)));
        Assert.True(cache.HasBitmap);
    }

    [Fact]
    public void HasCached_矩形が異なる場合はfalseを返す()
    {
        using var cache = new CaptureClipCache();
        cache.Store(new EsPixelRect(10, 20, 41, 31), CreateImage());

        // 1 ピクセルでも違えば同じ画像にならないため再切り出しする
        Assert.False(cache.HasCached(new EsPixelRect(11, 20, 41, 31)));
        Assert.False(cache.HasCached(new EsPixelRect(10, 20, 40, 31)));
    }

    [Fact]
    public void HasCached_未格納の場合はfalseを返す()
    {
        using var cache = new CaptureClipCache();

        Assert.False(cache.HasCached(new EsPixelRect(0, 0, 1, 1)));
        Assert.False(cache.HasBitmap);
    }

    [Fact]
    public void Take_一致する画像を返して以降のDisposeで破棄しない()
    {
        using var cache = new CaptureClipCache();
        cache.Store(new EsPixelRect(10, 20, 41, 31), CreateImage());

        var taken = cache.Take(new EsPixelRect(10, 20, 41, 31));

        Assert.NotNull(taken);
        Assert.False(cache.HasBitmap); // 所有権が移っている

        // Dispose しても取り出した画像は生きたまま（二重破棄しない）
        cache.Dispose();
        Assert.False(taken!.IsDisposed);
        taken.Dispose();
    }

    [Fact]
    public void Take_矩形が異なる場合はnullを返してキャッシュを維持する()
    {
        using var cache = new CaptureClipCache();
        cache.Store(new EsPixelRect(10, 20, 41, 31), CreateImage());

        Assert.Null(cache.Take(new EsPixelRect(99, 99, 1, 1)));
        Assert.True(cache.HasBitmap); // 一致しない取り出しでは失われない
    }

    [Fact]
    public void Take_2回目はnullを返す()
    {
        using var cache = new CaptureClipCache();
        cache.Store(new EsPixelRect(10, 20, 41, 31), CreateImage());

        using var first = cache.Take(new EsPixelRect(10, 20, 41, 31));

        Assert.Null(cache.Take(new EsPixelRect(10, 20, 41, 31)));
    }

    [Fact]
    public void Store_置き換え時に旧い画像を破棄する()
    {
        using var cache = new CaptureClipCache();
        var old = CreateImage();
        cache.Store(new EsPixelRect(0, 0, 1, 1), old);

        cache.Store(new EsPixelRect(5, 5, 2, 2), CreateImage());

        Assert.True(old.IsDisposed);
        Assert.True(cache.HasCached(new EsPixelRect(5, 5, 2, 2)));
    }

    [Fact]
    public void Store_nullは無視して現在のキャッシュを維持する()
    {
        using var cache = new CaptureClipCache();
        cache.Store(new EsPixelRect(10, 20, 41, 31), CreateImage());

        cache.Store(new EsPixelRect(99, 99, 1, 1), null);

        Assert.True(cache.HasCached(new EsPixelRect(10, 20, 41, 31)));
    }

    [Fact]
    public void StoreCount_格納した回数を数える()
    {
        using var cache = new CaptureClipCache();

        cache.Store(new EsPixelRect(0, 0, 1, 1), CreateImage());
        cache.Store(new EsPixelRect(1, 1, 1, 1), CreateImage());

        Assert.Equal(2, cache.StoreCount);
    }

    [Fact]
    public void Store_破棄後は渡された画像をその場で破棄する()
    {
        var cache = new CaptureClipCache();
        cache.Dispose();
        var bitmap = CreateImage();

        cache.Store(new EsPixelRect(0, 0, 1, 1), bitmap);

        // 保持しない（破棄漏れを防ぐ）ため、呼び出し側の後片付けは不要
        Assert.True(bitmap.IsDisposed);
        Assert.False(cache.HasBitmap);
    }

    [Fact]
    public void Dispose_複数回呼んでも安全である()
    {
        var cache = new CaptureClipCache();
        cache.Store(new EsPixelRect(0, 0, 1, 1), CreateImage());

        cache.Dispose();
        cache.Dispose();

        Assert.False(cache.HasBitmap);
    }

    [Fact]
    public void Dispose_保持している画像を破棄する()
    {
        var cache = new CaptureClipCache();
        var bitmap = CreateImage();
        cache.Store(new EsPixelRect(0, 0, 1, 1), bitmap);

        cache.Dispose();

        Assert.True(bitmap.IsDisposed);
    }
}
