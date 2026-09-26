using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Platform.Core.Tests.Geometry;

/// <summary><see cref="EsRectInt"/> の純粋計算テスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 値型の算術のみを検証するため、UI フレームワークの初期化に依存せず <c>[Fact]</c> で並列実行する。
/// </remarks>
public class EsRectIntTests
{
    // ====================================================================
    // 境界（Right / Bottom / TopLeft / BottomRight / Center）
    // ====================================================================

    [Fact]
    public void Right_左端と幅の和を返す()
    {
        var rect = new EsRectInt(10, 20, 100, 50);

        Assert.Equal(110, rect.Right);
    }

    [Fact]
    public void Bottom_上端と高さの和を返す()
    {
        var rect = new EsRectInt(10, 20, 100, 50);

        Assert.Equal(70, rect.Bottom);
    }

    [Fact]
    public void BottomRight_右端と下端を返す()
    {
        var rect = new EsRectInt(10, 20, 100, 50);

        Assert.Equal(new EsPointInt(110, 70), rect.BottomRight);
    }

    [Fact]
    public void TopLeft_左端と上端を返す()
    {
        // 矩形の原点（画像の原点等）を UI が座標を手組みせず取り出せるようにする（BottomRight と対称）
        var rect = new EsRectInt(10, 20, 100, 50);

        Assert.Equal(new EsPointInt(10, 20), rect.TopLeft);
    }

    [Fact]
    public void TopLeft_原点が負値でも座標をそのまま返す()
    {
        // マルチモニタ（仮想スクリーン原点が負値）でも原点を取り出せる
        var rect = new EsRectInt(-1920, -200, 100, 50);

        Assert.Equal(new EsPointInt(-1920, -200), rect.TopLeft);
    }

    [Fact]
    public void Center_中心座標を返す()
    {
        var rect = new EsRectInt(10, 20, 100, 50);

        // 幅・高さを 2 で割った整数を左上に加算する
        Assert.Equal(new EsPointInt(60, 45), rect.Center);
    }

    [Fact]
    public void Center_端数は切り捨てる()
    {
        var rect = new EsRectInt(0, 0, 101, 51);

        Assert.Equal(new EsPointInt(50, 25), rect.Center);
    }

    // ====================================================================
    // Contains
    // ====================================================================

    [Fact]
    public void Contains_内側の点を含む()
    {
        var rect = new EsRectInt(10, 20, 100, 50);

        Assert.True(rect.Contains(new EsPointInt(50, 40)));
    }

    [Fact]
    public void Contains_左端と上端を含む()
    {
        var rect = new EsRectInt(10, 20, 100, 50);

        // 左端・上端は含む（半開区間の開始側）
        Assert.True(rect.Contains(new EsPointInt(10, 20)));
    }

    [Fact]
    public void Contains_右端と下端は含まない()
    {
        var rect = new EsRectInt(10, 20, 100, 50);

        // 右端・下端は含まない（半開区間の終端側）
        Assert.False(rect.Contains(new EsPointInt(110, 70)));
        Assert.True(rect.Contains(new EsPointInt(109, 69)));
    }

    [Fact]
    public void Contains_外側の点を含まない()
    {
        var rect = new EsRectInt(10, 20, 100, 50);

        Assert.False(rect.Contains(new EsPointInt(9, 40)));
        Assert.False(rect.Contains(new EsPointInt(50, 19)));
    }

    [Fact]
    public void Contains_空の矩形は常に含まない()
    {
        var rect = new EsRectInt(10, 20, 0, 0);

        Assert.False(rect.Contains(new EsPointInt(10, 20)));
    }

    // ====================================================================
    // Union（仮想スクリーン境界の算出）
    // ====================================================================

    [Fact]
    public void Union_両者を包含する最小の矩形を返す()
    {
        var a = new EsRectInt(0, 0, 100, 100);
        var b = new EsRectInt(200, 50, 100, 100);

        var result = a.Union(b);

        Assert.Equal(new EsRectInt(0, 0, 300, 150), result);
    }

    [Fact]
    public void Union_負の原点を持つ矩形も包含する()
    {
        // プライマリモニタが (0,0) にないマルチモニタ構成を模擬する
        var a = new EsRectInt(-1920, 0, 1920, 1080);
        var b = new EsRectInt(0, 0, 2560, 1440);

        var result = a.Union(b);

        Assert.Equal(new EsRectInt(-1920, 0, 4480, 1440), result);
    }

    [Fact]
    public void Union_自身が空なら相手を返す()
    {
        var empty = new EsRectInt(10, 10, 0, 0);
        var other = new EsRectInt(0, 0, 100, 100);

        Assert.Equal(other, empty.Union(other));
    }

    [Fact]
    public void Union_相手が空なら自身を返す()
    {
        var self = new EsRectInt(0, 0, 100, 100);
        var empty = new EsRectInt(10, 10, 0, 0);

        Assert.Equal(self, self.Union(empty));
    }
}
