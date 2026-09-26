using Xunit;
using EsUtil.SaveEvidence.Common.Imaging;
using EsUtil.Feature.MarqueeCore.Setting;


namespace EsUtil.Feature.MarqueeCore.Tests;

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

        // 色は描画にだけ必要な設定のため本型は持たない（描画層の MarqueePaint が単一所有する）
        Assert.True(options.ShowHandles);
        Assert.Equal(10.0, options.HandleSize);
        Assert.Equal(16.0, options.MarkerLength);
        Assert.False(options.ShowCoordLabel);
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
        Assert.Equal(MarqueeOptions.Default.HandleSize, options.HandleSize);
        Assert.Equal(MarqueeOptions.Default.CoordLabelFontSize, options.CoordLabelFontSize);
        Assert.Equal(MarqueeOptions.Default.MinSelectionSize, options.MinSelectionSize);
        Assert.True(options.ClampToDomainBounds);
    }

    [Fact]
    public void ハンドルのサイズを変更しても他の項目は既定値を保つ()
    {
        var options = MarqueeOptions.Default with { HandleSize = 20 };

        Assert.Equal(20, options.HandleSize);
        Assert.True(options.ShowHandles);
    }

    [Fact]
    public void 同名で同値の設定は等価である()
    {
        var a = MarqueeOptions.Default with { HandleSize = 12.0, ShowCoordLabel = true };
        var b = MarqueeOptions.Default with { HandleSize = 12.0, ShowCoordLabel = true };

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

    // ====================================================================
    // 寸法値の補正（生成時に適用する）
    // ====================================================================

    [Theory]
    [InlineData(double.NaN, 14.0)]
    [InlineData(double.PositiveInfinity, 14.0)]
    [InlineData(1.0, 6.0)]
    [InlineData(100.0, 48.0)]
    [InlineData(14.0, 14.0)]
    public void SizeLabelFontSizeは安全域へ補正される(double value, double expected)
    {
        var options = MarqueeOptions.Default with { SizeLabelFontSize = value };

        Assert.Equal(expected, options.SizeLabelFontSize);
    }

    [Fact]
    public void HandleSize_指定された値をそのまま保持する()
    {
        var options = MarqueeOptions.Default with { HandleSize = 12.5 };

        Assert.Equal(12.5, options.HandleSize);
    }

    [Fact]
    public void ラベルの体裁定数はコアが単一所有する()
    {
        // 内側余白は全ラベル共通の規則（Platform.Core の EsLabelLayout）が単一所有する。
        // 本型はコントロール固有の値（カーソルとラベルの距離）だけを公開する。
        Assert.Equal(4.0, EsLabelLayout.Padding);
        Assert.Equal(8.0, MarqueeOptions.CoordLabelOffset);
        Assert.Equal(1.5, MarqueeOptions.HandleBorderThickness);
    }
}
