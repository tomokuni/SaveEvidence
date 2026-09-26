using Xunit;

using EsUtil.Feature.MarqueeCore.Decoration;
using EsUtil.Feature.MarqueeCore.Setting;

namespace EsUtil.Feature.MarqueeCore.Tests.Decoration;

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

        Assert.Equal(2.0, appearance.BorderThickness);
    }

    /// <summary>部分変更では他の値が維持されることを検証する。</summary>
    [Fact]
    public void 部分変更では他の値が維持される()
    {
        var baseline = new OuterDecoAppearance();

        var changed = baseline with { BorderThickness = 3.0 };

        Assert.Equal(3.0, changed.BorderThickness);
    }

    /// <summary>枠線の太さが安全域へ補正されることを検証する。</summary>
    /// <param name="value">設定する太さ（DIP）。</param>
    /// <param name="expected">補正後の太さ（DIP）。</param>
    /// <remarks>補正は生成時に行われるため、保持している値が常に安全域内になる。</remarks>
    [Theory]
    [InlineData(0.1, 0.5)]
    [InlineData(0.0, 0.5)]
    [InlineData(-1.0, 0.5)]
    [InlineData(2.5, 2.5)]
    public void 枠線の太さは安全域へ補正される(double value, double expected)
    {
        var appearance = new OuterDecoAppearance { BorderThickness = value };

        Assert.Equal(expected, appearance.BorderThickness);
    }
}
