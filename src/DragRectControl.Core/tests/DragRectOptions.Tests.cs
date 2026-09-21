using Xunit;

namespace Estable.DragRect.Tests;

/// <summary><see cref="DragRectOptions"/> の単体テスト（既定値・部分変更・値補正・等価性）。</summary>
/// <remarks>
/// 設定の既定値と補正は本型が単一所有するため、両 UI のプロパティの既定値・補正は本テストで担保する。<br/>
/// 色は UI 非依存の <see cref="System.Drawing.Color"/> で比較する。
/// </remarks>
public class DragRectOptionsTests
{
    [Fact]
    public void 既定値が両UIの期待値と一致する()
    {
        var options = DragRectOptions.Default;

        // 枠線は DarkGray（#FFA9A9A9。設定画面の選択枠の色と一致）
        Assert.Equal(System.Drawing.Color.FromArgb(255, 169, 169, 169), options.BorderColor);
        Assert.Equal(2.0, options.BorderThickness);
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
        Assert.Equal(DragRectOptions.Default, new DragRectOptions());
        Assert.Equal(DragRectOptions.Default.GetHashCode(), new DragRectOptions().GetHashCode());
    }

    [Fact]
    public void 一部の項目だけを変更しても他の項目は既定値を保つ()
    {
        var options = DragRectOptions.Default with { BorderThickness = 3.0 };

        Assert.Equal(3.0, options.BorderThickness);
        Assert.Equal(DragRectOptions.Default.BorderColor, options.BorderColor);
        Assert.Equal(DragRectOptions.Default.HandleSize, options.HandleSize);
        Assert.Equal(DragRectOptions.Default.CoordLabelFontSize, options.CoordLabelFontSize);
        Assert.Equal(DragRectOptions.Default.MinSelectionSize, options.MinSelectionSize);
        Assert.True(options.ClampToDomainBounds);
    }

    [Fact]
    public void 同名で同値の設定は等価である()
    {
        var a = DragRectOptions.Default with { BorderColor = System.Drawing.Color.Red, ShowCoordLabel = true };
        var b = DragRectOptions.Default with { BorderColor = System.Drawing.Color.Red, ShowCoordLabel = true };

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
        var options = DragRectOptions.Default with { CoordLabelFontSize = value };

        Assert.Equal(expected, options.CoordLabelFontSize);
    }

    [Fact]
    public void 設定は不変である()
    {
        var options = DragRectOptions.Default;
        var changed = options with { MinSelectionSize = 20.0 };

        // with は新しい値を作る（元の値は変わらない）
        Assert.Equal(6.0, options.MinSelectionSize);
        Assert.Equal(20.0, changed.MinSelectionSize);
        Assert.NotEqual(options, changed);
    }
}
