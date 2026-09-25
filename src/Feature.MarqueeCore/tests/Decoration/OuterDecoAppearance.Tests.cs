using Xunit;
using DrawingColor = System.Drawing.Color;

using Estable.Marquee.Decoration;

namespace Estable.Marquee.Tests;

/// <summary><see cref="OuterDecoAppearance"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 既定値と部分変更を検証する。<br/>
/// 本型は暗転と枠線の外観のみを所有する（サイズラベルはコアの <see cref="MarqueeOptions"/>、<br/>
/// 操作・情報ラベルはホストが自前で描くため、装飾は画面の暗転と枠線以外の外観を持たない）。
/// </remarks>
public class OuterDecoAppearanceTests
{
    // ====================================================================
    // 既定値
    // ====================================================================

    /// <summary>既定値が黒 50% の暗転とダークグレーの枠線であることを検証する。</summary>
    [Fact]
    public void 既定値が想定どおりである()
    {
        var appearance = new OuterDecoAppearance();

        Assert.Equal(DrawingColor.FromArgb(128, 0, 0, 0), appearance.OverlayColor);
        Assert.Equal(DrawingColor.FromArgb(255, 169, 169, 169), appearance.BorderColor);
        Assert.Equal(2.0, appearance.BorderThickness);
    }

    /// <summary>部分変更では他の値が維持されることを検証する。</summary>
    [Fact]
    public void 部分変更では他の値が維持される()
    {
        var baseline = new OuterDecoAppearance();

        var changed = baseline with { BorderThickness = 3.0, BorderColor = DrawingColor.Red };

        Assert.Equal(3.0, changed.BorderThickness);
        Assert.Equal(DrawingColor.Red, changed.BorderColor);
        Assert.Equal(baseline.OverlayColor, changed.OverlayColor);
    }
}
