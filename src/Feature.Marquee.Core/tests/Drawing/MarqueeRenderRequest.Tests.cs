using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Xunit;

using EsUtil.Feature.Marquee.Core.Drawing;
using EsUtil.Feature.Marquee.Core.Setting;

namespace EsUtil.Feature.Marquee.Core.Tests.Drawing;

/// <summary><see cref="MarqueeRenderRequest"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 描画入力の組み立てと、描画入力から導出する値（座標変換ポートの決定・論理座標から DIP への変換・穴あけ矩形の決定）を検証する。<br/>
/// UI 初期化に依存せず <c>[Fact]</c> で並列実行する。
/// </remarks>
public class MarqueeRenderRequestTests
{
    /// <summary>検証に使う描画入力を生成する（導出の検証に必要な値だけを指定する）。</summary>
    /// <param name="deco">装飾の設定。null の場合は既定。</param>
    /// <param name="selection">選択矩形（ドメイン座標）。null の場合は選択なし。</param>
    /// <param name="port">座標変換ポート。null の場合は未指定（倍率による）。</param>
    /// <param name="scaling">ポート未指定時に適用する倍率。</param>
    /// <returns>生成した描画入力。</returns>
    private static MarqueeRenderRequest CreateRequest(
        MarqueeDecoOptions? deco = null,
        EsRect? selection = null,
        ICoordTransform? port = null,
        double scaling = 1.0)
        => new(
            MarqueeOptions.Default,
            deco ?? MarqueeDecoOptions.Default,
            new EsSize(300, 300),
            selection,
            MarkerActive: false,
            MarkerPosition: default,
            MousePosition: default,
            port,
            scaling,
            SizeLabelOverrideProvider: null,
            DisplayScaleScaling: 1.0);

    /// <summary>組み立てがすべての値をそのまま保持することを検証する。</summary>
    [Fact]
    public void Create_すべての値をそのまま保持する()
    {
        var options = MarqueeOptions.Default with { ShowHandles = false };
        var deco = MarqueeDecoOptions.Default with { ShowInnerFill = true };
        var port = CoordTransform.Scale(2.0);
        Func<string?> sizeLabel = () => "S";
        Func<EsRect, double> scaling = _ => 1.5;

        var request = MarqueeRenderRequest.Create(
            options,
            deco,
            new EsSize(640, 480),
            new EsRect(10, 20, 30, 40),
            markerActive: true,
            new EsPoint(50, 60),
            new EsPoint(70, 80),
            port,
            domainToDipScaling: 1.25,
            sizeLabel,
            displayScaleScaling: 1.5,
            scaling);

        Assert.Equal(options, request.Options);
        Assert.Equal(deco, request.DecoOptions);
        Assert.Equal(new EsSize(640, 480), request.ClientSize);
        Assert.Equal(new EsRect(10, 20, 30, 40), request.SelectionRect);
        Assert.True(request.MarkerActive);
        Assert.Equal(new EsPoint(50, 60), request.MarkerPosition);
        Assert.Equal(new EsPoint(70, 80), request.MousePosition);
        Assert.Same(port, request.Port);
        Assert.Equal(1.25, request.DomainToDipScaling);
        Assert.Same(sizeLabel, request.SizeLabelOverrideProvider);
        Assert.Equal(1.5, request.DisplayScaleScaling);
        Assert.Same(scaling, request.SizeLabelDisplayScalingProvider);
    }

    /// <summary>省略したプロバイダが null になることを検証する。</summary>
    [Fact]
    public void Create_省略したプロバイダはnullになる()
    {
        var request = MarqueeRenderRequest.Create(
            MarqueeOptions.Default,
            MarqueeDecoOptions.Default,
            new EsSize(100, 100),
            selectionRect: null,
            markerActive: false,
            new EsPoint(0, 0),
            new EsPoint(0, 0),
            port: null,
            domainToDipScaling: 1.0,
            sizeLabelOverrideProvider: null,
            displayScaleScaling: 1.0);

        Assert.Null(request.SizeLabelDisplayScalingProvider);
        Assert.Null(request.SelectionRect);
    }

    /// <summary>等価変換では終点に +1 した閉区間の矩形になることを検証する。</summary>
    [Fact]
    public void ToDipRect_等価変換で終点に1を加算した閉区間になる()
    {
        var logical = new EsRect(10, 20, 30, 40);

        var result = CreateRequest().ToDipRect(logical, CoordTransform.Identity);

        Assert.Equal(new EsRect(10, 20, 31, 41), result);
    }

    /// <summary>スケール 2 倍のポートで座標が拡大されることを検証する。</summary>
    [Fact]
    public void ToDipRect_スケール2倍のポートで座標が拡大される()
    {
        var logical = new EsRect(10, 20, 30, 40);

        var result = CreateRequest().ToDipRect(logical, CoordTransform.Scale(2.0));

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

        var result = CreateRequest().ToDipRect(logical, CoordTransform.Identity, closedInterval: false);

        Assert.Equal(new EsRect(10, 20, 30, 40), result);
    }

    /// <summary>穴あけ矩形が設定と状態から導出されることを検証する（選択矩形は閉区間）。</summary>
    [Fact]
    public void Hole_選択矩形を閉区間として返す()
    {
        var selection = new EsRect(10, 20, 30, 40);

        var hole = CreateRequest(selection: selection).Hole;

        Assert.NotNull(hole);
        Assert.Equal(selection, hole.Value.Rect);
        Assert.True(hole.Value.ClosedInterval);
    }

    /// <summary>どちらも無ければ穴あけ矩形が null になることを検証する。</summary>
    [Fact]
    public void Hole_選択もハイライトも無ければnullになる()
    {
        Assert.Null(CreateRequest().Hole);
    }

    /// <summary>DIP 矩形がドメイン座標の矩形へ逆換算されることを検証する。</summary>
    [Fact]
    public void ToDomainRect_等価変換では同じ矩形になる()
    {
        var dip = new EsRect(10, 20, 30, 40);

        var result = CreateRequest().ToDomainRect(dip, CoordTransform.Identity);

        Assert.Equal(dip, result);
    }

    /// <summary>スケール 2 倍のポートで座標が縮小されることを検証する。</summary>
    [Fact]
    public void ToDomainRect_スケール2倍のポートで座標が縮小される()
    {
        var dip = new EsRect(20, 40, 60, 80);

        var result = CreateRequest().ToDomainRect(dip, CoordTransform.Scale(2.0));

        Assert.Equal(10, result.X);
        Assert.Equal(20, result.Y);
        Assert.Equal(30, result.Width);
        Assert.Equal(40, result.Height);
    }

    /// <summary>ハイライト矩形があればハイライト矩形を優先することを検証する。</summary>
    [Fact]
    public void ResolveHoleRect_ハイライトがあればハイライト矩形を優先する()
    {
        var highlight = new EsRect(1, 2, 3, 4);
        var selection = new EsRect(10, 20, 30, 40);

        var result = MarqueeRenderRequest.ResolveHoleRect(highlight, selection);

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

        var result = MarqueeRenderRequest.ResolveHoleRect(null, selection);

        Assert.NotNull(result);
        Assert.Equal(selection, result.Value.Rect);
        // 選択矩形は閉区間（終点のピクセルも含む）として扱う
        Assert.True(result.Value.ClosedInterval);
    }

    /// <summary>どちらも null なら null を返すことを検証する。</summary>
    [Fact]
    public void ResolveHoleRect_両方nullならnullを返す()
    {
        var result = MarqueeRenderRequest.ResolveHoleRect(null, null);

        Assert.Null(result);
    }

    /// <summary>ハイライト矩形が選ばれた場合は終点を加算せずに DIP 変換されることを検証する。</summary>
    [Fact]
    public void ResolveHoleRectDip_ハイライト矩形は終点を加算せずDIP変換する()
    {
        var highlight = new EsRect(1, 2, 3, 4);
        var request = CreateRequest(
            deco: MarqueeDecoOptions.Default with { OuterHighlightRect = highlight },
            selection: new EsRect(10, 20, 30, 40));

        var result = request.ResolveHoleRectDip(CoordTransform.Identity);

        Assert.Equal(new EsRect(1, 2, 3, 4), result);
    }

    /// <summary>選択矩形が選ばれた場合は終点を加算して DIP 変換されることを検証する。</summary>
    [Fact]
    public void ResolveHoleRectDip_選択矩形は終点を加算してDIP変換する()
    {
        var request = CreateRequest(selection: new EsRect(10, 20, 30, 40));

        var result = request.ResolveHoleRectDip(CoordTransform.Identity);

        Assert.Equal(new EsRect(10, 20, 31, 41), result);
    }

    /// <summary>どちらも null なら null を返すことを検証する。</summary>
    [Fact]
    public void ResolveHoleRectDip_両方nullならnullを返す()
    {
        var result = CreateRequest().ResolveHoleRectDip(CoordTransform.Identity);

        Assert.Null(result);
    }

    /// <summary>ポートが指定されている場合はそれが優先されることを検証する。</summary>
    [Fact]
    public void ResolvePort_ポート指定時はポートを返す()
    {
        var port = CoordTransform.Scale(2.0);

        var result = MarqueeRenderRequest.ResolvePort(port, domainToDipScaling: 3.0);

        Assert.Same(port, result);
    }

    /// <summary>ポート未指定の場合は倍率から等倍スケールを生成することを検証する。</summary>
    [Fact]
    public void ResolvePort_ポート未指定なら倍率を適用する()
    {
        var result = MarqueeRenderRequest.ResolvePort(null, domainToDipScaling: 2.0);

        Assert.Equal(new EsPoint(20, 40), result.Inverse(new EsPoint(10, 20)));
    }

    /// <summary>ポート未指定で倍率が 0 以下・等倍の場合は等価変換になることを検証する。</summary>
    /// <param name="domainToDipScaling">適用する倍率。</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(1.0)]
    [InlineData(double.NaN)]
    public void ResolvePort_倍率が無効なら等価変換になる(double domainToDipScaling)
    {
        var result = MarqueeRenderRequest.ResolvePort(null, domainToDipScaling);

        Assert.Same(CoordTransform.Identity, result);
    }
}
