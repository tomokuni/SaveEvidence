using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Xunit;

using EsUtil.Feature.MarqueeCore.Drawing;

namespace EsUtil.Feature.MarqueeCore.Tests.Drawing;

/// <summary><see cref="MarqueeFrameGeometry"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 1 フレーム分の描画内容に共通する幾何計算（座標変換ポートの決定・論理座標から DIP への変換・穴あけ矩形の決定）を検証する。<br/>
/// UI 初期化に依存せず <c>[Fact]</c> で並列実行する。
/// </remarks>
public class MarqueeFrameGeometryTests
{
    /// <summary>等価変換では終点に +1 した閉区間の矩形になることを検証する。</summary>
    [Fact]
    public void ToDipRect_等価変換で終点に1を加算した閉区間になる()
    {
        var logical = new EsRect(10, 20, 30, 40);

        var result = MarqueeFrameGeometry.ToDipRect(logical, CoordTransform.Identity);

        Assert.Equal(new EsRect(10, 20, 31, 41), result);
    }

    /// <summary>スケール 2 倍のポートで座標が拡大されることを検証する。</summary>
    [Fact]
    public void ToDipRect_スケール2倍のポートで座標が拡大される()
    {
        var logical = new EsRect(10, 20, 30, 40);

        var result = MarqueeFrameGeometry.ToDipRect(logical, CoordTransform.Scale(2.0));

        // 左上 (10,20) → (20,40)、右下 (41,61) → (82,122)
        Assert.Equal(20, result.X);
        Assert.Equal(40, result.Y);
        Assert.Equal(62, result.Width);
        Assert.Equal(82, result.Height);
    }

    /// <summary>閉区間を無効にすると終点を加算しないことを検証する。</summary>
    [Fact]
    public void ToDipRect_closedIntervalがfalseなら終点を加算しない()
    {
        var logical = new EsRect(10, 20, 30, 40);

        var result = MarqueeFrameGeometry.ToDipRect(logical, CoordTransform.Identity, closedInterval: false);

        Assert.Equal(new EsRect(10, 20, 30, 40), result);
    }

    /// <summary>ポートが null の場合は等価変換として扱うことを検証する。</summary>
    [Fact]
    public void ToDipRect_ポートがnullなら等価変換として扱う()
    {
        var logical = new EsRect(5, 6, 7, 8);

        var result = MarqueeFrameGeometry.ToDipRect(logical);

        Assert.Equal(new EsRect(5, 6, 8, 9), result);
    }

    /// <summary>ハイライト矩形があればハイライト矩形を優先することを検証する。</summary>
    [Fact]
    public void ResolveHoleRect_ハイライトがあればハイライト矩形を優先する()
    {
        var highlight = new EsRect(1, 2, 3, 4);
        var selection = new EsRect(10, 20, 30, 40);

        var result = MarqueeFrameGeometry.ResolveHoleRect(highlight, selection);

        Assert.NotNull(result);
        Assert.Equal(highlight, result.Value.Rect);
        // ハイライト矩形はホストが閉区間として与えるため、終点を +1 しない
        Assert.False(result.Value.ClosedInterval);
    }

    /// <summary>ハイライトが無く選択だけがある場合は選択矩形を閉区間として返すことを検証する。</summary>
    [Fact]
    public void ResolveHoleRect_ハイライトが無ければ選択矩形を返す()
    {
        var selection = new EsRect(10, 20, 30, 40);

        var result = MarqueeFrameGeometry.ResolveHoleRect(null, selection);

        Assert.NotNull(result);
        Assert.Equal(selection, result.Value.Rect);
        // 選択矩形は閉区間（終点のピクセルも含む）として扱う
        Assert.True(result.Value.ClosedInterval);
    }

    /// <summary>どちらも null なら null を返すことを検証する。</summary>
    [Fact]
    public void ResolveHoleRect_両方nullならnullを返す()
    {
        var result = MarqueeFrameGeometry.ResolveHoleRect(null, null);

        Assert.Null(result);
    }

    /// <summary>ポートが指定されている場合はそれが優先されることを検証する。</summary>
    [Fact]
    public void ResolvePort_ポート指定時はポートを返す()
    {
        var port = CoordTransform.Scale(2.0);

        var result = MarqueeFrameGeometry.ResolvePort(port, scaling: 3.0);

        Assert.Same(port, result);
    }

    /// <summary>ポート未指定の場合は倍率から等倍スケールを生成することを検証する。</summary>
    [Fact]
    public void ResolvePort_ポート未指定なら倍率を適用する()
    {
        var result = MarqueeFrameGeometry.ResolvePort(null, scaling: 2.0);

        Assert.Equal(new EsPoint(20, 40), result.Inverse(new EsPoint(10, 20)));
    }

    /// <summary>ポート未指定で倍率が 0 以下・等倍の場合は等価変換になることを検証する。</summary>
    /// <param name="scaling">適用する倍率。</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(1.0)]
    [InlineData(double.NaN)]
    public void ResolvePort_倍率が無効なら等価変換になる(double scaling)
    {
        var result = MarqueeFrameGeometry.ResolvePort(null, scaling);

        Assert.Same(CoordTransform.Identity, result);
    }
}
