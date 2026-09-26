using Xunit;

using EsUtil.Feature.MarqueeCore.Decoration;

namespace EsUtil.Feature.MarqueeCore.Tests.Decoration;

/// <summary><see cref="MarqueeDecoOptions"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 既定値・部分変更と、表示可否の述語（描画の可否の判断）を検証する。<br/>
/// 描画の可否は設定の明示フラグが単一所有する（色のアルファを「描かない」の代用にしない）。
/// </remarks>
public class MarqueeDecoOptionsTests
{
    /// <summary>既定値が暗転と枠線を描き、内側は描かないことを検証する。</summary>
    [Fact]
    public void 既定値が想定どおりである()
    {
        var options = new MarqueeDecoOptions();

        Assert.True(options.ShowOverlay);
        Assert.True(options.ShowOuterBorder);
        Assert.False(options.OuterShowFullOverlay);
        Assert.Null(options.OuterHighlightRect);
        Assert.False(options.ShowInnerFill);
        Assert.False(options.ShowInnerBorder);
    }

    /// <summary>穴あけ矩形があれば暗転を描画すると判定することを検証する。</summary>
    [Fact]
    public void ShouldDrawOverlay_穴あけ矩形があればtrueを返す()
    {
        Assert.True(MarqueeDecoOptions.Default.ShouldDrawOverlay(hasHoleRect: true));
    }

    /// <summary>穴あけ矩形が無く全面暗転なら描画すると判定することを検証する。</summary>
    [Fact]
    public void ShouldDrawOverlay_穴あけ矩形がなく全面暗転ならtrueを返す()
    {
        var options = MarqueeDecoOptions.Default with { OuterShowFullOverlay = true };

        Assert.True(options.ShouldDrawOverlay(hasHoleRect: false));
    }

    /// <summary>穴あけ矩形も全面暗転も無効なら描画しないと判定することを検証する。</summary>
    /// <param name="hasHoleRect">穴あけ矩形があるかどうか。</param>
    /// <param name="showFullOverlay">選択なしでも全面暗転を表示するか。</param>
    /// <param name="expected">描画するかどうか。</param>
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void ShouldDrawOverlay_穴あけ矩形と全面暗転の組合せで決まる(bool hasHoleRect, bool showFullOverlay, bool expected)
    {
        var options = MarqueeDecoOptions.Default with { OuterShowFullOverlay = showFullOverlay };

        Assert.Equal(expected, options.ShouldDrawOverlay(hasHoleRect));
    }
}
