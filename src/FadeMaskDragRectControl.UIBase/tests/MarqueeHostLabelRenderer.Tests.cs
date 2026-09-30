using EsUtil.Feature.Marquee.Presenter;
using EsUtil.Platform.Core.Geometry;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.SaveEvidence.FadeMaskDragRect.UIBase.Tests;

/// <summary><see cref="MarqueeHostLabelRenderer"/> の単体テスト（体裁の適用 = 配置指示の解決）。</summary>
/// <remarks>
/// 付加情報ラベルは本コントロール固有の情報だが、体裁（背景 = 文字の実寸 + 内側余白 × 2）はサイズラベルと同じ規則であり、<br/>
/// 体裁の値（<see cref="MarqueeAppearance"/>）と組み立て（<see cref="MarqueeHostLabelRenderer"/>）が単一所有する。<br/>
/// 両 UI のホストが同じ組み立てを書かないことを、配置指示の値で固定する。<br/>
/// 描画は各 UI が行う（本層は描画 API を持たない）ため、本テストは画素ではなく値を検証する。
/// </remarks>
public class MarqueeHostLabelRendererTests
{
    /// <summary>文字色（白・不透明）。</summary>
    private static readonly DrawingColor s_text = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>背景色（黒・不透明）。</summary>
    private static readonly DrawingColor s_background = DrawingColor.FromArgb(255, 0, 0, 0);

    /// <summary>測定に使う固定の実寸（テキストの実測値を模した値）。</summary>
    private static readonly EsSize s_measured = new(40.0, 14.0);

    /// <summary>実寸を固定で返す測定関数。</summary>
    /// <param name="text">測定する文言。</param>
    /// <param name="fontSize">フォントサイズ。</param>
    /// <returns>固定の実寸。</returns>
    private static EsSize Measure(string text, double fontSize) => s_measured;

    [Fact]
    public void Resolve_基準点を背景の左上に置く()
    {
        var anchor = new EsPoint(10, 20);

        var placement = MarqueeHostLabelRenderer.Resolve(anchor, "W", 14.0, s_text, s_background, 4, Measure);

        // 背景は基準点（10, 20）を左上とする
        Assert.Equal(anchor, placement.DipRect.TopLeft);
    }

    [Fact]
    public void Resolve_背景を実寸と内側余白から求める()
    {
        var placement = MarqueeHostLabelRenderer.Resolve(
            new EsPoint(10, 20), "W", 14.0, s_text, s_background, padding: 4, Measure);

        // 背景 = 文字の実寸 + 内側余白 × 2（四方へ同じ余白を空ける）
        Assert.Equal(s_measured.Width + 8, placement.DipRect.Width);
        Assert.Equal(s_measured.Height + 8, placement.DipRect.Height);
        Assert.Equal(4, placement.PaddingX);
        Assert.Equal(4, placement.PaddingY);
    }

    [Fact]
    public void Resolve_余白が大きいほど背景が広がる()
    {
        var narrow = MarqueeHostLabelRenderer.Resolve(
            new EsPoint(10, 10), "W", 14.0, s_text, s_background, padding: 1, Measure);
        var wide = MarqueeHostLabelRenderer.Resolve(
            new EsPoint(10, 10), "W", 14.0, s_text, s_background, padding: 8, Measure);

        Assert.True(wide.DipRect.Width > narrow.DipRect.Width, "内側余白が背景の大きさへ反映されていない");
        Assert.True(wide.DipRect.Height > narrow.DipRect.Height, "内側余白が背景の高さへ反映されていない");
    }

    [Fact]
    public void Resolve_文言と体裁を配置指示へ写す()
    {
        var placement = MarqueeHostLabelRenderer.Resolve(
            new EsPoint(0, 0), "確定: Enter", 12.0, s_text, s_background, 4, Measure);

        Assert.Equal("確定: Enter", placement.Text);
        Assert.Equal(12.0, placement.FontSize);
        Assert.Equal(s_text, placement.TextColor);
        Assert.Equal(s_background, placement.BackgroundColor);
    }

    [Fact]
    public void Resolve_文言がnullなら例外()
        => Assert.Throws<ArgumentNullException>(
            () => MarqueeHostLabelRenderer.Resolve(new EsPoint(0, 0), null!, 14.0, s_text, s_background, 4, Measure));

    [Fact]
    public void Resolve_測定関数がnullなら例外()
        => Assert.Throws<ArgumentNullException>(
            () => MarqueeHostLabelRenderer.Resolve(new EsPoint(0, 0), "W", 14.0, s_text, s_background, 4, null!));
}
