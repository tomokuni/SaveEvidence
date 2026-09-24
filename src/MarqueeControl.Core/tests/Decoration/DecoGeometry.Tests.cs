using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Xunit;

using Estable.Marquee.Decoration;

namespace Estable.Marquee.Tests;

/// <summary><see cref="DecoGeometry"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 暗転オーバーレイの幾何計算のみを検証するため、UI 初期化に依存せず <c>[Fact]</c> で並列実行する。<br/>
/// 各 UI（Avalonia / MewUI）の描画は <see cref="EsRect"/> を自身の描画 API へ渡すだけであり、<br/>
/// 本テストが両 UI の見た目を保証する（幾何計算の単一所有）。
/// </remarks>
public class DecoGeometryTests
{
    /// <summary>等価変換では終点に +1 した閉区間の矩形になることを検証する。</summary>
    [Fact]
    public void ToDipRect_等価変換で終点に1を加算した閉区間になる()
    {
        var logical = new EsRect(10, 20, 30, 40);

        var result = DecoGeometry.ToDipRect(logical, CoordTransform.Identity);

        Assert.Equal(new EsRect(10, 20, 31, 41), result);
    }

    /// <summary>スケール 2 倍のポートで座標が拡大されることを検証する。</summary>
    [Fact]
    public void ToDipRect_スケール2倍のポートで座標が拡大される()
    {
        var logical = new EsRect(10, 20, 30, 40);

        var result = DecoGeometry.ToDipRect(logical, CoordTransform.Scale(2.0));

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

        var result = DecoGeometry.ToDipRect(logical, CoordTransform.Identity, closedInterval: false);

        Assert.Equal(new EsRect(10, 20, 30, 40), result);
    }

    /// <summary>ポートが null の場合は等価変換として扱うことを検証する。</summary>
    [Fact]
    public void ToDipRect_ポートがnullなら等価変換として扱う()
    {
        var logical = new EsRect(5, 6, 7, 8);

        var result = DecoGeometry.ToDipRect(logical);

        Assert.Equal(new EsRect(5, 6, 8, 9), result);
    }

    /// <summary>枠線が四辺（上下左右）の矩形で返ることを検証する。</summary>
    [Fact]
    public void GetBorderRects_四辺の矩形を返す()
    {
        var rect = new EsRect(10, 20, 100, 50);

        var result = DecoGeometry.GetBorderRects(rect, 2.0);

        Assert.Equal(4, result.Count);
        Assert.Equal(new EsRect(10, 20, 100, 2), result[0]);     // 上
        Assert.Equal(new EsRect(10, 68, 100, 2), result[1]);     // 下
        Assert.Equal(new EsRect(10, 22, 2, 46), result[2]);      // 左
        Assert.Equal(new EsRect(108, 22, 2, 46), result[3]);     // 右
    }

    /// <summary>枠線の太さが 0.5 未満なら 0.5 に正規化されることを検証する。</summary>
    [Fact]
    public void GetBorderRects_太さ05未満は05に正規化される()
    {
        var rect = new EsRect(0, 0, 100, 50);

        var result = DecoGeometry.GetBorderRects(rect, 0.1);

        Assert.Equal(0.5, result[0].Height);
    }

    /// <summary>高さより太い枠線でも負のサイズにならないことを検証する。</summary>
    [Fact]
    public void GetBorderRects_高さより太い枠線でも負にならない()
    {
        var rect = new EsRect(0, 0, 10, 2);

        var result = DecoGeometry.GetBorderRects(rect, 2.0);

        Assert.Equal(0, result[2].Height);
        Assert.Equal(0, result[3].Height);
    }

    /// <summary>ハイライトが有効ならハイライト矩形を優先することを検証する。</summary>
    [Fact]
    public void ResolveHoleRect_ハイライト有効時はハイライト矩形を優先する()
    {
        var highlight = new EsRect(1, 2, 3, 4);
        var selection = new EsRect(10, 20, 30, 40);

        var result = DecoGeometry.ResolveHoleRect(true, highlight, true, selection);

        Assert.Equal(highlight, result);
    }

    /// <summary>ハイライトが無効で選択が有効なら選択矩形を返すことを検証する。</summary>
    [Fact]
    public void ResolveHoleRect_ハイライト無効で選択有効時は選択矩形を返す()
    {
        var selection = new EsRect(10, 20, 30, 40);

        var result = DecoGeometry.ResolveHoleRect(false, null, true, selection);

        Assert.Equal(selection, result);
    }

    /// <summary>ハイライトと選択の両方が無効なら null を返すことを検証する。</summary>
    [Fact]
    public void ResolveHoleRect_両方無効ならnullを返す()
    {
        var result = DecoGeometry.ResolveHoleRect(false, null, false, null);

        Assert.Null(result);
    }

    /// <summary>穴あけ矩形があれば暗転を描画すると判定することを検証する。</summary>
    [Fact]
    public void ShouldDrawOverlay_穴あけ矩形があればtrueを返す()
    {
        Assert.True(DecoGeometry.ShouldDrawOverlay(new EsRect(10, 20, 50, 40), showFullOverlay: false));
    }

    /// <summary>穴あけ矩形が無く全面暗転なら描画すると判定することを検証する。</summary>
    [Fact]
    public void ShouldDrawOverlay_穴あけ矩形がなく全面暗転ならtrueを返す()
    {
        Assert.True(DecoGeometry.ShouldDrawOverlay(null, showFullOverlay: true));
    }

    /// <summary>穴あけ矩形も全面暗転も無効なら描画しないと判定することを検証する。</summary>
    [Fact]
    public void ShouldDrawOverlay_両方無効ならfalseを返す()
    {
        Assert.False(DecoGeometry.ShouldDrawOverlay(null, showFullOverlay: false));
    }

    /// <summary>ポートが指定されている場合はそれが優先されることを検証する。</summary>
    [Fact]
    public void ResolvePort_ポート指定時はポートを返す()
    {
        var port = CoordTransform.Scale(2.0);

        var result = DecoGeometry.ResolvePort(port, scaling: 3.0);

        Assert.Same(port, result);
    }

    /// <summary>ポート未指定の場合は倍率から等倍スケールを生成することを検証する。</summary>
    [Fact]
    public void ResolvePort_ポート未指定なら倍率を適用する()
    {
        var result = DecoGeometry.ResolvePort(null, scaling: 2.0);

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
        var result = DecoGeometry.ResolvePort(null, scaling);

        Assert.Same(CoordTransform.Identity, result);
    }

    /// <summary>穴あけ矩形がない場合は全面が暗転領域になることを検証する。</summary>
    [Fact]
    public void ComputeOverlayRegions_穴あけ矩形がなければ全面を返す()
    {
        var result = DecoGeometry.ComputeOverlayRegions(new EsSize(100, 100), null);

        Assert.Single(result);
        Assert.Equal(new EsRect(0, 0, 100, 100), result[0]);
    }

    /// <summary>穴あけ矩形がある場合は四辺の帯（上・下・左・右）になることを検証する。</summary>
    [Fact]
    public void ComputeOverlayRegions_穴あけ矩形がある場合は四辺の帯になる()
    {
        var hole = new EsRect(20, 30, 40, 50);

        var result = DecoGeometry.ComputeOverlayRegions(new EsSize(100, 100), hole);

        // 上（全幅・高さ 30）/ 下（全幅・高さ 20）/ 左（高さ 50・幅 20）/ 右（高さ 50・幅 40）
        Assert.Equal(4, result.Count);
        Assert.Equal(new EsRect(0, 0, 100, 30), result[0]);
        Assert.Equal(new EsRect(0, 80, 100, 20), result[1]);
        Assert.Equal(new EsRect(0, 30, 20, 50), result[2]);
        Assert.Equal(new EsRect(60, 30, 40, 50), result[3]);
    }

    /// <summary>穴あけ矩形がクライアントと交差しない場合は全面を暗転することを検証する。</summary>
    [Fact]
    public void ComputeOverlayRegions_穴あけ矩形が外側なら全面を返す()
    {
        var hole = new EsRect(200, 200, 50, 50);

        var result = DecoGeometry.ComputeOverlayRegions(new EsSize(100, 100), hole);

        Assert.Single(result);
        Assert.Equal(new EsRect(0, 0, 100, 100), result[0]);
    }

    /// <summary>穴あけ矩形がクライアント全体の場合は暗転領域がなくなることを検証する。</summary>
    [Fact]
    public void ComputeOverlayRegions_穴あけ矩形が全面なら暗転なしになる()
    {
        var result = DecoGeometry.ComputeOverlayRegions(new EsSize(100, 100), new EsRect(0, 0, 100, 100));

        Assert.Empty(result);
    }

    /// <summary>穴あけ矩形がクライアント端に接する場合はその辺の帯を生成しないことを検証する。</summary>
    [Fact]
    public void ComputeOverlayRegions_穴あけ矩形が上端左端に接する場合は下右のみになる()
    {
        var hole = new EsRect(0, 0, 40, 40);

        var result = DecoGeometry.ComputeOverlayRegions(new EsSize(100, 100), hole);

        // 上（Top == 0）と左（Left == 0）は幅・高さが 0 のため生成されない
        Assert.Equal(2, result.Count);
        Assert.Equal(new EsRect(0, 40, 100, 60), result[0]); // 下
        Assert.Equal(new EsRect(40, 0, 60, 40), result[1]); // 右
    }

    /// <summary>穴あけ矩形がサイズ 0 の場合は全面が暗転領域になることを検証する。</summary>
    [Fact]
    public void ComputeOverlayRegions_穴あけ矩形がサイズ0なら全面を返す()
    {
        var hole = new EsRect(50, 50, 0, 0);

        var result = DecoGeometry.ComputeOverlayRegions(new EsSize(100, 100), hole);

        // 穴あけ矩形が交差しない（幅・高さ 0）ため、暗転の抜けが生じないよう全面を返す
        Assert.Single(result);
        Assert.Equal(new EsRect(0, 0, 100, 100), result[0]);
    }
}
