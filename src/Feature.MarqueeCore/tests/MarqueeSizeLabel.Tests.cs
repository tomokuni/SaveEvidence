using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.MarqueeCore.Tests;

/// <summary><see cref="MarqueeSizeLabel"/> の単体テスト（文言の書式と配置・体裁）。</summary>
/// <remarks>
/// 文言の書式（コアが単一所有する）と、配置（文字の実寸と内側余白から決まる）を検証する。<br/>
/// 背景・文字原点の計算は <see cref="EsLabelLayout"/>（Platform.Core）が単一所有するため、<br/>
/// 本テストは「どの余白を使うか」というコア固有の指定を固定する。
/// </remarks>
public class MarqueeSizeLabelTests
{
    private static readonly DrawingColor s_text = DrawingColor.White;
    private static readonly DrawingColor s_background = DrawingColor.FromArgb(153, 0, 0, 0);

    private static MarqueeSizeLabel CreateLabel(string text = "(0, 0) - (50, 14),  (50×14)", EsPoint? anchor = null, double fontSize = 14.0)
        => new(text, anchor ?? new EsPoint(0, 30), fontSize, s_text, s_background);

    // ====================================================================
    // ComputeFrame（配置）
    // ====================================================================

    [Fact]
    public void ComputeFrame_基準点を背景の左上に置き内側余白を四方へ空ける()
    {
        var label = CreateLabel(anchor: new EsPoint(10, 30));

        var frame = label.ComputeFrame(new EsSize(50, 14));

        // 背景は基準点（10, 30）を左上とし、文字の実寸（50x14）へ余白 4 を四方へ加えた大きさ
        Assert.Equal(new EsRect(10, 30, 58, 22), frame.Background);
        // 文字は背景の内側へ余白分だけ入る（四方の余白が等しい）
        Assert.Equal(new EsPoint(14, 34), frame.TextOrigin);
        Assert.Equal(4.0, frame.TextOrigin.X - frame.Background.X);
        Assert.Equal(4.0, frame.TextOrigin.Y - frame.Background.Y);
    }

    [Fact]
    public void ComputeFrame_行送りは背景の下端と一致する()
    {
        // 背景は基準点（アンカー）を左上とするため、次の行の上端は背景の下端と一致する（重なりも隙間も無い）。
        // ホストが描く操作・情報ラベルも同じ基準点を使うため、行送りだけで正しく縦に積める。
        var anchor = new EsPoint(0, 30);
        var textSize = new EsSize(50, 14);
        var frame = CreateLabel(anchor: anchor).ComputeFrame(textSize);

        Assert.Equal(
            frame.Background.Bottom,
            EsLabelLayout.NextTop(anchor.Y, textSize.Height, EsLabelLayout.Padding));
    }

    [Fact]
    public void ComputeFrame_付加情報ラベルと同じ内側余白を使う()
    {
        // ホストが描く操作・情報ラベルと同じ体裁（同じ内側余白）で縦に積めることを保証する
        var textSize = new EsSize(30, 10);
        var sizeLabelBox = CreateLabel(anchor: new EsPoint(0, 0)).ComputeFrame(textSize).Background;
        var hostLabelBox = EsLabelLayout.ComputeBoxAtAnchor(
            new EsPoint(0, 0), textSize, EsLabelLayout.Padding, EsLabelLayout.Padding);

        Assert.Equal(hostLabelBox.Width, sizeLabelBox.Width);
        Assert.Equal(hostLabelBox.Height, sizeLabelBox.Height);
    }

    [Fact]
    public void ToElement_配置と外観を要素の指示へ写す()
    {
        var element = CreateLabel(anchor: new EsPoint(10, 30)).ToElement(
            new EsSize(50, 14), s_text, s_background);

        Assert.Equal(new EsRect(10, 30, 58, 22), element.Bounds);
        Assert.Equal(new EsPoint(14, 34), element.TextOrigin);
        Assert.Equal(14.0, element.FontSize);
        Assert.Equal(s_text, element.TextColor);
        Assert.Equal(s_background, element.BackgroundColor);
        // サイズラベルの背景は四角形である（角丸なし）
        Assert.Equal(0, element.CornerRadius);
    }

    // ====================================================================
    // Format（文言の書式）
    // ====================================================================

    [Fact]
    public void Format_座標と寸法と表示倍率を並べる()
    {
        var text = MarqueeSizeLabel.Format(new EsRect(10, 20, 30, 40), scaling: 1.0, displayScaling: 1.0);

        Assert.Equal("(10, 20) - (40, 60),  (30×40), DisplayScale: (100%)", text);
    }

    [Fact]
    public void Format_座標と寸法は換算係数を掛けた整数になる()
    {
        // 125% 表示: (10, 20) - (40, 60) / 30x40 → 12, 25, 50, 75 / 37, 50
        var text = MarqueeSizeLabel.Format(new EsRect(10, 20, 30, 40), scaling: 1.25, displayScaling: 1.25);

        Assert.Equal("(12, 25) - (50, 75),  (37×50), DisplayScale: (125%)", text);
    }

    [Fact]
    public void Format_表示倍率だけを別の係数にできる()
    {
        // 座標は画像ピクセルのまま（1.0）、表示倍率だけ 150% を示す
        var text = MarqueeSizeLabel.Format(new EsRect(10, 20, 30, 40), scaling: 1.0, displayScaling: 1.5);

        Assert.Equal("(10, 20) - (40, 60),  (30×40), DisplayScale: (150%)", text);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void Format_換算係数が0以下なら等倍として扱う(double scaling)
    {
        var text = MarqueeSizeLabel.Format(new EsRect(10, 20, 30, 40), scaling, displayScaling: 0.0);

        Assert.Equal("(10, 20) - (40, 60),  (30×40), DisplayScale: (100%)", text);
    }

    // ====================================================================
    // ResolveDisplayScaling（0 以下は未指定）
    // ====================================================================

    [Fact]
    public void ResolveDisplayScaling_0以下は座標換算の係数を使う()
    {
        Assert.Equal(1.25, MarqueeSizeLabel.ResolveDisplayScaling(0, 1.25));
        Assert.Equal(1.25, MarqueeSizeLabel.ResolveDisplayScaling(-1, 1.25));
        Assert.Equal(2.0, MarqueeSizeLabel.ResolveDisplayScaling(2.0, 1.25));
    }

    [Fact]
    public void ResolveDisplayScaling_両方0以下なら等倍になる()
        => Assert.Equal(1.0, MarqueeSizeLabel.ResolveDisplayScaling(0, 0));
}
