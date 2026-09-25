using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.LoupeCore.Tests;

/// <summary><see cref="LoupeLogFormat"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 設定値の変更ログが出力する値の書式（両 UI で共通）を検証する。
/// </remarks>
public class LoupeLogFormatTests
{
    [Fact]
    public void Null_未設定はnullと表記する()
    {
        Assert.Equal("null", LoupeLogFormat.Null);
    }

    [Fact]
    public void FormatPoint_座標を括弧付きで表記する()
    {
        Assert.Equal("(123, 456)", LoupeLogFormat.FormatPoint(new EsPoint(123, 456)));
    }

    [Fact]
    public void FormatPoint_小数はそのまま表記する()
    {
        // ドメイン座標（元画像ピクセル）はそのままの値を表示する（丸めない）
        Assert.Equal("(1.5, -2.25)", LoupeLogFormat.FormatPoint(new EsPoint(1.5, -2.25)));
    }

    [Fact]
    public void FormatPoint_未設定はnullと表記する()
    {
        Assert.Equal("null", LoupeLogFormat.FormatPoint(null));
    }

    [Fact]
    public void FormatNumber_整数を文字列化する()
    {
        Assert.Equal("8", LoupeLogFormat.FormatNumber(8));
        Assert.Equal("-3", LoupeLogFormat.FormatNumber(-3));
    }

    [Fact]
    public void FormatNumber_小数を文字列化する()
    {
        // 不変の表記（コンマ区切りのカルチャでもピリオド）を使う
        Assert.Equal("1.5", LoupeLogFormat.FormatNumber(1.5));
        Assert.Equal("1", LoupeLogFormat.FormatNumber(1.0));
    }

    [Fact]
    public void DescribeImage_寸法を記号付きで表記する()
    {
        Assert.Equal("1920x1080", LoupeLogFormat.DescribeImage(1920, 1080));
    }

    [Theory]
    [InlineData(null, 1080)]
    [InlineData(1920, null)]
    [InlineData(null, null)]
    public void DescribeImage_片方でも未設定ならnullと表記する(int? width, int? height)
    {
        Assert.Equal("null", LoupeLogFormat.DescribeImage(width, height));
    }
}
