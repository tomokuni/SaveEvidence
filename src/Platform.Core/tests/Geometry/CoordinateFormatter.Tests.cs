using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests;

/// <summary><see cref="CoordinateFormatter"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 座標ラベルのテキスト整形のみを検証するため、UI フレームワークのヘッドレス初期化に依存せず<br/>
/// <c>[Fact]</c> で並列実行する。
/// </remarks>
public class CoordinateFormatterTests
{
    [Fact]
    public void Format_整数座標を括弧付きで整形する()
    {
        Assert.Equal("(123, 456)", CoordinateFormatter.Format(new EsPoint(123, 456)));
    }

    [Fact]
    public void Format_小数座標は整数に丸める()
    {
        // F0 により四捨五入される
        Assert.Equal("(123, 457)", CoordinateFormatter.Format(new EsPoint(123.4, 456.7)));
        Assert.Equal("(0, 0)", CoordinateFormatter.Format(new EsPoint(0, 0)));
    }

    [Fact]
    public void Format_負の座標も括弧付きで整形する()
    {
        Assert.Equal("(-5, -10)", CoordinateFormatter.Format(new EsPoint(-5, -10)));
    }
}
