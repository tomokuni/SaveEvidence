using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace Estable.Marquee.Decoration.Tests;

/// <summary><see cref="OuterDecoFrame"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 「どの矩形を暗転するか」「枠線を描くか」「どのラベルを描くか」の判断を検証する。<br/>
/// これらの判断は両 UI で共通のため本テストが単一所有し、各 UI の描画テストは<br/>
/// 「与えられたフレームを描く」ことのみを検証する（UI 層は判断を持たない）。
/// </remarks>
public class OuterDecoFrameTests
{
    /// <summary>検証に使うクライアント矩形（DIP）。</summary>
    private static readonly EsRect s_client = new(0, 0, 300, 300);

    /// <summary>検証に使う選択矩形（ドメイン座標）。</summary>
    private static readonly EsRect s_selection = new(50, 50, 100, 80);

    /// <summary>テスト用の描画入力を生成する。</summary>
    /// <param name="appearance">外観。null の場合は既定（黒 50% の暗転・白枠）。</param>
    /// <param name="operationText">操作ラベル（2 行目）の文言。</param>
    /// <param name="infoText">情報ラベル（3 行目）の文言。</param>
    /// <param name="selectionRect">選択矩形（ドメイン座標）。</param>
    /// <param name="highlightRect">ハイライト矩形（ドメイン座標）。</param>
    /// <param name="hasSelection">選択が有効か。</param>
    /// <param name="hasHighlight">ハイライトが有効か。</param>
    /// <param name="showFullOverlay">全面暗転を表示するか。</param>
    /// <param name="port">座標変換ポート。</param>
    /// <param name="scaling">ポート未指定時に適用する倍率。</param>
    /// <param name="provider">サイズラベルの文言プロバイダ。</param>
    /// <param name="displayScale">サイズラベルの自動生成に使うスケーリング。</param>
    /// <returns>生成した描画入力。</returns>
    private static OuterDecoRequest CreateRequest(
        OuterDecoAppearance? appearance = null,
        string? operationText = null,
        string? infoText = null,
        EsRect? selectionRect = null,
        EsRect? highlightRect = null,
        bool hasSelection = false,
        bool hasHighlight = false,
        bool showFullOverlay = false,
        ICoordTransform? port = null,
        double scaling = 1.0,
        Func<string?>? provider = null,
        double displayScale = 1.0)
        => new(
            new OuterDecoOptions
            {
                Appearance = appearance ?? new OuterDecoAppearance(),
                ShowFullOverlay = showFullOverlay,
                LogicalSelectionRect = selectionRect,
                LogicalHighlightRect = highlightRect,
                HasSelection = hasSelection,
                HasHighlight = hasHighlight,
            },
            s_client,
            port,
            scaling,
            provider,
            operationText is null ? null : () => operationText,
            infoText is null ? null : () => infoText,
            displayScale);

    // ====================================================================
    // 暗転領域（穴あけ矩形の決定）
    // ====================================================================

    /// <summary>選択矩形が穴あけ矩形になり、4 辺が暗転領域になることを検証する。</summary>
    [Fact]
    public void Resolve_選択矩形の4辺が暗転領域になる()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(selectionRect: s_selection, hasSelection: true));

        Assert.Equal(4, frame.OverlayRegions.Count);
        // 閉区間 +1 の DIP 変換（始点はそのまま・終点に +1）
        Assert.Equal(new EsRect(50, 50, 101, 81), frame.LabelAnchor);
        Assert.Equal(4, frame.BorderRects.Count);
    }

    /// <summary>ハイライトが有効な場合はハイライト矩形が優先されることを検証する。</summary>
    [Fact]
    public void Resolve_ハイライトが選択より優先される()
    {
        var highlight = new EsRect(10, 20, 30, 40);

        var frame = OuterDecoFrame.Resolve(CreateRequest(
            selectionRect: s_selection, highlightRect: highlight, hasSelection: true, hasHighlight: true));

        Assert.Equal(highlight, frame.LabelAnchor);
    }

    /// <summary>穴あけ矩形が無く全面暗転が有効な場合は全面が暗転領域になることを検証する。</summary>
    [Fact]
    public void Resolve_全面暗転なら全面が暗転領域になる()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(showFullOverlay: true));

        Assert.Equal(s_client, Assert.Single(frame.OverlayRegions));
        // 穴あけ位置が無いため枠線・ラベルは持たない
        Assert.Empty(frame.BorderRects);
        Assert.Null(frame.LabelAnchor);
        Assert.Null(frame.SizeLabelText);
        Assert.Empty(frame.SubLines);
    }

    /// <summary>穴あけ矩形も全面暗転も無い場合は何も描画しないフレームになることを検証する。</summary>
    [Fact]
    public void Resolve_穴あけも全面暗転も無ければ空になる()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest());

        Assert.Empty(frame.OverlayRegions);
        Assert.Empty(frame.BorderRects);
        Assert.Null(frame.LabelAnchor);
    }

    /// <summary>選択矩形が有効でもハイライトが無効で矩形が null の場合は穴あけなしになることを検証する。</summary>
    [Fact]
    public void Resolve_選択有効でも矩形がnullなら穴あけなし()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(hasSelection: true));

        Assert.Empty(frame.OverlayRegions);
    }

    // ====================================================================
    // 閉区間の扱い（選択 +1 / ハイライト +0）
    // ====================================================================

    /// <summary>選択矩形は閉区間（終点 +1）で DIP 変換されることを検証する。</summary>
    [Fact]
    public void Resolve_選択矩形は終点に1を加算する()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(
            selectionRect: new EsRect(10, 20, 30, 40), hasSelection: true));

        Assert.Equal(new EsRect(10, 20, 31, 41), frame.LabelAnchor);
    }

    /// <summary>ハイライト矩形は終点を加算しない（既に閉区間表現）ことを検証する。</summary>
    [Fact]
    public void Resolve_ハイライト矩形は終点を加算しない()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(
            highlightRect: new EsRect(10, 20, 30, 40), hasHighlight: true));

        Assert.Equal(new EsRect(10, 20, 30, 40), frame.LabelAnchor);
    }

    /// <summary>座標変換ポートが適用されることを検証する。</summary>
    [Fact]
    public void Resolve_座標変換ポートを適用する()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(
            highlightRect: new EsRect(10, 20, 30, 40), hasHighlight: true,
            port: CoordTransform.Scale(2.0)));

        Assert.Equal(new EsRect(20, 40, 60, 80), frame.LabelAnchor);
    }

    /// <summary>ポート未指定の場合は倍率が等倍スケールとして適用されることを検証する。</summary>
    [Fact]
    public void Resolve_ポート未指定なら倍率を適用する()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(
            highlightRect: new EsRect(10, 20, 30, 40), hasHighlight: true, scaling: 2.0));

        Assert.Equal(new EsRect(20, 40, 60, 80), frame.LabelAnchor);
    }

    /// <summary>ポートと倍率が両方指定された場合はポートが優先されることを検証する。</summary>
    [Fact]
    public void Resolve_ポートと倍率はポートが優先される()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(
            highlightRect: new EsRect(10, 20, 30, 40), hasHighlight: true,
            port: CoordTransform.Scale(3.0), scaling: 2.0));

        Assert.Equal(new EsRect(30, 60, 90, 120), frame.LabelAnchor);
    }

    /// <summary>倍率が 0 以下・等倍の場合は等価変換（座標が変わらない）になることを検証する。</summary>
    /// <param name="scaling">適用する倍率。</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(1.0)]
    public void Resolve_倍率が無効なら等価変換になる(double scaling)
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(
            highlightRect: new EsRect(10, 20, 30, 40), hasHighlight: true, scaling: scaling));

        Assert.Equal(new EsRect(10, 20, 30, 40), frame.LabelAnchor);
    }

    // ====================================================================
    // 透明指定（暗転・枠線）
    // ====================================================================

    /// <summary>暗転色が透明なら暗転領域を生成しないことを検証する。</summary>
    [Fact]
    public void Resolve_暗転色が透明なら暗転領域を生成しない()
    {
        var appearance = new OuterDecoAppearance { OverlayColor = DrawingColor.FromArgb(0, 0, 0, 0) };

        var frame = OuterDecoFrame.Resolve(CreateRequest(appearance, selectionRect: s_selection, hasSelection: true));

        Assert.Empty(frame.OverlayRegions);
        // 枠線・ラベルは穴あけ矩形があるため生成される
        Assert.NotEmpty(frame.BorderRects);
        Assert.NotNull(frame.LabelAnchor);
    }

    /// <summary>枠線色が透明なら枠線矩形を生成しないことを検証する。</summary>
    [Fact]
    public void Resolve_枠線色が透明なら枠線矩形を生成しない()
    {
        var appearance = new OuterDecoAppearance { BorderColor = DrawingColor.FromArgb(0, 0, 0, 0) };

        var frame = OuterDecoFrame.Resolve(CreateRequest(appearance, selectionRect: s_selection, hasSelection: true));

        Assert.NotEmpty(frame.OverlayRegions);
        Assert.Empty(frame.BorderRects);
    }

    // ====================================================================
    // サイズラベル（1 行目）
    // ====================================================================

    /// <summary>サイズラベルを非表示にすると文言を生成しないことを検証する。</summary>
    [Fact]
    public void Resolve_サイズラベル非表示なら文言を生成しない()
    {
        var appearance = new OuterDecoAppearance { ShowSizeLabel = false };
        var invoked = false;

        var frame = OuterDecoFrame.Resolve(CreateRequest(
            appearance, selectionRect: s_selection, hasSelection: true,
            provider: () => { invoked = true; return "ラベル"; }));

        Assert.Null(frame.SizeLabelText);
        // 非表示時はプロバイダを評価しない（無駄な呼び出しを避ける）
        Assert.False(invoked);
    }

    /// <summary>プロバイダの文言がそのまま使われることを検証する。</summary>
    [Fact]
    public void Resolve_プロバイダの文言を使う()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(
            selectionRect: s_selection, hasSelection: true, provider: () => "Custom: (10, 20)"));

        Assert.Equal("Custom: (10, 20)", frame.SizeLabelText);
    }

    /// <summary>プロバイダが未指定の場合は矩形から自動生成することを検証する。</summary>
    [Fact]
    public void Resolve_プロバイダ未指定なら自動生成する()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(
            selectionRect: new EsRect(10, 20, 30, 40), hasSelection: true));

        // 閉区間 +1 の DIP 矩形 (10, 20, 31, 41) が等倍で表示される
        Assert.Equal("(10, 20) - (41, 61),  (31×41), DisplayScale: (100%)", frame.SizeLabelText);
    }

    /// <summary>プロバイダが空文字を返した場合も自動生成することを検証する（両 UI 共通）。</summary>
    [Fact]
    public void Resolve_プロバイダが空文字なら自動生成する()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(
            selectionRect: new EsRect(10, 20, 30, 40), hasSelection: true, provider: () => ""));

        Assert.Equal("(10, 20) - (41, 61),  (31×41), DisplayScale: (100%)", frame.SizeLabelText);
    }

    /// <summary>プロバイダが空白のみを返した場合はそのまま使われる（空文字とみなさない）ことを検証する。</summary>
    [Fact]
    public void Resolve_プロバイダが空白のみならそのまま使う()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(
            selectionRect: new EsRect(10, 20, 30, 40), hasSelection: true, provider: () => "  "));

        Assert.Equal("  ", frame.SizeLabelText);
    }

    /// <summary>自動生成の DisplayScale に表示スケーリングが反映されることを検証する。</summary>
    [Fact]
    public void Resolve_自動生成に表示スケーリングを反映する()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(
            selectionRect: new EsRect(10, 20, 30, 40), hasSelection: true, displayScale: 1.25));

        Assert.Contains("DisplayScale: (125%)", frame.SizeLabelText, StringComparison.Ordinal);
    }

    /// <summary>穴あけ矩形が無い場合はプロバイダを評価しないことを検証する。</summary>
    [Fact]
    public void Resolve_穴あけ矩形が無ければプロバイダを評価しない()
    {
        var invoked = false;

        _ = OuterDecoFrame.Resolve(CreateRequest(provider: () => { invoked = true; return "ラベル"; }));

        Assert.False(invoked);
    }

    /// <summary>サイズラベルの背景がアルファ 0 の場合は黒 50% へ解決されることを検証する。</summary>
    [Fact]
    public void Resolve_サイズラベルの背景が透明なら黒50パーセントになる()
    {
        var appearance = new OuterDecoAppearance { SizeLabelBackgroundColor = DrawingColor.FromArgb(0, 0, 0, 0) };

        var frame = OuterDecoFrame.Resolve(CreateRequest(appearance, selectionRect: s_selection, hasSelection: true));

        Assert.Equal(DrawingColor.FromArgb(128, 0, 0, 0), frame.SizeLabelStyle.BackgroundColor);
    }

    /// <summary>サイズラベルの外観（フォントサイズ・文字色・背景色）が外観から引き継がれることを検証する。</summary>
    [Fact]
    public void Resolve_サイズラベルの外観を引き継ぐ()
    {
        var appearance = new OuterDecoAppearance
        {
            SizeLabelFontSize = 18.0,
            SizeLabelColor = DrawingColor.Yellow,
            SizeLabelBackgroundColor = DrawingColor.FromArgb(200, 0, 0, 255),
        };

        var frame = OuterDecoFrame.Resolve(CreateRequest(appearance, selectionRect: s_selection, hasSelection: true));

        Assert.Equal(18.0, frame.SizeLabelStyle.FontSize);
        Assert.Equal(DrawingColor.Yellow, frame.SizeLabelStyle.Color);
        Assert.Equal(DrawingColor.FromArgb(200, 0, 0, 255), frame.SizeLabelStyle.BackgroundColor);
    }

    // ====================================================================
    // サブライン（2 行目以降）
    // ====================================================================

    /// <summary>操作ラベル（2 行目）が組み立てられることを検証する。</summary>
    [Fact]
    public void Resolve_操作ラベルを組み立てる()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(
            operationText: "ダブルクリック: 確定", selectionRect: s_selection, hasSelection: true));

        var line = Assert.Single(frame.SubLines);
        Assert.Equal("ダブルクリック: 確定", line.Text);
    }

    /// <summary>情報ラベル（3 行目）が追加されることを検証する。</summary>
    [Fact]
    public void Resolve_情報ラベルを追加する()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(
            operationText: "確定: Enter", infoText: "メモ帳",
            highlightRect: s_selection, hasHighlight: true));

        Assert.Equal(2, frame.SubLines.Count);
        Assert.Equal("メモ帳", frame.SubLines[1].Text);
    }

    /// <summary>操作ラベルを非表示にすると情報ラベルのみになることを検証する。</summary>
    [Fact]
    public void Resolve_操作ラベル非表示なら情報ラベルのみになる()
    {
        var appearance = new OuterDecoAppearance { ShowSubLines = false };

        var frame = OuterDecoFrame.Resolve(CreateRequest(
            appearance, operationText: "確定: Enter", infoText: "メモ帳",
            highlightRect: s_selection, hasHighlight: true));

        Assert.Equal("メモ帳", Assert.Single(frame.SubLines).Text);
    }

    /// <summary>サブラインの背景がアルファ 0 の場合は黒 50% へ解決されることを検証する。</summary>
    [Fact]
    public void Resolve_サブラインの背景が透明なら黒50パーセントになる()
    {
        var appearance = new OuterDecoAppearance { SubLineBackgroundColor = DrawingColor.FromArgb(0, 0, 0, 0) };

        var frame = OuterDecoFrame.Resolve(CreateRequest(
            appearance, operationText: "確定: Enter", selectionRect: s_selection, hasSelection: true));

        Assert.Equal(DrawingColor.FromArgb(128, 0, 0, 0), Assert.Single(frame.SubLines).BackgroundColor);
    }

    // ====================================================================
    // 色・枠線の引き継ぎ
    // ====================================================================

    /// <summary>暗転色・枠線色がフレームへ引き継がれることを検証する。</summary>
    [Fact]
    public void Resolve_色と枠線の設定を引き継ぐ()
    {
        var appearance = new OuterDecoAppearance
        {
            OverlayColor = DrawingColor.FromArgb(150, 255, 0, 0),
            BorderColor = DrawingColor.FromArgb(255, 0, 0, 255),
            BorderThickness = 4.0,
        };

        var frame = OuterDecoFrame.Resolve(CreateRequest(appearance, selectionRect: s_selection, hasSelection: true));

        Assert.Equal(DrawingColor.FromArgb(150, 255, 0, 0), frame.OverlayColor);
        Assert.Equal(DrawingColor.FromArgb(255, 0, 0, 255), frame.BorderColor);
        Assert.Equal(4.0, frame.BorderRects[0].Height);
    }
}
