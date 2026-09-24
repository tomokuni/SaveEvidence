using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Xunit;
using DrawingColor = System.Drawing.Color;

using Estable.Marquee.Decoration;

namespace Estable.Marquee.Tests;

/// <summary><see cref="OuterDecoFrame"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 「どの矩形を暗転するか」「枠線を描くか」の判断を検証する。<br/>
/// これらの判断は両 UI で共通のため本テストが単一所有し、各 UI の描画テストは<br/>
/// 「与えられたフレームを描く」ことのみを検証する（UI 層は判断を持たない）。<br/>
/// <b>ラベルは本型が持たない</b>: サイズラベルは選択コア（<see cref="MarqueeFrameTests"/> と
/// <see cref="MarqueeSizeLabel"/> のテスト）が、操作・情報ラベルはホストが自前で描く。
/// </remarks>
public class OuterDecoFrameTests
{
    /// <summary>検証に使う描画領域のサイズ（DIP）。</summary>
    private static readonly EsSize s_clientSize = new(300, 300);

    /// <summary>検証に使う選択矩形（ドメイン座標）。</summary>
    private static readonly EsRect s_selection = new(50, 50, 100, 80);

    /// <summary>テスト用の描画入力を生成する。</summary>
    /// <param name="appearance">外観。null の場合は既定（黒 50% の暗転・ダークグレーの枠線）。</param>
    /// <param name="selectionRect">選択矩形（ドメイン座標）。</param>
    /// <param name="highlightRect">ハイライト矩形（ドメイン座標）。</param>
    /// <param name="hasSelection">選択が有効か。</param>
    /// <param name="hasHighlight">ハイライトが有効か。</param>
    /// <param name="showFullOverlay">全面暗転を表示するか。</param>
    /// <param name="port">座標変換ポート。</param>
    /// <param name="scaling">ポート未指定時に適用する倍率。</param>
    /// <returns>生成した描画入力。</returns>
    private static OuterDecoRequest CreateRequest(
        OuterDecoAppearance? appearance = null,
        EsRect? selectionRect = null,
        EsRect? highlightRect = null,
        bool hasSelection = false,
        bool hasHighlight = false,
        bool showFullOverlay = false,
        ICoordTransform? port = null,
        double scaling = 1.0)
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
            s_clientSize,
            port,
            scaling);

    /// <summary>枠線矩形から穴あけ矩形（DIP）を復元する（枠線は穴あけ矩形の内側に描かれる）。</summary>
    /// <param name="frame">描画内容。</param>
    /// <returns>穴あけ矩形（DIP）。枠線が無い場合は null。</returns>
    /// <remarks>
    /// 穴あけ矩形は本型の公開項目ではないため、枠線の上辺（位置と幅）と左辺（高さ）から復元する。<br/>
    /// 枠線の太さは枠線矩形の高さから得る（上辺の高さ = 太さ）。
    /// </remarks>
    private static EsRect? HoleFromBorder(OuterDecoFrame frame)
    {
        if (frame.BorderRects.Count < 4)
        {
            return null;
        }

        var top = frame.BorderRects[0];
        var left = frame.BorderRects[2];
        return new EsRect(top.X, top.Y, top.Width, left.Height + top.Height * 2);
    }

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
        Assert.Equal(new EsRect(50, 50, 101, 81), HoleFromBorder(frame));
        Assert.Equal(4, frame.BorderRects.Count);
    }

    /// <summary>ハイライトが有効な場合はハイライト矩形が優先されることを検証する。</summary>
    [Fact]
    public void Resolve_ハイライトが選択より優先される()
    {
        var highlight = new EsRect(10, 20, 30, 40);

        var frame = OuterDecoFrame.Resolve(CreateRequest(
            selectionRect: s_selection, highlightRect: highlight, hasSelection: true, hasHighlight: true));

        Assert.Equal(highlight, HoleFromBorder(frame));
    }

    /// <summary>穴あけ矩形が無く全面暗転が有効な場合は全面が暗転領域になることを検証する。</summary>
    [Fact]
    public void Resolve_全面暗転なら全面が暗転領域になる()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(showFullOverlay: true));

        Assert.Equal(new EsRect(0, 0, 300, 300), Assert.Single(frame.OverlayRegions));
        // 穴あけ位置が無いため枠線は持たない
        Assert.Empty(frame.BorderRects);
    }

    /// <summary>穴あけ矩形も全面暗転も無い場合は何も描画しないフレームになることを検証する。</summary>
    [Fact]
    public void Resolve_穴あけも全面暗転も無ければ空になる()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest());

        Assert.Empty(frame.OverlayRegions);
        Assert.Empty(frame.BorderRects);
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

        Assert.Equal(new EsRect(10, 20, 31, 41), HoleFromBorder(frame));
    }

    /// <summary>ハイライト矩形は終点を加算しない（既に閉区間表現）ことを検証する。</summary>
    [Fact]
    public void Resolve_ハイライト矩形は終点を加算しない()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(
            highlightRect: new EsRect(10, 20, 30, 40), hasHighlight: true));

        Assert.Equal(new EsRect(10, 20, 30, 40), HoleFromBorder(frame));
    }

    /// <summary>座標変換ポートが適用されることを検証する。</summary>
    [Fact]
    public void Resolve_座標変換ポートを適用する()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(
            highlightRect: new EsRect(10, 20, 30, 40), hasHighlight: true,
            port: CoordTransform.Scale(2.0)));

        Assert.Equal(new EsRect(20, 40, 60, 80), HoleFromBorder(frame));
    }

    /// <summary>ポート未指定の場合は倍率が等倍スケールとして適用されることを検証する。</summary>
    [Fact]
    public void Resolve_ポート未指定なら倍率を適用する()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(
            highlightRect: new EsRect(10, 20, 30, 40), hasHighlight: true, scaling: 2.0));

        Assert.Equal(new EsRect(20, 40, 60, 80), HoleFromBorder(frame));
    }

    /// <summary>ポートと倍率が両方指定された場合はポートが優先されることを検証する。</summary>
    [Fact]
    public void Resolve_ポートと倍率はポートが優先される()
    {
        var frame = OuterDecoFrame.Resolve(CreateRequest(
            highlightRect: new EsRect(10, 20, 30, 40), hasHighlight: true,
            port: CoordTransform.Scale(3.0), scaling: 2.0));

        Assert.Equal(new EsRect(30, 60, 90, 120), HoleFromBorder(frame));
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

        Assert.Equal(new EsRect(10, 20, 30, 40), HoleFromBorder(frame));
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
        // 枠線は穴あけ矩形があるため生成される
        Assert.NotEmpty(frame.BorderRects);
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
        // 上辺の高さが枠線の太さになる
        Assert.Equal(4.0, frame.BorderRects[0].Height);
    }
}
