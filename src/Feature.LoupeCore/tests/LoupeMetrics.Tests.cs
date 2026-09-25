using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.LoupeCore.Tests;

/// <summary><see cref="LoupeMetrics"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 既定値・安全域への補正・中心座標ラベルの表示可否を検証する。<br/>
/// 色は描画にだけ必要な設定であり本型は持たない（<c>LoupePaint</c> のテストが担う）。<br/>
/// 実際の描画結果は描画層（<c>LoupeControl.UIBase</c>）のレンダラーテストが担う。
/// </remarks>
public class LoupeMetricsTests
{
    [Fact]
    public void 既定値がコアの既定値と一致する()
    {
        var metrics = new LoupeMetrics();

        Assert.Equal(LoupeValueCoercer.CheckerSizeDefault, metrics.CheckerSize);
        Assert.Equal(LoupeValueCoercer.FrameWidthDefault, metrics.FrameWidth);
        Assert.Equal(LoupeValueCoercer.CoordLabelFontSizeDefault, metrics.CoordLabelFontSize);
        Assert.True(metrics.ShowCoordLabel);
    }

    [Fact]
    public void with式で一部だけ変更できる()
    {
        var metrics = new LoupeMetrics();

        var changed = metrics with { FrameWidth = 5 };

        Assert.Equal(5, changed.FrameWidth);
        // 他の項目は維持される
        Assert.Equal(metrics.CheckerSize, changed.CheckerSize);
        Assert.Equal(metrics.CoordLabelCornerRadius, changed.CoordLabelCornerRadius);
    }

    /// <summary>数値項目が生成時に安全域へ補正されることを検証する。</summary>
    /// <remarks>
    /// 補正を <c>init</c> アクセサで行うため、オブジェクト初期化子・<c>with</c> 式・逆シリアル化の
    /// どの設定経路でも範囲内の値だけが保持される（UI 側に補正の登録を要求しない）。
    /// </remarks>
    [Fact]
    public void 生成時に数値項目が安全域へ補正される()
    {
        var metrics = new LoupeMetrics
        {
            CheckerSize = 1000,
            FrameWidth = 0,
            CoordLabelFontSize = 100,
            CoordLabelCornerRadius = 1000,
        };

        Assert.Equal(LoupeValueCoercer.CheckerSizeMax, metrics.CheckerSize);
        Assert.Equal(LoupeValueCoercer.FrameWidthMin, metrics.FrameWidth);
        Assert.Equal(LoupeValueCoercer.CoordLabelFontSizeMax, metrics.CoordLabelFontSize);
        Assert.Equal(LoupeValueCoercer.CoordLabelCornerRadiusMax, metrics.CoordLabelCornerRadius);
    }

    /// <summary><c>with</c> 式による変更も生成時と同じ補正を受けることを検証する。</summary>
    [Fact]
    public void with式の変更も安全域へ補正される()
    {
        var changed = new LoupeMetrics() with { CheckerSize = 1000 };

        Assert.Equal(LoupeValueCoercer.CheckerSizeMax, changed.CheckerSize);
    }

    [Fact]
    public void 非有限のフォントサイズは既定値へ補正される()
    {
        var metrics = new LoupeMetrics { CoordLabelFontSize = double.NaN };

        Assert.Equal(LoupeValueCoercer.CoordLabelFontSizeDefault, metrics.CoordLabelFontSize);
    }

    [Fact]
    public void 安全域内の値は変更されない()
    {
        var metrics = new LoupeMetrics
        {
            CheckerSize = 8,
            FrameWidth = 3,
            CoordLabelFontSize = 16,
        };

        Assert.Equal(8, metrics.CheckerSize);
        Assert.Equal(3, metrics.FrameWidth);
        Assert.Equal(16, metrics.CoordLabelFontSize);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ShouldShowCoordLabel_設定と中心の有無で判定する(bool show, bool hasCenter, bool expected)
    {
        var metrics = new LoupeMetrics { ShowCoordLabel = show };

        Assert.Equal(expected, metrics.ShouldShowCoordLabel(hasCenter));
    }

    [Fact]
    public void 等価性は値で判定される()
    {
        var a = new LoupeMetrics { CheckerSize = 4 };
        var b = new LoupeMetrics { CheckerSize = 4 };

        Assert.Equal(a, b);
        Assert.NotEqual(a, a with { CheckerSize = 5 });
    }

    [Fact]
    public void ラベルに使う座標は文字列化される()
    {
        // ラベル文言はコアの CoordinateFormatter が単一所有する（寸法は文言を持たない）
        var text = CoordinateFormatter.Format(new EsPoint(123, 456));

        Assert.Equal("(123, 456)", text);
    }
}
