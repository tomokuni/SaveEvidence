using EsUtil.SaveEvidence.App.Core.Services;
using Xunit;

namespace EsUtil.SaveEvidence.App.Core.Tests;

/// <summary><see cref="AppVersion"/> の仕様テスト（表記の整形）。</summary>
public class AppVersionTests
{
    [Fact]
    internal void Current_は空でない()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppVersion.Current));
    }

    [Fact]
    internal void Current_は末尾の0を省く()
    {
        // Directory.Build.props の <Version> は 3 桁（0.1.0）。 4 桁目（Revision=0）だけを省く
        Assert.DoesNotMatch(@"^\d+\.\d+\.\d+\.0$", AppVersion.Current);
        Assert.Matches(@"^\d+\.\d+\.\d+", AppVersion.Current);
    }

    [Fact]
    internal void For_はUI名と版を空白2つとv付きで並べる()
    {
        Assert.Equal($"AvaloniaUI  v{AppVersion.Current}", AppVersion.For("AvaloniaUI"));
    }

    [Fact]
    internal void For_はUI名が空ならv付きの版だけを返す()
    {
        Assert.Equal($"v{AppVersion.Current}", AppVersion.For(""));
        Assert.Equal($"v{AppVersion.Current}", AppVersion.For("   "));
    }
}
