using Xunit;

using EsUtil.Feature.Loupe.Presenter;

namespace EsUtil.Feature.Loupe.Presenter.Tests;

/// <summary><see cref="LoupeLogFormat"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// UI がログへ出す値の書式（未設定の表記・画像の寸法）を検証する。<br/>
/// コアが出力する設定値の書式はコアのテストが検証する。
/// </remarks>
public class LoupeLogFormatTests
{
    /// <summary>未設定を表す文字列が期待どおりであることを検証する。</summary>
    [Fact]
    public void Null_未設定の表記はnullである()
    {
        Assert.Equal("null", LoupeLogFormat.Null);
    }

    /// <summary>画像の寸法が幅x高さの形式になることを検証する。</summary>
    [Fact]
    public void DescribeImage_幅と高さをxで連結する()
    {
        Assert.Equal("1920x1080", LoupeLogFormat.DescribeImage(1920, 1080));
    }

    /// <summary>幅または高さが未設定の場合は未設定の表記になることを検証する。</summary>
    /// <param name="width">画像の幅（ピクセル）。</param>
    /// <param name="height">画像の高さ（ピクセル）。</param>
    [Theory]
    [InlineData(null, null)]
    [InlineData(1920, null)]
    [InlineData(null, 1080)]
    public void DescribeImage_片方でも未設定なら未設定の表記になる(int? width, int? height)
    {
        Assert.Equal("null", LoupeLogFormat.DescribeImage(width, height));
    }
}
