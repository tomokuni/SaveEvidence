using Xunit;

namespace Estable.Marquee.Tests;

/// <summary><see cref="MarqueeOptions"/> の単体テスト（既定値・部分変更・値補正・等価性）。</summary>
/// <remarks>
/// 設定の既定値と補正は本型が単一所有するため、両 UI のプロパティの既定値・補正は本テストで担保する。<br/>
/// 色は UI 非依存の <see cref="System.Drawing.Color"/> で比較する。
/// </remarks>
public class MarqueeOptionsTests
{
    [Fact]
    public void 既定値が両UIの期待値と一致する()
    {
        var options = MarqueeOptions.Default;

        // 枠線は DarkGray（#FFA9A9A9。設定画面の選択枠の色と一致）。外側の装飾の外観が単一所有する
        Assert.Equal(System.Drawing.Color.FromArgb(255, 169, 169, 169), options.OuterAppearance.BorderColor);
        Assert.Equal(2.0, options.OuterAppearance.BorderThickness);
        // 汎用の選択矩形コントロールの既定では暗転せず、ラベルも描かない
        Assert.Equal(System.Drawing.Color.Transparent, options.OuterAppearance.OverlayColor);
        Assert.False(options.OuterAppearance.ShowSizeLabel);
        Assert.False(options.OuterAppearance.ShowSubLines);
        Assert.False(options.OuterAppearance.InfoLabelVisible);
        Assert.False(options.OuterShowFullOverlay);
        Assert.Null(options.OuterHighlightRect);
        Assert.False(options.OuterHasHighlight);
        // 内側の装飾（穴あけ矩形の内側の塗りと枠線）は既定で描かない（用途依存のためホストが明示的に指定する）
        Assert.Equal(System.Drawing.Color.Transparent, options.InnerAppearance.FillColor);
        Assert.Equal(System.Drawing.Color.Transparent, options.InnerAppearance.BorderColor);
        Assert.Equal(2.0, options.InnerAppearance.BorderThickness);
        Assert.True(options.ShowHandles);
        Assert.Equal(System.Drawing.Color.White, options.HandleColor);
        Assert.Equal(10.0, options.HandleSize);
        Assert.Equal(System.Drawing.Color.White, options.MarkerColor);
        Assert.Equal(16.0, options.MarkerLength);
        Assert.False(options.ShowCoordLabel);
        Assert.Equal(System.Drawing.Color.White, options.CoordLabelColor);
        // 背景色は不透明度をアルファに統合（黒 60% = #99000000）
        Assert.Equal(System.Drawing.Color.FromArgb(153, 0, 0, 0), options.CoordLabelBackgroundColor);
        Assert.Equal(12.0, options.CoordLabelFontSize);
        Assert.Equal(12.0, options.HandleHitRadius);
        Assert.Equal(6.0, options.MinSelectionSize);
        Assert.True(options.ClampToDomainBounds);
    }

    [Fact]
    public void newとDefaultは等価である()
    {
        // プロパティ初期化子で既定値が入るため、暗黙の new() でも Default と等しくなる
        Assert.Equal(MarqueeOptions.Default, new MarqueeOptions());
        Assert.Equal(MarqueeOptions.Default.GetHashCode(), new MarqueeOptions().GetHashCode());
    }

    [Fact]
    public void 一部の項目だけを変更しても他の項目は既定値を保つ()
    {
        var options = MarqueeOptions.Default with { ShowHandles = false };

        Assert.False(options.ShowHandles);
        Assert.Equal(MarqueeOptions.Default.OuterAppearance, options.OuterAppearance);
        Assert.Equal(MarqueeOptions.Default.HandleSize, options.HandleSize);
        Assert.Equal(MarqueeOptions.Default.CoordLabelFontSize, options.CoordLabelFontSize);
        Assert.Equal(MarqueeOptions.Default.MinSelectionSize, options.MinSelectionSize);
        Assert.True(options.ClampToDomainBounds);
    }

    [Fact]
    public void 外側の装飾の外観を変更しても他の項目は既定値を保つ()
    {
        var options = MarqueeOptions.Default with
        {
            OuterAppearance = MarqueeOptions.Default.OuterAppearance with { BorderThickness = 3.0 },
        };

        Assert.Equal(3.0, options.OuterAppearance.BorderThickness);
        Assert.Equal(MarqueeOptions.Default.OuterAppearance.BorderColor, options.OuterAppearance.BorderColor);
        Assert.Equal(MarqueeOptions.Default.HandleSize, options.HandleSize);
        Assert.True(options.ShowHandles);
    }

    [Fact]
    public void 同名で同値の設定は等価である()
    {
        var appearance = MarqueeOptions.Default.OuterAppearance with { BorderColor = System.Drawing.Color.Red };
        var a = MarqueeOptions.Default with { OuterAppearance = appearance, ShowCoordLabel = true };
        var b = MarqueeOptions.Default with { OuterAppearance = appearance, ShowCoordLabel = true };

        Assert.Equal(a, b);
        Assert.NotEqual(a, a with { ShowCoordLabel = false });
    }

    [Theory]
    [InlineData(double.NaN, 12.0)]
    [InlineData(double.PositiveInfinity, 12.0)]
    [InlineData(double.NegativeInfinity, 12.0)]
    [InlineData(1.0, 6.0)]
    [InlineData(100.0, 48.0)]
    [InlineData(6.0, 6.0)]
    [InlineData(48.0, 48.0)]
    [InlineData(12.0, 12.0)]
    public void CoordLabelFontSizeは安全域へ補正される(double value, double expected)
    {
        var options = MarqueeOptions.Default with { CoordLabelFontSize = value };

        Assert.Equal(expected, options.CoordLabelFontSize);
    }

    [Fact]
    public void 設定は不変である()
    {
        var options = MarqueeOptions.Default;
        var changed = options with { MinSelectionSize = 20.0 };

        // with は新しい値を作る（元の値は変わらない）
        Assert.Equal(6.0, options.MinSelectionSize);
        Assert.Equal(20.0, changed.MinSelectionSize);
        Assert.NotEqual(options, changed);
    }
}
