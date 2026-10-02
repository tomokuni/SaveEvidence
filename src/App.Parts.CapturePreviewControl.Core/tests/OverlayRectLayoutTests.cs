using EsUtil.Platform.Core.Geometry;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests;

/// <summary><see cref="OverlayRectLayout"/> と <see cref="OverlayRectFrame"/> の仕様テスト。</summary>
/// <remarks>
/// 2 つの UI のベクター描画経路が同じ規則で矩形を描くための判断（負サイズのクランプ・太さの下限・<br/>
/// 破線かどうか・背景色を使うか・ダッシュ長）を検証する。<br/>
/// UI 初期化を必要としないため、Avalonia ヘッドレスに依存せず並列実行できる。
/// </remarks>
public class OverlayRectLayoutTests
{
    /// <summary>検証に使う前景色（赤）。</summary>
    private static readonly DrawingColor s_foreground = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>検証に使う背景色（青）。</summary>
    private static readonly DrawingColor s_background = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>検証に使う矩形情報を生成する。</summary>
    /// <param name="rect">矩形（ドメイン座標）。</param>
    /// <param name="style">枠線スタイル。</param>
    /// <param name="thickness">枠線の太さ。</param>
    /// <returns>矩形情報。</returns>
    private static OverlayRect Create(EsRect rect, OverlayLineStyle style, double thickness = 2)
        => new(rect, style, s_foreground, s_background, thickness);

    /// <summary>ドメイン矩形をビューポート座標（同じ値）へ解決する。</summary>
    /// <param name="rect">矩形情報。</param>
    /// <returns>描画入力。</returns>
    private static OverlayRectFrame Resolve(OverlayRect rect)
        => OverlayRectLayout.Resolve(
            rect, new EsPoint(rect.Bounds.X, rect.Bounds.Y), new EsPoint(rect.Bounds.Right, rect.Bounds.Bottom));

    [Fact]
    public void EffectiveThickness_1以上はそのまま返す()
    {
        Assert.Equal(2.5, OverlayRectLayout.EffectiveThickness(2.5));
        Assert.Equal(OverlayRectLayout.MinimumThickness, OverlayRectLayout.EffectiveThickness(OverlayRectLayout.MinimumThickness));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-3.0)]
    [InlineData(0.4)]
    public void EffectiveThickness_下限未満は最小値へ補正する(double thickness)
    {
        // 太さ 0 以下や下限未満を要求されても枠線が消えないよう、最小値を保証する
        Assert.Equal(OverlayRectLayout.MinimumThickness, OverlayRectLayout.EffectiveThickness(thickness));
    }

    [Fact]
    public void EffectiveThickness_最小値は1である()
    {
        Assert.Equal(1.0, OverlayRectLayout.MinimumThickness);
    }

    [Fact]
    public void ViewportRectFromCorners_正のサイズはそのまま返す()
    {
        var rect = OverlayRectLayout.ViewportRectFromCorners(new EsPoint(10, 20), new EsPoint(40, 50));

        Assert.Equal(10, rect.X);
        Assert.Equal(20, rect.Y);
        Assert.Equal(30, rect.Width);
        Assert.Equal(30, rect.Height);
    }

    [Theory]
    [InlineData(40, 30)]
    [InlineData(10, 50)]
    [InlineData(40, 50)]
    public void ViewportRectFromCorners_逆転した右下は幅高さ0へクランプする(double x, double y)
    {
        // 縮小・中央寄せで右下が左上を逆転しても負のサイズを返さない（描画先によっては例外になる）
        var rect = OverlayRectLayout.ViewportRectFromCorners(new EsPoint(10, 20), new EsPoint(x, y));

        Assert.True(rect.Width >= 0, $"幅が負です: {rect.Width}");
        Assert.True(rect.Height >= 0, $"高さが負です: {rect.Height}");
    }

    [Fact]
    public void Resolve_配置と色をそのまま引き継ぐ()
    {
        var frame = Resolve(Create(new EsRect(10, 20, 30, 40), OverlayLineStyle.Solid));

        Assert.Equal(10, frame.Bounds.X);
        Assert.Equal(20, frame.Bounds.Y);
        Assert.Equal(30, frame.Bounds.Width);
        Assert.Equal(40, frame.Bounds.Height);
        Assert.Equal(s_foreground, frame.Foreground);
        Assert.Equal(s_background, frame.Background);
    }

    [Fact]
    public void Resolve_実線は破線でなく背景を描かない()
    {
        var frame = Resolve(Create(new EsRect(0, 0, 10, 10), OverlayLineStyle.Solid));

        Assert.False(frame.IsDashed);
        Assert.False(frame.DrawsBackground);
        Assert.Equal(0, frame.DashLength);
        Assert.Equal(0, frame.GapLength);
    }

    [Theory]
    [InlineData(OverlayLineStyle.Dashed, 8.0, 4.0)]
    [InlineData(OverlayLineStyle.Dotted, 2.0, 2.0)]
    public void Resolve_破線はダッシュ長をコアの値から引き継ぎ背景を描く(OverlayLineStyle style, double dashLength, double gapLength)
    {
        var frame = Resolve(Create(new EsRect(0, 0, 10, 10), style));

        Assert.True(frame.IsDashed);
        Assert.True(frame.DrawsBackground);
        Assert.Equal(dashLength, frame.DashLength);
        Assert.Equal(gapLength, frame.GapLength);
    }

    [Fact]
    public void Resolve_太さの下限を適用する()
    {
        var frame = Resolve(Create(new EsRect(0, 0, 10, 10), OverlayLineStyle.Solid, thickness: 0));

        Assert.Equal(OverlayRectLayout.MinimumThickness, frame.Thickness);
        Assert.Equal(OverlayRectLayout.MinimumThickness / 2, frame.HalfThickness);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-5, 10)]
    [InlineData(0, 0)]
    public void HasDrawableSize_幅または高さが0以下ならfalse(double width, double height)
    {
        var frame = Resolve(Create(new EsRect(5, 5, width, height), OverlayLineStyle.Solid));

        Assert.False(frame.HasDrawableSize);
    }

    [Fact]
    public void HasDrawableSize_幅と高さが正ならtrue()
    {
        var frame = Resolve(Create(new EsRect(5, 5, 1, 1), OverlayLineStyle.Solid));

        Assert.True(frame.HasDrawableSize);
    }

    [Fact]
    public void HalfThickness_は太さの半分である()
    {
        var frame = Resolve(Create(new EsRect(0, 0, 10, 10), OverlayLineStyle.Solid, thickness: 3));

        Assert.Equal(3.0, frame.Thickness);
        Assert.Equal(1.5, frame.HalfThickness);
    }

    [Fact]
    public void Resolve_2点の逆転で負のサイズにならない()
    {
        var rect = Create(new EsRect(10, 10, 10, 10), OverlayLineStyle.Solid);
        var frame = OverlayRectLayout.Resolve(rect, new EsPoint(50, 50), new EsPoint(20, 20));

        Assert.Equal(0, frame.Bounds.Width);
        Assert.Equal(0, frame.Bounds.Height);
        Assert.False(frame.HasDrawableSize);
    }
}
