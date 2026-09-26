using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Geometry;

/// <summary><see cref="EsPoint"/> の表示整形テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 座標ラベルの文言のみを検証するため、UI フレームワークのヘッドレス初期化に依存せず<br/>
/// <c>[Fact]</c> で並列実行する。
/// </remarks>
public class EsPointTests
{
    // ====================================================================
    // Format（表示用の整形）
    // ====================================================================

    [Fact]
    public void Format_既定は整数へ丸める()
    {
        // 小数点以下は四捨五入される（既定 0 桁）
        Assert.Equal("(123, 457)", new EsPoint(123.4, 456.7).Format());
    }

    [Fact]
    public void Format_桁数を指定すると指定桁で丸める()
    {
        Assert.Equal("(123.40, 456.70)", new EsPoint(123.4, 456.7).Format(2));
    }

    [Fact]
    public void Format_ゼロは括弧付きの0と表示する()
    {
        Assert.Equal("(0, 0)", new EsPoint(0, 0).Format());
    }

    [Fact]
    public void Format_負の座標も括弧付きで整形する()
    {
        Assert.Equal("(-5, -10)", new EsPoint(-5, -10).Format());
    }

    [Fact]
    public void Format_0桁の指定は既定と同じ結果になる()
    {
        var point = new EsPoint(12.6, -3.4);

        Assert.Equal(point.Format(), point.Format(0));
    }

    [Fact]
    public void Format_最大桁数を指定できる()
    {
        var text = new EsPoint(1.5, 2.5).Format(15);

        Assert.StartsWith("(1.5", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    [InlineData(16)]
    [InlineData(int.MaxValue)]
    public void Format_表示できない桁数は例外(int decimals)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new EsPoint(1, 2).Format(decimals));
}
