using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Xunit;

using EsUtil.Feature.Marquee.Core.Drawing;
using EsUtil.Feature.Marquee.Core.Setting;

namespace EsUtil.Feature.Marquee.Core.Tests.Drawing;

/// <summary><see cref="MarqueeRenderRequest"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 描画入力の組み立てと、描画入力から導出する値（座標変換ポートの決定・論理座標から DIP への変換・注目矩形の決定）を検証する。<br/>
/// UI 初期化に依存せず <c>[Fact]</c> で並列実行する。
/// </remarks>
public class MarqueeRenderRequestTests
{
    /// <summary>検証に使う描画入力を生成する（導出の検証に必要な値だけを指定する）。</summary>
    /// <param name="highlight">ハイライト矩形（ドメイン座標）。 null の場合はハイライトなし。</param>
    /// <param name="selection">選択矩形（ドメイン座標）。 null の場合は選択なし。</param>
    /// <param name="port">座標変換ポート。 null の場合は未指定（倍率による）。</param>
    /// <param name="scaling">ポート未指定時に適用する倍率。</param>
    /// <returns>生成した描画入力。</returns>
    private static MarqueeRenderRequest CreateRequest(
        EsRect? highlight = null,
        EsRect? selection = null,
        ICoordTransform? port = null,
        double scaling = 1.0)
        => new(
            MarqueeOptions.Default,
            new EsSize(300, 300),
            selection,
            MarkerActive: false,
            MarkerPosition: default,
            MousePosition: default,
            port,
            scaling)
        {
            HighlightDomainRect = highlight,
        };

    /// <summary>組み立てがすべての値をそのまま保持することを検証する。</summary>
    [Fact]
    public void Create_すべての値をそのまま保持する()
    {
        var options = MarqueeOptions.Default with { HandleSize = 14.0 };
        var highlight = new EsRect(1, 2, 3, 4);
        var port = CoordTransform.DipToDomain(() => 2.0);

        var request = MarqueeRenderRequest.Create(
            options,
            new EsSize(640, 480),
            new EsRect(10, 20, 30, 40),
            markerActive: true,
            new EsPoint(50, 60),
            new EsPoint(70, 80),
            port,
            dipToDomainScaling: 1.25,
            highlightDomainRect: highlight);

        Assert.Equal(options, request.Options);
        Assert.Equal(new EsSize(640, 480), request.ClientSize);
        Assert.Equal(new EsRect(10, 20, 30, 40), request.SelectionDomainRect);
        Assert.True(request.MarkerActive);
        Assert.Equal(new EsPoint(50, 60), request.MarkerPosition);
        Assert.Equal(new EsPoint(70, 80), request.MousePosition);
        Assert.Same(port, request.Port);
        Assert.Equal(1.25, request.DipToDomainScaling);
        Assert.Equal(highlight, request.HighlightDomainRect);
    }

    /// <summary>省略したハイライト矩形が null になることを検証する。</summary>
    [Fact]
    public void Create_省略したハイライト矩形はnullになる()
    {
        var request = MarqueeRenderRequest.Create(
            MarqueeOptions.Default,
            new EsSize(100, 100),
            selectionDomainRect: null,
            markerActive: false,
            new EsPoint(0, 0),
            new EsPoint(0, 0),
            port: null,
            dipToDomainScaling: 1.0);

        Assert.Null(request.HighlightDomainRect);
        Assert.Null(request.SelectionDomainRect);
    }

    /// <summary>閉区間の矩形は終点に +1 してから DIP 変換されることを検証する。</summary>
    [Fact]
    public void ToDipRect_閉区間の矩形は終点に1を加算する()
    {
        var logical = new EsRect(10, 20, 30, 40, EsRectInterval.Closed);

        var result = CreateRequest().ToDipRect(logical, CoordTransform.Identity);

        Assert.Equal(new EsRect(10, 20, 31, 41), result);
    }

    /// <summary>ドメインが DIP の 2 倍のポートでは、ドメインが DIP へ半分に換算されることを検証する。</summary>
    [Fact]
    public void ToDipRect_ドメインがDIPの2倍のポートでDIPへ換算される()
    {
        var logical = new EsRect(20, 40, 40, 80);

        var result = CreateRequest().ToDipRect(logical, CoordTransform.DipToDomain(() => 2.0));

        // ToDip = ドメイン ÷ 2（左上 (20,40) → (10,20)、右下 (60,120) → (30,60)）
        Assert.Equal(new EsRect(10, 20, 20, 40), result);
    }

    /// <summary>半開区間の矩形は終点を加算しないことを検証する（ハイライト矩形・検出結果）。</summary>
    [Fact]
    public void ToDipRect_半開区間の矩形は終点を加算しない()
    {
        var logical = new EsRect(10, 20, 30, 40);

        var result = CreateRequest().ToDipRect(logical, CoordTransform.Identity);

        Assert.Equal(new EsRect(10, 20, 30, 40), result);
    }

    /// <summary>注目矩形が設定と状態から導出されることを検証する（選ばれた矩形をそのまま返す）。</summary>
    [Fact]
    public void Focus_選択矩形をそのまま返す()
    {
        var selection = new EsRect(10, 20, 30, 40, EsRectInterval.Closed);

        var focus = CreateRequest(selection: selection).Focus;

        Assert.Equal(selection, focus);
        Assert.True(focus!.Value.IsClosedInterval);
    }

    /// <summary>どちらも無ければ注目矩形が null になることを検証する。</summary>
    [Fact]
    public void Focus_選択もハイライトも無ければnullになる()
    {
        Assert.Null(CreateRequest().Focus);
    }

    /// <summary>DIP 矩形がドメイン座標の矩形へ逆換算されることを検証する。</summary>
    [Fact]
    public void ToDomainRect_等価変換では同じ矩形になる()
    {
        var dip = new EsRect(10, 20, 30, 40);

        var result = CreateRequest().ToDomainRect(dip, CoordTransform.Identity);

        Assert.Equal(dip, result);
    }

    /// <summary>ドメインが DIP の 2 倍のポートでは、DIP がドメインへ 2 倍に換算されることを検証する。</summary>
    [Fact]
    public void ToDomainRect_ドメインがDIPの2倍のポートでドメインへ換算される()
    {
        var dip = new EsRect(10, 20, 30, 40);

        var result = CreateRequest().ToDomainRect(dip, CoordTransform.DipToDomain(() => 2.0));

        // ToDomain = DIP × 2（左上 (10,20) → (20,40)、右下 (40,60) → (80,120)）
        Assert.Equal(new EsRect(20, 40, 60, 80), result);
    }

    /// <summary>ハイライト矩形があればハイライト矩形を優先することを検証する。</summary>
    [Fact]
    public void ResolveFocusRect_ハイライトがあればハイライト矩形を優先する()
    {
        var highlight = new EsRect(1, 2, 3, 4);
        var selection = new EsRect(10, 20, 30, 40, EsRectInterval.Closed);

        var result = MarqueeRenderRequest.ResolveFocusRect(highlight, selection);

        Assert.Equal(highlight, result);
        // ハイライト矩形は検出結果（全ピクセルを含む）のため、終点を +1 しない半開区間を引き継ぐ
        Assert.False(result!.Value.IsClosedInterval);
    }

    /// <summary>ハイライトが無く選択だけがある場合は選択矩形を返すことを検証する。</summary>
    [Fact]
    public void ResolveFocusRect_ハイライトが無ければ選択矩形を返す()
    {
        var selection = new EsRect(10, 20, 30, 40, EsRectInterval.Closed);

        var result = MarqueeRenderRequest.ResolveFocusRect(null, selection);

        Assert.Equal(selection, result);
        // ドラッグの選択矩形は閉区間（終点のピクセルも含む）を引き継ぐ
        Assert.True(result!.Value.IsClosedInterval);
    }

    /// <summary>どちらも null なら null を返すことを検証する。</summary>
    [Fact]
    public void ResolveFocusRect_両方nullならnullを返す()
    {
        var result = MarqueeRenderRequest.ResolveFocusRect(null, null);

        Assert.Null(result);
    }

    /// <summary>ハイライト矩形が選ばれた場合は終点を加算せずに DIP 変換されることを検証する。</summary>
    [Fact]
    public void ResolveFocusDipRect_ハイライト矩形は終点を加算せずDIP変換する()
    {
        var highlight = new EsRect(1, 2, 3, 4);
        var request = CreateRequest(highlight: highlight, selection: new EsRect(10, 20, 30, 40, EsRectInterval.Closed));

        var result = request.ResolveFocusDipRect(CoordTransform.Identity);

        Assert.Equal(new EsRect(1, 2, 3, 4), result);
    }

    /// <summary>選択矩形が選ばれた場合は終点を加算して DIP 変換されることを検証する。</summary>
    [Fact]
    public void ResolveFocusDipRect_選択矩形は終点を加算してDIP変換する()
    {
        var request = CreateRequest(selection: new EsRect(10, 20, 30, 40, EsRectInterval.Closed));

        var result = request.ResolveFocusDipRect(CoordTransform.Identity);

        Assert.Equal(new EsRect(10, 20, 31, 41), result);
    }

    /// <summary>どちらも null なら null を返すことを検証する。</summary>
    [Fact]
    public void ResolveFocusDipRect_両方nullならnullを返す()
    {
        var result = CreateRequest().ResolveFocusDipRect(CoordTransform.Identity);

        Assert.Null(result);
    }

    /// <summary>ポートが指定されている場合はそれが優先されることを検証する。</summary>
    [Fact]
    public void ResolvePort_ポート指定時はポートを返す()
    {
        var port = CoordTransform.DipToDomain(() => 2.0);

        var result = MarqueeRenderRequest.ResolvePort(port, dipToDomainScaling: 3.0);

        Assert.Same(port, result);
    }

    /// <summary>ポート未指定の場合は倍率からポートを生成することを検証する。</summary>
    [Fact]
    public void ResolvePort_ポート未指定なら倍率を適用する()
    {
        var result = MarqueeRenderRequest.ResolvePort(null, dipToDomainScaling: 2.0);

        // 倍率はドメイン座標 / ローカル DIP（ドメイン (10,20) → DIP (5,10)）
        Assert.Equal(new EsPoint(5, 10), result.ToDip(new EsPoint(10, 20)));
    }

    /// <summary>ポート未指定で倍率が 0 以下・等倍の場合は等価変換になることを検証する。</summary>
    /// <param name="dipToDomainScaling">適用する倍率。</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(1.0)]
    [InlineData(double.NaN)]
    public void ResolvePort_倍率が無効なら等価変換になる(double dipToDomainScaling)
    {
        var result = MarqueeRenderRequest.ResolvePort(null, dipToDomainScaling);

        Assert.Same(CoordTransform.Identity, result);
    }

    /// <summary>解決済みの座標変換ポートがポート優先で公開されることを検証する。</summary>
    [Fact]
    public void ResolvedPort_ポート指定時はそのポートを返す()
    {
        var port = CoordTransform.DipToDomain(() => 3.0);

        var request = CreateRequest(port: port, scaling: 2.0);

        Assert.Same(port, request.ResolvedPort);
    }

    /// <summary>ポート未指定の場合は倍率から解決したポートを返すことを検証する。</summary>
    [Fact]
    public void ResolvedPort_ポート未指定なら倍率から解決する()
    {
        var request = CreateRequest(scaling: 2.0);

        Assert.Equal(new EsPoint(5, 10), request.ResolvedPort.ToDip(new EsPoint(10, 20)));
    }
}
