using Estable.Platform.Geometry;
using Xunit;

namespace Estable.Platform.Tests;

/// <summary><see cref="EsRect"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 値型の算術のみを検証するため、UI フレームワークの初期化に依存せず <c>[Fact]</c> で並列実行する。
/// </remarks>
public class EsRectTests
{
    // ====================================================================
    // 境界（Right / Bottom）
    // ====================================================================

    [Fact]
    public void Right_左上Xと幅の和を返す()
    {
        var rect = new EsRect(10.5, 20.25, 100.5, 50.25);

        Assert.Equal(111.0, rect.Right, 6);
    }

    [Fact]
    public void Bottom_左上Yと高さの和を返す()
    {
        var rect = new EsRect(10.5, 20.25, 100.5, 50.25);

        Assert.Equal(70.5, rect.Bottom, 6);
    }

    [Fact]
    public void Right_幅が0なら左上Xを返す()
    {
        var rect = new EsRect(7.5, 3.5, 0, 0);

        Assert.Equal(7.5, rect.Right, 6);
        Assert.Equal(3.5, rect.Bottom, 6);
    }

    [Fact]
    public void Right_負の幅は左上Xより小さい値を返す()
    {
        // 負の幅は空の矩形を表す（境界も左上より小さくなる）
        var rect = new EsRect(100, 100, -20, -30);

        Assert.Equal(80.0, rect.Right, 6);
        Assert.Equal(70.0, rect.Bottom, 6);
    }
}
