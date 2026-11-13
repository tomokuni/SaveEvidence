using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="ThumbnailPlaceholder"/> の単体テスト（代替画像の寸法と共有）。</summary>
public class ThumbnailPlaceholderTests
{
    /// <summary>指定寸法の正方形を返すことを検証する。</summary>
    [Fact]
    internal void Get_指定寸法の正方形を返す()
    {
        var placeholder = ThumbnailPlaceholder.Get(96);

        Assert.Equal(96, placeholder.Width);
        Assert.Equal(96, placeholder.Height);
    }

    /// <summary>同じ寸法は同じ実体を共有することを検証する（行ごとに画像を作らない）。</summary>
    [Fact]
    internal void Get_同じ寸法は同じ実体を共有する()
    {
        var first = ThumbnailPlaceholder.Get(64);
        var second = ThumbnailPlaceholder.Get(64);

        Assert.Same(first, second);
    }

    /// <summary>寸法が違えば別の実体になることを検証する。</summary>
    [Fact]
    internal void Get_寸法が違えば別の実体になる()
    {
        var small = ThumbnailPlaceholder.Get(48);
        var large = ThumbnailPlaceholder.Get(192);

        Assert.NotSame(small, large);
        Assert.Equal(48, small.Width);
        Assert.Equal(192, large.Width);
    }

    /// <summary>1 未満の寸法は 1 として扱うことを検証する（0 の画像を作らない）。</summary>
    [Fact]
    internal void Get_1未満の寸法は1として扱う()
    {
        var placeholder = ThumbnailPlaceholder.Get(0);

        Assert.Equal(1, placeholder.Width);
        Assert.Equal(1, placeholder.Height);
    }
}
