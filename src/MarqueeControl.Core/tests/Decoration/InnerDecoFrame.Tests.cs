using EsUtil.Platform.Core.Coordinate;
using EsUtil.Platform.Core.Geometry;
using Xunit;
using DrawingColor = System.Drawing.Color;

using Estable.Marquee.Decoration;

namespace Estable.Marquee.Tests;

/// <summary><see cref="InnerDecoFrame"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 穴あけ矩形の<b>内側に描く装飾</b>（塗り・内枠）の描画内容の解決を検証する。<br/>
/// 穴の決定規則（ハイライト優先 → 選択）と論理 → DIP の変換は外側の装飾（<c>OuterDecoFrame</c>）と共通であり、<br/>
/// 片方だけ変更されると内外の枠線がずれるため、変換規則の一致も本テストで固定する。
/// </remarks>
public class InnerDecoFrameTests
{
    /// <summary>検証に使う選択矩形（ドメイン座標）。</summary>
    private static readonly EsRect s_selection = new(50, 50, 100, 80);

    /// <summary>検証に使うハイライト矩形（ドメイン座標）。</summary>
    private static readonly EsRect s_highlight = new(10, 20, 30, 40);

    /// <summary>塗りと枠線を描く外観を生成する。</summary>
    /// <param name="fillColor">塗り色。</param>
    /// <param name="borderColor">枠線色。</param>
    /// <param name="borderThickness">枠線の太さ。</param>
    /// <returns>生成した外観。</returns>
    private static InnerDecoAppearance CreateAppearance(
        DrawingColor fillColor, DrawingColor borderColor, double borderThickness = 2.0)
        => new() { FillColor = fillColor, BorderColor = borderColor, BorderThickness = borderThickness };

    /// <summary>塗りと枠線を描く外観（赤 16% の塗り + 赤の枠線）。</summary>
    /// <returns>生成した外観。</returns>
    private static InnerDecoAppearance VisibleDecoration()
        => CreateAppearance(DrawingColor.FromArgb(40, 255, 59, 48), DrawingColor.FromArgb(255, 255, 59, 48));

    /// <summary>フレームを解決する。</summary>
    /// <param name="appearance">外観。</param>
    /// <param name="selection">選択矩形。null の場合は選択なし。</param>
    /// <param name="highlight">ハイライト矩形。null の場合はハイライトなし。</param>
    /// <param name="port">座標変換ポート。</param>
    /// <param name="scaling">ポート未指定の場合の倍率。</param>
    /// <returns>解決したフレーム。</returns>
    private static InnerDecoFrame Resolve(
        InnerDecoAppearance appearance,
        EsRect? selection = null,
        EsRect? highlight = null,
        ICoordTransform? port = null,
        double scaling = 1.0)
        => InnerDecoFrame.Resolve(new InnerDecoRequest(
            new InnerDecoOptions
            {
                Appearance = appearance,
                LogicalSelectionRect = selection,
                HasSelection = selection.HasValue,
                LogicalHighlightRect = highlight,
                HasHighlight = highlight.HasValue,
            },
            port,
            scaling));

    [Fact]
    public void Resolve_既定の外観は何も描かない()
    {
        // 汎用の選択矩形コントロールであるため、塗り・内枠は用途依存（既定では描かない）
        var frame = Resolve(new InnerDecoAppearance(), selection: s_selection);

        Assert.Null(frame.FillRect);
        Assert.Empty(frame.BorderRects);
    }

    [Fact]
    public void Resolve_選択矩形の内側を塗る()
    {
        var frame = Resolve(VisibleDecoration(), selection: s_selection);

        // 選択矩形は閉区間（+1）で変換される（外側の枠線と一致させる）
        Assert.Equal(new EsRect(50, 50, 101, 81), frame.FillRect);
    }

    [Fact]
    public void Resolve_ハイライト矩形は終点を加算しない()
    {
        var frame = Resolve(VisibleDecoration(), highlight: s_highlight);

        Assert.Equal(s_highlight, frame.FillRect);
    }

    [Fact]
    public void Resolve_ハイライトが選択より優先される()
    {
        var frame = Resolve(VisibleDecoration(), selection: s_selection, highlight: s_highlight);

        Assert.Equal(s_highlight, frame.FillRect);
    }

    [Fact]
    public void Resolve_穴が無ければ空のフレームになる()
    {
        var frame = Resolve(VisibleDecoration());

        Assert.Null(frame.FillRect);
        Assert.Empty(frame.BorderRects);
    }

    [Fact]
    public void Resolve_塗り色が透明なら塗らない()
    {
        var frame = Resolve(
            CreateAppearance(DrawingColor.Transparent, DrawingColor.White), selection: s_selection);

        Assert.Null(frame.FillRect);
        Assert.NotEmpty(frame.BorderRects);
    }

    [Fact]
    public void Resolve_枠線色が透明なら枠線を生成しない()
    {
        var frame = Resolve(
            CreateAppearance(DrawingColor.White, DrawingColor.Transparent), selection: s_selection);

        Assert.NotNull(frame.FillRect);
        Assert.Empty(frame.BorderRects);
    }

    [Fact]
    public void Resolve_枠線は穴の内側4辺になる()
    {
        var frame = Resolve(VisibleDecoration(), selection: s_selection);

        // 穴あけ矩形（50,50,101,81）の内側に太さ 2 の 4 辺
        Assert.Equal(4, frame.BorderRects.Count);
        Assert.Equal(new EsRect(50, 50, 101, 2), frame.BorderRects[0]);          // 上
        Assert.Equal(new EsRect(50, 129, 101, 2), frame.BorderRects[1]);         // 下
        Assert.Equal(new EsRect(50, 52, 2, 77), frame.BorderRects[2]);           // 左
        Assert.Equal(new EsRect(149, 52, 2, 77), frame.BorderRects[3]);          // 右
    }

    [Fact]
    public void Resolve_枠線の太さは下限へ正規化される()
    {
        // 太さ 0 以下は 0.5 へ正規化される（規則は幾何計算が単一所有する）
        var frame = Resolve(
            CreateAppearance(DrawingColor.White, DrawingColor.White, borderThickness: 0), selection: s_selection);

        Assert.Equal(new EsRect(50, 50, 101, 0.5), frame.BorderRects[0]);
    }

    [Fact]
    public void Resolve_座標変換ポートを適用する()
    {
        var port = CoordTransform.Scale(2.0);

        var frame = Resolve(VisibleDecoration(), selection: s_selection, port: port);

        // 倍率 2 のポートでは終点 +1 してから変換する（(50,50)-(151,131) → (100,100)-(302,262)）
        Assert.Equal(new EsRect(100, 100, 202, 162), frame.FillRect);
    }

    [Fact]
    public void Resolve_ポート未指定なら倍率を適用する()
    {
        var frame = Resolve(VisibleDecoration(), selection: s_selection, scaling: 2.0);

        Assert.Equal(new EsRect(100, 100, 202, 162), frame.FillRect);
    }

    [Fact]
    public void Resolve_倍率が無効なら等価変換になる()
    {
        var frame = Resolve(VisibleDecoration(), selection: s_selection, scaling: 0);

        Assert.Equal(new EsRect(50, 50, 101, 81), frame.FillRect);
    }

    [Fact]
    public void Resolve_色は外観をそのまま引き継ぐ()
    {
        var fill = DrawingColor.FromArgb(40, 1, 2, 3);
        var border = DrawingColor.FromArgb(255, 4, 5, 6);

        var frame = Resolve(CreateAppearance(fill, border), selection: s_selection);

        Assert.Equal(fill, frame.FillColor);
        Assert.Equal(border, frame.BorderColor);
    }
}
