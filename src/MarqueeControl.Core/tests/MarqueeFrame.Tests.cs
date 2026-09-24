using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Xunit;
using DrawingColor = System.Drawing.Color;

using Estable.Marquee.Decoration;

namespace Estable.Marquee.Tests;

/// <summary><see cref="MarqueeFrame"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 1 フレーム分の描画内容（装飾・ハンドル・始点マーカー・ラベル・層の順序）の解決を検証する。<br/>
/// UI 実装は <see cref="MarqueeDrawContent.Layers"/> の順に各層を反映するだけであり、<br/>
/// 「描くかどうか」「どの順に描くか」の判断がコアにあることを本テストが担保する。<br/>
/// 装飾のフレーム単体（暗転領域・枠線）は <see cref="OuterDecoFrameTests"/> が検証する。
/// </remarks>
public class MarqueeFrameTests
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
        => MarqueeFrame.ResolveContent(new MarqueeFrameRequest(
            session.Options,
            decoOptions ?? MarqueeDecoOptions.Default,
            s_clientSize,
            session.HasSelection ? session.SelectionRect : null,
            session.HasSelection,
            session.NudgeActive && session.CrosshairVisible,
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
                HandleColor = DrawingColor.Red,
                HandleBorderColor = DrawingColor.Blue,
                HandleSize = 12,
            },
            s_selection);

        var content = ResolveContent(session);

        Assert.True(content.Handles is not null);
        var handles = content.Handles.Value;
        Assert.Equal(new EsRect(50, 50, 101, 81), handles.Rect);
        Assert.Equal(12, handles.HandleSize);
        Assert.Equal(DrawingColor.Red, handles.FillColor);
        Assert.Equal(DrawingColor.Blue, handles.BorderColor);
    }

    /// <summary>ハンドルの塗り色が未指定（全成分 0）の場合は白へ解決されることを検証する。</summary>
    [Fact]
    public void ResolveContent_ハンドルの塗り色が未指定なら白へ解決する()
    {
        var session = CreateSession(MarqueeOptions.Default with { HandleColor = DrawingColor.Empty }, s_selection);

        var content = ResolveContent(session);

        Assert.True(content.Handles is not null);
        Assert.Equal(DrawingColor.White, content.Handles.Value.FillColor);
    }

    /// <summary>ハンドルの枠線色の既定がダークグレーであることを検証する。</summary>
    [Fact]
    public void ResolveContent_ハンドルの枠線色の既定はダークグレー()
    {
        var session = CreateSession(selection: s_selection);

        var content = ResolveContent(session);

        Assert.Equal(DrawingColor.FromArgb(255, 169, 169, 169), content.Handles!.Value.BorderColor);
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
        Assert.Equal(new EsRect(100, 100, 202, 162), content.Handles!.Value.Rect);
    }

    // ====================================================================
    // 始点マーカー
    // ====================================================================

    /// <summary>nudge 中は始点マーカーが得られることを検証する。</summary>
    [Fact]
    public void ResolveContent_nudge中は始点マーカーを解決する()
    {
        var session = CreateSession(MarqueeOptions.Default with { MarkerColor = DrawingColor.Yellow, MarkerLength = 20 });
        session.NudgeByKey(EsKey.Right, new EsPoint(10, 20));

        var content = ResolveContent(session);

        Assert.True(content.Marker is not null);
        var marker = content.Marker.Value;
        Assert.Equal(DrawingColor.Yellow, marker.Color);
        Assert.Equal(MarqueeAppearance.MarkerThickness, marker.Thickness);
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

    /// <summary>座標ラベルを表示する設定では文言と配置の基準が得られることを検証する。</summary>
    [Fact]
    public void ResolveContent_座標ラベルを解決する()
    {
        var session = CreateSession(
            MarqueeOptions.Default with { ShowCoordLabel = true, CoordLabelFontSize = 16 },
            s_selection);
        session.Move(new EsPoint(40, 60));

        var content = ResolveContent(session);

        Assert.True(content.CoordLabel is not null);
        var label = content.CoordLabel.Value;
        // 文言の書式はコアの CoordinateFormatter が単一所有する
        Assert.Equal("(40, 60)", label.Text);
        Assert.Equal(16, label.FontSize);
        Assert.Equal(new EsSize(300, 300), label.ClientSize);
    }

    /// <summary>座標ラベルを表示しない設定では持たないことを検証する。</summary>
    [Fact]
    public void ResolveContent_座標ラベル非表示なら持たない()
    {
        var session = CreateSession(MarqueeOptions.Default with { ShowCoordLabel = false }, s_selection);

        Assert.Null(ResolveContent(session).CoordLabel);
    }

    // ====================================================================
    // サイズラベル（穴あけ矩形の位置と寸法）
    // ====================================================================

    /// <summary>選択があればサイズラベルが得られ、基準点が穴あけ矩形の左上になることを検証する。</summary>
    [Fact]
    public void ResolveContent_サイズラベルの基準点は穴あけ矩形の左上になる()
    {
        var session = CreateSession(
            MarqueeOptions.Default with { ShowSizeLabel = true, SizeLabelFontSize = 18, SizeLabelColor = DrawingColor.Yellow },
            s_selection);

        var content = ResolveContent(session);

        Assert.True(content.SizeLabel is not null);
        var label = content.SizeLabel.Value;
        // 閉区間 +1 の DIP 矩形 (50, 50, 101, 81) の左上が基準点になる
        Assert.Equal(new EsPoint(50, 50), label.Anchor);
        Assert.Equal(18, label.FontSize);
        Assert.Equal(DrawingColor.Yellow, label.TextColor);
        // 文言は矩形から自動生成される
        Assert.Contains("DisplayScale: (100%)", label.Text, StringComparison.Ordinal);
    }

    /// <summary>サイズラベルを表示しない設定では持たないことを検証する。</summary>
    [Fact]
    public void ResolveContent_サイズラベル非表示なら持たない()
    {
        var session = CreateSession(MarqueeOptions.Default with { ShowSizeLabel = false }, s_selection);

        Assert.Null(ResolveContent(session).SizeLabel);
    }

    /// <summary>穴あけ矩形が無い場合はサイズラベルを持たないことを検証する（全面暗転時も含む）。</summary>
    [Fact]
    public void ResolveContent_穴あけ矩形が無ければサイズラベルを持たない()
    {
        // 全面暗転のみ（選択・ハイライトなし）
        var session = CreateSession();

        Assert.Null(ResolveContent(session).SizeLabel);
    }

    /// <summary>サイズラベルの文言プロバイダが優先されることを検証する。</summary>
    [Fact]
    public void ResolveContent_サイズラベルのプロバイダを優先する()
    {
        var session = CreateSession(selection: s_selection);

        var content = ResolveContent(session, sizeLabelProvider: () => "Custom");

        Assert.Equal("Custom", content.SizeLabel!.Value.Text);
    }

    /// <summary>プロバイダが空文字を返した場合は自動生成されることを検証する。</summary>
    [Fact]
    public void ResolveContent_サイズラベルのプロバイダが空文字なら自動生成する()
    {
        var session = CreateSession(selection: s_selection);

        var content = ResolveContent(session, sizeLabelProvider: () => "");

        Assert.NotEqual("", content.SizeLabel!.Value.Text);
        Assert.Contains("DisplayScale:", content.SizeLabel.Value.Text, StringComparison.Ordinal);
    }

    /// <summary>表示スケーリングがサイズラベルの文言へ反映されることを検証する。</summary>
    [Fact]
    public void ResolveContent_サイズラベルの表示スケーリングを反映する()
    {
        var session = CreateSession(selection: s_selection);

        var content = ResolveContent(session, displayScale: 1.25);

        Assert.Contains("DisplayScale: (125%)", content.SizeLabel!.Value.Text, StringComparison.Ordinal);
    }

    /// <summary>ハイライトが有効な場合はハイライト矩形がサイズラベルの基準点になることを検証する。</summary>
    [Fact]
    public void ResolveContent_ハイライトのサイズラベルはハイライト矩形を使う()
    {
        var session = CreateSession(selection: s_selection);
        var deco = MarqueeDecoOptions.Default with
        {
            OuterHasHighlight = true,
            OuterHighlightRect = new EsRect(10, 20, 30, 40),
        };

        var content = ResolveContent(session, deco);

        // ハイライト矩形は終点 +0 のため (10, 20, 30, 40) が基準になる
        Assert.Equal(new EsPoint(10, 20), content.SizeLabel!.Value.Anchor);
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

    /// <summary>選択がある場合は装飾の後にハンドルが描かれることを検証する。</summary>
    [Fact]
    public void ResolveContent_選択ありならハンドルが装飾の後に来る()
    {
        var session = CreateSession(MarqueeOptions.Default with { ShowSizeLabel = false }, s_selection);

        var content = ResolveContent(session);

        Assert.Equal([MarqueeDrawLayer.Decoration, MarqueeDrawLayer.Handles], content.Layers);
    }

    /// <summary>ラベルは必ず最後の層になることを検証する（常に最上位に表示する）。</summary>
    [Fact]
    public void ResolveContent_ラベルは最後の層になる()
    {
        var session = CreateSession(
            MarqueeOptions.Default with { ShowSizeLabel = true, ShowCoordLabel = true, ShowHandles = true },
            s_selection);

        var content = ResolveContent(session);

        Assert.Equal(
            [MarqueeDrawLayer.Decoration, MarqueeDrawLayer.Handles, MarqueeDrawLayer.SizeLabel, MarqueeDrawLayer.CoordLabel],
            content.Layers);
        // ラベルは末尾で連続する（間に図形の層が入らない）
        Assert.Equal(MarqueeDrawLayer.CoordLabel, content.Layers[^1]);
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
            OuterAppearance = new OuterDecoAppearance { OverlayColor = DrawingColor.FromArgb(100, 0, 0, 0) },
        };

        var content = ResolveContent(session, deco);

        Assert.Equal(4, content.Decoration.OverlayRegions.Count);
        Assert.Equal(DrawingColor.FromArgb(100, 0, 0, 0), content.Decoration.OverlayColor);
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
            InnerAppearance = new InnerDecoAppearance { FillColor = DrawingColor.FromArgb(40, 255, 0, 0) },
        };

        var content = ResolveContent(session, deco);

        Assert.NotNull(content.InnerDecoration.FillRect);
        Assert.Equal(DrawingColor.FromArgb(40, 255, 0, 0), content.InnerDecoration.FillColor);
    }

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
