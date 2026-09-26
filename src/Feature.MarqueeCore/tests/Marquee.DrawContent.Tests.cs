using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.MarqueeCore.Session;
using Xunit;

using EsUtil.Feature.MarqueeCore.Decoration;
using EsUtil.Feature.MarqueeCore.Drawing;
using EsUtil.Feature.MarqueeCore.Setting;

namespace EsUtil.Feature.MarqueeCore.Tests;

/// <summary><see cref="Marquee.ResolveFrame"/>（1 フレーム分の描画内容の解決）のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 1 フレーム分の描画内容（装飾・ハンドル・始点マーカー・ラベル・層の順序）の解決を検証する。<br/>
/// UI 実装は <see cref="MarqueeDrawContent.Layers"/> の順に各層を反映するだけであり、<br/>
/// 「描くかどうか」「どの順に描くか」の判断がコアにあることを本テストが担保する。<br/>
/// 装飾の判断（穴あけ矩形・暗転領域・枠線）もフレームの一部であるため本テストが検証する。
/// </remarks>
public class MarqueeDrawContentTests
{
    /// <summary>検証に使う描画領域のサイズ（DIP）。</summary>
    private static readonly EsSize s_clientSize = new(300, 300);

    /// <summary>検証に使う選択矩形（ドメイン座標）。</summary>
    private static readonly EsRect s_selection = new(50, 50, 100, 80);

    /// <summary>テスト用のセッションを生成する。</summary>
    /// <param name="options">設定。null の場合は既定。</param>
    /// <param name="selection">設定する選択矩形（ドメイン座標）。null の場合は設定しない。</param>
    /// <returns>生成したセッション。</returns>
    private static MarqueeSession CreateSession(MarqueeOptions? options = null, EsRect? selection = null)
    {
        var session = new MarqueeSession(options ?? MarqueeOptions.Default);
        if (selection is { } rect)
        {
            session.SetSelection(rect);
        }

        return session;
    }

    /// <summary>セッションから描画入力を組み立てて描画内容を解決する。</summary>
    /// <param name="session">セッション（設定・状態の参照元）。</param>
    /// <param name="decoOptions">装飾の設定。null の場合は既定。</param>
    /// <param name="port">座標変換ポート。null の場合はセッションのポートを使う。</param>
    /// <param name="scaling">ポート未指定時に適用する倍率。</param>
    /// <param name="sizeLabelProvider">サイズラベルの文言プロバイダ。</param>
    /// <param name="displayScale">サイズラベルの自動生成に使う表示スケーリング。</param>
    /// <returns>解決した描画内容。</returns>
    private static MarqueeDrawContent ResolveContent(
        MarqueeSession session,
        MarqueeDecoOptions? decoOptions = null,
        ICoordTransform? port = null,
        double scaling = 1.0,
        Func<string?>? sizeLabelProvider = null,
        double displayScale = 1.0)
        => Marquee.ResolveFrame(new MarqueeRenderRequest(
            session.Options,
            decoOptions ?? MarqueeDecoOptions.Default,
            s_clientSize,
            session.HasSelection ? session.SelectionRect : null,
            session.NudgeActive,
            session.MarkerPosition,
            session.LastMousePosition,
            port,
            scaling,
            sizeLabelProvider,
            displayScale));

    // ====================================================================
    // ドラッグハンドル
    // ====================================================================

    /// <summary>選択があり、ハンドルが有効な場合にハンドルの描画内容が得られることを検証する。</summary>
    [Fact]
    public void ResolveContent_ハンドルを解決する()
    {
        var session = CreateSession(
            MarqueeOptions.Default with
            {
                HandleSize = 12,
            },
            s_selection);

        var content = ResolveContent(session);

        Assert.True(content.Handles is not null);
        var handles = content.Handles.Value;
        Assert.Equal(8, handles.Rects.Count);
        Assert.Equal(new EsRect(44, 44, 12, 12), handles.Rects[0]);   // 左上（中心 50, 50）
        Assert.Equal(MarqueeOptions.HandleBorderThickness, handles.BorderThickness);
    }

    /// <summary>ハンドルの枠線色は描画の設定が単一所有する（描画内容には含まれない）ことを検証する。</summary>
    [Fact]
    public void ResolveContent_ハンドルに色を含めない()
    {
        var session = CreateSession(selection: s_selection);

        var content = ResolveContent(session);

        Assert.True(content.Handles is not null);
        Assert.Equal(8, content.Handles.Value.Rects.Count);
    }

    /// <summary>ハンドルを非表示にした設定ではハンドルを持たないことを検証する。</summary>
    [Fact]
    public void ResolveContent_ハンドル非表示なら持たない()
    {
        var session = CreateSession(MarqueeOptions.Default with { ShowHandles = false }, s_selection);

        Assert.Null(ResolveContent(session).Handles);
    }

    /// <summary>選択が無い場合はハンドルを持たないことを検証する。</summary>
    [Fact]
    public void ResolveContent_選択なしならハンドルを持たない()
    {
        Assert.Null(ResolveContent(CreateSession()).Handles);
    }

    /// <summary>座標変換ポートがハンドル矩形へ適用されることを検証する。</summary>
    [Fact]
    public void ResolveContent_ハンドルへ座標変換ポートを適用する()
    {
        var session = CreateSession(selection: s_selection);

        var content = ResolveContent(session, port: CoordTransform.Scale(2.0));

        // 選択矩形は閉区間（終点 +1）で DIP 変換される（両 UI で同一の規則）
        // 左上ハンドルの中心は変換後の左上（100, 100）であり、一辺 10 の中 心合わせで (95, 95) から始まる
        Assert.Equal(new EsRect(95, 95, 10, 10), content.Handles!.Value.Rects[0]);
    }

    // ====================================================================
    // 始点マーカー
    // ====================================================================

    /// <summary>nudge 中は始点マーカーが得られることを検証する。</summary>
    [Fact]
    public void ResolveContent_nudge中は始点マーカーを解決する()
    {
        var session = CreateSession(MarqueeOptions.Default with { MarkerLength = 20 });
        session.NudgeByKey(EsKey.Right, new EsPoint(10, 20));

        var content = ResolveContent(session);

        Assert.True(content.Marker is not null);
        var marker = content.Marker.Value;
        Assert.Equal(2.0, marker.Thickness);
        // 設定は全長のため、描画内容は半幅（全長 / 2）になる
        Assert.Equal(10, marker.Size);
    }

    /// <summary>nudge 中でない場合は始点マーカーを持たないことを検証する。</summary>
    [Fact]
    public void ResolveContent_nudge中でなければ始点マーカーを持たない()
    {
        Assert.Null(ResolveContent(CreateSession(selection: s_selection)).Marker);
    }

    // ====================================================================
    // カーソル座標ラベル
    // ====================================================================

    /// <summary>座標ラベルを表示する設定では文言と基準点が得られることを検証する。</summary>
    [Fact]
    public void ResolveContent_座標ラベルを解決する()
    {
        var session = CreateSession(MarqueeOptions.Default with { ShowCoordLabel = true }, s_selection);
        session.Move(new EsPoint(40, 60));

        var content = ResolveContent(session);

        Assert.True(content.CoordLabelText is not null);
        var label = content.CoordLabelText.Value;
        // 文言の書式はコアの EsPoint.Format が単一所有する
        Assert.Equal("(40, 60)", label.Text);
        // 基準点はカーソル + オフセットであり、左端・上端に合わせる
        Assert.Equal(new EsPoint(48, 68), label.Point);
        Assert.Equal(EsHorizontalAlign.Left, label.Horizontal);
        Assert.Equal(EsVerticalAlign.Top, label.Vertical);
        // クランプ領域は描画領域（はみ出しを領域内へ戻す）
        Assert.Equal(new EsSize(300, 300), label.ClampArea);
    }

    /// <summary>座標ラベルを表示しない設定では持たないことを検証する。</summary>
    [Fact]
    public void ResolveContent_座標ラベル非表示なら持たない()
    {
        var session = CreateSession(MarqueeOptions.Default with { ShowCoordLabel = false }, s_selection);

        Assert.Null(ResolveContent(session).CoordLabelText);
    }

    // ====================================================================
    // サイズラベル（穴あけ矩形の位置と寸法）
    // ====================================================================

    /// <summary>選択があればサイズラベルが得られ、基準点が穴あけ矩形の左上になることを検証する。</summary>
    [Fact]
    public void ResolveContent_サイズラベルの基準点は穴あけ矩形の左上になる()
    {
        var session = CreateSession(MarqueeOptions.Default with { ShowSizeLabel = true }, s_selection);

        var content = ResolveContent(session);

        Assert.True(content.SizeLabelText is not null);
        var label = content.SizeLabelText.Value;
        // 閉区間 +1 の DIP 矩形 (50, 50, 101, 81) の左上が基準点になる
        Assert.Equal(new EsPoint(50, 50), label.Point);
        Assert.Equal(EsHorizontalAlign.Left, label.Horizontal);
        Assert.Equal(EsVerticalAlign.Top, label.Vertical);
        // クランプ領域は指定しない（描画領域へはみ出しても位置を動かさない）
        Assert.Equal(default, label.ClampArea);
        // 文言は矩形から自動生成される（書式はコアの内部に閉じる）
        Assert.Equal("(50, 50) - (151, 131),  (101×81), DisplayScale: (100%)", label.Text);
    }

    /// <summary>サイズラベルの書式（座標・寸法と表示倍率）を検証する。</summary>
    /// <remarks>
    /// 書式はコアの内部手順であるため、公開契約（描画内容）を通して固定する。<br/>
    /// 表示スケーリングは座標・寸法の換算にも使う（DIP × スケーリングを整数切り捨てで表示する）。
    /// </remarks>
    [Fact]
    public void ResolveContent_サイズラベルの文言は座標と寸法と表示倍率を並べる()
    {
        var session = CreateSession(selection: s_selection);

        var content = ResolveContent(session, displayScale: 1.25);

        // 閉区間 +1 の DIP 矩形 (50, 50, 101, 81) を 1.25 倍して整数へ切り捨てる
        Assert.Equal("(62, 62) - (188, 163),  (126×101), DisplayScale: (125%)", content.SizeLabelText!.Value.Text);
    }

    /// <summary>表示スケーリングが 0 以下の場合は等倍（100%）として扱うことを検証する。</summary>
    /// <param name="displayScale">指定する表示スケーリング。</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void ResolveContent_サイズラベルの表示スケーリングが0以下なら等倍になる(double displayScale)
    {
        var session = CreateSession(selection: s_selection);

        var content = ResolveContent(session, displayScale: displayScale);

        Assert.Contains("DisplayScale: (100%)", content.SizeLabelText!.Value.Text, StringComparison.Ordinal);
    }

    /// <summary>サイズラベルを表示しない設定では持たないことを検証する。</summary>
    [Fact]
    public void ResolveContent_サイズラベル非表示なら持たない()
    {
        var session = CreateSession(MarqueeOptions.Default with { ShowSizeLabel = false }, s_selection);

        Assert.Null(ResolveContent(session).SizeLabelText);
    }

    /// <summary>穴あけ矩形が無い場合はサイズラベルを持たないことを検証する（全面暗転時も含む）。</summary>
    [Fact]
    public void ResolveContent_穴あけ矩形が無ければサイズラベルを持たない()
    {
        // 全面暗転のみ（選択・ハイライトなし）
        var session = CreateSession();

        Assert.Null(ResolveContent(session).SizeLabelText);
    }

    /// <summary>サイズラベルの文言プロバイダが優先されることを検証する。</summary>
    [Fact]
    public void ResolveContent_サイズラベルのプロバイダを優先する()
    {
        var session = CreateSession(selection: s_selection);

        var content = ResolveContent(session, sizeLabelProvider: () => "Custom");

        Assert.Equal("Custom", content.SizeLabelText!.Value.Text);
    }

    /// <summary>プロバイダが空文字を返した場合は自動生成されることを検証する。</summary>
    [Fact]
    public void ResolveContent_サイズラベルのプロバイダが空文字なら自動生成する()
    {
        var session = CreateSession(selection: s_selection);

        var content = ResolveContent(session, sizeLabelProvider: () => "");

        Assert.NotEqual("", content.SizeLabelText!.Value.Text);
        Assert.Contains("DisplayScale:", content.SizeLabelText.Value.Text, StringComparison.Ordinal);
    }

    /// <summary>表示スケーリングがサイズラベルの文言へ反映されることを検証する。</summary>
    [Fact]
    public void ResolveContent_サイズラベルの表示スケーリングを反映する()
    {
        var session = CreateSession(selection: s_selection);

        var content = ResolveContent(session, displayScale: 1.25);

        Assert.Contains("DisplayScale: (125%)", content.SizeLabelText!.Value.Text, StringComparison.Ordinal);
    }

    /// <summary>ハイライトが有効な場合はハイライト矩形がサイズラベルの基準点になることを検証する。</summary>
    [Fact]
    public void ResolveContent_ハイライトのサイズラベルはハイライト矩形を使う()
    {
        var session = CreateSession(selection: s_selection);
        var deco = MarqueeDecoOptions.Default with { OuterHighlightRect = new EsRect(10, 20, 30, 40) };

        var content = ResolveContent(session, deco);

        // ハイライト矩形は終点 +0 のため (10, 20, 30, 40) が基準になる
        Assert.Equal(new EsPoint(10, 20), content.SizeLabelText!.Value.Point);
    }

    // ====================================================================
    // 層の順序（描画順はコアが単一所有する）
    // ====================================================================

    /// <summary>選択なしの場合は装飾だけが描く層になることを検証する。</summary>
    [Fact]
    public void ResolveContent_選択なしなら装飾だけを描く()
    {
        var content = ResolveContent(CreateSession());

        Assert.Equal([MarqueeDrawLayer.Decoration], content.Layers);
    }

    /// <summary>選択ありでテキストなしならハンドルが最後の層になることを検証する。</summary>
    [Fact]
    public void ResolveContent_テキストなしならハンドルが最後の層になる()
    {
        var session = CreateSession(MarqueeOptions.Default with { ShowSizeLabel = false }, s_selection);

        var content = ResolveContent(session);

        Assert.Equal([MarqueeDrawLayer.Decoration, MarqueeDrawLayer.Handles], content.Layers);
    }

    /// <summary>2 つのテキスト（サイズ・カーソル座標）は同じ型で別々の位置を持ち、テキストの層を 1 つだけ末尾に置くことを検証する。</summary>
    [Fact]
    public void ResolveContent_2つのテキストは別々の基準点を持ち末尾の層にまとまる()
    {
        var session = CreateSession(
            MarqueeOptions.Default with { ShowSizeLabel = true, ShowCoordLabel = true, ShowHandles = true },
            s_selection);
        session.Move(new EsPoint(40, 60));

        var content = ResolveContent(session);

        // テキストはまとめて 1 つの層になり、常に最上位（UI 要素として置く UI は本層を無視してよい）
        Assert.Equal(
            [MarqueeDrawLayer.Decoration, MarqueeDrawLayer.Handles, MarqueeDrawLayer.Label],
            content.Layers);
        Assert.True(content.HasText);
        Assert.NotEqual(content.SizeLabelText!.Value.Point, content.CoordLabelText!.Value.Point);
    }

    /// <summary>テキストが無ければ層に含まれないことを検証する。</summary>
    [Fact]
    public void ResolveContent_テキストが無ければ層に含まれない()
    {
        var session = CreateSession(
            MarqueeOptions.Default with { ShowSizeLabel = false, ShowCoordLabel = false },
            s_selection);

        Assert.DoesNotContain(MarqueeDrawLayer.Label, ResolveContent(session).Layers);
    }

    /// <summary>nudge 中はマーカーが描く層に含まれることを検証する。</summary>
    [Fact]
    public void ResolveContent_マーカーは描く層に含まれる()
    {
        // nudge は新規選択の微調整であるため、選択がある場合はマーカーを描かない
        var session = CreateSession(MarqueeOptions.Default with { ShowSizeLabel = false });
        session.NudgeByKey(EsKey.Right, new EsPoint(10, 20));

        var content = ResolveContent(session);

        Assert.Equal([MarqueeDrawLayer.Decoration, MarqueeDrawLayer.Marker], content.Layers);
    }

    /// <summary>装飾は描くものが無くても常に層に含まれることを検証する（UI の分岐を増やさない）。</summary>
    [Fact]
    public void ResolveContent_装飾は常に層に含まれる()
    {
        var content = ResolveContent(CreateSession());

        Assert.Contains(MarqueeDrawLayer.Decoration, content.Layers);
        // 描くものが無い場合は各項目が空になる
        Assert.Empty(content.Decoration.OverlayRegions);
        Assert.Empty(content.Decoration.BorderRects);
    }

    /// <summary>同じ組合せの層の一覧は事前生成したものを共有することを検証する（描画のたびの確保をしない）。</summary>
    [Fact]
    public void ResolveContent_同じ組合せの層の一覧を共有する()
    {
        var session = CreateSession(selection: s_selection);

        var first = ResolveContent(session);
        var second = ResolveContent(session);

        Assert.Same(first.Layers, second.Layers);
    }

    // ====================================================================
    // 装飾の統合（1 値で返す）
    // ====================================================================

    /// <summary>装飾の設定が描画内容へ反映されることを検証する。</summary>
    [Fact]
    public void ResolveContent_装飾を解決する()
    {
        var session = CreateSession(selection: s_selection);
        var deco = MarqueeDecoOptions.Default with
        {
            OuterAppearance = new OuterDecoAppearance { BorderThickness = 2.0 },
            ShowOverlay = true,
        };

        var content = ResolveContent(session, deco);

        Assert.Equal(4, content.Decoration.OverlayRegions.Count);
    }

    /// <summary>全面暗転の設定が描画内容へ反映されることを検証する。</summary>
    [Fact]
    public void ResolveContent_全面暗転を解決する()
    {
        var session = CreateSession();
        var deco = MarqueeDecoOptions.Default with { OuterShowFullOverlay = true };

        var content = ResolveContent(session, deco);

        Assert.Equal(new EsRect(0, 0, 300, 300), Assert.Single(content.Decoration.OverlayRegions));
    }

    /// <summary>内側の装飾（塗り・内枠）が描画内容へ反映されることを検証する。</summary>
    [Fact]
    public void ResolveContent_内側の装飾を解決する()
    {
        var session = CreateSession(selection: s_selection);
        var deco = MarqueeDecoOptions.Default with
        {
            InnerAppearance = new InnerDecoAppearance { BorderThickness = 2.0 },
            ShowInnerFill = true,
        };

        var content = ResolveContent(session, deco);

        Assert.NotNull(content.InnerDecoration.FillRect);
    }

    // ====================================================================
    // 穴あけ矩形と暗転領域（外側の装飾）
    // ====================================================================

    /// <summary>選択矩形が穴あけ矩形（閉区間 +1）になり、4 辺が暗転領域になることを検証する。</summary>
    [Fact]
    public void ResolveContent_選択矩形の4辺が暗転領域になる()
    {
        var content = ResolveContent(CreateSession(selection: s_selection));

        // 選択は閉区間（終点 +1）で DIP 変換される
        Assert.Equal(new EsRect(50, 50, 101, 81), content.HoleRect);
        Assert.Equal(4, content.Decoration.OverlayRegions.Count);
        Assert.Equal(4, content.Decoration.BorderRects.Count);
    }

    /// <summary>ハイライト矩形が選択矩形より優先されることを検証する。</summary>
    [Fact]
    public void ResolveContent_ハイライトが選択より優先される()
    {
        var highlight = new EsRect(10, 20, 30, 40);
        var deco = MarqueeDecoOptions.Default with { OuterHighlightRect = highlight };

        var content = ResolveContent(CreateSession(selection: s_selection), deco);

        // ハイライト矩形は終点 +0（既に閉区間表現）
        Assert.Equal(highlight, content.HoleRect);
    }

    /// <summary>ハイライト矩形を null にすると、穴あけ矩形が選択矩形へ戻ることを検証する。</summary>
    [Fact]
    public void ResolveContent_ハイライトを解除すると選択矩形へ戻る()
    {
        var session = CreateSession(selection: s_selection);
        var withHighlight = MarqueeDecoOptions.Default with { OuterHighlightRect = new EsRect(10, 20, 30, 40) };

        // ハイライト中はハイライト矩形、解除後は選択矩形（閉区間 +1）が穴あけ矩形になる
        Assert.Equal(new EsRect(10, 20, 30, 40), ResolveContent(session, withHighlight).HoleRect);
        Assert.Equal(
            new EsRect(50, 50, 101, 81),
            ResolveContent(session, MarqueeDecoOptions.Default with { OuterHighlightRect = null }).HoleRect);
    }

    /// <summary>穴あけ矩形も全面暗転も無い場合は穴あけも枠線も無いことを検証する。</summary>
    [Fact]
    public void ResolveContent_穴あけも全面暗転も無ければ空になる()
    {
        var content = ResolveContent(CreateSession());

        Assert.Null(content.HoleRect);
        Assert.Empty(content.Decoration.OverlayRegions);
        Assert.Empty(content.Decoration.BorderRects);
    }

    /// <summary>暗転を描かない設定なら暗転領域を生成しないことを検証する。</summary>
    [Fact]
    public void ResolveContent_暗転を描かない設定なら暗転領域を生成しない()
    {
        var deco = MarqueeDecoOptions.Default with { ShowOverlay = false };

        var content = ResolveContent(CreateSession(selection: s_selection), deco);

        Assert.Empty(content.Decoration.OverlayRegions);
        // 枠線は穴あけ矩形があるため生成される
        Assert.NotEmpty(content.Decoration.BorderRects);
    }

    /// <summary>枠線を描かない設定なら枠線矩形を生成しないことを検証する。</summary>
    [Fact]
    public void ResolveContent_枠線を描かない設定なら枠線矩形を生成しない()
    {
        var deco = MarqueeDecoOptions.Default with { ShowOuterBorder = false };

        var content = ResolveContent(CreateSession(selection: s_selection), deco);

        Assert.NotEmpty(content.Decoration.OverlayRegions);
        Assert.Empty(content.Decoration.BorderRects);
    }

    /// <summary>外側の枠線が穴あけ矩形の内側 4 辺になり、太さが外観から引き継がれることを検証する。</summary>
    [Fact]
    public void ResolveContent_外側の枠線は穴の内側4辺になる()
    {
        var deco = MarqueeDecoOptions.Default with
        {
            OuterAppearance = new OuterDecoAppearance { BorderThickness = 4.0 },
        };

        var content = ResolveContent(CreateSession(selection: s_selection), deco);

        // 穴あけ矩形（50, 50, 101, 81）の内側に太さ 4 の 4 辺
        Assert.Equal(4, content.Decoration.BorderRects.Count);
        Assert.Equal(new EsRect(50, 50, 101, 4), content.Decoration.BorderRects[0]);      // 上
        Assert.Equal(new EsRect(50, 127, 101, 4), content.Decoration.BorderRects[1]);     // 下
        Assert.Equal(new EsRect(50, 54, 4, 73), content.Decoration.BorderRects[2]);       // 左
        Assert.Equal(new EsRect(147, 54, 4, 73), content.Decoration.BorderRects[3]);      // 右
    }

    /// <summary>枠線の太さが下限（0.5）へ正規化されることを検証する。</summary>
    [Fact]
    public void ResolveContent_枠線の太さは下限へ正規化される()
    {
        var deco = MarqueeDecoOptions.Default with
        {
            OuterAppearance = new OuterDecoAppearance { BorderThickness = 0 },
        };

        var content = ResolveContent(CreateSession(selection: s_selection), deco);

        Assert.Equal(new EsRect(50, 50, 101, 0.5), content.Decoration.BorderRects[0]);
    }

    /// <summary>座標変換ポートが穴あけ矩形へ適用されることを検証する。</summary>
    [Fact]
    public void ResolveContent_穴あけ矩形へ座標変換ポートを適用する()
    {
        var deco = MarqueeDecoOptions.Default with { OuterHighlightRect = new EsRect(10, 20, 30, 40) };

        var content = ResolveContent(CreateSession(selection: s_selection), deco, CoordTransform.Scale(2.0));

        Assert.Equal(new EsRect(20, 40, 60, 80), content.HoleRect);
    }

    /// <summary>ポート未指定の場合は倍率が等倍スケールとして穴あけ矩形へ適用されることを検証する。</summary>
    [Fact]
    public void ResolveContent_ポート未指定なら倍率を穴あけ矩形へ適用する()
    {
        var deco = MarqueeDecoOptions.Default with { OuterHighlightRect = new EsRect(10, 20, 30, 40) };

        var content = ResolveContent(CreateSession(selection: s_selection), deco, scaling: 2.0);

        Assert.Equal(new EsRect(20, 40, 60, 80), content.HoleRect);
    }

    /// <summary>倍率が 0 以下・等倍の場合は等価変換（座標が変わらない）になることを検証する。</summary>
    /// <param name="scaling">適用する倍率。</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(1.0)]
    public void ResolveContent_倍率が無効なら等価変換になる(double scaling)
    {
        var deco = MarqueeDecoOptions.Default with { OuterHighlightRect = new EsRect(10, 20, 30, 40) };

        var content = ResolveContent(CreateSession(selection: s_selection), deco, scaling: scaling);

        Assert.Equal(new EsRect(10, 20, 30, 40), content.HoleRect);
    }

    // ====================================================================
    // 穴あけ矩形の内側の装飾
    // ====================================================================

    /// <summary>既定の内側の装飾は何も描かないことを検証する（塗り・内枠は用途依存）。</summary>
    [Fact]
    public void ResolveContent_既定の内側の装飾は何も描かない()
    {
        var content = ResolveContent(CreateSession(selection: s_selection));

        Assert.Null(content.InnerDecoration.FillRect);
        Assert.Empty(content.InnerDecoration.BorderRects);
    }

    /// <summary>選択矩形の内側が塗られ、閉区間（+1）で変換されることを検証する。</summary>
    [Fact]
    public void ResolveContent_選択矩形の内側を塗る()
    {
        var content = ResolveContent(CreateSession(selection: s_selection), VisibleInnerDecoration());

        Assert.Equal(new EsRect(50, 50, 101, 81), content.InnerDecoration.FillRect);
    }

    /// <summary>ハイライト矩形の内側を塗る場合は終点を加算しないことを検証する。</summary>
    [Fact]
    public void ResolveContent_ハイライトの内側は終点を加算しない()
    {
        var highlight = new EsRect(10, 20, 30, 40);
        var deco = VisibleInnerDecoration() with { OuterHighlightRect = highlight };

        var content = ResolveContent(CreateSession(selection: s_selection), deco);

        Assert.Equal(highlight, content.InnerDecoration.FillRect);
    }

    /// <summary>塗りを描かない設定なら塗らず、枠線だけを生成することを検証する。</summary>
    [Fact]
    public void ResolveContent_塗りを描かない設定なら塗らない()
    {
        var deco = MarqueeDecoOptions.Default with { ShowInnerBorder = true };

        var content = ResolveContent(CreateSession(selection: s_selection), deco);

        Assert.Null(content.InnerDecoration.FillRect);
        Assert.NotEmpty(content.InnerDecoration.BorderRects);
    }

    /// <summary>内側の枠線は穴あけ矩形の内側 4 辺になり、外側の枠線と同じ矩形へ重なることを検証する。</summary>
    [Fact]
    public void ResolveContent_内側の枠線は外側と同じ矩形になる()
    {
        var deco = VisibleInnerDecoration() with
        {
            OuterAppearance = new OuterDecoAppearance { BorderThickness = 2.0 },
        };

        var content = ResolveContent(CreateSession(selection: s_selection), deco);

        Assert.Equal(4, content.InnerDecoration.BorderRects.Count);
        Assert.Equal(content.Decoration.BorderRects, content.InnerDecoration.BorderRects);
    }

    /// <summary>内側の塗り・枠線を描く設定を生成する（ハイライト矩形なし）。</summary>
    /// <returns>塗りと内枠を描く設定。</returns>
    private static MarqueeDecoOptions VisibleInnerDecoration()
        => MarqueeDecoOptions.Default with { ShowInnerFill = true, ShowInnerBorder = true };

    // ====================================================================
    // 文字の有無の通知
    // ====================================================================

    /// <summary>ラベルの有無が <see cref="MarqueeDrawContent.HasText"/> へ反映されることを検証する。</summary>
    /// <param name="showSizeLabel">サイズラベルを表示するか。</param>
    /// <param name="showCoordLabel">座標ラベルを表示するか。</param>
    /// <param name="expected">文字を描く必要があるか。</param>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void ResolveContent_文字の有無を通知する(bool showSizeLabel, bool showCoordLabel, bool expected)
    {
        var session = CreateSession(
            MarqueeOptions.Default with { ShowSizeLabel = showSizeLabel, ShowCoordLabel = showCoordLabel },
            s_selection);

        Assert.Equal(expected, ResolveContent(session).HasText);
    }
}
