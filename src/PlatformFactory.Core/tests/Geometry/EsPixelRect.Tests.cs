using Estable.Platform.Geometry;
using Xunit;

namespace Estable.Platform.Tests;

/// <summary><see cref="EsPixelRect"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 値型の算術のみを検証するため、UI フレームワークの初期化に依存せず <c>[Fact]</c> で並列実行する。
/// </remarks>
public class EsPixelRectTests
{
    // ====================================================================
    // 境界（Right / Bottom / BottomRight / Center）
    // ====================================================================

    [Fact]
    public void Right_左端と幅の和を返す()
    {
        var rect = new EsPixelRect(10, 20, 100, 50);

        Assert.Equal(110, rect.Right);
    }

    [Fact]
    public void Bottom_上端と高さの和を返す()
    {
        var rect = new EsPixelRect(10, 20, 100, 50);

        Assert.Equal(70, rect.Bottom);
    }

    [Fact]
    public void BottomRight_右端と下端を返す()
    {
        var rect = new EsPixelRect(10, 20, 100, 50);

        Assert.Equal(new EsPixelPoint(110, 70), rect.BottomRight);
    }

    [Fact]
    public void Center_中心座標を返す()
    {
        var rect = new EsPixelRect(10, 20, 100, 50);

        // 幅・高さを 2 で割った整数を左上に加算する
        Assert.Equal(new EsPixelPoint(60, 45), rect.Center);
    }

    [Fact]
    public void Center_端数は切り捨てる()
    {
        var rect = new EsPixelRect(0, 0, 101, 51);

        Assert.Equal(new EsPixelPoint(50, 25), rect.Center);
    }

    // ====================================================================
    // Contains
    // ====================================================================

    [Fact]
    public void Contains_内側の点を含む()
    {
        var rect = new EsPixelRect(10, 20, 100, 50);

        Assert.True(rect.Contains(new EsPixelPoint(50, 40)));
    }

    [Fact]
    public void Contains_左端と上端を含む()
    {
        var rect = new EsPixelRect(10, 20, 100, 50);

        // 左端・上端は含む（半開区間の開始側）
        Assert.True(rect.Contains(new EsPixelPoint(10, 20)));
    }

    [Fact]
    public void Contains_右端と下端は含まない()
    {
        var rect = new EsPixelRect(10, 20, 100, 50);

        // 右端・下端は含まない（半開区間の終端側）
        Assert.False(rect.Contains(new EsPixelPoint(110, 70)));
        Assert.True(rect.Contains(new EsPixelPoint(109, 69)));
    }

    [Fact]
    public void Contains_外側の点を含まない()
    {
        var rect = new EsPixelRect(10, 20, 100, 50);

        Assert.False(rect.Contains(new EsPixelPoint(9, 40)));
        Assert.False(rect.Contains(new EsPixelPoint(50, 19)));
    }

    [Fact]
    public void Contains_空の矩形は常に含まない()
    {
        var rect = new EsPixelRect(10, 20, 0, 0);

        Assert.False(rect.Contains(new EsPixelPoint(10, 20)));
    }

    // ====================================================================
    // Union（仮想スクリーン境界の算出）
    // ====================================================================

    [Fact]
    public void Union_両者を包含する最小の矩形を返す()
    {
        var a = new EsPixelRect(0, 0, 100, 100);
        var b = new EsPixelRect(200, 50, 100, 100);

        var result = a.Union(b);

        Assert.Equal(new EsPixelRect(0, 0, 300, 150), result);
    }

    [Fact]
    public void Union_負の原点を持つ矩形も包含する()
    {
        // プライマリモニタが (0,0) にないマルチモニタ構成を模擬する
        var a = new EsPixelRect(-1920, 0, 1920, 1080);
        var b = new EsPixelRect(0, 0, 2560, 1440);

        var result = a.Union(b);

        Assert.Equal(new EsPixelRect(-1920, 0, 4480, 1440), result);
    }

    [Fact]
    public void Union_自身が空なら相手を返す()
    {
        var empty = new EsPixelRect(10, 10, 0, 0);
        var other = new EsPixelRect(0, 0, 100, 100);

        Assert.Equal(other, empty.Union(other));
    }

    [Fact]
    public void Union_相手が空なら自身を返す()
    {
        var self = new EsPixelRect(0, 0, 100, 100);
        var empty = new EsPixelRect(10, 10, 0, 0);

        Assert.Equal(self, self.Union(empty));
    }
}
