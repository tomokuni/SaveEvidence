using EsUtil.Platform.Core.Geometry;
using Xunit;
using EsUtil.Feature.LoupeCore.Setting;

namespace EsUtil.Feature.LoupeCore.Tests.Setting;

/// <summary><see cref="LoupeOptions"/> のテスト（UI 非依存・並列実行）。</summary>
/// <remarks>
/// 設定の既定値・安全域への補正・部分変更・等価性と、寸法（枠線の太さ）の補正・
/// 中心座標ラベルの表示可否を検証する。<br/>
/// 色は描画にだけ必要な設定であり本型は持たない（<c>LoupePaint</c> のテストが担う）。<br/>
/// UI のプロパティ経由（バインディング等）の反映は各 UI のコントロールテストが担う。
/// </remarks>
public class LoupeOptionsTests
{
    [Fact]
    public void 既定値がコアの既定値と一致する()
    {
        var options = new LoupeOptions();

        Assert.Equal(Loupe.Limits.RequestedSizeDefault, options.RequestedSize);
        Assert.Equal(Loupe.Limits.ZoomLevelDefault, options.ZoomLevel);
        Assert.Equal(Loupe.Limits.BorderThicknessDefault, options.BorderThickness);
        Assert.True(options.ShowCoordLabel);
        Assert.True(options.ShowCrosshair);
        Assert.True(options.ShowBorder);
    }

    [Fact]
    public void Defaultは既定値と同じ値を持つ()
    {
        Assert.Equal(new LoupeOptions(), LoupeOptions.Default);
    }

    [Fact]
    public void with式で一部だけ変更できる()
    {
        var options = LoupeOptions.Default;

        var changed = options with { ZoomLevel = 8 };

        Assert.Equal(8, changed.ZoomLevel);
        // 他の項目は維持される（部分指定でもゼロ値にならない）
        Assert.Equal(options.RequestedSize, changed.RequestedSize);
        Assert.Equal(options.BorderThickness, changed.BorderThickness);
        Assert.Equal(options.ShowCoordLabel, changed.ShowCoordLabel);
    }

    /// <summary>数値項目が生成時に安全域へ補正されることを検証する。</summary>
    /// <remarks>
    /// 補正を <c>init</c> アクセサで行うため、オブジェクト初期化子・<c>with</c> 式・逆シリアル化の<br/>
    /// どの設定経路でも範囲内の値だけが保持される（UI 側に補正の登録を要求しない）。
    /// </remarks>
    [Fact]
    public void 生成時に数値項目が安全域へ補正される()
    {
        var options = new LoupeOptions
        {
            RequestedSize = 100000,
            ZoomLevel = 0,
            BorderThickness = 0,
        };

        Assert.Equal(Loupe.Limits.RequestedSizeMax, options.RequestedSize);
        Assert.Equal(Loupe.Limits.ZoomLevelMin, options.ZoomLevel);
        Assert.Equal(Loupe.Limits.BorderThicknessMin, options.BorderThickness);
    }

    /// <summary><c>with</c> 式による変更も生成時と同じ補正を受けることを検証する。</summary>
    [Fact]
    public void with式の変更も安全域へ補正される()
    {
        var changed = LoupeOptions.Default with { RequestedSize = 0, ZoomLevel = 1000 };

        Assert.Equal(Loupe.Limits.RequestedSizeMin, changed.RequestedSize);
        Assert.Equal(Loupe.Limits.ZoomLevelMax, changed.ZoomLevel);
    }

    [Fact]
    public void 安全域内の値は変更されない()
    {
        var options = new LoupeOptions { BorderThickness = 3 };

        Assert.Equal(3, options.BorderThickness);
    }

    /// <summary>中心座標ラベルの表示可否が設定と中心の有無で決まることを検証する。</summary>
    /// <param name="show">表示の設定。</param>
    /// <param name="hasCenter">拡大中心が設定されているかどうか。</param>
    /// <param name="expected">期待する判定結果。</param>
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void ShouldShowCoordLabel_設定と中心の有無で判定する(bool show, bool hasCenter, bool expected)
    {
        var options = new LoupeOptions { ShowCoordLabel = show };

        Assert.Equal(expected, options.ShouldShowCoordLabel(hasCenter));
    }

    [Fact]
    public void 等価性は値で判定される()
    {
        var a = LoupeOptions.Default with { BorderThickness = 4 };
        var b = LoupeOptions.Default with { BorderThickness = 4 };

        Assert.Equal(a, b);
        Assert.NotEqual(a, a with { BorderThickness = 5 });
    }

    /// <summary>寸法の変更が値の等価性に反映されることを検証する。</summary>
    /// <param name="same">同じ値にする場合は true。</param>
    /// <remarks>等価性により、同値の設定を再代入しても UI 側の変更通知（再描画）は発生しない。</remarks>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 等価性が寸法の変更を反映する(bool same)
    {
        var options = new LoupeOptions();
        var other = same ? options : options with { BorderThickness = 8 };

        Assert.Equal(same, options == other);
        Assert.Equal(!same, options != other);
    }

    /// <summary>中心座標ラベルの文言はコアの値型が文字列化することを検証する。</summary>
    /// <remarks>設定は文言を持たない（表示可否だけを持つ）。</remarks>
    [Fact]
    public void ラベルに使う座標は文字列化される()
    {
        var text = new EsPoint(123, 456).Format();

        Assert.Equal("(123, 456)", text);
    }
}
