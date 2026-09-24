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

        // ハンドルの枠線は装飾の枠線と同じ見た目（DarkGray #FFA9A9A9）を既定にする
        Assert.Equal(System.Drawing.Color.FromArgb(255, 169, 169, 169), options.HandleBorderColor);
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
        Assert.Equal(MarqueeOptions.Default.HandleBorderColor, options.HandleBorderColor);
        Assert.Equal(MarqueeOptions.Default.HandleSize, options.HandleSize);
        Assert.Equal(MarqueeOptions.Default.CoordLabelFontSize, options.CoordLabelFontSize);
        Assert.Equal(MarqueeOptions.Default.MinSelectionSize, options.MinSelectionSize);
        Assert.True(options.ClampToDomainBounds);
    }

    [Fact]
    public void ハンドルの枠線色を変更しても他の項目は既定値を保つ()
    {
        var options = MarqueeOptions.Default with { HandleBorderColor = System.Drawing.Color.Red };

        Assert.Equal(System.Drawing.Color.Red, options.HandleBorderColor);
        Assert.Equal(MarqueeOptions.Default.HandleColor, options.HandleColor);
        Assert.Equal(MarqueeOptions.Default.HandleSize, options.HandleSize);
        Assert.True(options.ShowHandles);
    }

    [Fact]
    public void 同名で同値の設定は等価である()
    {
        var a = MarqueeOptions.Default with { HandleBorderColor = System.Drawing.Color.Red, ShowCoordLabel = true };
        var b = MarqueeOptions.Default with { HandleBorderColor = System.Drawing.Color.Red, ShowCoordLabel = true };

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
