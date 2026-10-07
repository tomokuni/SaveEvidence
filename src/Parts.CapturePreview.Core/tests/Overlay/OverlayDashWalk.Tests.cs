using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Overlay;

namespace EsUtil.SaveEvidence.Parts.CapturePreview.Core.Tests.Overlay;

/// <summary><see cref="OverlayDashWalk"/> の単体テスト（辺 → 破線の区間の分割）。</summary>
/// <remarks>
/// 2 つの破線ラスタライザ（外部提供矩形・選択矩形の破線）が共用する唯一の幾何規則であるため、<br/>
/// 軸の判定・刻み・端のクランプ・丸めをここで固定する。 UI 初期化なしの <c>[Fact]</c> で並列検証できる。
/// </remarks>
public class OverlayDashWalkTests
{
    /// <summary>区間を収集するコールバックを生成する。</summary>
    /// <param name="spans">収集先の一覧。</param>
    /// <returns>区間を受け取るコールバック。</returns>
    private static Action<OverlayDashWalk.Span> Collect(List<OverlayDashWalk.Span> spans) => spans.Add;

    [Fact]
    internal void 実線は1画素刻みで全区間を通知する()
    {
        var spans = new List<OverlayDashWalk.Span>();

        OverlayDashWalk.ForEach(
            new EsPoint(0, 0), new EsPoint(3, 0), dashed: false, dashLength: 0, gapLength: 0, Collect(spans));

        // 1 画素刻み（始点を含み終点を含む）
        Assert.Equal(3, spans.Count);
        Assert.Equal(new OverlayDashWalk.Span(true, 0, 1, 0.0, 0), spans[0]);
        Assert.Equal(new OverlayDashWalk.Span(true, 2, 3, 0.0, 2), spans[2]);
    }

    [Fact]
    internal void 破線はダッシュ長と隙間で区間を通知する()
    {
        var spans = new List<OverlayDashWalk.Span>();

        OverlayDashWalk.ForEach(
            new EsPoint(0, 0), new EsPoint(10, 0), dashed: true, dashLength: 2, gapLength: 1, Collect(spans));

        // 2 画素のダッシュ + 1 画素の隙間（3 画素周期）で、最後は端でクランプされる
        Assert.Equal(4, spans.Count);
        Assert.Equal(new OverlayDashWalk.Span(true, 0, 2, 0.0, 0), spans[0]);
        Assert.Equal(new OverlayDashWalk.Span(true, 3, 5, 0.0, 1), spans[1]);
        Assert.Equal(new OverlayDashWalk.Span(true, 9, 10, 0.0, 3), spans[3]);
    }

    [Fact]
    internal void 垂直辺は固定座標がXになる()
    {
        var spans = new List<OverlayDashWalk.Span>();

        OverlayDashWalk.ForEach(
            new EsPoint(5, 0), new EsPoint(5, 4), dashed: false, dashLength: 0, gapLength: 0, Collect(spans));

        Assert.All(spans, span => Assert.False(span.Horizontal));
        Assert.All(spans, span => Assert.Equal(5.0, span.FixedCoord));
    }

    [Fact]
    internal void 端の丸めは05を切り上げる()
    {
        var spans = new List<OverlayDashWalk.Span>();

        // 距離 1.5 のダッシュは端（1.5）を切り上げて 2 まで通知する（丸めは CropBoundsMath.RoundToPixel が単一所有する）
        OverlayDashWalk.ForEach(
            new EsPoint(0, 0), new EsPoint(1.5, 0), dashed: true, dashLength: 2, gapLength: 0, Collect(spans));

        var span = Assert.Single(spans);
        Assert.Equal(2, span.To);
    }

    [Fact]
    internal void 区間の番号は呼び出しごとに0から始まる()
    {
        var first = new List<OverlayDashWalk.Span>();
        var second = new List<OverlayDashWalk.Span>();

        OverlayDashWalk.ForEach(new EsPoint(0, 0), new EsPoint(2, 0), dashed: false, 0, 0, Collect(first));
        OverlayDashWalk.ForEach(new EsPoint(0, 0), new EsPoint(2, 0), dashed: false, 0, 0, Collect(second));

        // 色の交互切替は辺ごとに先頭から始まる（前の辺の番号を引き継がない）
        Assert.Equal(0, first[0].Index);
        Assert.Equal(0, second[0].Index);
    }

    [Fact]
    internal void 負の方向へ向かう辺は区間を通知しない()
    {
        var spans = new List<OverlayDashWalk.Span>();

        // 終点が始点より小さい辺（退化した辺。 通常は発生しない）は描画しない
        OverlayDashWalk.ForEach(
            new EsPoint(5, 0), new EsPoint(0, 0), dashed: false, dashLength: 0, gapLength: 0, Collect(spans));

        Assert.Empty(spans);
    }

    [Fact]
    internal void 距離が0の辺は区間を通知しない()
    {
        var spans = new List<OverlayDashWalk.Span>();

        OverlayDashWalk.ForEach(
            new EsPoint(3, 3), new EsPoint(3, 3), dashed: false, dashLength: 0, gapLength: 0, Collect(spans));

        Assert.Empty(spans);
    }

    [Fact]
    internal void コールバックがnullなら例外()
        => Assert.Throws<ArgumentNullException>(() => OverlayDashWalk.ForEach(
            new EsPoint(0, 0), new EsPoint(1, 0), dashed: false, dashLength: 0, gapLength: 0, sink: null!));
}
