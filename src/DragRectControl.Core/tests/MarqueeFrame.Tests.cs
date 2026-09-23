using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Estable.Marquee.Decoration;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.Marquee.Tests;

/// <summary><see cref="MarqueeFrame"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 選択矩形の<b>外側に描く装飾</b>（暗転マスク・穴あけ枠・ラベル）の描画内容の解決を検証する。<br/>
/// 選択矩形そのものの図形判断は装飾コア（<c>OuterDecoFrame</c>）が単一所有するため、<br/>
/// 本テストは「セッションの状態と外側の装飾の設定から正しい入力が組み立てられるか」を検証する。
/// </remarks>
public class MarqueeFrameTests
{
    /// <summary>検証に使うクライアント矩形（DIP）。</summary>
    private static readonly EsRect s_client = new(0, 0, 300, 300);

    /// <summary>検証に使う選択矩形（ドメイン座標）。</summary>
    private static readonly EsRect s_selection = new(50, 50, 100, 80);

    /// <summary>テスト用のセッションを生成する。</summary>
    /// <param name="options">設定。null の場合は既定。</param>
    /// <param name="selection">設定する選択矩形（ドメイン座標）。null の場合は設定しない。</param>
    /// <returns>生成したセッション。</returns>
    private static MarqueeSession CreateSession(MarqueeOptions? options = null, EsRect? selection = null)
    {
        var session = new MarqueeSession(null, options ?? MarqueeOptions.Default);
        if (selection is { } rect)
        {
            session.SetSelection(rect);
        }

        return session;
    }

    /// <summary>外側の装飾の外観を生成する。</summary>
    /// <param name="overlayColor">暗転色。</param>
    /// <param name="borderColor">枠線色。</param>
    /// <returns>生成した外観。</returns>
    private static OuterDecoAppearance CreateAppearance(DrawingColor overlayColor, DrawingColor borderColor)
        => new()
        {
            OverlayColor = overlayColor,
            BorderColor = borderColor,
            BorderThickness = 2.0,
            ShowSizeLabel = false,
            ShowSubLines = false,
            InfoLabelVisible = false,
        };

    /// <summary>暗転と枠線を描く外観を生成する。</summary>
    /// <returns>生成した外観。</returns>
    private static OuterDecoAppearance VisibleDecoration()
        => CreateAppearance(DrawingColor.FromArgb(128, 0, 0, 0), DrawingColor.White);

    /// <summary>フレームを解決する。</summary>
    /// <param name="session">セッション。</param>
    /// <param name="port">座標変換ポート。null の場合は倍率を使う。</param>
    /// <param name="scaling">倍率。</param>
    /// <param name="provider">サイズラベルの文言プロバイダ。</param>
    /// <param name="displayScale">サイズラベルの自動生成に使うスケーリング。</param>
    /// <returns>解決したフレーム。</returns>
    private static OuterDecoFrame Resolve(
        MarqueeSession session,
        ICoordTransform? port = null,
        double scaling = 1.0,
        Func<string?>? provider = null,
        double displayScale = 1.0)
        => MarqueeFrame.Resolve(session, s_client, port, scaling, provider, displayScale);

    // ====================================================================
    // 選択矩形の導出（セッションが単一のソース）
    // ====================================================================

    /// <summary>選択矩形がある場合に 4 辺の暗転領域と枠線が得られることを検証する。</summary>
    [Fact]
    public void Resolve_選択矩形の4辺が暗転領域と枠線になる()
    {
        var session = CreateSession(MarqueeOptions.Default with { OuterAppearance = VisibleDecoration() }, s_selection);

        var frame = Resolve(session);

        Assert.Equal(4, frame.OverlayRegions.Count);
        Assert.Equal(4, frame.BorderRects.Count);
        // 選択矩形は閉区間（終点 +1）で DIP 変換される（規則は装飾コアが単一所有する）
        Assert.Equal(new EsRect(50, 50, 101, 81), frame.LabelAnchor);
    }

    /// <summary>選択矩形を描かない場合は空のフレームになることを検証する。</summary>
    [Fact]
    public void Resolve_選択なしなら空のフレームを返す()
    {
        var session = CreateSession(MarqueeOptions.Default with { OuterAppearance = VisibleDecoration() });

        var frame = Resolve(session);

        Assert.Empty(frame.OverlayRegions);
        Assert.Empty(frame.BorderRects);
        Assert.Null(frame.LabelAnchor);
    }

    /// <summary>選択矩形を設定してもハンドル表示フラグは枠線の描画に影響しないことを検証する。</summary>
    [Fact]
    public void Resolve_ハンドル非表示でも枠線を描く()
    {
        var session = CreateSession(
            MarqueeOptions.Default with
            {
                ShowHandles = false,
                OuterAppearance = CreateAppearance(DrawingColor.Transparent, DrawingColor.White),
            },
            s_selection);

        var frame = Resolve(session);

        // ハンドルの表示可否は選択矩形の描画（UI 層）が判断し、外側の装飾は本値に影響されない
        Assert.Empty(frame.OverlayRegions);
        Assert.Equal(4, frame.BorderRects.Count);
    }

    /// <summary>選択矩形が無効なサイズの場合は空のフレームになることを検証する。</summary>
    [Fact]
    public void Resolve_選択矩形が無効なサイズなら空のフレームを返す()
    {
        var session = CreateSession(
            MarqueeOptions.Default with { OuterAppearance = VisibleDecoration() },
            new EsRect(10, 20, 0, 40));

        var frame = Resolve(session);

        Assert.Empty(frame.OverlayRegions);
        Assert.Empty(frame.BorderRects);
    }

    // ====================================================================
    // ハイライト・全面暗転（外側の装飾の設定）
    // ====================================================================

    /// <summary>ハイライトが選択矩形より優先されることを検証する。</summary>
    [Fact]
    public void Resolve_ハイライトが選択より優先される()
    {
        var highlight = new EsRect(10, 20, 30, 40);
        var session = CreateSession(
            MarqueeOptions.Default with
            {
                OuterAppearance = VisibleDecoration(),
                OuterHighlightRect = highlight,
                OuterHasHighlight = true,
            },
            s_selection);

        var frame = Resolve(session);

        // ハイライト矩形は終点を加算しない（既に閉区間表現）
        Assert.Equal(highlight, frame.LabelAnchor);
    }

    /// <summary>選択が無くてもハイライトがある場合は穴あけすることを検証する。</summary>
    [Fact]
    public void Resolve_選択なしでもハイライトを穴あけに使う()
    {
        var session = CreateSession(
            MarqueeOptions.Default with
            {
                OuterAppearance = VisibleDecoration(),
                OuterHighlightRect = new EsRect(10, 20, 30, 40),
                OuterHasHighlight = true,
            });

        var frame = Resolve(session);

        Assert.Equal(4, frame.OverlayRegions.Count);
        Assert.Equal(4, frame.BorderRects.Count);
    }

    /// <summary>ハイライトの有効フラグが false の場合は無視されることを検証する。</summary>
    [Fact]
    public void Resolve_ハイライト矩形があっても無効なら使わない()
    {
        var session = CreateSession(
            MarqueeOptions.Default with
            {
                OuterAppearance = VisibleDecoration(),
                OuterHighlightRect = new EsRect(10, 20, 30, 40),
                OuterHasHighlight = false,
            });

        var frame = Resolve(session);

        Assert.Empty(frame.OverlayRegions);
    }

    /// <summary>全面暗転が有効な場合は穴あけを行わず全面を塗ることを検証する。</summary>
    [Fact]
    public void Resolve_全面暗転なら全面を塗る()
    {
        var session = CreateSession(
            MarqueeOptions.Default with
            {
                OuterAppearance = VisibleDecoration(),
                OuterShowFullOverlay = true,
            });

        var frame = Resolve(session);

        Assert.Equal(s_client, Assert.Single(frame.OverlayRegions));
        Assert.Empty(frame.BorderRects);
        Assert.Null(frame.LabelAnchor);
    }

    // ====================================================================
    // 枠線・暗転の透明指定（描画可否は装飾コアが判断する）
    // ====================================================================

    /// <summary>既定の外側の装飾では暗転せず枠線だけを描くことを検証する。</summary>
    [Fact]
    public void Resolve_既定は暗転せず枠線だけを描く()
    {
        var session = CreateSession(selection: s_selection);

        var frame = Resolve(session);

        Assert.Empty(frame.OverlayRegions);
        Assert.Equal(4, frame.BorderRects.Count);
        Assert.Null(frame.SizeLabelText);
        Assert.Empty(frame.SubLines);
    }

    /// <summary>枠線が透明な場合は枠線を描かないことを検証する。</summary>
    [Fact]
    public void Resolve_枠線が透明なら描かない()
    {
        var session = CreateSession(
            MarqueeOptions.Default with
            {
                OuterAppearance = CreateAppearance(DrawingColor.FromArgb(128, 0, 0, 0), DrawingColor.Transparent),
            },
            s_selection);

        var frame = Resolve(session);

        Assert.Equal(4, frame.OverlayRegions.Count);
        Assert.Empty(frame.BorderRects);
    }

    // ====================================================================
    // 座標変換（ポート優先・倍率・等価変換）
    // ====================================================================

    /// <summary>座標変換ポートが適用されることを検証する。</summary>
    [Fact]
    public void Resolve_座標変換ポートを適用する()
    {
        var session = CreateSession(
            MarqueeOptions.Default with { OuterAppearance = CreateAppearance(DrawingColor.Transparent, DrawingColor.White) },
            new EsRect(10, 20, 30, 40));

        var frame = Resolve(session, port: CoordTransform.Scale(2.0));

        // 閉区間 +1 の後にポートで変換する（規則は装飾コアが単一所有する）
        Assert.Equal(new EsRect(20, 40, 62, 82), frame.LabelAnchor);
    }

    /// <summary>ポート未指定の場合は倍率が適用されることを検証する。</summary>
    [Fact]
    public void Resolve_ポート未指定なら倍率を適用する()
    {
        var session = CreateSession(
            MarqueeOptions.Default with { OuterAppearance = CreateAppearance(DrawingColor.Transparent, DrawingColor.White) },
            new EsRect(10, 20, 30, 40));

        var frame = Resolve(session, scaling: 2.0);

        Assert.Equal(new EsRect(20, 40, 62, 82), frame.LabelAnchor);
    }

    /// <summary>ポートと倍率が両方指定された場合はポートが優先されることを検証する。</summary>
    [Fact]
    public void Resolve_ポートと倍率はポートが優先される()
    {
        var session = CreateSession(
            MarqueeOptions.Default with { OuterAppearance = CreateAppearance(DrawingColor.Transparent, DrawingColor.White) },
            new EsRect(10, 20, 30, 40));

        var frame = Resolve(session, port: CoordTransform.Scale(3.0), scaling: 2.0);

        Assert.Equal(new EsRect(30, 60, 93, 123), frame.LabelAnchor);
    }

    /// <summary>倍率が 0 以下・等倍の場合は等価変換になることを検証する。</summary>
    /// <param name="scaling">適用する倍率。</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(1.0)]
    public void Resolve_倍率が無効なら等価変換になる(double scaling)
    {
        var session = CreateSession(
            MarqueeOptions.Default with { OuterAppearance = CreateAppearance(DrawingColor.Transparent, DrawingColor.White) },
            new EsRect(10, 20, 30, 40));

        var frame = Resolve(session, scaling: scaling);

        Assert.Equal(new EsRect(10, 20, 31, 41), frame.LabelAnchor);
    }

    // ====================================================================
    // ラベル（文言プロバイダ・サブライン）
    // ====================================================================

    /// <summary>サイズラベルの文言プロバイダが使用されることを検証する。</summary>
    [Fact]
    public void Resolve_サイズラベルのプロバイダを使う()
    {
        var session = CreateSession(
            MarqueeOptions.Default with
            {
                OuterAppearance = CreateAppearance(DrawingColor.Transparent, DrawingColor.White) with { ShowSizeLabel = true },
            },
            new EsRect(10, 20, 30, 40));

        var frame = Resolve(session, provider: () => "元画像 30x40");

        Assert.Equal("元画像 30x40", frame.SizeLabelText);
    }

    /// <summary>プロバイダ未指定の場合は穴あけ矩形から文言が自動生成されることを検証する。</summary>
    [Fact]
    public void Resolve_プロバイダ未指定なら文言を自動生成する()
    {
        var session = CreateSession(
            MarqueeOptions.Default with
            {
                OuterAppearance = CreateAppearance(DrawingColor.Transparent, DrawingColor.White) with { ShowSizeLabel = true },
            },
            new EsRect(10, 20, 30, 40));

        var frame = Resolve(session, provider: () => null);

        // 文言の書式は装飾コア（OuterDecoLabels.FormatSizeLabel）が単一所有する
        Assert.NotNull(frame.SizeLabelText);
        Assert.StartsWith("(10, 20) - (41, 61)", frame.SizeLabelText, StringComparison.Ordinal);
    }

    /// <summary>消去ラベルを表示しない設定ではサブラインも描かないことを検証する。</summary>
    [Fact]
    public void Resolve_ラベル非表示ならサブラインも持たない()
    {
        var session = CreateSession(
            MarqueeOptions.Default with
            {
                OuterAppearance = CreateAppearance(DrawingColor.Transparent, DrawingColor.White),
                OuterRenderMode = OuterDecoRenderMode.AreaSelect,
                OuterWindowTitle = "メモ帳",
            },
            new EsRect(10, 20, 30, 40));

        var frame = Resolve(session);

        Assert.Null(frame.SizeLabelText);
        Assert.Empty(frame.SubLines);
    }

    /// <summary>表示する設定では描画モードとタイトルからサブラインが組み立てられることを検証する。</summary>
    [Fact]
    public void Resolve_サブラインを組み立てる()
    {
        var session = CreateSession(
            MarqueeOptions.Default with
            {
                OuterAppearance = CreateAppearance(DrawingColor.Transparent, DrawingColor.White) with
                {
                    ShowSizeLabel = false,
                    ShowSubLines = true,
                    InfoLabelVisible = true,
                },
                OuterRenderMode = OuterDecoRenderMode.WindowSelect,
                OuterWindowTitle = "メモ帳",
            },
            new EsRect(10, 20, 30, 40));

        var frame = Resolve(session);

        // 文言の組み立ては装飾コア（OuterDecoAppearance.BuildSubLines）が単一所有する
        Assert.NotEmpty(frame.SubLines);
        Assert.Contains(frame.SubLines, line => line.Text.Contains("メモ帳", StringComparison.Ordinal));
    }

    // ====================================================================
    // 引数の検証
    // ====================================================================

    /// <summary>セッションが null の場合は例外になることを検証する。</summary>
    [Fact]
    public void Resolve_セッションがnullなら例外()
        => Assert.Throws<ArgumentNullException>(() => MarqueeFrame.Resolve(null!, s_client, null, 1.0, null, 1.0));
}
